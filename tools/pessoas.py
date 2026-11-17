# Pessoas da cidade: personagens CC0 da Quaternius (Ultimate Animated Character Pack) com esqueleto
# ajustado para Humanoid (nomes Mixamo; pé preso na perna; pernas filhas do quadril) e malha separada por material.
# uso: python -I pessoas.py -- pasta_fbx pasta_saida Nome1 Nome2 ...
import bpy, sys, os, json
a = sys.argv[sys.argv.index('--')+1:]
src, out, names = a[0], a[1], a[2:]
M = 'mixamorig:'
ren = {'Hips':'Hips','Abdomen':'Spine','Torso':'Spine1','Neck':'Neck','Head':'Head'}
for s, L in (('L','Left'),('R','Right')):
    ren.update({f'Shoulder.{s}':f'{L}Shoulder',f'UpperArm.{s}':f'{L}Arm',f'LowerArm.{s}':f'{L}ForeArm',f'Fist.{s}':f'{L}Hand',
                f'UpperLeg.{s}':f'{L}UpLeg',f'LowerLeg.{s}':f'{L}Leg',f'Foot.{s}':f'{L}Foot'})
cores = {}
for n in names:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(src, n + '.fbx'))
    for ac in list(bpy.data.actions): bpy.data.actions.remove(ac)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    for o in meshes:
        o.animation_data_clear()
        # pesos do osso "Body" (acima do quadril) passam para o quadril
        if 'Body' in o.vertex_groups and 'Hips' in o.vertex_groups:
            gb, gh = o.vertex_groups['Body'], o.vertex_groups['Hips']
            for v in o.data.vertices:
                wb = next((g.weight for g in v.groups if g.group == gb.index), 0)
                if wb > 0:
                    wh = next((g.weight for g in v.groups if g.group == gh.index), 0)
                    gh.add([v.index], wh + wb, 'REPLACE')
            o.vertex_groups.remove(gb)
    if arm.animation_data: arm.animation_data.action = None
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.select_all(action='SELECT'); bpy.ops.pose.transforms_clear()
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    for s in ('L', 'R'):
        eb[f'Foot.{s}'].parent = eb[f'LowerLeg.{s}']; eb[f'Foot.{s}'].use_connect = False
        eb[f'UpperLeg.{s}'].parent = eb['Hips']
        for x in (f'PoleTarget.{s}_end', f'PoleTarget.{s}'):
            if x in eb: eb.remove(eb[x])
    bpy.ops.object.mode_set(mode='OBJECT')
    for b in arm.data.bones:
        if b.name in ren: b.name = M + ren[b.name]
    for o in meshes:
        for g in o.vertex_groups:
            if g.name in ren: g.name = M + ren[g.name]
    # separa por material: cada parte vira um objeto "M_<material>"
    cor = {}
    for o in meshes:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    for o in [o for o in bpy.data.objects if o.type == 'MESH']:
        mat = o.material_slots[0].material if o.material_slots else None
        mn = mat.name.split('.')[0] if mat else 'X'
        o.name = 'M_' + mn
        if mat: cor[mn] = [round(c, 4) for c in mat.diffuse_color[:3]]
    cores[n] = cor
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, n + '.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    print('OK', n, sorted(cor))
with open(os.path.join(out, 'cores.txt'), 'w') as f:
    for n, cor in cores.items():
        f.write(n + ';' + ';'.join(f'{k}={v[0]},{v[1]},{v[2]}' for k, v in cor.items()) + '\n')
