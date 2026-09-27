"""Run through Blender MCP. Isolated scene; preserves existing Blender work."""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path('C:/Users/yusuk/Documents/GitHub/EternalEnigma')
OUT = ROOT / 'Assets/Art/EternalEnigma/Props/Dungeon'
SOURCE = ROOT / 'ArtSource/DungeonProps'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('EE Dungeon Props')
bpy.context.window.scene = scene
colors = [(0.24,.105,.045,1),(.46,.25,.10,1),(.14,.19,.22,1),(.74,.52,.20,1),
          (.33,.38,.40,1),(.23,.76,.83,1),(.18,.43,.12,1),(.72,.20,.10,1)]
materials=[]
for i,c in enumerate(colors):
    m=bpy.data.materials.new('DungeonProp_'+str(i)); m.diffuse_color=c; m.use_nodes=True
    shader=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value=c
    shader.inputs['Roughness'].default_value=.72
    materials.append(m)
parts=[]
models=[]
def finish_part(name,bm,color):
    mesh=bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    mesh.materials.append(materials[color])
    obj=bpy.data.objects.new(name,mesh); scene.collection.objects.link(obj); parts.append(obj)
    uv=mesh.uv_layers.new(name='PaletteUV')
    for loop in uv.data: loop.uv=((color+.5)/8,.5)
    return obj
def box(name,center,size,color,bevel=.018):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co=Vector(center)+Vector((v.co.x*size[0],v.co.y*size[1],v.co.z*size[2]))
    if bevel: bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=2)
    return finish_part(name,bm,color)
def rings(name,center,profiles,color,sides=12):
    bm=bmesh.new(); rows=[]
    for z,r in profiles:
        rows.append([bm.verts.new((center[0]+r*math.cos(i*math.tau/sides),center[1]+r*math.sin(i*math.tau/sides),center[2]+z)) for i in range(sides)])
    for a,b in zip(rows,rows[1:]):
        for i in range(sides): bm.faces.new((a[i],a[(i+1)%sides],b[(i+1)%sides],b[i]))
    bm.faces.new(tuple(reversed(rows[0]))); bm.faces.new(tuple(rows[-1]))
    return finish_part(name,bm,color)
def export(name):
    for o in bpy.context.selected_objects: o.select_set(False)
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
    o=bpy.context.object; o.name=name; o.data.name=name
    # Unity importer merges the material slots; UVs retain the shared palette.
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,bake_anim=False,use_triangles=True)
    o.data.calc_loop_triangles(); models.append({'name':name,'triangles':len(o.data.loop_triangles),'dimensions':list(o.dimensions)})
    o.location=(len(models)%4*1.8,len(models)//4*1.8,0); parts.clear()

# Individual planks, curved lid, iron straps, rivets and brass hasp.
for i in range(6):
    box('Chest plank',(i*.17-.425,0,.30),(.162,.68,.52),i%2)
for i in range(9):
    a=(i+.5)*math.pi/9
    p=box('Lid stave',(0,math.cos(a)*.34,.56+math.sin(a)*.22),(1.04,.13,.065),i%2,.01)
    # Mesh-space rotation around the barrel-lid axis.
    from mathutils import Matrix
    pivot=Vector((0,math.cos(a)*.34,.56+math.sin(a)*.22))
    for v in p.data.vertices: v.co=pivot+Matrix.Rotation(a-math.pi/2,3,'X')@(v.co-pivot)
for x in [-.37,.37]:
    for i in range(12):
        a=(i+.5)*math.pi/12
        pivot=Vector((x,math.cos(a)*.355,.56+math.sin(a)*.25))
        p=box('Lid iron strap',pivot,(.075,.105,.04),2,.008)
        for v in p.data.vertices: v.co=pivot+Matrix.Rotation(a-math.pi/2,3,'X')@(v.co-pivot)
    box('Iron front band',(x,-.35,.31),(.075,.045,.55),2)
    box('Iron rear band',(x,.35,.31),(.075,.045,.55),2)
    for z in [.12,.46]: rings('Rivet',(x,-.38,z),[(0,.025),(.025,.018)],3,8)
box('Lock plate',(0,-.385,.48),(.15,.045,.20),3)
box('Keyhole',(0,-.412,.48),(.032,.009,.07),2,.004)
export('Chest')
for i in range(5): box('Crate plank',(i*.18-.36,0,.43),(.172,.85,.8),i%2)
for z in [.09,.78]:
    for y in [-.44,.44]: box('Brace',(0,y,z),(.94,.08,.11),1)
for y in [-.49,.49]:
    p=box('Diagonal brace',(0,y,.44),(1.12,.07,.09),1)
    pivot=Vector((0,y,.44))
    for v in p.data.vertices: v.co=pivot+Matrix.Rotation(.64,3,'Y')@(v.co-pivot)
export('Crate')
rings('Terracotta urn',(0,0,0),[(0,.22),(.08,.3),(.25,.39),(.50,.35),(.66,.19),(.79,.18),(.82,.24),(.87,.24),(.87,.16),(.77,.14)],1,16)
for z,r in [(.13,.33),(.59,.26),(.81,.245)]: rings('Urn band',(0,0,z),[(0,r),(.04,r)],3,16)
export('Urn')
for x,y,s in [(0,0,1),(-.27,.12,.65),(.28,.08,.72),(.1,-.27,.48)]:
    rings('Crystal',(x,y,0),[(0,.19*s),(.53*s,.16*s),(.85*s,.005)],5,5)
for i in range(7):
    a=i*math.tau/7; rings('Ore matrix',(.3*math.cos(a),.3*math.sin(a),0),[(0,.17),(.12,.2),(.24,.08)],4,7)
export('CrystalOre')
rings('Hazard stone rim',(0,0,0),[(0,.54),(.09,.57),(.16,.52),(.16,.43),(.07,.42)],4,16)
rings('Toxic surface',(0,0,.095),[(0,.42),(.014,.42)],6,16)
for x,y in [(-.18,0),(.12,.15),(.15,-.21)]: rings('Bubble',(x,y,.11),[(0,.06),(.04,.045),(.06,.008)],5,8)
export('HazardPool')
for i in range(7):
    a=i*math.tau/7
    rings('Stem',(.18*math.cos(a),.18*math.sin(a),0),[(0,.023),(.42,.012)],6,6)
    for z in [.12,.25,.36]:
        p=rings('Pointed leaf',(.18*math.cos(a),.18*math.sin(a),z),[(0,.01),(.1,.085),(.22,.001)],6,5)
        pivot=Vector((.18*math.cos(a),.18*math.sin(a),z))
        for v in p.data.vertices: v.co=pivot+Matrix.Rotation(.95,3,'Y')@(v.co-pivot)
export('Herbs')
for x,y,s in [(-.19,0,1),(.16,.12,.8),(.13,-.19,.6)]:
    rings('Mushroom stalk',(x,y,0),[(0,.055*s),(.33*s,.045*s)],1,8)
    rings('Mushroom cap',(x,y,.24*s),[(0,.20*s),(.06*s,.22*s),(.16*s,.12*s),(.20*s,.005)],7,12)
export('Mushrooms')
for x,y in [(-.25,-.2),(.2,-.18),(0,.22)]:
    rings('Iron caltrop',(x,y,0),[(0,.10),(.10,.075),(.38,.001)],2,4)
    for a in [0,2.094,4.189]:
        p=rings('Caltrop prong',(x,y,.08),[(0,.055),(.24,.001)],2,4)
        pivot=Vector((x,y,.08))
        for v in p.data.vertices: v.co=pivot+Matrix.Rotation(a,3,'Z')@Matrix.Rotation(1.3,3,'Y')@(v.co-pivot)
export('Caltrops')
box('Spike trap iron base',(0,0,.055),(1.05,1.05,.11),2)
for x in [-.43,.43]:
    for y in [-.43,.43]: rings('Base bolt',(x,y,.11),[(0,.036),(.035,.036)],3,6)
for x in [-.3,0,.3]:
    for y in [-.3,0,.3]:
        rings('Spike socket',(x,y,.11),[(0,.09),(.035,.09)],4,8)
        rings('Forged spike',(x,y,.145),[(0,.075),(.1,.062),(.48,.001)],2,4)
export('SpikeTrap')
box('Spring trap base',(0,0,.05),(1.05,1.05,.1),2)
for x in [-.3,.3]:
    for y in [-.3,.3]:
        rings('Spring axle',(x,y,.1),[(0,.035),(.27,.035)],4,8)
        # Six stacked metal coils keep the launcher readable from the game camera.
        for z in [.11,.15,.19,.23,.27,.31]:
            rings('Spring coil',(x,y,z),[(0,.085),(.023,.085),(.023,.055),(0,.055)],3,10)
box('Pressure plate',(0,0,.38),(.87,.87,.12),4)
for x in [-.38,.38]: box('Plate edge',(x,0,.45),(.08,.88,.04),3,.006)
for y in [-.2,.1]:
    for side in [-1,1]:
        p=box('Launch chevron',(side*.12,y,.462),(.30,.065,.018),3,.006)
        pivot=Vector((side*.12,y,.462))
        for v in p.data.vertices: v.co=pivot+Matrix.Rotation(side*-.65,3,'Z')@(v.co-pivot)
export('BumpTrap')
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=12
        area.spaces.active.region_3d.view_location=(3,2,.1)
        area.spaces.active.region_3d.view_rotation=Vector((1,-2,3)).to_track_quat('Z','Y')
bpy.data.libraries.write(str(SOURCE/'DungeonProps.blend'),{scene})
(SOURCE/'geometry.json').write_text(json.dumps(models,indent=2))
print(json.dumps(models))
