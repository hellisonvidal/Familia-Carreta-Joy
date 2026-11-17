# Pessoas da cidade: personagem humano da Kenney (Animated Characters, CC0) com esqueleto Humanoid
# (nomes Mixamo, sem os ossos de controle) + roupas/peles variadas geradas a partir das peles originais.
# uso: python -I pessoas_kenney.py -- modelo.fbx saida.fbx pasta_peles_entrada... -- pasta_peles_saida
import bpy, sys, os, colorsys, random
a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
M = 'mixamorig:'
ren = {'Hips': 'Hips', 'Spine': 'Spine', 'Chest': 'Spine1', 'UpperChest': 'Spine2', 'Neck': 'Neck', 'Head': 'Head'}
for s in ('Left', 'Right'):
    for b in ('Shoulder', 'Arm', 'ForeArm', 'Hand', 'UpLeg', 'Leg', 'Foot'): ren[s + b] = s + b
    ren[s + 'Toes'] = s + 'ToeBase'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
for ac in list(bpy.data.actions): bpy.data.actions.remove(ac)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print('grupos', sorted({g.name for o in meshes for g in o.vertex_groups})[:60])
if arm.animation_data: arm.animation_data.action = None
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.select_all(action='SELECT'); bpy.ops.pose.transforms_clear()
for b in arm.pose.bones: b.constraints.clear() if hasattr(b.constraints, 'clear') else [b.constraints.remove(c) for c in list(b.constraints)]
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
for b in list(eb):
    if 'Ctrl' in b.name or 'Roll' in b.name or 'FootIK' in b.name:
        if b.name != 'HipsCtrl': eb.remove(b)
bpy.ops.object.mode_set(mode='OBJECT')
for b in arm.data.bones:
    if b.name in ren: b.name = M + ren[b.name]
for o in meshes:
    for g in o.vertex_groups:
        if g.name in ren: g.name = M + ren[g.name]
    o.name = 'Corpo'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', path_mode='STRIP')
print('OK', out, [b.name for b in arm.data.bones][:8])
