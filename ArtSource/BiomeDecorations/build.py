"""Run through Blender MCP. Repeatable bespoke low-poly meshes; no scene clearing."""
import bpy, math, json, os
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT = str(Path(EE_PROJECT_ROOT) if 'EE_PROJECT_ROOT' in globals() else Path(__file__).resolve().parents[2])
DEST = ROOT + '/Assets/Art/EternalEnigma/BiomeDecorations'
SOURCE = ROOT + '/ArtSource/BiomeDecorations'
os.makedirs(DEST, exist_ok=True)
collection = bpy.data.collections.get('EE Biome Decorations')
if collection is None:
    collection = bpy.data.collections.new('EE Biome Decorations')
    bpy.context.scene.collection.children.link(collection)
for obj in list(collection.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
colors = [(0.13,.16,.19,1),(.38,.20,.085,1),(.78,.52,.18,1),(.98,.72,.25,1),
          (.19,.42,.15,1),(.67,.70,.64,1),(.22,.65,.73,1),(.61,.19,.14,1),
          (.36,.22,.47,1),(.67,.84,.94,1),(.11,.10,.14,1),(.98,.28,.055,1)]
mats=[]
for i, color in enumerate(colors):
    mat=bpy.data.materials.get('EE_Deco_%02d'%i) or bpy.data.materials.new('EE_Deco_%02d'%i)
    mat.diffuse_color=color; mat.use_nodes=True
    node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value=color
    node.inputs['Roughness'].default_value=.82
    mats.append(mat)

class Model:
    def __init__(self): self.v=[]; self.f=[]; self.m=[]
    def poly(self, verts, faces, mat):
        start=len(self.v); self.v.extend(verts)
        for f in faces: self.f.append(tuple(start+i for i in f)); self.m.append(mat)
    def box(self, c, s, mat):
        x,y,z=c; a,b,d=[v/2 for v in s]
        self.poly([(x-a,y-b,z-d),(x+a,y-b,z-d),(x+a,y+b,z-d),(x-a,y+b,z-d),
                   (x-a,y-b,z+d),(x+a,y-b,z+d),(x+a,y+b,z+d),(x-a,y+b,z+d)],
                  [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)
    def rod(self,a,b,r,mat,n=4,r2=None):
        a,b=Vector(a),Vector(b); q=(b-a).to_track_quat('Z','Y'); rr=r if r2 is None else r2
        vs=[tuple(p+q@Vector((radius*math.cos(i*2*math.pi/n),radius*math.sin(i*2*math.pi/n),0))) for p,radius in [(a,r),(b,rr)] for i in range(n)]
        self.poly(vs,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
    def gem(self,c,s,mat,n=4):
        x,y,z=c; a,b,d=s
        self.poly([(x,y,z+d)]+[(x+a*math.cos(i*2*math.pi/n),y+b*math.sin(i*2*math.pi/n),z) for i in range(n)]+[(x,y,z-d)],
                  [(0,1+i,1+(i+1)%n) for i in range(n)]+[(n+1,1+(i+1)%n,1+i) for i in range(n)],mat)
    def banner(self,mat,shape=0):
        points=[(-.22,-.06,.45),(.22,-.06,.45),(.20,-.06,.07),(0,-.06,-.04),(-.20,-.06,.07)]
        self.poly(points,[(0,1,2,3,4),(4,3,2,1,0)],mat)
        self.rod((-.29,0,.48),(.29,0,.48),.035,1)

biomes=['Grassland','Desert','Water','Mountain','Forest','Tundra','Marsh','Volcanic']
base=[1,2,5,0,1,5,1,10]; glow=[3,3,6,6,3,9,6,11]
def fixture(i):
    m=Model(); metal=base[i]; lit=glow[i]
    m.box((0,.035,.40),(.13,.08,.18),metal)
    m.rod((0,0,.48),(0,-.22,.48),.035,metal)
    if i==0: # oak-supported iron lantern with sloped cap
        m.box((0,-.23,.20),(.22,.20,.29),3)
        m.rod((0,-.23,.035),(0,-.23,.065),.19,0)
        m.rod((0,-.23,.35),(0,-.23,.44),.19,0,r2=.07)
        for x in [-.12,.12]: m.rod((x,-.34,.06),(x,-.34,.36),.02,0)
    elif i==1: # pierced brass bands around faceted oil vessel
        m.rod((0,-.23,.06),(0,-.23,.16),.08,2,n=6,r2=.17)
        m.rod((0,-.23,.16),(0,-.23,.37),.17,2,n=6,r2=.065)
        for x in [-.09,0,.09]: m.gem((x,-.375,.22),(.024,.006,.047),10)
        m.gem((0,-.23,.42),(.06,.06,.08),3)
    elif i==2: # shell fan with pearl
        for j in range(5):
            a=(j-2)*.42
            m.rod((0,-.20,.06),(.23*math.sin(a),-.20,.12+.27*math.cos(a)),.042,5,n=3,r2=.065)
        m.gem((0,-.30,.22),(.12,.10,.12),6,n=6)
    elif i==3:
        m.gem((0,-.23,.23),(.13,.11,.21),6)
        for x in [-.17,.17]: m.rod((x,-.23,.05),(x,-.23,.43),.025,0)
        for z in [.05,.43]: m.rod((-.17,-.23,z),(.17,-.23,z),.035,0)
    elif i==4: # bentwood ribs around a seed
        m.gem((0,-.22,.23),(.10,.095,.16),3,n=5)
        for x in [-1,1]:
            m.rod((0,-.22,.03),(x*.17,-.22,.23),.027,1)
            m.rod((x*.17,-.22,.23),(0,-.22,.46),.027,1)
        m.gem((.14,-.24,.42),(.12,.025,.05),4)
    elif i==5:
        m.rod((0,-.23,.07),(0,-.23,.36),.14,9,n=6,r2=.11)
        for z in [.05,.39]: m.rod((0,-.23,z),(0,-.23,z+.025),.17,5,n=6)
        m.gem((.11,-.28,.07),(.035,.04,.10),9)
    elif i==6:
        m.gem((0,-.23,.22),(.14,.10,.15),6,n=5)
        for j in range(4):
            x=(j-1.5)*.09
            m.rod((x*.6,-.34,.05),(x,-.34,.41),.018,1,n=3)
        m.rod((-.16,-.34,.11),(.16,-.34,.11),.025,5)
    else:
        m.gem((0,-.23,.21),(.13,.10,.18),11)
        for x in [-1,1]:
            m.rod((x*.14,-.24,.02),(x*.19,-.24,.30),.05,10,n=3,r2=.025)
            m.rod((x*.19,-.24,.30),(0,-.24,.44),.025,0,n=3)
        m.gem((0,-.24,.02),(.20,.14,.07),10)
    return m
def ornament(i):
    m=Model()
    if i in [0,1,5]:
        m.banner([3,7,9][[0,1,5].index(i)])
        if i==0:
            m.gem((0,-.08,.30),(.095,.01,.095),2,n=6)
            for x in [-.12,.12]:
                m.rod((x,-.085,.08),(x,-.085,.24),.012,1,n=3)
                m.gem((x,-.085,.21),(.045,.015,.075),2)
        elif i==1:
            for z in [.13,.31]: m.gem((0,-.08,z),(.14,.012,.08),2)
        else:
            for x in [-.15,0,.15]: m.rod((x,-.08,.10),(x,-.08,-.08),.024,5,n=3)
    elif i==2:
        m.rod((-.23,0,.43),(0,-.04,.10),.023,1)
        m.rod((0,-.04,.10),(.23,0,.43),.023,1)
        for x,z in [(-.16,.34),(0,.14),(.16,.34)]: m.gem((x,-.06,z),(.085,.03,.10),5,n=5)
    elif i==3:
        m.box((0,0,.25),(.42,.07,.43),5)
        for a,b in [((-.1,-.05,.12),(0,-.05,.38)),((0,-.05,.38),(.1,-.05,.12)),((-.08,-.05,.22),(.08,-.05,.22))]:m.rod(a,b,.018,6,n=3)
    elif i==4:
        m.gem((0,-.01,.20),(.11,.04,.18),4,n=4)
        for x in [-1,1]:
            m.rod((x*.08,0,.24),(x*.22,0,.49),.025,5)
            m.rod((x*.15,0,.35),(x*.28,0,.37),.018,5,n=3)
    elif i==6:
        for x in [-.16,0,.16]: m.rod((x,0,.08),(x+.03,0,.48),.021,1,n=3)
        m.rod((-.22,-.02,.36),(.22,-.02,.36),.025,5)
        m.gem((0,-.04,.09),(.10,.04,.07),5)
    else:
        for a,b in [((-.20,0,.1),(0,0,.47)),((0,0,.47),(.20,0,.1)),((-.20,0,.1),(.20,0,.1)),((0,0,0),(0,0,.4))]:m.rod(a,b,.028,0)
        m.gem((0,-.035,.24),(.07,.03,.09),11)
    return m
def accent(i):
    m=Model()
    if i==0:
        m.rod((-.20,0,.02),(.16,0,.47),.018,1,n=3)
        for j in range(5):
            x=-.17+j*.07; z=.06+j*.085
            m.gem((x+(.06 if j%2 else -.06),-.015,z),(.09,.025,.06),4)
        for x,z in [(-.19,.14),(.12,.4)]:m.gem((x,-.05,z),(.06,.03,.05),3)
    elif i==1:
        for j in range(6):
            a=j*math.pi/3
            m.gem((.13*math.cos(a),-.02,.24+.13*math.sin(a)),(.10,.055,.075),2)
        m.gem((0,-.09,.24),(.09,.04,.085),3)
    elif i in [3,5,7]:
        for j in range(5):
            x=(j-2)*.10; z=.20+(.07 if j%2 else 0)
            if i==5:m.rod((x,0,.45),(x,-.02,.08+(j%3)*.055),.045,9,n=4,r2=0)
            else:m.gem((x,-.02,z),(.085,.055,.10+(j%3)*.07),[2,6,9,11][[1,3,5,7].index(i)])
        if i==7:m.box((0,.025,.10),(.5,.1,.13),10)
    elif i==2:
        for x in [-.17,0,.17]:
            m.rod((x,0,.02),(x,-.02,.31),.035,7,n=4,r2=.025)
            m.rod((x,0,.15),(x+.07,-.02,.23),.027,7,n=3,r2=.015)
        for x in [-.1,.12]:m.gem((x,-.06,.06),(.07,.05,.05),5,n=5)
    elif i==4:
        m.rod((-.20,0,0),(-.08,0,.46),.035,1,n=4)
        m.rod((-.08,0,.46),(.15,0,.40),.025,1,n=4)
        for x,z in [(-.12,.1),(.04,.23),(-.04,.34)]:m.rod((x,-.07,z),(x,-.07,z+.055),.12,7 if i==4 else 6,n=5,r2=.075)
    else:
        for x,z in [(-.13,.14),(.08,.19),(0,.37)]:
            m.gem((x,0,z),(.15,.025,.11),4)
            m.rod((x,-.03,z-.10),(x,-.12,z),.022,5,n=3)
            m.rod((x,-.12,z),(x,-.12,z+.05),.08,6,n=5,r2=.03)
    return m
def post(i,sign=False):
    m=Model(); material=5 if i in [1,2,3] else base[i]
    pts=[(0,0,0),((.08 if i in [4,6] else 0),0,.60),((-.08 if i==6 else 0),0,1.2)]
    for a,b in zip(pts,pts[1:]):m.rod(a,b,.065 if i!=3 else .09,material,n=4 if i in [0,5,6] else 5)
    m.rod((0,0,0),(0,0,.1),.14,10 if i==7 else material,n=4)
    if i in [0,5,6]:
        for z in [.20,.8]:m.rod((0,0,z),(0,0,z+.045),.08,0 if i==0 else 5,n=4)
    elif i==4:m.rod((.03,0,.65),(.25,0,.94),.04,1)
    elif i==1:
        for z in [.18,.82]:m.rod((0,0,z),(0,0,z+.045),.085,2,n=5)
    elif i==2:
        m.gem((0,-.08,.32),(.12,.04,.13),5,n=5)
        m.rod((0,0,.62),(-.17,0,.86),.045,1,n=4,r2=.025)
    elif i==3:
        m.gem((0,-.09,.48),(.035,.008,.12),0)
        m.box((0,0,1.16),(.21,.19,.08),5)
    elif i==7:m.gem((0,0,.08),(.21,.15,.18),10)
    if not sign:
        f=fixture(i); start=len(m.v)
        m.v.extend([(x,y,z+.80) for x,y,z in f.v]);m.f.extend([tuple(start+v for v in face) for face in f.f]);m.m.extend(f.m)
    return m
def panel(i):
    m=Model()
    # Arrow silhouette is independent from its pole, for up to three branches.
    p=[(-.48,0,-.09),(.31,0,-.09),(.49,0,0),(.31,0,.09),(-.48,0,.09)]
    m.poly([(x,y-.035,z) for x,y,z in p]+[(x,y+.035,z) for x,y,z in p],
        [(4,3,2,1,0),(5,6,7,8,9)]+[(j,(j+1)%5,(j+1)%5+5,j+5) for j in range(5)],base[i])
    if i==2:m.gem((-.40,-.055,0),(.07,.025,.06),5,n=5)
    elif i==4:m.rod((-.49,.045,-.09),(.32,.045,-.09),.025,1,n=3)
    elif i==6:m.rod((-.43,-.045,-.12),(-.43,-.045,.12),.018,5,n=3)
    elif i==7:m.gem((-.41,-.045,0),(.045,.02,.075),11)
    return m

report=[]
for i,biome in enumerate(biomes):
    for j,(kind,m) in enumerate([('Fixture',fixture(i)),('Ornament',ornament(i)),('Accent',accent(i)),('LampPost',post(i)),('SignPost',post(i,True)),('SignPanel',panel(i))]):
        name=biome+'_'+kind
        mesh=bpy.data.meshes.new(name); mesh.from_pydata(m.v,[],m.f); mesh.materials.clear()
        for mat in mats:mesh.materials.append(mat)
        for poly,idx in zip(mesh.polygons,m.m):poly.material_index=idx
        uv=mesh.uv_layers.new(name='Palette')
        for poly in mesh.polygons:
            for loop in poly.loop_indices:uv.data[loop].uv=((poly.material_index+.5)/len(mats),.5)
        mesh.calc_loop_triangles(); triangles=len(mesh.loop_triangles)
        assert triangles <= (240 if kind in ['LampPost','SignPost'] else 120),(name,triangles)
        obj=bpy.data.objects.new(name,mesh); collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(filepath=DEST+'/'+name+'.fbx',use_selection=True,add_leaf_bones=False,bake_anim=False)
        obj.location=(j*1.6+4,i*1.9,0)
        report.append({'id':name,'triangles':triangles,'bounds_min':[min(v[k] for v in m.v) for k in range(3)],'bounds_max':[max(v[k] for v in m.v) for k in range(3)]})
json.dump({'palette':colors,'models':report},open(SOURCE+'/manifest.json','w'),indent=2)
# Save a copy, preserving the user's active file and unrelated objects.
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+'/BiomeDecorations.blend',copy=True)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_location=Vector((8,6.5,.4))
        area.spaces.active.region_3d.view_distance=20
        area.spaces.active.region_3d.view_rotation=Quaternion((.883,.441,.071,.14)).normalized()
        area.spaces.active.shading.color_type='MATERIAL'
print(json.dumps(report))
