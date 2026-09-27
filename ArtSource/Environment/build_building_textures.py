"""Blender: biome building atlases and face-projected UVs, no extra geometry."""
import bpy, math, json
ROOT='C:/Users/yusuk/Documents/GitHub/EternalEnigma'
OUT=ROOT+'/Assets/Art/EnvironmentKit'
scene=bpy.data.scenes['EE Environment Kit'];bpy.context.window.scene=scene
for biome in ['Grassland','Desert','Water','Mountain','Forest','Tundra','Marsh','Volcanic']:
    palette=bpy.data.images['Palette_'+biome];source=list(palette.pixels)
    atlas=bpy.data.images.get('Buildings_'+biome) or bpy.data.images.new('Buildings_'+biome,256,256,alpha=True)
    pixels=[]
    for y in range(256):
        for x in range(256):
            c=(y//64)*4+x//64;u=x%64;v=y%64
            i=((c//4*16+8)*64+c%4*16+8)*4;base=source[i:i+3]
            noise=(((u*37+v*71+u*v*3)%29)/28-.5)*.06
            shade=1+noise
            if c in [0,1]:
                shade+=.12*math.sin(u*.9+math.sin(v*.12))
                if u%21<2:shade*=.6
            elif c==2:
                shade+=.025*math.sin(u*.32)*math.sin(v*.51)
                if v<14:
                    row=v//7;edge=v%7==0 or (u+row*9)%18<2
                    base=[source[(8*64+56)*4+j] for j in range(3)]
                    shade=.66 if edge else 1+((u//18+row*7)%3)*.09
                elif u in [2,3,31,32,60,61] or v in [16,17,58,59] or abs((u%32)-(v-18)*.72)<1.1:
                    base=source[(8*64+8)*4:(8*64+8)*4+3];shade=1+noise
                elif v in [24,25,26] and (u*7)%31==0:shade*=.78
            elif c in [3,9]:
                row=v//12
                if v%12<2 or (u+(row%2)*12)%24<2:shade*=.62
                else:shade+=((u//24+row*3)%5)*.025
            elif c in [4,5]:
                row=v//10
                if v%10<2 or (u+(row%2)*8)%16==0:shade*=.67
                else:shade+=.09*(v%10)/10
            pixels.extend((*[max(0,min(1,b*shade)) for b in base],1))
    atlas.pixels=pixels;atlas.file_format='PNG';atlas.filepath_raw=OUT+'/Textures/Buildings_'+biome+'.png';atlas.save()
mat=bpy.data.materials.get('DetailedBuildings') or bpy.data.materials.new('DetailedBuildings');mat.use_nodes=True
node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
tex=next((n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE'),None) or mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image=bpy.data.images['Buildings_Grassland'];mat.node_tree.links.new(tex.outputs['Color'],node.inputs['Base Color'])
node.inputs['Roughness'].default_value=.95
for obj in scene.objects:
    if obj.type!='MESH' or not (obj.name.startswith('SmartHouse') or obj.name in ['House','Inn','Shop','Trainer']):continue
    mesh=obj.data;uv=mesh.uv_layers.active
    for poly in mesh.polygons:
        old=uv.data[poly.loop_start].uv;c=int(old.x*4)+int(old.y*4)*4
        axes=(0,1) if abs(poly.normal.z)>.5 else (0,2) if abs(poly.normal.y)>.5 else (1,2)
        coords=[mesh.vertices[mesh.loops[i].vertex_index].co for i in poly.loop_indices]
        lows=[min(v[a] for v in coords) for a in axes];highs=[max(v[a] for v in coords) for a in axes]
        for i,v in zip(poly.loop_indices,coords):
            p=[(v[a]-lo)/max(.00001,hi-lo) for a,lo,hi in zip(axes,lows,highs)]
            uv.data[i].uv=((c%4+(2+60*p[0])/64)/4,(c//4+(2+60*p[1])/64)/4)
    mesh.materials.clear();mesh.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    position=obj.location.copy();obj.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+obj.name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
    obj.location=position
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Environment/EnvironmentKit.blend')
print('Eight 256px building atlases; eight building meshes re-UVed; unchanged triangle counts.')
