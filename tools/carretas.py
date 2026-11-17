# Carretas do jogo, modeladas por código no Blender.
#  - Rodas separadas ("Roda_N", com o aro cromado como filho) para girarem no jogo.
#  - Cada parte leva o nome do material ("M_Pintura", "M_Cromo"...): o jogo troca pelo material certo.
#  - "TetoA"/"TetoB" marcam os cantos do palco onde a turma sobe e dança.
# uso: python -I carretas.py -- pasta_saida [render]
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index('--') + 1:]
OUT = args[0]
RENDER = len(args) > 1 and args[1] == 'render'
ORIG = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Carreta_original.fbx')   # a malha sem rodas separadas
ALBEDO = '/home/user/familia-carreta-joy/unity/Assets/Resources/Vehicles/Carreta_albedo.jpg'

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def link(o):
    bpy.context.scene.collection.objects.link(o)
    return o

def mesh_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    return link(bpy.data.objects.new(name, me))

def bevel(o, w, seg=2, ang=None):
    m = o.modifiers.new('b', 'BEVEL'); m.width = w; m.segments = seg; m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(ang or 40)
    return o

def box(mat, c, s, bev=0.0, seg=2, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(s), verts=bm.verts)
    if rot: bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(rot[1]), 3, rot[0]))
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    o = mesh_obj('M_' + mat, bm)
    if bev > 0: bevel(o, bev, seg)
    return o

def cyl(mat, c, r, h, axis='Z', n=24, bev=0.0, r2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=n, radius1=r, radius2=r if r2 is None else r2, depth=h)
    if axis == 'X': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'))
    if axis == 'Y': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    o = mesh_obj('M_' + mat, bm)
    if bev > 0: bevel(o, bev, 2)
    for p in o.data.polygons: p.use_smooth = True
    return o

def sphere(mat, c, r, seg=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2, radius=r)
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    o = mesh_obj('M_' + mat, bm)
    for p in o.data.polygons: p.use_smooth = True
    return o

def tube(mat, a, b, r=0.04, n=10):
    a, b = Vector(a), Vector(b)
    d = b - a
    o = cyl(mat, (0, 0, 0), r, d.length, n=n)
    o.matrix_world = Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    return o

# ------------------------------------------------------------------ rodas
def wheel(idx, c, r, w, twin=False, side=1):
    """Pneu com banda de rodagem + aro cromado com porcas. Centro em c, eixo em X."""
    bm = bmesh.new()
    parts = [(-w / 4, w / 2 - 0.01), (w / 4, w / 2 - 0.01)] if twin else [(0, w)]
    for off, ww in parts:
        g = bmesh.ops.create_cone(bm, cap_ends=True, segments=28, radius1=r, radius2=r, depth=ww)
        bmesh.ops.rotate(bm, verts=g['verts'], cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'))
        bmesh.ops.translate(bm, vec=Vector((off, 0, 0)), verts=g['verts'])
    t = mesh_obj('Roda_%d' % idx, bm)
    bevel(t, r * 0.12, 2, 50)
    for p in t.data.polygons: p.use_smooth = True
    apply_mods([t])
    # garras da banda de rodagem (depois do chanfro, para não multiplicar triângulos)
    bm = bmesh.new(); bm.from_mesh(t.data)
    for k in range(20):
        a = k / 20 * 2 * math.pi
        g = bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector((w * 0.86, 0.09 * r, 0.06 * r)), verts=g['verts'])
        bmesh.ops.translate(bm, vec=Vector((0, 0, r * 1.0)), verts=g['verts'])
        bmesh.ops.rotate(bm, verts=g['verts'], cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, 'X'))
    bm.to_mesh(t.data); bm.free()
    # aro: disco, cubo, porcas (do lado de fora)
    rim = []
    sx = side * (w / 2 + 0.005)
    rim.append(cyl('Cromo', (sx * 0.96, 0, 0), r * 0.62, r * 0.06, axis='X', n=28))
    rim.append(cyl('Cromo', (sx * 0.96 + side * r * 0.05, 0, 0), r * 0.28, r * 0.12, axis='X', n=16, r2=r * 0.2))
    rim.append(cyl('Preto', (sx * 0.96 + side * r * 0.025, 0, 0), r * 0.48, r * 0.025, axis='X', n=28))
    for k in range(10):
        a = k / 10 * 2 * math.pi
        rim.append(cyl('Cromo', (sx * 0.96 + side * r * 0.05, math.cos(a) * r * 0.38, math.sin(a) * r * 0.38), r * 0.035, r * 0.07, axis='X', n=6))
    apply_mods(rim)
    bpy.ops.object.select_all(action='DESELECT')
    for o in rim: o.select_set(True)
    bpy.context.view_layer.objects.active = rim[0]
    bpy.ops.object.join()
    aro = bpy.context.view_layer.objects.active; aro.name = 'M_Cromo_aro'
    # origem no centro da roda
    t.location = Vector(c); aro.location = Vector(c)
    aro.parent = t; aro.location = (0, 0, 0)
    return t

def apply_mods(objs):
    for o in objs:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

def join_by_material():
    """Junta as peças soltas (não rodas) por material, para o jogo ter poucos objetos."""
    groups = {}
    for o in list(bpy.data.objects):
        if o.type != 'MESH' or o.parent is not None or o.name.startswith('Roda_') or o.name == 'Carreta': continue
        key = o.name.split('.')[0]
        groups.setdefault(key, []).append(o)
    apply_mods([o for g in groups.values() for o in g])
    for key, objs in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1: bpy.ops.object.join()
        bpy.context.view_layer.objects.active.name = key

def marker(name, p):
    e = bpy.data.objects.new(name, None); e.location = Vector(p); link(e)

def export(path):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', path_mode='STRIP', mesh_smooth_type='FACE')

# ------------------------------------------------------------------ peças
W = 2.5; HW = W / 2
R = 0.52   # raio das rodas

def cab_over(c1, c2):
    """Cabine europeia (frente reta), frente em -Y: y de -8 a -5.7."""
    y0, y1 = -8.0, -5.75
    box('Preto', (0, -4.5, 0.75), (0.9, 7.0, 0.3))                               # longarinas
    box('Pintura', (0, (y0 + y1) / 2 + 0.05, 2.15), (W, y1 - y0 - 0.1, 2.3), bev=0.18, seg=3)     # cabine
    box('Pintura2', (0, (y0 + y1) / 2 + 0.05, 3.42), (W - 0.1, y1 - y0 - 0.3, 0.3), bev=0.12)     # teto alto
    box('Pintura', (0, (y0 + y1) / 2 + 0.25, 3.75), (W - 0.3, y1 - y0 - 0.9, 0.4), bev=0.15)      # defletor
    box('Vidro', (0, y0 - 0.01, 2.55), (W - 0.35, 0.06, 1.05), bev=0.04)           # para-brisa
    for s in (-1, 1):
        box('Vidro', (s * (HW + 0.005), y0 + 0.7, 2.6), (0.04, 0.95, 0.85), bev=0.02)   # janelas laterais
        box('Cromo', (s * (HW + 0.25), y0 + 0.25, 2.65), (0.08, 0.08, 0.6), bev=0.02)   # retrovisor
        tube('Cromo', (s * (HW - 0.05), y0 + 0.5, 2.95), (s * (HW + 0.25), y0 + 0.25, 2.95), 0.025)
        box('Preto', (s * (HW - 0.1), y0 + 1.2, 0.85), (0.25, 0.7, 0.18), bev=0.03)     # degraus
        box('Preto', (s * (HW - 0.1), y0 + 1.2, 0.55), (0.25, 0.7, 0.18), bev=0.03)
        cyl('Cromo', (s * (HW - 0.35), -4.9, 0.95), 0.33, 1.3, axis='Y', n=24, bev=0.05)  # tanques
        cyl('Cromo', (s * (HW - 0.25), -5.55, 3.3), 0.09, 2.4, n=14)                    # escapamentos
        cyl('Preto', (s * (HW - 0.25), -5.55, 4.55), 0.1, 0.12, n=14)
        box('Pintura2', (s * (HW - 0.12), -7.25, 1.05), (0.3, 1.25, 0.12), bev=0.04)   # para-lama
        box('Farol', (s * (HW - 0.4), y0 - 0.03, 1.1), (0.55, 0.06, 0.25), bev=0.04)   # faróis
        box('Lanterna', (s * (HW - 0.12), y0 - 0.03, 1.45), (0.18, 0.06, 0.12))        # pisca
    box('Cromo', (0, y0 - 0.06, 1.65), (W - 0.6, 0.05, 0.75), bev=0.03)            # grade
    for k in range(6):
        box('Preto', (0, y0 - 0.08, 1.35 + k * 0.11), (W - 0.75, 0.04, 0.05))
    box('Preto', (0, y0 - 0.1, 0.8), (W + 0.05, 0.3, 0.4), bev=0.08)                # para-choque
    box('Cromo', (0, y0 - 0.25, 0.8), (W - 0.2, 0.06, 0.12), bev=0.02)
    box('Preto', (0, y0 - 0.05, 3.15), (W - 0.2, 0.35, 0.12), bev=0.03)             # quebra-sol
    for k in range(5):
        box('Farol', (-0.8 + k * 0.4, y0 + 0.1, 3.62), (0.16, 0.1, 0.1), bev=0.02)    # luzes do teto
    box('Neon', (0, y0 - 0.02, 3.25), (W - 0.3, 0.05, 0.05))
    for s in (-1, 1):
        box('Neon', (s * (HW + 0.01), (y0 + y1) / 2, 1.05), (0.04, y1 - y0 - 0.3, 0.05))

def cab_bonnet(c1, c2):
    """Cabine americana (bicuda), frente em -Y: capô longo + cabine + dormitório."""
    y0 = -8.0
    box('Preto', (0, -4.5, 0.75), (0.9, 7.0, 0.3))
    box('Pintura', (0, y0 + 1.0, 1.55), (W - 0.6, 2.0, 1.1), bev=0.25, seg=4)       # capô
    box('Cromo', (0, y0 - 0.02, 1.5), (W - 0.85, 0.12, 1.0), bev=0.05)             # grade alta
    for k in range(9):
        box('Preto', (-0.7 + k * 0.175, y0 - 0.08, 1.5), (0.05, 0.04, 0.85))
    box('Pintura2', (0, y0 + 2.9, 2.1), (W, 1.8, 2.2), bev=0.18, seg=3)            # cabine
    box('Pintura', (0, y0 + 3.4, 3.35), (W - 0.15, 1.0, 0.5), bev=0.18)            # dormitório alto
    box('Vidro', (0, y0 + 2.0, 2.65), (W - 0.4, 0.05, 0.75), rot=('X', -12), bev=0.03)
    for s in (-1, 1):
        box('Vidro', (s * (HW + 0.005), y0 + 2.65, 2.6), (0.04, 0.95, 0.7), bev=0.02)
        box('Pintura2', (s * (HW - 0.2), y0 + 0.9, 1.15), (0.45, 1.6, 0.55), bev=0.2, seg=3)   # para-lamas
        box('Farol', (s * (HW - 0.45), y0 + 0.1, 1.25), (0.3, 0.06, 0.2), bev=0.04)
        cyl('Cromo', (s * (HW - 0.12), y0 + 3.85, 3.6), 0.11, 3.4, n=16)            # chaminés
        cyl('Cromo', (s * (HW - 0.12), y0 + 3.85, 5.35), 0.13, 0.15, n=16, r2=0.1)
        box('Cromo', (s * (HW + 0.25), y0 + 2.2, 2.6), (0.08, 0.1, 0.55), bev=0.02)
        tube('Cromo', (s * (HW - 0.05), y0 + 2.3, 2.9), (s * (HW + 0.25), y0 + 2.2, 2.9), 0.025)
        cyl('Cromo', (s * (HW - 0.35), -4.9, 0.95), 0.33, 1.3, axis='Y', n=24, bev=0.05)
        box('Preto', (s * (HW - 0.1), y0 + 2.6, 0.7), (0.25, 0.6, 0.15), bev=0.03)
        box('Lanterna', (s * (HW - 0.05), y0 + 3.9, 3.7), (0.1, 0.15, 0.1))
    box('Cromo', (0, y0 - 0.15, 0.8), (W + 0.05, 0.25, 0.35), bev=0.08)            # para-choque cromado
    for k in range(5):
        box('Farol', (-0.6 + k * 0.3, y0 + 2.2, 3.28), (0.12, 0.08, 0.08), bev=0.02)
    box('Neon', (0, y0 - 0.02, 2.05), (W - 0.9, 0.05, 0.05))

def axles(ys, twin, start_idx, z=R):
    idx = start_idx
    for y in ys:
        for s in (-1, 1):
            w = 0.62 if twin else 0.34
            x = s * (HW - w / 2 - 0.02)
            wheel(idx, (x, y, z), R, w, twin=twin, side=s); idx += 1
        box('Preto', (0, y, z), (W - 0.6, 0.14, 0.14))                              # eixo
    return idx

def trailer_base(y0, y1, deck, c_skirt='Pintura'):
    L = y1 - y0
    box('Preto', (0, (y0 + y1) / 2, 1.15), (W - 0.2, L, 0.3), bev=0.03)             # chassi
    box('Preto', (0, -5.2, 1.15), (1.2, 0.8, 0.12))                                 # quinta-roda
    for s in (-1, 1):
        box('Cromo', (s * (HW - 0.02), (y0 + y1) / 2, 1.33), (0.05, L, 0.08))        # friso
        for k in range(int(L / 1.4)):
            box('Lanterna', (s * (HW + 0.01), y0 + 0.7 + k * 1.4, 1.2), (0.03, 0.1, 0.06))   # luzes laterais âmbar
    # para-lamas sobre as rodas traseiras
    for s in (-1, 1):
        box('Pintura2', (s * (HW - 0.3), 4.95, 1.18), (0.62, 3.6, 0.08), bev=0.03)
    # traseira
    box('Preto', (0, y1 + 0.05, 0.95), (W, 0.12, 0.25), bev=0.03)
    for s in (-1, 1):
        box('Lanterna', (s * (HW - 0.3), y1 + 0.12, 1.0), (0.45, 0.04, 0.16), bev=0.02)
    box('Preto', (0, y1 + 0.1, 0.55), (W - 0.4, 0.06, 0.12))                         # para-choque baixo
    box('Preto', (0, (y0 + y1) / 2, deck - 0.04), (W, L, 0.08))                     # piso do palco
    box('Piso', (0, (y0 + y1) / 2, deck + 0.005), (W - 0.1, L - 0.1, 0.02))

def railing(y0, y1, deck, gap_front=False):
    h = deck + 1.0
    posts = []
    n = int((y1 - y0) / 1.5)
    for s in (-1, 1):
        x = s * (HW - 0.06)
        for k in range(n + 1):
            y = y0 + k * (y1 - y0) / n
            tube('Cromo', (x, y, deck), (x, y, h), 0.035)
        tube('Cromo', (x, y0, h), (x, y1, h), 0.04)
        tube('Cromo', (x, y0, deck + 0.5), (x, y1, deck + 0.5), 0.025)
        box('Neon', (x, (y0 + y1) / 2, h + 0.05), (0.05, y1 - y0, 0.04))
    tube('Cromo', (-HW + 0.06, y1, h), (HW - 0.06, y1, h), 0.04)
    tube('Cromo', (-HW + 0.06, y1, deck + 0.5), (HW - 0.06, y1, deck + 0.5), 0.025)
    if not gap_front:
        tube('Cromo', (-HW + 0.06, y0, h), (HW - 0.06, y0, h), 0.04)

def speaker(c, w, h, d, face=1):
    """Caixa de som: gabinete preto, cones e grade; face=-1 (para a frente) ou 1 (para trás)."""
    x, y, z = c
    box('Caixa', (x, y, z), (w, d, h), bev=0.03)
    n = max(1, int(h / (w * 0.9)))
    for k in range(n):
        zz = z - h / 2 + (k + 0.5) * h / n
        r = min(w, h / n) * 0.38
        cyl('Preto', (x, y + face * (d / 2 + 0.005), zz), r, 0.02, axis='Y', n=24)
        cyl('Cromo', (x, y + face * (d / 2 + 0.012), zz), r * 0.3, 0.02, axis='Y', n=16, r2=r * 0.15)
        cyl('Neon', (x, y + face * (d / 2 + 0.008), zz), r * 1.05, 0.006, axis='Y', n=24)

def truss_arch(y, deck, top):
    """Pórtico de treliça com refletores."""
    for s in (-1, 1):
        x = s * (HW - 0.15)
        for dx in (-0.12, 0.12):
            for dy in (-0.12, 0.12):
                tube('Cromo', (x + dx, y + dy, deck), (x + dx, y + dy, top), 0.025, n=8)
        for k in range(int((top - deck) / 0.35)):
            z = deck + k * 0.35
            tube('Cromo', (x - 0.12, y - 0.12, z), (x + 0.12, y - 0.12, z + 0.35), 0.012, n=6)
            tube('Cromo', (x - 0.12, y + 0.12, z), (x + 0.12, y + 0.12, z + 0.35), 0.012, n=6)
    for dz in (-0.12, 0.12):
        for dy in (-0.12, 0.12):
            tube('Cromo', (-HW + 0.15, y + dy, top + dz), (HW - 0.15, y + dy, top + dz), 0.025, n=8)
    for k in range(int((W - 0.3) / 0.3)):
        x = -HW + 0.15 + k * 0.3
        tube('Cromo', (x, y - 0.12, top - 0.12), (x + 0.3, y - 0.12, top + 0.12), 0.012, n=6)
    for k in range(4):
        x = -0.75 + k * 0.5
        cyl('Preto', (x, y, top - 0.32), 0.12, 0.28, n=14)
        cyl('Farol', (x, y, top - 0.47), 0.1, 0.02, n=14)

def disco_ball(c, r):
    sphere('Cromo', c, r, seg=20)
    tube('Cromo', (c[0], c[1], c[2] + r), (c[0], c[1], c[2] + r + 0.5), 0.015, n=6)

def palm(c, h):
    x, y, z = c
    for k in range(6):
        cyl('Madeira', (x + k * 0.02, y, z + k * h / 6 + h / 12), 0.12 - k * 0.01, h / 6 + 0.02, n=10)
    for k in range(7):
        a = k / 7 * 2 * math.pi
        e = Vector((math.cos(a), math.sin(a), 0))
        for j in range(3):
            p = Vector((x, y, z + h)) + e * (0.35 + j * 0.45) + Vector((0, 0, -0.12 * j * j))
            box('Folha', tuple(p), (0.5, 0.22, 0.04), rot=('Z', math.degrees(a)))

# ------------------------------------------------------------------ variações
def variant_neon():
    """Carreta Neon: baú com faixas de LED, palco com grade cromada, caixas de som e pórtico de luz."""
    cab_over('Pintura', 'Pintura2')
    deck = 3.25
    trailer_base(-5.5, 8.0, deck)
    box('Pintura', (0, 1.25, 2.25), (W, 13.4, 1.9), bev=0.06)                        # baú
    for s in (-1, 1):
        for k, z in enumerate((1.75, 2.25, 2.75)):
            box('Neon', (s * (HW + 0.01), 1.25, z), (0.03, 12.6, 0.07))
        box('Pintura2', (s * (HW + 0.012), 1.25, 2.25), (0.02, 4.5, 1.3), bev=0.02)   # painel central
        for k in range(8):
            cyl('Farol', (s * (HW + 0.02), -4.2 + k * 1.7, 3.05), 0.07, 0.03, axis='X', n=10)
    railing(-5.3, 7.8, deck)
    for s in (-1, 1):
        speaker((s * 0.75, -4.75, deck + 0.75), 0.85, 1.5, 0.7, face=1)
    speaker((0, -4.85, deck + 0.35), 0.6, 0.7, 0.55, face=1)
    truss_arch(7.2, deck, deck + 2.8)
    disco_ball((0, 7.2, deck + 2.25), 0.3)
    axles([4.0, 5.3, 6.6], True, axles([-7.0], False, axles([-4.35, -3.05], True, 0)))
    marker('TetoA', (-HW + 0.25, -4.1, deck)); marker('TetoB', (HW - 0.25, 7.6, deck))

def variant_tropical():
    """Carreta Tropical: cabine bicuda, carroceria com ondas, coqueiros e toldo listrado."""
    cab_bonnet('Pintura', 'Pintura2')
    deck = 3.0
    trailer_base(-5.0, 8.0, deck)
    box('Pintura', (0, 1.5, 2.1), (W, 12.9, 1.7), bev=0.06)
    for s in (-1, 1):
        for k in range(12):
            y = -4.3 + k * 1.1
            box('Pintura2', (s * (HW + 0.012), y, 2.05 + 0.25 * math.sin(k * 1.3)), (0.02, 1.0, 0.45), bev=0.02, rot=('X', 18 * math.cos(k * 1.3)))
        box('Neon', (s * (HW + 0.015), 1.5, 2.9), (0.03, 12.5, 0.06))
    railing(-4.8, 7.8, deck)
    palm((-HW + 0.45, -4.4, deck), 2.6); palm((HW - 0.45, 7.3, deck), 2.6)
    # toldo listrado na frente do palco
    for k in range(6):
        box('Pintura2' if k % 2 else 'Toldo', (-HW + 0.21 + k * 0.415, -3.3, deck + 2.55), (0.415, 2.4, 0.05), rot=('X', -6))
    for s in (-1, 1):
        for y in (-4.4, -2.2):
            tube('Cromo', (s * (HW - 0.1), y, deck), (s * (HW - 0.1), y, deck + 2.55 - (y + 3.3) * 0.1), 0.035)
    for s in (-1, 1):
        speaker((s * 0.7, 7.45, deck + 0.6), 0.8, 1.2, 0.6, face=-1)
    axles([4.2, 5.5, 6.8], True, axles([-7.0], False, axles([-4.35, -3.05], True, 0)))
    marker('TetoA', (-HW + 0.25, -4.6, deck)); marker('TetoB', (HW - 0.25, 7.6, deck))

def variant_disco():
    """Carreta Disco: palco aberto com telão de LED, torres de luz e caixas dos dois lados."""
    cab_over('Pintura', 'Pintura2')
    deck = 2.6
    trailer_base(-5.5, 8.0, deck)
    box('Pintura2', (0, 1.25, 1.9), (W, 13.4, 1.25), bev=0.06)
    for s in (-1, 1):
        for k in range(16):
            box('Neon' if k % 2 == 0 else 'Pintura', (s * (HW + 0.012), -5.0 + k * 0.84, 1.9), (0.02, 0.7, 0.9), bev=0.02)
    railing(-5.3, 7.8, deck, gap_front=True)
    # telão
    box('Preto', (0, -5.15, deck + 1.6), (W - 0.2, 0.3, 2.4), bev=0.05)
    box('Tela', (0, -4.99, deck + 1.65), (W - 0.45, 0.03, 2.1))
    for s in (-1, 1):
        speaker((s * (HW - 0.4), -4.5, deck + 0.6), 0.65, 1.2, 0.55, face=1)
        speaker((s * (HW - 0.4), 7.3, deck + 0.6), 0.65, 1.2, 0.55, face=-1)
        # torres de luz
        x = s * (HW - 0.15)
        tube('Cromo', (x, 2.0, deck), (x, 2.0, deck + 2.6), 0.05)
        for k in range(3):
            cyl('Preto', (x - s * 0.15, 2.0, deck + 1.6 + k * 0.4), 0.11, 0.22, axis='X', n=12)
            cyl('Farol', (x - s * 0.27, 2.0, deck + 1.6 + k * 0.4), 0.09, 0.02, axis='X', n=12)
    disco_ball((0, 2.0, deck + 2.4), 0.28)
    tube('Cromo', (-HW + 0.15, 2.0, deck + 2.6), (HW - 0.15, 2.0, deck + 2.6), 0.04)
    axles([4.0, 5.3, 6.6], True, axles([-7.0], False, axles([-4.35, -3.05], True, 0)))
    marker('TetoA', (-HW + 0.25, -4.3, deck)); marker('TetoB', (HW - 0.25, 7.6, deck))

def original_wheels():
    """Carreta original (malha única): põe rodas que giram por cima das rodas pintadas."""
    bpy.ops.import_scene.fbx(filepath=ORIG)
    for o in list(bpy.data.objects):
        if o.type != 'MESH': bpy.data.objects.remove(o)
    m = [o for o in bpy.data.objects if o.type == 'MESH'][0]
    m.name = 'Carreta'
    bpy.context.view_layer.objects.active = m; m.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    global R
    r, zc = 0.0605, 0.061
    idx = 0
    for y, twin in ((-0.80, False), (-0.59, True), (-0.508, True), (0.262, True), (0.41, True), (0.55, True)):
        for s in (-1, 1):
            w = 0.068 if twin else 0.046
            x = s * (0.167 - w / 2)
            wheel(idx, (x, y, zc), r, w, twin=twin, side=s); idx += 1

def preview(path, dist=26, scale=1.0, pal=None):
    sc = bpy.context.scene
    cols = {'Pintura': (0.9, 0.1, 0.5), 'Pintura2': (0.35, 0.1, 0.8), 'Cromo': (0.9, 0.9, 0.92), 'Preto': (0.03, 0.03, 0.03),
            'Vidro': (0.05, 0.1, 0.15), 'Neon': (0.1, 1, 1), 'Farol': (1, 1, 0.9), 'Lanterna': (1, 0.3, 0.05), 'Piso': (0.1, 0.1, 0.12),
            'Caixa': (0.05, 0.05, 0.06), 'Madeira': (0.45, 0.3, 0.15), 'Folha': (0.1, 0.6, 0.15), 'Toldo': (0.95, 0.95, 0.9), 'Tela': (0.2, 0.4, 1)}
    if pal: cols.update(pal)
    mats = {}
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        key = 'Preto' if o.name.startswith('Roda_') else o.name[2:].split('_')[0].split('.')[0]
        if o.name == 'Carreta':
            mt = bpy.data.materials.new('tex'); mt.use_nodes = True
            t = mt.node_tree.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(ALBEDO)
            mt.node_tree.links.new(t.outputs[0], mt.node_tree.nodes['Principled BSDF'].inputs[0])
            mt.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.35
            o.data.materials.clear(); o.data.materials.append(mt); continue
        if key not in mats:
            mt = bpy.data.materials.new(key); mt.use_nodes = True
            b = mt.node_tree.nodes['Principled BSDF']
            b.inputs['Base Color'].default_value = (*cols.get(key, (0.5, 0.5, 0.5)), 1)
            b.inputs['Metallic'].default_value = 1.0 if key == 'Cromo' else 0.0
            b.inputs['Roughness'].default_value = 0.2 if key in ('Cromo', 'Vidro', 'Pintura', 'Pintura2') else 0.6
            if key in ('Neon', 'Farol', 'Tela'):
                b.inputs['Emission Color'].default_value = (*cols[key], 1); b.inputs['Emission Strength'].default_value = 3
            mats[key] = mt
        o.data.materials.clear(); o.data.materials.append(mats[key])
    cam = link(bpy.data.objects.new('cam', bpy.data.cameras.new('c'))); sc.camera = cam
    cam.data.lens = 50
    cam.location = (dist * 0.62 * scale, -dist * 0.62 * scale, dist * 0.2 * scale)
    tgt = Vector((0, -0.6 * scale, 1.9 * scale))
    floor = bpy.data.objects.new('chao', bpy.data.meshes.new('chao')); link(floor)
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=60 * scale); bm.to_mesh(floor.data); bm.free()
    fm = bpy.data.materials.new('chao'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.08, 0.05, 0.12, 1)
    fm.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.25
    floor.data.materials.append(fm)
    cam.rotation_euler = (cam.location - tgt).to_track_quat('Z', 'Y').to_euler()
    sun = link(bpy.data.objects.new('sun', bpy.data.lights.new('s', 'SUN'))); sun.rotation_euler = (0.7, 0.2, 0.8); sun.data.energy = 3.5
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.25, 0.18, 0.4, 1)
    for k, (pos, col) in enumerate((((-8, -10, 8), (1, 0.3, 0.7)), ((10, 6, 7), (0.3, 0.7, 1)))):
        lo = link(bpy.data.objects.new('l%d' % k, bpy.data.lights.new('l%d' % k, 'AREA')))
        lo.location = Vector(pos) * scale; lo.data.energy = 4000 * scale * scale; lo.data.size = 6 * scale; lo.data.color = col
        lo.rotation_euler = (lo.location - Vector((0, 0, 2 * scale))).to_track_quat('Z', 'Y').to_euler()
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 32; sc.cycles.device = 'CPU'; sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
    sc.render.resolution_x, sc.render.resolution_y = 512, 320
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

def tris():
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')

PAL = {'CarretaNeon': {'Pintura': (0.85, 0.05, 0.45), 'Pintura2': (0.3, 0.05, 0.6)},
       'CarretaTropical': {'Pintura': (1.0, 0.75, 0.05), 'Pintura2': (0.05, 0.6, 0.3)},
       'CarretaDisco': {'Pintura': (0.05, 0.25, 0.85), 'Pintura2': (0.92, 0.92, 0.95)}}
SCR = '/tmp/claude-0/-home-user/f644ca88-04de-5c00-92a9-5f72148979e9/scratchpad/'
for name, fn in (('Carreta', original_wheels), ('CarretaNeon', variant_neon), ('CarretaTropical', variant_tropical), ('CarretaDisco', variant_disco)):
    reset()
    fn()
    join_by_material()
    print('MODELO', name, 'triângulos', tris(), 'objetos', len(bpy.data.objects))
    export(os.path.join(OUT, name + '.fbx'))
    if RENDER:
        preview('/home/user/familia-carreta-joy/unity/Assets/Resources/Previews/' + name + '.png', scale=(0.12 if name == 'Carreta' else 1.0), pal=PAL.get(name))
