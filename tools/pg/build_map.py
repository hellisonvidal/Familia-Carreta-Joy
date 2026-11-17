"""
Converte OpenStreetMap + relevo de Ponta Grossa em dados do jogo (Unity).
Saída: unity/Assets/Resources/Map/pg_map.bytes (+ pg_height.bytes)
Coordenadas: metros, origem na Catedral Sant'Ana, x = leste, z = norte, y = altitude - ELEV0.
"""
import xml.etree.ElementTree as ET, glob, math, struct, random, heapq, json, sys, os
import numpy as np
from shapely.geometry import Polygon, LineString, box, Point, MultiPolygon, GeometryCollection
from shapely.ops import unary_union
from shapely import prepared
import mapbox_earcut as earcut

LAT0, LON0 = -25.0982041, -50.1589514          # Catedral Sant'Ana
KX = math.cos(math.radians(LAT0)) * 111320.0
KZ = 110540.0
def P(lat, lon): return ((lon - LON0) * KX, (lat - LAT0) * KZ)

OUT = '/home/user/familia-carreta-joy/unity/Assets/Resources/Map'
os.makedirs(OUT, exist_ok=True)
rnd = random.Random(42)

# ---------------------------------------------------------------- relevo
dem = np.load('/home/user/pg/dem_full.npy')
DLAT0, DLAT1, DLON0, DLON1 = -25.165, -25.030, -50.220, -50.080
def elev_raw(lat, lon):
    r = (DLAT1 - lat) / (DLAT1 - DLAT0) * (dem.shape[0] - 1)
    c = (lon - DLON0) / (DLON1 - DLON0) * (dem.shape[1] - 1)
    r = min(max(r, 0), dem.shape[0] - 1.001); c = min(max(c, 0), dem.shape[1] - 1.001)
    r0, c0 = int(r), int(c); fr, fc = r - r0, c - c0
    return (dem[r0, c0] * (1 - fr) * (1 - fc) + dem[r0, c0 + 1] * (1 - fr) * fc + dem[r0 + 1, c0] * fr * (1 - fc) + dem[r0 + 1, c0 + 1] * fr * fc)
xmin, zmin = P(DLAT0, DLON0); xmax, zmax = P(DLAT1, DLON1)
CELL = 8.0
NX = int((xmax - xmin) / CELL) + 1; NZ = int((zmax - zmin) / CELL) + 1
H = np.zeros((NZ, NX), np.float32)
for j in range(NZ):
    z = zmin + j * CELL; lat = LAT0 + z / KZ
    lons = LON0 + (xmin + np.arange(NX) * CELL) / KX
    for i in range(NX): H[j, i] = elev_raw(lat, lons[i])
# suaviza um pouco (o dado de satélite tem degraus)
for _ in range(2):
    Hp = np.pad(H, 1, mode='edge')
    H = (Hp[1:-1, 1:-1] * 4 + Hp[:-2, 1:-1] + Hp[2:, 1:-1] + Hp[1:-1, :-2] + Hp[1:-1, 2:]) / 8
ELEV0 = float(H.min()) - 2
print('relevo', NX, NZ, 'min', H.min(), 'max', H.max())
def hgt(x, z):
    fi = (x - xmin) / CELL; fj = (z - zmin) / CELL
    fi = min(max(fi, 0), NX - 1.001); fj = min(max(fj, 0), NZ - 1.001)
    i, j = int(fi), int(fj); a, b = fi - i, fj - j
    return float(H[j, i] * (1 - a) * (1 - b) + H[j, i + 1] * a * (1 - b) + H[j + 1, i] * (1 - a) * b + H[j + 1, i + 1] * a * b) - ELEV0

# ---------------------------------------------------------------- OSM
nodes, ways, rels = {}, {}, []
for f in sorted(glob.glob('/home/user/pg/tiles/*.xml')):
    try: r = ET.parse(f).getroot()
    except Exception as e: print('arquivo ruim', f, e); continue
    for n in r.iter('node'):
        tags = {t.get('k'): t.get('v') for t in n.findall('tag')}
        nodes[n.get('id')] = (float(n.get('lat')), float(n.get('lon')), tags)
    for w in r.iter('way'):
        ways[w.get('id')] = ([nd.get('ref') for nd in w.findall('nd')], {t.get('k'): t.get('v') for t in w.findall('tag')})
    for rl in r.iter('relation'):
        tags = {t.get('k'): t.get('v') for t in rl.findall('tag')}
        if tags.get('type') == 'multipolygon':
            rels.append(([(m.get('ref'), m.get('role')) for m in rl.findall('member') if m.get('type') == 'way'], tags))
print('osm', len(nodes), 'nós', len(ways), 'vias', len(rels), 'relações')

def coords(refs):
    out = []
    for r in refs:
        n = nodes.get(r)
        if n: out.append(P(n[0], n[1]))
    return out

ROAD_W = {'motorway': 16, 'trunk': 14, 'primary': 13, 'secondary': 11, 'tertiary': 9, 'unclassified': 7, 'residential': 7.5,
          'living_street': 6, 'service': 4.5, 'pedestrian': 7, 'footway': 2.5, 'path': 2, 'steps': 2.5, 'track': 4, 'cycleway': 2.5}
ROAD_T = {k: i for i, k in enumerate(ROAD_W)}
SIDEWALK = {'motorway': 0, 'trunk': 2.5, 'primary': 3, 'secondary': 2.5, 'tertiary': 2.2, 'residential': 2, 'living_street': 1.5, 'unclassified': 1.2}

roads, areas, osm_buildings, waterways = [], [], [], []
def area_kind(t):
    l, n, le, am = t.get('landuse'), t.get('natural'), t.get('leisure'), t.get('amenity')
    if n == 'water' or l in ('reservoir', 'basin') or t.get('water'): return 'water'
    if le == 'pitch': return 'pitch'
    if le in ('park', 'garden', 'playground', 'nature_reserve', 'dog_park') or t.get('place') == 'square': return 'park'
    if l in ('grass', 'meadow', 'recreation_ground', 'village_green', 'farmland', 'greenfield') or n in ('grassland', 'scrub', 'heath'): return 'grass'
    if l == 'forest' or n == 'wood': return 'forest'
    if am == 'parking' or l == 'railway': return 'parking'
    if l == 'cemetery' or am == 'grave_yard': return 'cemetery'
    if le in ('sports_centre', 'stadium', 'track'): return 'sport'
    return None
AREA_ID = {'grass': 0, 'park': 1, 'forest': 2, 'water': 3, 'pitch': 4, 'parking': 5, 'cemetery': 6, 'sport': 7, 'plaza': 8}

zones = []  # (kind, polygon) landuse para escolher o tipo de construção
for wid, (refs, t) in ways.items():
    c = coords(refs)
    if len(c) < 2: continue
    closed = refs[0] == refs[-1] and len(c) >= 4
    if 'highway' in t:
        h = t['highway'].replace('_link', '')
        if h == 'pedestrian' and t.get('area') == 'yes' and closed:
            areas.append(('plaza', Polygon(c))); continue
        if h not in ROAD_W: continue
        if t.get('tunnel') in ('yes', 'culvert'): continue
        w = float(ROAD_W[h]) * (1.25 if t.get('lanes', '2') in ('4', '6') else 1)
        unpaved = t.get('surface') in ('unpaved', 'dirt', 'gravel', 'ground', 'sand', 'grass', 'compacted')
        roads.append(dict(kind=h, w=w, unpaved=unpaved, pts=c, name=t.get('name', ''), oneway=t.get('oneway') == 'yes', id=wid))
    elif 'building' in t and closed:
        try:
            poly = Polygon(c)
            if not poly.is_valid: poly = poly.buffer(0)
            if poly.area < 8: continue
        except Exception: continue
        lv = t.get('building:levels'); hh = t.get('height')
        try: height = float(hh.split()[0]) if hh else (float(lv) * 3.1 + 1 if lv else None)
        except Exception: height = None
        osm_buildings.append(dict(poly=poly, height=height, tags=t))
    elif 'waterway' in t and t['waterway'] in ('river', 'stream', 'canal', 'drain', 'ditch'):
        waterways.append(dict(w=6 if t['waterway'] == 'river' else 2.5, pts=c))
    elif closed:
        k = area_kind(t)
        if k:
            try:
                p = Polygon(c)
                if not p.is_valid: p = p.buffer(0)
                areas.append((k, p))
            except Exception: pass
        l = t.get('landuse')
        if l in ('residential', 'commercial', 'retail', 'industrial'):
            try: zones.append((l, Polygon(c).buffer(0)))
            except Exception: pass
for members, t in rels:
    k = area_kind(t)
    outers = [coords(ways[m][0]) for m, role in members if role == 'outer' and m in ways]
    for c in outers:
        if len(c) >= 4 and k:
            try: areas.append((k, Polygon(c).buffer(0)))
            except Exception: pass
print('ruas', len(roads), 'áreas', len(areas), 'prédios OSM', len(osm_buildings), 'rios', len(waterways))

# ---------------------------------------------------------------- pontos famosos
LANDMARKS = [
    ('catedral', "Catedral Sant'Ana", -25.0982041, -50.1589514, 34),
    ('estacao', 'Estação Saudade', -25.0933828, -50.1532606, 40),
    ('vilahilda', 'Mansão Vila Hilda', -25.0917531, -50.1620343, 26),
    ('estadio', 'Estádio Germano Krüger', -25.1161924, -50.1565794, 70),
    ('calcadao', 'Calçadão Coronel Cláudio', -25.0939219, -50.1575761, 0),
    ('lago', 'Lago de Olarias', -25.1175461, -50.1388012, 0),
]
landmarks = []
for k, name, la, lo, r in LANDMARKS:
    x, z = P(la, lo); landmarks.append(dict(kind=k, name=name, x=x, z=z, r=r))
# nomes de bairros (rótulos do mapa)
LABELS = [('Centro', -25.0944, -50.1609), ('Oficinas', -25.1239, -50.1484), ('Olarias', -25.1030, -50.1528), ('Uvaranas', -25.0900, -50.1163),
          ('Nova Rússia', -25.0815, -50.1758), ('Órfãs', -25.0715, -50.1600), ('Ronda', -25.0999, -50.1753), ('Jardim Carvalho', -25.0760, -50.1524),
          ('Estrela', -25.1111, -50.1720), ('Boa Vista', -25.0668, -50.1751), ('Contorno', -25.1004, -50.1946), ('Cará-Cará', -25.1300, -50.1150)]
labels = [(n, *P(la, lo)) for n, la, lo in LABELS]

# ---------------------------------------------------------------- ocupação (grade de 2 m)
G = 2.0
gx0, gz0 = xmin, zmin
GW, GH = int((xmax - xmin) / G) + 1, int((zmax - zmin) / G) + 1
occ = np.zeros((GH, GW), np.bool_)
def mark_poly(poly, pad=0.0):
    if pad: poly = poly.buffer(pad)
    if poly.is_empty: return
    a, b, c, d = poly.bounds
    i0, i1 = max(0, int((a - gx0) / G)), min(GW - 1, int((c - gx0) / G) + 1)
    j0, j1 = max(0, int((b - gz0) / G)), min(GH - 1, int((d - gz0) / G) + 1)
    if i1 <= i0 or j1 <= j0: return
    from matplotlib.path import Path as MP
    xs = gx0 + (np.arange(i0, i1) + 0.5) * G; zs = gz0 + (np.arange(j0, j1) + 0.5) * G
    XX, ZZ = np.meshgrid(xs, zs)
    pts = np.c_[XX.ravel(), ZZ.ravel()]
    geoms = poly.geoms if hasattr(poly, 'geoms') else [poly]
    m = np.zeros(len(pts), bool)
    for g in geoms:
        if g.geom_type != 'Polygon': continue
        m |= MP(np.asarray(g.exterior.coords)).contains_points(pts)
    occ[j0:j1, i0:i1] |= m.reshape(XX.shape)
def mark_line(pts, half):
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        x0, x1 = min(ax, bx) - half, max(ax, bx) + half; z0, z1 = min(az, bz) - half, max(az, bz) + half
        i0, i1 = max(0, int((x0 - gx0) / G)), min(GW - 1, int((x1 - gx0) / G) + 1)
        j0, j1 = max(0, int((z0 - gz0) / G)), min(GH - 1, int((z1 - gz0) / G) + 1)
        if i1 <= i0 or j1 <= j0: continue
        xs = gx0 + (np.arange(i0, i1) + 0.5) * G; zs = gz0 + (np.arange(j0, j1) + 0.5) * G
        XX, ZZ = np.meshgrid(xs, zs)
        dx, dz = bx - ax, bz - az; L2 = dx * dx + dz * dz or 1e-9
        t = np.clip(((XX - ax) * dx + (ZZ - az) * dz) / L2, 0, 1)
        d2 = (XX - ax - t * dx) ** 2 + (ZZ - az - t * dz) ** 2
        occ[j0:j1, i0:i1] |= d2 < half * half
def free_rect(cx, cz, ux, uz, w, d):
    # amostra o retângulo a cada 1.5 m
    vx, vz = -uz, ux
    for a in np.arange(-w / 2, w / 2 + 0.01, 1.5):
        for b in np.arange(-d / 2, d / 2 + 0.01, 1.5):
            x = cx + ux * a + vx * b; z = cz + uz * a + vz * b
            i, j = int((x - gx0) / G), int((z - gz0) / G)
            if i < 0 or j < 0 or i >= GW or j >= GH or occ[j, i]: return False
    return True

for r in roads: mark_line(r['pts'], r['w'] / 2 + SIDEWALK.get(r['kind'], 0.5) + 0.3)
for w in waterways: mark_line(w['pts'], w['w'] / 2 + 3)
for k, p in areas:
    if k != 'grass': mark_poly(p, 1.0)
for b in osm_buildings: mark_poly(b['poly'], 0.8)
for L in landmarks:
    if L['r'] > 0: mark_poly(Point(L['x'], L['z']).buffer(L['r']))
print('ocupação pronta')

# zonas (para escolher o tipo de construção)
zone_index = [(k, prepared.prep(p), p.bounds) for k, p in zones if not p.is_empty]
def zone_at(x, z):
    for k, pp, (a, b, c, d) in zone_index:
        if a <= x <= c and b <= z <= d and pp.contains(Point(x, z)): return k
    return None

# ---------------------------------------------------------------- construções geradas ao longo das ruas
CENTRO = Point(0, 0)
gen = []
STREET_OK = {'residential': 1, 'living_street': 1, 'tertiary': 1, 'secondary': 1, 'primary': 1, 'unclassified': 0.5}
for r in roads:
    if r['kind'] not in STREET_OK: continue
    pts = r['pts']; side_off = r['w'] / 2 + SIDEWALK.get(r['kind'], 1.0)
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        L = math.hypot(bx - ax, bz - az)
        if L < 6: continue
        ux, uz = (bx - ax) / L, (bz - az) / L
        for side in (-1, 1):
            nx, nz = -uz * side, ux * side
            s = rnd.uniform(1, 4)
            while s < L - 4:
                mx, mz = ax + ux * s, az + uz * s
                dist = math.hypot(mx, mz)
                zk = zone_at(mx, mz)
                if zk == 'industrial': kind, w, d, setb = 4, rnd.uniform(18, 34), rnd.uniform(16, 30), 4
                elif dist < 700 or (zk in ('commercial', 'retail')):
                    kind = 1 if rnd.random() < 0.6 else 2
                    w, d, setb = rnd.uniform(10, 18), rnd.uniform(12, 20), 0.3
                elif r['kind'] in ('primary', 'secondary') and rnd.random() < 0.6:
                    kind, w, d, setb = 2, rnd.uniform(9, 15), rnd.uniform(10, 16), 0.5
                else:
                    kind, w, d, setb = 0, rnd.uniform(7, 11), rnd.uniform(8, 11), rnd.uniform(2, 5)
                if rnd.random() > STREET_OK[r['kind']]: s += w; continue
                dense = kind in (1, 2)
                if dense: d = rnd.uniform(16, 26)
                placed = False
                for shrink in ((1, 1), (0.75, 0.85), (0.55, 0.7)):     # não coube? tenta menor
                    w2, d2 = w * shrink[0], d * shrink[1]
                    off = side_off + 0.4 + setb + d2 / 2   # fica fora da calçada marcada
                    cx, cz = mx + nx * off, mz + nz * off
                    if w2 >= 5 and free_rect(cx, cz, ux, uz, w2 + (0.4 if dense else 1.2), d2):
                        w, d, placed = w2, d2, True; break
                if not placed: s += 2.0; continue
                if True:
                    if kind == 0: floors = 1 if rnd.random() < 0.7 else 2
                    elif kind == 1: floors = int(np.clip(rnd.gauss(9 if dist < 400 else 5, 4), 3, 22))
                    elif kind == 2: floors = rnd.choice([1, 2, 2, 3, 4])
                    else: floors = 1
                    height = floors * (3.1 if kind != 4 else 7) + (0.6 if kind == 1 else 0)
                    vx, vz = -uz, ux
                    corners = [(cx + ux * a + vx * b, cz + uz * a + vz * b) for a, b in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2))]
                    mark_poly(Polygon(corners), 0.6)
                    gen.append(dict(kind=kind, height=height, floors=floors, pts=corners, roof=1 if kind == 0 else 0))
                s += w + (rnd.uniform(0.2, 0.7) if kind in (1, 2) else rnd.uniform(0.8, 3))
print('construções geradas', len(gen))

def osm_kind(t):
    b = t.get('building'); a = t.get('amenity')
    if a == 'place_of_worship' or b in ('church', 'cathedral', 'chapel'): return 5
    if b in ('industrial', 'warehouse', 'factory', 'hangar'): return 4
    if b in ('school', 'university', 'college') or a in ('school', 'university', 'college'): return 6
    if b in ('house', 'detached', 'residential', 'semidetached_house'): return 0
    if b in ('apartments',): return 1
    if b in ('commercial', 'retail', 'office', 'supermarket'): return 2
    if b in ('hospital',) or a == 'hospital': return 7
    return 3
buildings = []
for b in osm_buildings:
    k = osm_kind(b['tags']); poly = b['poly']
    if poly.geom_type != 'Polygon': continue
    h = b['height'] or {0: 5.5, 1: 18, 2: 7, 3: 7, 4: 9, 5: 14, 6: 9, 7: 12}[k]
    buildings.append(dict(kind=k, height=h, pts=list(poly.exterior.coords)[:-1], roof=0, name=b['tags'].get('name', '')))
buildings += gen

# árvores: araucárias e copas nos parques, matas e calçadas
trees = []
for k, p in areas:
    if k not in ('park', 'forest', 'cemetery', 'sport'): continue
    if p.is_empty: continue
    a, b, c, d = p.bounds
    dens = {'park': 1 / 160, 'forest': 1 / 90, 'cemetery': 1 / 400, 'sport': 1 / 500}[k]
    n = min(int(p.area * dens), 1500)
    pp = prepared.prep(p); tries = 0
    while n > 0 and tries < n * 6:
        tries += 1
        x, z = rnd.uniform(a, c), rnd.uniform(b, d)
        if pp.contains(Point(x, z)):
            i, j = int((x - gx0) / G), int((z - gz0) / G)
            trees.append((x, z, 0 if rnd.random() < (0.5 if k == 'forest' else 0.35) else 1)); n -= 1
for r in roads:
    if r['kind'] not in ('residential', 'tertiary', 'secondary'): continue
    pts = r['pts']; off = r['w'] / 2 + SIDEWALK.get(r['kind'], 1) * 0.6
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        L = math.hypot(bx - ax, bz - az)
        if L < 10: continue
        ux, uz = (bx - ax) / L, (bz - az) / L
        s = rnd.uniform(5, 20)
        while s < L - 5:
            if rnd.random() < 0.35:
                side = rnd.choice((-1, 1))
                trees.append((ax + ux * s - uz * off * side, az + uz * s + ux * off * side, 0 if rnd.random() < 0.25 else 1))
            s += rnd.uniform(14, 30)
print('árvores', len(trees))

# ---------------------------------------------------------------- percurso da carreta (ruas reais)
DRIVE = {'primary', 'secondary', 'tertiary', 'residential', 'unclassified', 'living_street'}
graph = {}; npos = {}
for wid, (refs, t) in ways.items():
    h = t.get('highway', '').replace('_link', '')
    if h not in DRIVE: continue
    cost_k = {'primary': 0.7, 'secondary': 0.75, 'tertiary': 0.85, 'residential': 1.3, 'unclassified': 1.4, 'living_street': 2}[h]
    rr = [r for r in refs if r in nodes]
    for a, b in zip(rr, rr[1:]):
        pa, pb = P(*nodes[a][:2]), P(*nodes[b][:2]); npos[a] = pa; npos[b] = pb
        L = math.hypot(pb[0] - pa[0], pb[1] - pa[1]) * cost_k
        graph.setdefault(a, []).append((b, L)); graph.setdefault(b, []).append((a, L))
def nearest(x, z):
    best, bd = None, 1e18
    for n, (px, pz) in npos.items():
        d = (px - x) ** 2 + (pz - z) ** 2
        if d < bd and len(graph.get(n, [])) > 1: bd, best = d, n
    return best
def route(a, b):
    dist = {a: 0}; prev = {}; pq = [(0, a)]
    while pq:
        d, u = heapq.heappop(pq)
        if u == b: break
        if d > dist[u]: continue
        for v, w in graph.get(u, []):
            nd = d + w
            if nd < dist.get(v, 1e18): dist[v] = nd; prev[v] = u; heapq.heappush(pq, (nd, v))
    if b not in dist: return None
    path = [b]
    while path[-1] != a: path.append(prev[path[-1]])
    return path[::-1]
# paradas de festa (praças e pontos famosos) e bairros por onde a carreta passa
STOPS = [('Catedral', -25.0975, -50.1589), ('Estação Saudade', -25.0940, -50.1530), ('Olarias', -25.1100, -50.1420),
         ('Estádio (Oficinas)', -25.1150, -50.1560), ('Estrela', -25.1100, -50.1700), ('Ronda', -25.0995, -50.1745),
         ('Nova Rússia', -25.0830, -50.1740), ('Órfãs', -25.0720, -50.1600), ('Jardim Carvalho', -25.0790, -50.1500),
         ('Uvaranas', -25.0905, -50.1300)]
wp = [nearest(*P(la, lo)) for _, la, lo in STOPS]
full = []
for a, b in zip(wp, wp[1:] + wp[:1]):
    seg = route(a, b)
    if seg is None: print('sem rota', a, b); continue
    full += seg if not full else seg[1:]
rpts = [npos[n] for n in full]
# tira ida-e-volta (pontas) e reamostra a cada 4 m
res = [rpts[0]]
for p in rpts[1:]:
    q = res[-1]; L = math.hypot(p[0] - q[0], p[1] - q[1])
    if L < 0.5: continue
    k = max(1, int(L / 4))
    for i in range(1, k + 1): res.append((q[0] + (p[0] - q[0]) * i / k, q[1] + (p[1] - q[1]) * i / k))
rl = sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(res, res[1:]))
print('percurso', len(res), 'pontos', round(rl / 1000, 1), 'km')
stops_xz = [P(la, lo) for _, la, lo in STOPS]

# ---------------------------------------------------------------- pedaços (chunks)
CH = 200.0
def cid(x, z): return (int(math.floor(x / CH)), int(math.floor(z / CH)))
chunks = {}
def C(k): return chunks.setdefault(k, dict(roads=[], bld=[], areas=[], trees=[], water=[]))
for r in roads:
    pts = r['pts']; cur = None; piece = []
    # quebra em pedaços por chunk (com subdivisão a cada 4 m para seguir o relevo)
    dense = [pts[0]]
    for p in pts[1:]:
        q = dense[-1]; L = math.hypot(p[0] - q[0], p[1] - q[1]); k = max(1, int(L / 4))
        for i in range(1, k + 1): dense.append((q[0] + (p[0] - q[0]) * i / k, q[1] + (p[1] - q[1]) * i / k))
    for a, b in zip(dense, dense[1:]):
        k = cid((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        if k != cur:
            if len(piece) > 1: C(cur)['roads'].append((r, piece))
            cur = k; piece = [a]
        piece.append(b)
    if len(piece) > 1: C(cur)['roads'].append((r, piece))
for w in waterways:
    for a, b in zip(w['pts'], w['pts'][1:]):
        C(cid((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))['water'].append((w['w'], [a, b]))
for b in buildings:
    xs = [p[0] for p in b['pts']]; zs = [p[1] for p in b['pts']]
    C(cid(sum(xs) / len(xs), sum(zs) / len(zs)))['bld'].append(b)
for k, p in areas:
    if p.is_empty: continue
    a, b, c, d = p.bounds
    for i in range(int(math.floor(a / CH)), int(math.floor(c / CH)) + 1):
        for j in range(int(math.floor(b / CH)), int(math.floor(d / CH)) + 1):
            try: piece = p.intersection(box(i * CH, j * CH, (i + 1) * CH, (j + 1) * CH))
            except Exception: continue
            if piece.is_empty: continue
            gs = piece.geoms if hasattr(piece, 'geoms') else [piece]
            for g in gs:
                if g.geom_type == 'Polygon' and g.area > 4: C((i, j))['areas'].append((k, g))
for t in trees: C(cid(t[0], t[1]))['trees'].append(t)
print('chunks', len(chunks))

# ---------------------------------------------------------------- escrita binária
def tri_poly(g):
    ext = list(g.exterior.coords)[:-1]
    rings = [ext] + [list(r.coords)[:-1] for r in g.interiors]
    verts = np.array([p for r in rings for p in r], dtype=np.float64)
    ends = np.cumsum([len(r) for r in rings]).astype(np.uint32)
    idx = earcut.triangulate_float64(verts, ends)
    return verts, idx

def wstr(f, s):
    b = s.encode('utf-8')[:255]; f.write(struct.pack('<B', len(b))); f.write(b)

blob = bytearray(); index = []
for (ci, cj), c in sorted(chunks.items()):
    out = bytearray()
    def W(fmt, *a): out.extend(struct.pack(fmt, *a))
    W('<H', len(c['roads']))
    for r, pts in c['roads']:
        W('<BfBH', ROAD_T[r['kind']], r['w'], 1 if r['unpaved'] else 0, len(pts))
        W('<B', 1 if r['kind'] in SIDEWALK and SIDEWALK[r['kind']] > 0 else 0)
        for x, z in pts: W('<ff', x, z)
    W('<H', len(c['water']))
    for w, pts in c['water']:
        W('<fH', w, len(pts))
        for x, z in pts: W('<ff', x, z)
    W('<H', len(c['bld']))
    for b in c['bld']:
        pts = b['pts']
        try:
            g = Polygon(pts)
            if not g.exterior.is_ccw: pts = pts[::-1]
            verts, idx = tri_poly(Polygon(pts))
        except Exception: continue_flag = True; idx = np.array([], np.uint32)
        W('<BfBBH', b['kind'], b['height'], b.get('roof', 0), rnd.randrange(255), len(pts))
        for x, z in pts: W('<ff', x, z)
        W('<H', len(idx)); out.extend(np.asarray(idx, '<u2').tobytes())
    W('<H', len(c['areas']))
    for k, g in c['areas']:
        try: verts, idx = tri_poly(g)
        except Exception: verts, idx = np.zeros((0, 2)), np.array([], np.uint32)
        W('<BH', AREA_ID[k], len(verts))
        out.extend(verts.astype('<f4').tobytes())
        W('<I', len(idx)); out.extend(np.asarray(idx, '<u2' if len(verts) < 65535 else '<u4').tobytes())
    W('<H', len(c['trees']))
    for x, z, k in c['trees']: W('<ffB', x, z, k)
    index.append((ci, cj, len(blob), len(out)))
    blob.extend(out)

with open(OUT + '/pg_map.bytes', 'wb') as f:
    f.write(b'PGM1')
    f.write(struct.pack('<fffI', CH, ELEV0, 0, len(index)))
    for ci, cj, off, ln in index: f.write(struct.pack('<hhII', ci, cj, off, ln))
    f.write(struct.pack('<I', len(landmarks)))
    for L in landmarks: f.write(struct.pack('<B', 0)); wstr(f, L['kind']); wstr(f, L['name']); f.write(struct.pack('<fff', L['x'], L['z'], L['r']))
    f.write(struct.pack('<I', len(labels)))
    for n, x, z in labels: wstr(f, n); f.write(struct.pack('<ff', x, z))
    f.write(struct.pack('<I', len(res)))
    for x, z in res: f.write(struct.pack('<ff', x, z))
    f.write(struct.pack('<I', len(stops_xz)))
    for (n, _, _), (x, z) in zip(STOPS, stops_xz): wstr(f, n); f.write(struct.pack('<ff', x, z))
    f.write(blob)
with open(OUT + '/pg_height.bytes', 'wb') as f:
    f.write(struct.pack('<IIffff', NX, NZ, xmin, zmin, CELL, ELEV0))
    f.write(np.clip((H - ELEV0) * 10, 0, 65535).astype('<u2').tobytes())
print('ok', os.path.getsize(OUT + '/pg_map.bytes') // 1024, 'KB', os.path.getsize(OUT + '/pg_height.bytes') // 1024, 'KB')
json.dump(dict(route=res, stops=stops_xz, lm=[(L['x'], L['z'], L['name']) for L in landmarks]), open('/home/user/pg/debug.json', 'w'))

# ---------------------------------------------------------------- mapa de cores do chão (4 m por pixel)
from PIL import Image, ImageDraw, ImageFilter
GP = 4.0
IW, IH = int((xmax - xmin) / GP), int((zmax - zmin) / GP)
def px(x, z): return ((x - xmin) / GP, (zmax - z) / GP)
img = Image.new('RGB', (IW, IH), (128, 150, 92))
dr = ImageDraw.Draw(img)
COL = {'grass': (120, 160, 80), 'park': (86, 150, 66), 'forest': (46, 92, 40), 'water': (52, 110, 170), 'pitch': (70, 160, 70),
       'parking': (120, 120, 118), 'cemetery': (140, 150, 120), 'sport': (110, 150, 90), 'plaza': (190, 180, 160)}
for k, p in zones:   # bairros: terreno urbano (gramado/terra batida)
    if p.is_empty or p.geom_type != 'Polygon': continue
    c = {'residential': (150, 156, 110), 'commercial': (170, 165, 150), 'retail': (170, 165, 150), 'industrial': (150, 145, 135)}[k]
    dr.polygon([px(*q) for q in p.exterior.coords], fill=c)
order = ['grass', 'cemetery', 'sport', 'forest', 'park', 'parking', 'plaza', 'pitch', 'water']
for kk in order:
    for k, p in areas:
        if k != kk or p.is_empty: continue
        for g in (p.geoms if hasattr(p, 'geoms') else [p]):
            if g.geom_type == 'Polygon' and g.area > 4: dr.polygon([px(*q) for q in g.exterior.coords], fill=COL[k])
# terreno ao redor das construções (quintais/calçadas): um pouco mais claro
for b in buildings:
    if b['kind'] in (1, 2, 3, 7): dr.polygon([px(*q) for q in b['pts']], fill=(175, 170, 160))
img = img.filter(ImageFilter.GaussianBlur(0.7))
# variação natural (ruído)
noise = (np.random.default_rng(1).random((IH, IW, 1)) * 18 - 9).astype(np.int16)
arr = np.clip(np.asarray(img).astype(np.int16) + noise, 0, 255).astype(np.uint8)
Image.fromarray(arr).save(OUT + '/pg_ground.png', optimize=True)
with open(OUT + '/pg_ground_meta.txt', 'w') as f: f.write(f'{xmin} {zmin} {xmax} {zmax}')
print('chão', IW, IH, os.path.getsize(OUT + '/pg_ground.png') // 1024, 'KB')
