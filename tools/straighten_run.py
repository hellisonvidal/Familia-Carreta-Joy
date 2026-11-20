# Endireita as pernas nas animações de corrida (Run.fbx) e caminhada (Walk.fbx):
#  - Joelhos (LeftLeg / RightLeg): remove torção (Y) e dobra lateral impossível (Z ~ 11° que entortava a perna!),
#    mantendo 100% da flexão natural para trás (X).
#  - Coxas (LeftUpLeg / RightUpLeg): remove cruzamento/torção excessiva (±15°), mantendo a passada reta para frente/trás.
#  - Pés (LeftFoot / RightFoot): mantém os pés alinhados para a frente (sem virar para os lados).
#  - Mantém 100% da T-pose Mixamo original, movimento de braços, tronco e ritmo!
#
# Uso:
#   blender --background --python tools/straighten_run.py -- entrada.fbx saida.fbx

import bpy, sys, math
from mathutils import Euler

src, out = sys.argv[sys.argv.index('--') + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
act = bpy.data.actions[0]
f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
sc = bpy.context.scene

# Fatores de amortecimento (0.0 = remove totalmente, 1.0 = mantém 100%) por osso (eixos locais X, Y, Z)
DAMP = {
    'LeftUpLeg':  (1.0, 0.10, 0.15),   # mantém passada X, quase zera torção Y e cruzamento Z
    'RightUpLeg': (1.0, 0.10, 0.15),
    'LeftLeg':    (1.0, 0.00, 0.00),   # joelho é dobradiça pura em X! zero dobra lateral Z!
    'RightLeg':   (1.0, 0.00, 0.00),
    'LeftFoot':   (1.0, 0.10, 0.10),   # pé reto para frente
    'RightFoot':  (1.0, 0.10, 0.10),
    'LeftToeBase':  (1.0, 0.05, 0.05),
    'RightToeBase': (1.0, 0.05, 0.05),
}

# Captura todas as poses limpas quadro a quadro antes de regravar
clean = {}
for f in range(f0, f1 + 1):
    sc.frame_set(f)
    bpy.context.view_layer.update()
    row = {}
    for pb in arm.pose.bones:
        sn = pb.name.split(':')[-1]
        if sn in DAMP:
            eu = pb.matrix_basis.to_euler('XYZ')
            kx, ky, kz = DAMP[sn]
            new_eu = Euler((eu.x * kx, eu.y * ky, eu.z * kz), 'XYZ')
            row[pb.name] = new_eu.to_quaternion()
    clean[f] = row

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for f in range(f0, f1 + 1):
    sc.frame_set(f)
    for bname, q in clean[f].items():
        pb = arm.pose.bones[bname]
        if pb.rotation_mode == 'QUATERNION':
            pb.rotation_quaternion = q
            pb.keyframe_insert(data_path='rotation_quaternion', frame=f)
        else:
            pb.rotation_euler = q.to_euler(pb.rotation_mode)
            pb.keyframe_insert(data_path='rotation_euler', frame=f)

# Truque essencial do exportador FBX do Blender (sem malha):
# Grava T-pose neutra (identidade) em frame = f0 - 1 e posiciona o timeline nesse quadro antes de exportar!
# Assim o FBX grava a T-pose perfeita como rest pose nos nós dos ossos, mas o bake_anim grava só f0..f1!
tpose_f = f0 - 1
sc.frame_set(tpose_f)
for pb in arm.pose.bones:
    pb.location = (0, 0, 0)
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.rotation_euler = (0, 0, 0)
    pb.scale = (1, 1, 1)
    pb.keyframe_insert(data_path='location', frame=tpose_f)
    if pb.rotation_mode == 'QUATERNION':
        pb.keyframe_insert(data_path='rotation_quaternion', frame=tpose_f)
    else:
        pb.keyframe_insert(data_path='rotation_euler', frame=tpose_f)
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')

sc.frame_start, sc.frame_end = f0, f1
sc.frame_set(tpose_f)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=True, object_types={'ARMATURE'},
    add_leaf_bones=False, bake_anim=True,
    bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
    bake_anim_step=1, bake_anim_simplify_factor=0.2,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y'
)
print('STRAIGHTENED', out, f'({f0}..{f1})')
