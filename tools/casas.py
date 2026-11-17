# Casas da cidade: "City Kit (Suburban)" da Kenney (CC0), simplificadas (menos triângulos para o celular),
# com a base no chão e centradas, prontas para o jogo encaixar no terreno de cada casa do mapa.
# uso: python -I casas.py -- pasta_fbx pasta_saida
import bpy, sys, os, glob
from mathutils import Vector
src, out = sys.argv[sys.argv.index('--') + 1:][:2]
os.makedirs(out, exist_ok=True)
for f in sorted(glob.glob(os.path.join(src, 'building-type-*.fbx'))):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f)
    ms = [o for o in bpy.data.objects if o.type == 'MESH']
    for o in list(bpy.data.objects):
        if o.type != 'MESH': o.select_set(False)
    bpy.ops.object.select_all(action='DESELECT')
    for o in ms: o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    if len(ms) > 1: bpy.ops.object.join()
    m = bpy.context.view_layer.objects.active
    m.parent = None if m.parent is None else m.parent
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    t0 = sum(len(p.vertices) - 2 for p in m.data.polygons)
    d = m.modifiers.new('d', 'DECIMATE'); d.ratio = min(1.0, 520.0 / max(1, t0)); d.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier='d')
    vs = [v.co for v in m.data.vertices]
    cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2; cy = (min(v.y for v in vs) + max(v.y for v in vs)) / 2; z0 = min(v.z for v in vs)
    for v in m.data.vertices: v.co -= Vector((cx, cy, z0))
    for o in list(bpy.data.objects):
        if o != m: bpy.data.objects.remove(o)
    name = 'casa_' + os.path.basename(f).split('-')[-1].replace('.fbx', '')
    m.name = name
    t1 = sum(len(p.vertices) - 2 for p in m.data.polygons)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + '.fbx'), object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', path_mode='STRIP', mesh_smooth_type='FACE')
    print('OK', name, t0, '->', t1, [round(x, 2) for x in m.dimensions])
