# Converte capturas da CMU (BVH, uso livre) em animações FBX com nomes de ossos Mixamo (Humanoid no Unity).
# uso: python -I cmuanims.py -- arquivo.bvh saida.fbx quadro_inicial quadro_final
import bpy, sys
src, out, f0, f1 = sys.argv[sys.argv.index('--')+1:][:4]
f0, f1 = int(f0), int(f1)
M = 'mixamorig:'
ren = {'Hips':'Hips','LowerBack':'Spine','Spine':'Spine1','Spine1':'Spine2','Neck':'Neck','Neck1':'Neck1','Head':'Head',
       'LHipJoint':'LHipJoint','RHipJoint':'RHipJoint'}
for s in ('Left','Right'):
    for b in ('UpLeg','Leg','Foot','ToeBase','Shoulder','Arm','ForeArm','Hand'): ren[s+b] = s+b
    ren[s+'FingerBase'] = s+'FingerBase'; ren[s+'HandIndex1'] = s+'HandIndex1'
ren['LThumb'] = 'LThumb'; ren['RThumb'] = 'RThumb'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_anim.bvh(filepath=src, global_scale=0.056444, frame_start=1, use_fps_scale=False, rotate_mode='NATIVE', axis_forward='-Z', axis_up='Y')
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
act = arm.animation_data.action
from bpy_extras.anim_utils import action_get_channelbag_for_slot
cb = action_get_channelbag_for_slot(act, act.slots[0])
for b in arm.data.bones:
    if b.name in ren: b.name = M + ren[b.name]
for fc in cb.fcurves:
    if '"' in fc.data_path:
        nm = fc.data_path.split('"')[1]
        if nm in ren: fc.data_path = fc.data_path.replace(f'"{nm}"', f'"{M}{ren[nm]}"')
# recorta: desloca as chaves para começar no quadro 0 e apaga o resto
for fc in cb.fcurves:
    kps = fc.keyframe_points
    for i in range(len(kps) - 1, -1, -1):
        if kps[i].co.x < f0 or kps[i].co.x > f1: kps.remove(kps[i], fast=True)
    for k in kps: k.co.x -= f0; k.handle_left.x -= f0; k.handle_right.x -= f0
    fc.update()
sc = bpy.context.scene
sc.render.fps = 120
sc.frame_start, sc.frame_end = 0, f1 - f0
arm.name = 'Armature'
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False,
    bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_step=4, bake_anim_simplify_factor=0.3,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y')
print('OK', out, f1 - f0, 'quadros (120/s)')
