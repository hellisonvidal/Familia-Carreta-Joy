# Miniaturas 3D dos pontos de partida (menu "Partida"): prédios, ruas e árvores reais de cada local
# (dados do pg_map.bytes, que vêm do OpenStreetMap), renderizados no Blender em vista aérea.
# uso: python -I miniaturas.py -- pasta_saida
import bpy, bmesh, struct, sys, os, math, random
import numpy as np
from mathutils import Vector

OUT = sys.argv[sys.argv.index('--') + 1]
MAP = '/home/user/familia-carreta-joy/unity/Assets/Resources/Map/'
d = open(MAP + 'pg_map.bytes', 'rb').read()
o = 0
def R(fmt):
    global o; v = struct.unpack_from(fmt, d, o); o += struct.calcsize(fmt); return v
def S():
    global o; n = d[o]; o += 1; s = d[o:o + n].decode(); o += n; return s
assert d[:4] == b'PGM1'; o = 4
CH, E0, _, n = R('<fffI'); idx = [R('<hhII') for _ in range(n)]
L = [(R('<B'), S(), S(), R('<fff')) for _ in range(R('<I')[0])]
labels = [(S(), R('<ff')) for _ in range(R('<I')[0])]
route = [R('<ff') for _ in range(R('<I')[0])]
stops = [(S(), R('<ff')) for _ in range(R('<I')[0])]
base = o

def read_area(cx, cz, rad):
    global o
    roads, builds, areas, trees = [], [], [], []
    for ci, cj, off, ln in idx:
        if abs((ci + .5) * CH - cx) > rad + CH or abs((cj + .5) * CH - cz) > rad + CH: continue
        o = base + off
        for _ in range(R('<H')[0]):
            t, w, unp, np_ = R('<BfBH'); sw, = R('<B'); pts = [R('<ff') for _ in range(np_)]; roads.append((w, unp, pts))
        for _ in range(R('<H')[0]):
            w, np_ = R('<fH'); [R('<ff') for _ in range(np_)]
        for _ in range(R('<H')[0]):
            k, h, roof, col, np_ = R('<BfBBH'); pts = [R('<ff') for _ in range(np_)]; ni, = R('<H'); o += 2 * ni
            builds.append((k, h, col, pts))
        for _ in range(R('<H')[0]):
            k, nv = R('<BH'); v = np.frombuffer(d, '<f4', nv * 2, o).reshape(-1, 2); o += nv * 8; ni, = R('<I'); o += ni * (2 if nv < 65535 else 4)
            areas.append((k, v))
        for _ in range(R('<H')[0]):
            x, z, k = R('<ffB'); trees.append((x, z, k))
    return roads, builds, areas, trees

HOUSE = [(0.97, 0.95, 0.88), (0.98, 0.88, 0.6), (0.72, 0.85, 0.95), (0.98, 0.78, 0.65), (0.75, 0.92, 0.78), (0.85, 0.85, 0.85), (0.97, 0.75, 0.8), (0.92, 0.83, 0.7)]
APT = [(0.95, 0.94, 0.9), (0.86, 0.8, 0.7), (0.7, 0.72, 0.75), (0.78, 0.62, 0.5), (0.6, 0.68, 0.78), (0.9, 0.88, 0.8)]

def mat(name, col, rough=0.7, emit=0):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*[c ** 2.2 for c in col], 1); b.inputs['Roughness'].default_value = rough
    if emit: b.inputs['Emission Color'].default_value = (*[c ** 2.2 for c in col], 1); b.inputs['Emission Strength'].default_value = emit
    return m

def scene_for(i, name, cx, cz, rad=230):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    roads, builds, areas, trees = read_area(cx, cz, rad)
    # chão: recorte do mapa pintado (4 m por pixel)
    x0, z0, x1, z1 = [float(v) for v in open(MAP + 'pg_ground_meta.txt').read().split()[:4]]
    img = bpy.data.images.load(MAP + 'pg_ground.png', check_existing=True)
    gm = bpy.data.materials.new('chao'); gm.use_nodes = True
    nt = gm.node_tree; tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Linear'
    nt.links.new(tex.outputs[0], nt.nodes['Principled BSDF'].inputs[0]); nt.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.95
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new()
    R2 = rad * 1.6
    vs = [bm.verts.new((cx + a * R2, cz + b * R2, 0)) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    f = bm.faces.new(vs)
    for lp in f.loops:
        x, y = lp.vert.co.x, lp.vert.co.y
        lp[uvl].uv = ((x - x0) / (x1 - x0), (y - z0) / (z1 - z0))
    me = bpy.data.meshes.new('chao'); bm.to_mesh(me); bm.free()
    g = bpy.data.objects.new('chao', me); sc.collection.objects.link(g); me.materials.append(gm)
    # ruas (faixa escura um pouco acima do chão)
    bm = bmesh.new()
    for w, unp, pts in roads:
        if unp or len(pts) < 2: continue
        hw = max(w, 3) / 2
        for a, b in zip(pts[:-1], pts[1:]):
            ax_, az = a; bx, bz = b
            dx, dz = bx - ax_, bz - az; l = math.hypot(dx, dz)
            if l < 0.01: continue
            nx, nz = -dz / l * hw, dx / l * hw
            q = [bm.verts.new((ax_ + nx, az + nz, 0.15)), bm.verts.new((bx + nx, bz + nz, 0.15)), bm.verts.new((bx - nx, bz - nz, 0.15)), bm.verts.new((ax_ - nx, az - nz, 0.15))]
            bm.faces.new(q)
    me = bpy.data.meshes.new('ruas'); bm.to_mesh(me); bm.free()
    ro = bpy.data.objects.new('ruas', me); sc.collection.objects.link(ro); me.materials.append(mat('asfalto', (0.22, 0.22, 0.24), 0.8))
    # prédios extrudados com a altura real (cores parecidas com as do jogo)
    rnd = random.Random(i)
    groups = {}
    for k, h, col, pts in builds:
        if len(pts) < 3: continue
        cxp = sum(p[0] for p in pts) / len(pts); czp = sum(p[1] for p in pts) / len(pts)
        if abs(cxp - cx) > R2 or abs(czp - cz) > R2: continue
        pal = APT if h > 9 else HOUSE
        c = pal[col % len(pal)] if k != 5 else (1.0, 0.45, 0.75)
        key = (round(c[0], 2), round(c[1], 2), round(c[2], 2))
        bm = groups.setdefault(key, bmesh.new())
        h = max(h, 3.0)
        bot = [bm.verts.new((x, z, 0)) for x, z in pts]
        top = [bm.verts.new((x, z, h)) for x, z in pts]
        try:
            bm.faces.new(top)
            for j in range(len(pts)):
                bm.faces.new((bot[j], bot[(j + 1) % len(pts)], top[(j + 1) % len(pts)], top[j]))
        except ValueError:
            pass
    for key, bm in groups.items():
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new('predios'); bm.to_mesh(me); bm.free()
        ob = bpy.data.objects.new('predios', me); sc.collection.objects.link(ob); me.materials.append(mat('p%s' % (key,), key, 0.6))
    # árvores
    bm = bmesh.new()
    for x, z, k in trees:
        if abs(x - cx) > R2 or abs(z - cz) > R2: continue
        hgt = rnd.uniform(6, 11)
        r = bmesh.ops.create_cone(bm, cap_ends=True, segments=7, radius1=hgt * 0.32, radius2=0.2 if k == 0 else hgt * 0.15, depth=hgt * 0.75)
        bmesh.ops.translate(bm, vec=Vector((x, z, hgt * 0.62)), verts=r['verts'])
    me = bpy.data.meshes.new('arvores'); bm.to_mesh(me); bm.free()
    to = bpy.data.objects.new('arvores', me); sc.collection.objects.link(to); me.materials.append(mat('folha', (0.16, 0.45, 0.2), 0.9))
    # marcador da partida: pino rosa brilhante
    bpy.ops.mesh.primitive_cylinder_add(radius=2.2, depth=60, location=(cx, cz, 30))
    bpy.context.object.data.materials.append(mat('pino', (1.0, 0.15, 0.6), 0.3, emit=4))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=9, location=(cx, cz, 66))
    bpy.context.object.data.materials.append(mat('pino', (1.0, 0.15, 0.6), 0.3, emit=4))
    bpy.ops.mesh.primitive_torus_add(major_radius=18, minor_radius=1.6, location=(cx, cz, 0.6))
    bpy.context.object.data.materials.append(mat('anel', (1.0, 0.85, 0.2), 0.3, emit=3))
    # câmera aérea em 3/4, sol de fim de tarde
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 32
    cam.location = (cx + 170, cz - 230, 230)
    cam.rotation_euler = (cam.location - Vector((cx, cz + 25, 0))).to_track_quat('Z', 'Y').to_euler()
    sun = bpy.data.objects.new('sol', bpy.data.lights.new('s', 'SUN')); sc.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(140)); sun.data.energy = 4; sun.data.angle = math.radians(3)
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs[0].default_value = (0.55, 0.7, 0.95, 1); bg.inputs[1].default_value = 0.9
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'; sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
    sc.render.resolution_x, sc.render.resolution_y = 512, 320
    sc.render.film_transparent = False
    sc.render.filepath = os.path.join(OUT, 'Partida_%d.png' % i)
    bpy.ops.render.render(write_still=True)
    print('OK', i, name, len(builds), 'prédios', len(roads), 'ruas', len(trees), 'árvores')

os.makedirs(OUT, exist_ok=True)
for i, (name, (x, z)) in enumerate(stops):
    scene_for(i, name, x, z)
