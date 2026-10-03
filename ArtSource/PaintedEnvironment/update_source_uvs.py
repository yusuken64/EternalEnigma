"""Run with Blender -b <source.blend> --python this-file. UV-only source update.

Matches PaintedEnvironmentAuthoring's semantic cells and dominant-axis mapping.
Retains the original UV layer so reruns never compound the remap. Mesh vertices,
polygons, normals, transforms and material slots are left intact.
"""
import bpy, sys
from pathlib import Path
root = Path(__file__).resolve().parents[2]
source = Path(bpy.data.filepath)
kind = source.parent.name
strip = 12 if kind == 'BiomeDecorations' else 8 if kind in ('DungeonProps','FantasyTraps') else 0
destination = root / ('Assets/Art/EternalEnigma/BiomeDecorations' if strip == 12 else
                      'Assets/Art/EternalEnigma/Props/Dungeon' if strip == 8 else 'Assets/Art/EnvironmentKit/Models')
texture_path = root / ('Assets/Art/PaintedEnvironment/BiomeDecorations.png' if strip == 12 else
                       'Assets/Art/PaintedEnvironment/DungeonProps.png' if strip == 8 else
                       'Assets/Art/EnvironmentKit/Textures/Buildings_Grassland.png')
image = bpy.data.images.load(str(texture_path),check_existing=True)
image.reload()
for material in bpy.data.materials:
    if not strip and material.name not in ('EnvironmentPalette','DetailedBuildings'): continue
    material.use_nodes=True
    shader=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if shader is None: continue
    texture=next((n for n in material.node_tree.nodes if n.type=='TEX_IMAGE'),None)
    if texture is None: texture=material.node_tree.nodes.new('ShaderNodeTexImage')
    texture.image=image
    material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
image.filepath=bpy.path.relpath(str(texture_path))
if '--materials-only' in sys.argv:
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    print('Updated painted source material bindings:',kind)
    sys.exit(0)
report = []
for obj in list(bpy.data.objects):
    model_id = obj.name.split('.')[0] if strip == 8 else obj.name
    if obj.type != 'MESH' or not (destination / (model_id + '.fbx')).exists(): continue
    if not strip and (obj.name == 'Paving' or obj.name.startswith(('SmartRoad', 'SmartShore'))): continue
    mesh = obj.data
    if not mesh.uv_layers.active: continue
    current = mesh.uv_layers.active
    original = mesh.uv_layers.get('OriginalSemanticUV')
    if original is None:
        original = mesh.uv_layers.new(name='OriginalSemanticUV')
        for a,b in zip(original.data,current.data): a.uv = b.uv
    building = not strip and (obj.name.startswith('SmartHouse') or obj.name in ('House','Inn','Shop','Trainer'))
    low = [min(v.co[k] for v in mesh.vertices) for k in range(3)]
    span = [max(.001,max(v.co[k] for v in mesh.vertices)-low[k]) for k in range(3)]
    for face in mesh.polygons:
        normal = face.normal
        axis = max(range(3),key=lambda k:abs(normal[k]))
        axes = (0,1) if axis == 2 else (1,2) if axis == 0 else (0,2)
        for loop in face.loop_indices:
            old = original.data[loop].uv
            if strip:
                cell = min(strip-1,max(0,int(old.x*strip))); cx,cy=cell%4,cell//4
            else: cx,cy=min(3,max(0,int(old.x*4))),min(3,max(0,int(old.y*4)))
            if building:
                current.data[loop].uv = (min((cx+.945)/4,max((cx+.055)/4,old.x)),min((cy+.945)/4,max((cy+.055)/4,old.y)))
            else:
                v = mesh.vertices[mesh.loops[loop].vertex_index].co
                p = [(v[k]-low[k])/span[k] for k in axes]
                current.data[loop].uv = ((cx+.065+p[0]*.87)/4,(cy+.065+p[1]*.87)/4)
    mesh.uv_layers.active = current
    current.active_render = True
    # Export only the render UV layer; retaining source UVs in FBX could change streams/batching.
    saved = obj.data
    exported = mesh.copy()
    for layer in list(exported.uv_layers):
        if layer.name != current.name: exported.uv_layers.remove(layer)
    obj.data = exported
    location = obj.location.copy()
    obj.location = (0,0,0)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=str(destination/(model_id+'.fbx')),use_selection=True,add_leaf_bones=False,bake_anim=False)
    obj.location=location;obj.data=saved;bpy.data.meshes.remove(exported)
    report.append(obj.name)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
print('UV-only source update:',kind,len(report),'models')
