# Endireita a pose de base dos personagens: pés apontando para a frente e pernas sem abrir para os lados.
# O Unity (Humanoid) usa a pose de base como "neutra"; se nela os pés estão virados para fora, toda
# animação sai com os pés tortos. Aqui o pé/perna é girado na pose, a malha acompanha e essa pose vira a base.
# uso: python -I endireitar.py -- entrada.fbx saida.fbx
import bpy, sys, math
from mathutils import Vector, Matrix
src, out = sys.argv[sys.argv.index('--') + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for ac in list(bpy.data.actions): bpy.data.actions.remove(ac)
if arm.animation_data: arm.animation_data.action = None
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT'); bpy.ops.pose.transforms_clear()
P = arm.pose.bones
M = arm.matrix_world

def bone(name):
    for b in P:
        if b.name.endswith(name): return b
    return None

def rotate_world(pb, R, pivot):
    """Gira o osso (e os filhos) em torno de 'pivot' (mundo) pela rotação R (mundo)."""
    if hasattr(R, "to_matrix"): R = R.to_matrix()
    T = Matrix.Translation(pivot) @ R.to_4x4() @ Matrix.Translation(-pivot)
    pb.matrix = M.inverted() @ T @ M @ pb.matrix
    bpy.context.view_layer.update()

def W(p): return M @ p
report = []
for side in ('Left', 'Right'):
    up, lo, ft, toe = bone(side + 'UpLeg'), bone(side + 'Leg'), bone(side + 'Foot'), bone(side + 'ToeBase')
    if not (up and lo and ft): continue
    # coxa: tira a abertura lateral (quadril -> joelho na vertical, vista de frente)
    for b, child in ((up, lo), (lo, ft)):
        a, c = W(b.head), W(child.head)
        d = (c - a)
        want = Vector((0, d.y, d.z))
        if want.length < 1e-5: continue
        ang = math.degrees(d.angle(want))
        if ang > 0.5:
            rotate_world(b, d.normalized().rotation_difference(want.normalized()), a)
            report.append(f'{b.name} {ang:.1f}°')
    # pé: ponta para a frente (-Y no Blender = frente do personagem)
    if toe:
        h, t = W(ft.head), W(toe.tail) if toe.tail else W(toe.head)
        d = t - h; d.z = 0
        if d.length > 1e-5:
            ang = math.atan2(d.x, -d.y)   # positivo = virado para +X
            if abs(math.degrees(ang)) > 1:
                rotate_world(ft, Matrix.Rotation(-ang, 3, 'Z'), h)
                report.append(f'{ft.name} {math.degrees(ang):.1f}°')
bpy.ops.object.mode_set(mode='OBJECT')
# a malha passa a ter a forma da pose nova...
for o in meshes:
    mods = [m for m in o.modifiers if m.type == 'ARMATURE']
    if not mods: continue
    if o.data.shape_keys: o.shape_key_clear()
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    name = mods[0].name
    bpy.ops.object.modifier_apply(modifier=name)
    m = o.modifiers.new('Armature', 'ARMATURE'); m.object = arm
# ...e a pose nova vira a pose de base
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', path_mode='STRIP', use_armature_deform_only=True)
print('OK', out, '; '.join(report))
