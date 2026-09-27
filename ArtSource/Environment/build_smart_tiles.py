"""Run in Blender after build_kit.py. Native TWC quarter tiles and six-way walls."""
import bpy, math, json, os
ROOT = 'C:/Users/yusuk/Documents/GitHub/EternalEnigma'
OUT = ROOT + '/Assets/Art/EnvironmentKit'
scene = bpy.data.scenes.get('EE Environment Kit')
bpy.context.window.scene = scene
# Reuse only the modeling helper, without recreating the existing scene or textures.
source = open(ROOT+'/ArtSource/Environment/build_kit.py', encoding='utf-8').read()
mat = next(m for m in bpy.data.materials if m.name.startswith('EnvironmentPalette'))
exec(source[source.index('class Model:'):source.index('\nmodels=')])
models=[]
for family in ['Mountain', 'House', 'Road']:
    for kind in ['Edge','Outer','Inner','Fill']:
        name='Smart'+family+kind
        old=bpy.data.objects.get(name)
        if old: bpy.data.objects.remove(old, do_unlink=True)
        m=Model(name)
        def height(x,y):
            a=.5-x; b=.5-y
            t=b if kind=='Edge' else min(a,b) if kind=='Outer' else max(a,b) if kind=='Inner' else 1
            return .02 if family=='Road' else (.68+.52*t if family=='House' else .82*min(1,t/.35))
        if family=='Road':
            # Convex/concave curb shapes leave underlying biome ground visible at the verge.
            outline=[(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)]
            if kind=='Edge': outline=[(-.5,-.5),(.5,-.5),(.5,.43),(-.5,.43)]
            if kind=='Outer': outline=[(-.5,-.5),(.43,-.5),(.43,.32),(.32,.43),(-.5,.43)]
            if kind=='Inner': outline=[(-.5,-.5),(.5,-.5),(.5,.43),(.43,.5),(-.5,.5)]
            m.shape([(0,0,.025)]+[(x,y,.025) for x,y in outline],[(0,i+1,(i+1)%len(outline)+1) for i in range(len(outline))],14)
            road_surface_count=len(m.f)
            curb=([(.5,.43),(-.5,.43)] if kind=='Edge' else
                [(.43,-.5),(.43,.32),(.32,.43),(-.5,.43)] if kind=='Outer' else
                [(.5,.43),(.43,.5)] if kind=='Inner' else [])
            for a,b in zip(curb,curb[1:]):
                dx=b[0]-a[0];dy=b[1]-a[1];length=math.hypot(dx,dy)
                nx=dy/length*.12;ny=-dx/length*.12
                c=(b[0]-nx,b[1]-ny);d=(a[0]-nx,a[1]-ny)
                # Top and both vertical faces; open ends meet the next rule piece without caps.
                m.quad([(a[0],a[1],.095),(b[0],b[1],.095),(c[0],c[1],.095),(d[0],d[1],.095)],3)
                m.quad([(a[0],a[1],.025),(b[0],b[1],.025),(b[0],b[1],.095),(a[0],a[1],.095)],3)
                m.quad([(c[0],c[1],.025),(d[0],d[1],.025),(d[0],d[1],.095),(c[0],c[1],.095)],3)
        else:
            verts=[(x,y,height(x,y)) for y in [-.5,0,.5] for x in [-.5,0,.5]]
            faces=[]
            for y in range(2):
                for x in range(2):
                    a=y*3+x; faces.extend([(a,a+1,a+4),(a,a+4,a+3)])
            m.shape(verts,faces,4 if family=='House' else 3)
            if family=='Mountain':
                # A shallow plateau and a steep faceted rim; matching boundaries on every piece.
                m=Model(name)
                if kind=='Fill':
                    m.shape([(x,y,.82) for x,y in [(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)]],[(0,1,2),(0,2,3)],3)
                else:
                    vs=[(x,y,height(x,y)) for y in [-.5,.15,.5] for x in [-.5,.15,.5]]
                    for yy in range(2):
                        for xx in range(2):
                            a=yy*3+xx
                            for f in [(a,a+1,a+4),(a,a+4,a+3)]:
                                m.shape([vs[i] for i in f],[(0,1,2)],9 if sum(vs[i][2] for i in f)<1.1 else 3)
            elif kind in ['Edge','Outer']:
                # Half-timbered facade only on the exposed perimeter. Joined edges have no internal walls.
                m.quad([(-.5,.5,.68),(.5,.5,.68),(.5,.5,0),(-.5,.5,0)],2)
                m.box(0,.485,.07,1,.035,.07,0);m.box(0,.485,.59,1,.035,.07,0)
                m.box(-.46,.485,0,.07,.04,.67,0)
                m.box(.14,.508,.25,.23,.02,.22,8)
                m.box(.14,.522,.25,.025,.015,.22,0)
                if kind=='Outer':
                    m.quad([(.5,.5,.68),(.5,-.5,.68),(.5,-.5,0),(.5,.5,0)],2)
                    m.box(.485,0,.07,.035,1,.07,0);m.box(.485,0,.59,.035,1,.07,0)
                    m.box(.485,.46,0,.04,.07,.67,0)
        obj=m.finish()
        if family=='Road':
            for poly in obj.data.polygons:
                for index in poly.loop_indices:
                    v=obj.data.vertices[obj.data.loops[index].vertex_index].co
                    obj.data.uv_layers.active.data[index].uv=(v.x+.5,.25+.75*(v.y+.5)) if poly.index<road_surface_count else (.5,.125)
        models.append(obj)
for kind,arms in [('Single',[]),('End',[0]),('Straight',[0,2]),('Corner',[0,3]),('Tee',[0,2,3]),('Cross',[0,1,2,3])]:
    name='SmartWall'+kind
    old=bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old, do_unlink=True)
    m=Model(name)
    m.box(0,0,0,.28,.28,.7,3);m.box(0,0,.7,.32,.32,.12,2)
    for arm in arms:
        a=arm*math.pi/2
        x=math.sin(a)*.32;y=math.cos(a)*.32
        sx=.26 if arm%2==0 else .36;sy=.36 if arm%2==0 else .26
        m.box(x,y,0,sx,sy,.56,3)
        m.box(x,y,.56,sx,sy,.08,2)
    models.append(m.finish())
# Separate inn sign attaches to the generated house perimeter without replacing its rule tiles.
old=bpy.data.objects.get('InnSign')
if old: bpy.data.objects.remove(old,do_unlink=True)
m=Model('InnSign');m.box(0,0,0,.08,.08,.8,0);m.box(.2,0,.72,.48,.07,.07,0);m.box(.28,0,.36,.31,.08,.29,8)
m.box(.28,-.045,.44,.22,.02,.06,0);m.box(.19,-.045,.39,.025,.02,.14,0);m.box(.37,-.045,.39,.025,.02,.09,0)
models.append(m.finish())
# Sparse silhouette accents for exposed upper terraces. All stay inside one terrain cell.
for name,peaks in [
 ('MountainRidge',[(-.25,0,.32,.7),(.12,.04,.32,1.05),(.36,0,.19,.62)]),
 ('MountainPeak',[(0,0,.43,1.4)]),
 ('MountainSpires',[(-.23,-.12,.20,.98),(.15,.12,.24,1.22),(.33,-.20,.15,.64)])]:
    old=bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old,do_unlink=True)
    m=Model(name)
    for x,y,r,h in peaks:
        n=5
        ring=[(x+r*math.cos(i*math.tau/n),y+r*math.sin(i*math.tau/n),0) for i in range(n)]
        snow=[(x+r*.25*math.cos(i*math.tau/n),y+r*.25*math.sin(i*math.tau/n),h*.75) for i in range(n)]
        for i in range(n):
            j=(i+1)%n
            m.shape([ring[i],ring[j],snow[j],snow[i]],[(0,1,2),(0,2,3)],3 if i%2 else 9)
            m.shape([snow[i],snow[j],(x+.03,y-.025,h)],[(0,1,2)],13 if name!='MountainSpires' else 3)
    models.append(m.finish())
# Joined summit quarters: low saddles on connected sides, a tapered exposed rim,
# and an asymmetric faceted peak. Eight triangles per quarter, one palette slot.
for kind in ['Edge','Outer','Inner','Fill']:
    name='SmartSummit'+kind
    old=bpy.data.objects.get(name)
    if old:bpy.data.objects.remove(old,do_unlink=True)
    m=Model(name)
    verts=[]
    for y in [-.5,0,.5]:
        for x in [-.5,0,.5]:
            a=.5-x;b=.5-y
            t=b if kind=='Edge' else min(a,b) if kind=='Outer' else max(a,b) if kind=='Inner' else 1
            z=.24*min(1,t/.35)
            if x==0 and y==0:z=1.05 if kind=='Fill' else .86
            verts.append((x,y,z))
    for yy in range(2):
        for xx in range(2):
            a=yy*3+xx
            for face in [(a,a+1,a+4),(a,a+4,a+3)]:
                m.shape([verts[i] for i in face],[(0,1,2)],3 if xx else 9)
    models.append(m.finish())
# Native water-side TWC quarters, confined to their cell to avoid overlaps in narrow rivers.
for kind in ['Edge','Outer','Inner']:
    name='SmartShore'+kind
    old=bpy.data.objects.get(name)
    if old:bpy.data.objects.remove(old,do_unlink=True)
    m=Model(name)
    m.shape([(-.5,-.5,.01),(.5,-.5,.01),(.5,.5,.01),(-.5,.5,.01)],[(0,1,2),(0,2,3)],14)
    obj=m.finish()
    for loop in obj.data.loops:
        v=obj.data.vertices[loop.vertex_index].co;a=.5-v.x;b=.5-v.y
        t=b if kind=='Edge' else min(a,b) if kind=='Outer' else max(a,b)
        obj.data.uv_layers.active.data[loop.index].uv=(v.x+.5,t)
    models.append(obj)
# One shared cobble/curb atlas, keeping every road piece to a single material.
atlas=bpy.data.images.get('SmartRoad') or bpy.data.images.new('SmartRoad',128,128,alpha=True)
base=list(bpy.data.images['MedievalPaving'].pixels);pixels=[]
for y in range(128):
    for x in range(128):
        if y<32:pixels.extend((.72,.69,.58,1))
        else:
            yy=min(127,int((y-32)/96*128));pixels.extend(base[(yy*128+x)*4:(yy*128+x)*4+4])
atlas.pixels=pixels;atlas.file_format='PNG';atlas.filepath_raw=OUT+'/Textures/SmartRoad.png';atlas.save()
roadmat=bpy.data.materials.get('SmartRoad') or bpy.data.materials.new('SmartRoad');roadmat.use_nodes=True
node=next(n for n in roadmat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
tex=next((n for n in roadmat.node_tree.nodes if n.type=='TEX_IMAGE'),None) or roadmat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image=atlas;roadmat.node_tree.links.new(tex.outputs['Color'],node.inputs['Base Color'])
for obj in models:
    if obj.name.startswith('SmartRoad'):obj.data.materials.clear();obj.data.materials.append(roadmat)
for i,obj in enumerate(models): obj.location=((i%6)*1.5,9+(i//6)*1.6,0)
for obj in models:
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    position=obj.location.copy();obj.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+obj.name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
    obj.location=position
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Environment/EnvironmentKit.blend')
with open(ROOT+'/ArtSource/Environment/manifest.json','w') as f:
    json.dump([{'name':o.name,'triangles':o['triangle_count'],'materials':len(o.data.materials),'bounds':list(o.dimensions)} for o in scene.objects if o.type=='MESH'],f,indent=2)
print(json.dumps({o.name:o['triangle_count'] for o in models}))
