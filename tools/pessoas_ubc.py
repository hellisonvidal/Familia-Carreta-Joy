# Pessoas da cidade: "Universal Base Characters" da Quaternius (CC0), corpo + olhos + sobrancelhas + cabelo,
# esqueleto com nomes Mixamo (Humanoid no Unity). Também exporta o mapa de regiões do corpo (tronco, pernas,
# pés...) nas coordenadas de textura, para pintar roupas (tools/roupas_ubc.py).
# uso: python -I pessoas_ubc.py -- pasta_ubc pasta_saida
import bpy, sys, os, json
src, out = sys.argv[sys.argv.index('--') + 1:][:2]
M = 'mixamorig:'
ren = {'pelvis': 'Hips', 'spine_01': 'Spine', 'spine_02': 'Spine1', 'spine_03': 'Spine2', 'neck_01': 'Neck', 'Head': 'Head'}
for s, L in (('l', 'Left'), ('r', 'Right')):
    ren.update({f'clavicle_{s}': f'{L}Shoulder', f'upperarm_{s}': f'{L}Arm', f'lowerarm_{s}': f'{L}ForeArm', f'hand_{s}': f'{L}Hand',
                f'thigh_{s}': f'{L}UpLeg', f'calf_{s}': f'{L}Leg', f'foot_{s}': f'{L}Foot', f'ball_{s}': f'{L}ToeBase'})
REG = {'pelvis': 'quadril', 'spine_01': 'tronco', 'spine_02': 'tronco', 'spine_03': 'tronco', 'clavicle_l': 'tronco', 'clavicle_r': 'tronco',
       'neck_01': 'pescoco', 'Head': 'cabeca', 'upperarm_l': 'braco', 'upperarm_r': 'braco', 'lowerarm_l': 'antebraco', 'lowerarm_r': 'antebraco',
       'hand_l': 'mao', 'hand_r': 'mao', 'thigh_l': 'coxa', 'thigh_r': 'coxa', 'calf_l': 'canela', 'calf_r': 'canela',
       'foot_l': 'pe', 'foot_r': 'pe', 'ball_l': 'pe', 'ball_r': 'pe'}
HAIRS = {'Male': ['Hair_SimpleParted', 'Hair_Buzzed', 'Hair_Beard'], 'Female': ['Hair_Long', 'Hair_Buns', 'Hair_BuzzedFemale']}
B = os.path.join(src, 'Base Characters', 'Unity'); H = os.path.join(src, 'Hairstyles', 'Rigged to Head Bone', 'FBX (Unity)')
regions = {}
for g, hairs in HAIRS.items():
    for hair in hairs:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=os.path.join(B, f'Superhero_{g}_FullBody.fbx'))
        arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
        before = set(bpy.data.objects)
        extra = [hair] + (['Hair_Buzzed'] if hair == 'Hair_Beard' else [])
        for hname in extra:
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=os.path.join(H, hname + '.fbx'))
            new = [o for o in bpy.data.objects if o not in before]
            for o in new:
                if o.type == 'MESH':
                    o.parent = arm
                    for md in o.modifiers:
                        if md.type == 'ARMATURE': md.object = arm
                    o.name = 'Cabelo'
            for o in new:
                if o.type == 'ARMATURE': bpy.data.objects.remove(o)
        for ac in list(bpy.data.actions): bpy.data.actions.remove(ac)
        body = None
        for o in [o for o in bpy.data.objects if o.type == 'MESH']:
            if o.name.startswith('Superhero') or o.name.startswith('SuperHero'): o.name = 'Corpo'; body = o
            elif o.name.startswith('Eyes'): o.name = 'Olhos'
            elif o.name.startswith('Eyebrows'): o.name = 'Sobrancelhas'
        # regiões do corpo por osso dominante de cada face (uma vez por gênero)
        if g not in regions:
            vg = {gr.index: gr.name for gr in body.vertex_groups}
            uvl = body.data.uv_layers.active.data
            faces = []
            for p in body.data.polygons:
                acc = {}
                for vi in p.vertices:
                    for ge in body.data.vertices[vi].groups:
                        r = REG.get(vg.get(ge.group, ''), None)
                        if r: acc[r] = acc.get(r, 0) + ge.weight
                if not acc: continue
                r = max(acc, key=acc.get)
                faces.append((r, [list(uvl[li].uv) for li in p.loop_indices]))
            regions[g] = faces
        # só a primeira camada de UV (a que as texturas usam); as outras confundem o Unity
        for o in [o for o in bpy.data.objects if o.type == 'MESH']:
            for l in list(o.data.uv_layers):
                if l.name != 'UVMap' and len(o.data.uv_layers) > 1: o.data.uv_layers.remove(l)
        # simplifica o corpo (o celular agradece)
        for o, ratio in [(o, 0.38 if o == body else 0.45) for o in bpy.data.objects if o.type == 'MESH' and (o == body or o.name.startswith('Cabelo') or o.name.startswith('Sobr'))]:
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            if o.data.shape_keys: o.shape_key_clear()   # expressões do rosto não são usadas e impedem simplificar
            d = o.modifiers.new('d', 'DECIMATE'); d.ratio = ratio
            bpy.ops.object.modifier_move_to_index(modifier='d', index=0)
            bpy.ops.object.modifier_apply(modifier='d')
        for b in arm.data.bones:
            if b.name in ren: b.name = M + ren[b.name]
        for o in [o for o in bpy.data.objects if o.type == 'MESH']:
            for gr in o.vertex_groups:
                if gr.name in ren: gr.name = M + ren[gr.name]
        bpy.ops.object.select_all(action='SELECT')
        name = f'Pessoa_{g[0]}_{hair.replace("Hair_", "")}'
        bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + '.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', path_mode='STRIP')
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
        print('OK', name, tris, 'triângulos', [o.name for o in bpy.data.objects if o.type == 'MESH'])
json.dump(regions, open(os.path.join(out, 'regioes.json'), 'w'))
