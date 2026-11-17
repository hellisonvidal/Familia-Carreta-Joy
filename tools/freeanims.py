import bpy, sys, os
src, kind, out = sys.argv[sys.argv.index('--')+1:][:3]
pairs = sys.argv[sys.argv.index('--')+4:]
M = 'mixamorig:'
if kind == 'ue':
    ren = {'pelvis':'Hips','spine_01':'Spine','spine_02':'Spine1','spine_03':'Spine2','neck_01':'Neck','Head':'Head'}
    for s, L in (('l','Left'),('r','Right')):
        ren.update({f'clavicle_{s}':f'{L}Shoulder',f'upperarm_{s}':f'{L}Arm',f'lowerarm_{s}':f'{L}ForeArm',f'hand_{s}':f'{L}Hand',
                    f'thigh_{s}':f'{L}UpLeg',f'calf_{s}':f'{L}Leg',f'foot_{s}':f'{L}Foot',f'ball_{s}':f'{L}ToeBase'})
else:
    ren = {'DEF-hips':'Hips','DEF-spine.001':'Spine','DEF-spine.002':'Spine1','DEF-spine.003':'Spine2','DEF-neck':'Neck','DEF-head':'Head'}
    for s, L in (('L','Left'),('R','Right')):
        ren.update({f'DEF-shoulder.{s}':f'{L}Shoulder',f'DEF-upper_arm.{s}':f'{L}Arm',f'DEF-forearm.{s}':f'{L}ForeArm',f'DEF-hand.{s}':f'{L}Hand',
                    f'DEF-thigh.{s}':f'{L}UpLeg',f'DEF-shin.{s}':f'{L}Leg',f'DEF-foot.{s}':f'{L}Foot',f'DEF-toe.{s}':f'{L}ToeBase'})
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for o in list(bpy.data.objects):
    if o.type != 'ARMATURE': bpy.data.objects.remove(o)
for b in arm.data.bones:
    if b.name in ren: b.name = M + ren[b.name]
# confere se as curvas acompanharam a troca de nome
acts = {a.name.split('|')[-1]: a for a in bpy.data.actions}
for p in pairs:
    name, newname = p.split('=')
    act = acts.get(name)
    if act is None: print('FALTA', name); continue
    arm.animation_data_create(); arm.animation_data.action = act
    try:
        if act.slots: arm.animation_data.action_slot = act.slots[0]
        from bpy_extras.anim_utils import action_get_channelbag_for_slot
        cb = action_get_channelbag_for_slot(act, act.slots[0])
        for fc in cb.fcurves:
            for old, new in ren.items():
                q = f'pose.bones["{old}"]'
                if fc.data_path.startswith(q): fc.data_path = fc.data_path.replace(q, f'pose.bones["{M}{new}"]'); break
        paths = sorted({fc.data_path.split('"')[1] for fc in cb.fcurves if '"' in fc.data_path})
        print('CANAIS', name, len(paths), [x for x in paths if 'Hips' in x or 'pelvis' in x or 'hips' in x][:3])
    except Exception as e: print('aviso', e)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = int(act.frame_range[0]), int(act.frame_range[1])
    for a in bpy.data.actions: a.use_fake_user = True
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    path = os.path.join(out, newname + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.5,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y')
    print('OK', newname, round(act.frame_range[1] - act.frame_range[0]), 'quadros')
