"""Single and grouped tree tiles, fitting one cell with no hidden base geometry."""
import bpy, math, json
ROOT='C:/Users/yusuk/Documents/GitHub/EternalEnigma';OUT=ROOT+'/Assets/Art/EnvironmentKit'
scene=bpy.data.scenes['EE Environment Kit'];bpy.context.window.scene=scene
source=open(ROOT+'/ArtSource/Environment/build_kit.py',encoding='utf-8').read()
mat=next(m for m in bpy.data.materials if m.name.startswith('EnvironmentPalette'))
exec(source[source.index('class Model:'):source.index('\nmodels=')])
biome_models=[]
for name in ['SnowPine','Palm','Willow','Mangrove','CharredTree']:
    old=bpy.data.objects.get(name)
    if old:bpy.data.objects.remove(old,do_unlink=True)
    m=Model(name)
    if name=='SnowPine':
        m.cone(0,0,0,.045,.3,1,4,top=.035)
        m.cone(0,0,.18,.31,.35,6,5);m.cone(0,0,.31,.25,.30,13,5);m.cone(0,0,.5,.18,.30,13,5)
    elif name=='Palm':
        m.cone(0,0,0,.065,.8,1,5,top=.035)
        for i in range(6):
            a=i*math.tau/6;dx=math.cos(a);dy=math.sin(a);nx=-dy*.1;ny=dx*.1
            m.shape([(0,0,.8),(.23*dx+nx,.23*dy+ny,.91),(.43*dx,.43*dy,.62),(.23*dx-nx,.23*dy-ny,.91)],[(0,2,1),(0,3,2)],6 if i%2 else 7)
    elif name in ['Willow','Mangrove']:
        m.cone(0,0,0,.07,.55,1,5,top=.04)
        if name=='Mangrove':
            for i in range(4):
                a=i*math.tau/4;m.shape([(0,0,.28),(.27*math.cos(a),.27*math.sin(a),0),(.27*math.cos(a+.2),.27*math.sin(a+.2),0)],[(0,1,2)],1)
        m.cone(0,0,.42,.29,.35,6,6)
        for x,y in [(-.21,-.1),(.19,-.13),(0,.22)]:
            m.cone(x,y,.17 if name=='Willow' else .38,.19,.45 if name=='Willow' else .26,7 if x>0 else 6,5)
    else:
        m.box(0,0,0,.065,.065,.64,9);m.box(-.09,0,.25,.22,.04,.045,9);m.box(-.18,0,.25,.04,.04,.17,9)
        m.box(.08,.01,.42,.2,.04,.04,9);m.box(0,-.035,.1,.025,.005,.13,15)
    obj=m.finish();obj.location=(len(biome_models)*1.5,23,0);biome_models.append(obj)
pair=[(-.23,-.08,.66,.66,.95,0),(.22,.13,.60,.60,.73,1.7)]
grove=[(-.23,-.15,.57,.57,.76,0),(.23,-.1,.60,.60,.9,2),(0,.23,.58,.58,.67,4)]
variants=[('TreeTall','Tree',[(0,0,.8,.8,1.3,0)]),('TreeWide','Tree',[(0,0,1.25,1.1,.8,0)]),
 ('TreePair','Tree',pair),('TreeGrove','Tree',grove),('PinePair','Pine',pair),('PineGrove','Pine',grove),
 ('DeadTreePair','DeadTree',pair),('CactusPair','Cactus',pair),('SnowPinePair','SnowPine',pair),('SnowPineGrove','SnowPine',grove),
 ('PalmPair','Palm',pair),('WillowPair','Willow',pair),('MangrovePair','Mangrove',pair),('CharredTreePair','CharredTree',pair)]
for index,(name,original,placements) in enumerate(variants):
    old=bpy.data.objects.get(name)
    if old:bpy.data.objects.remove(old,do_unlink=True)
    m=Model(name);mesh=bpy.data.objects[original].data
    for x,y,sx,sy,sz,angle in placements:
        for poly in mesh.polygons:
            uv=mesh.uv_layers.active.data[poly.loop_start].uv;c=int(uv.x*4)+int(uv.y*4)*4
            vs=[]
            for i in poly.vertices:
                v=mesh.vertices[i].co;xx=v.x*sx;yy=v.y*sy
                vs.append((x+xx*math.cos(angle)-yy*math.sin(angle),y+xx*math.sin(angle)+yy*math.cos(angle),v.z*sz))
            m.shape(vs,[tuple(range(len(vs)))],c)
    obj=m.finish();obj.location=(index%4*1.5,19+index//4*1.6,0)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    position=obj.location.copy();obj.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
    obj.location=position
for obj in biome_models:
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    position=obj.location.copy();obj.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+obj.name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
    obj.location=position
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Environment/EnvironmentKit.blend')
with open(ROOT+'/ArtSource/Environment/manifest.json','w') as f:
    json.dump([{'name':o.name,'triangles':o['triangle_count'],'materials':len(o.data.materials),'bounds':list(o.dimensions)} for o in scene.objects if o.type=='MESH'],f,indent=2)
print(json.dumps({name:bpy.data.objects[name]['triangle_count'] for name,_,_ in variants}))
