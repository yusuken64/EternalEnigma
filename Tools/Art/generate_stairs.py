"""Run in Blender; writes the project-owned staircase source and FBX."""
import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path('C:/Users/yusuk/Documents/GitHub/EternalEnigma')
OUT = ROOT / 'Assets/Art/EternalEnigma/Props/Stairs'
SOURCE = ROOT / 'ArtSource/Stairs'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
for old in list(bpy.data.scenes):
    if old.name.startswith('EE Stone Stairs'):
        for obj in list(old.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.scenes.remove(old)
for mesh in list(bpy.data.meshes):
    if mesh.users == 0 and mesh.name.startswith('EE_Prop_StoneStairs'):
        bpy.data.meshes.remove(mesh)
scene = bpy.data.scenes.new('EE Stone Stairs')
bpy.context.window.scene = scene
parts = []
colors = [(0.29,0.33,0.37,1), (0.43,0.48,0.52,1),
          (0.55,0.59,0.61,1), (0.66,0.68,0.66,1),
          (0.36,0.41,0.44,1), (0.50,0.55,0.57,1),
          (0.27,0.35,0.31,1), (0.68,0.55,0.34,1)]
palette = bpy.data.images.new('EE_Stairs_Palette', width=128, height=16)
palette.pixels = [v for y in range(16) for x in range(128) for v in colors[x // 16]]
palette.filepath_raw = str(OUT / 'EE_Stairs_Palette.png')
palette.file_format = 'PNG'
palette.save()
mat = bpy.data.materials.new('EE_Stairs_Stone')
mat.use_nodes = True
shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
shader.inputs['Roughness'].default_value = 0.87
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = palette
mat.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])

def stone(name, center, size, shade, bevel=0.025):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x*size[0]+center[0], v.co.y*size[1]+center[1], v.co.z*size[2]+center[2]))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(mat)
    uv = mesh.uv_layers.new(name='PaletteUV')
    for poly in mesh.polygons:
        swatch = min(shade + (1 if poly.normal.z > 0.5 else 0), 7)
        for i in poly.loop_indices:
            uv.data[i].uv = ((swatch+0.5)/8, 0.5)
    parts.append(obj)
    return obj

# Broad treads ascend towards Blender +Y. Source base is Z=-0.2.
stone('Foundation', (0,0,-0.14), (1.46,1.64,0.12), 0)
for i in range(5):
    y = -0.64+i*0.32
    top = 0.02+i*0.185
    stone('Riser_%02d'%i, (0,y,(-0.08+top)/2), (1.04,0.32,top+0.08), 0, 0.016)
    stone('Tread_%02d'%i, (0,y-0.012,top+0.045), (1.12,0.338,0.09), 2+(i%2), 0.022)
    for side in (-1,1):
        height = top+0.29
        stone('Cheek_%d_%d'%(side,i), (side*0.635,y,(-0.08+height)/2), (0.20,0.312,height+0.08), 1+(i%2), 0.018)
# A restrained warm inset on the top landing gives the exit a distinct cue.
stone('Landing_Inlay', (0,0.64,0.855), (0.32,0.16,0.018), 7, 0.007)
for obj in bpy.context.selected_objects:
    obj.select_set(False)
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
model = bpy.context.object
model.name = 'EE_Prop_StoneStairs'
model.data.name = model.name
model.data.calc_loop_triangles()
stats = {'triangles':len(model.data.loop_triangles), 'vertices':len(model.data.vertices), 'materials':len(model.data.materials), 'dimensions_blender':list(model.dimensions)}
bpy.ops.export_scene.fbx(filepath=str(OUT/'EE_Prop_StoneStairs.fbx'), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, bake_anim=False, add_leaf_bones=False, use_triangles=True)
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        area.spaces.active.shading.type = 'MATERIAL'
        area.spaces.active.region_3d.view_distance = 3.7
        area.spaces.active.region_3d.view_location = (0,0,0.3)
        area.spaces.active.region_3d.view_rotation = Vector((2,-3,2.6)).to_track_quat('Z','Y')
palette.pack()
bpy.data.libraries.write(str(SOURCE/'EE_Prop_StoneStairs.blend'), {scene})
(SOURCE/'geometry.json').write_text(json.dumps(stats, indent=2))
print(json.dumps(stats))
