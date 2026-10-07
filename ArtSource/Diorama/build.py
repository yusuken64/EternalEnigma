"""Run via Blender MCP. Missing diorama forms, in an isolated scene; deterministic and rerunnable.
The JSON export is XY/-Z for Unity. Vendor meshes are never read or changed here.
"""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector

ROOT = Path(EE_PROJECT_ROOT) if 'EE_PROJECT_ROOT' in globals() else Path(__file__).resolve().parents[2]
DEST = ROOT / 'ArtSource' / 'Diorama'
DEST.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.get('EE Diorama Authoring') or bpy.data.scenes.new('EE Diorama Authoring')
for obj in list(scene.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
roles = ['Plaster','Timber','Roof','Door','Glass','Leaf','Flower','Stone','Metal','Glow','Snow','Bark']
colors = [(0.91,.84,.68,1),(.30,.20,.14,1),(.70,.27,.17,1),(.34,.46,.48,1),(.54,.75,.81,1),
          (.39,.62,.18,1),(.92,.44,.32,1),(.57,.56,.49,1),(.17,.20,.22,1),(.99,.77,.33,1),(.90,.95,1,1),(.42,.30,.18,1)]
materials=[]
for name,color in zip(roles,colors):
    mat=bpy.data.materials.get('EE Diorama '+name) or bpy.data.materials.new('EE Diorama '+name)
    mat.use_nodes=True
    node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value=color;node.inputs['Roughness'].default_value=.85
    materials.append(mat)

class Model:
    def __init__(self): self.v=[];self.f=[];self.m=[]
    def poly(self,verts,faces,mat):
        start=len(self.v);self.v.extend(verts)
        for face in faces:self.f.append(tuple(start+i for i in face));self.m.append(roles.index(mat))
    def box(self,c,s,mat,bevel=.025):
        x,y,z=c;a,b,h=[v/2 for v in s];e=min(bevel,a*.25,b*.25,h*.4)
        # Three octagonal rings: bevel catches light on vertical and horizontal edges.
        def ring(a,b,z):return [(x-a+e,y-b,z),(x+a-e,y-b,z),(x+a,y-b+e,z),(x+a,y+b-e,z),(x+a-e,y+b,z),(x-a+e,y+b,z),(x-a,y+b-e,z),(x-a,y-b+e,z)]
        rings=[ring(a-e,b-e,z-h),ring(a,b,z-h+e),ring(a,b,z+h-e),ring(a-e,b-e,z+h)]
        verts=sum(rings,[]);faces=[tuple(reversed(range(8))),tuple(range(24,32))]
        for r in range(3):
            for i in range(8):faces.append((r*8+i,r*8+(i+1)%8,(r+1)*8+(i+1)%8,(r+1)*8+i))
        self.poly(verts,faces,mat)
    def rod(self,a,b,r,mat,n=8,r2=None):
        a,b=Vector(a),Vector(b);q=(b-a).to_track_quat('Z','Y');r2=r if r2 is None else r2
        verts=[tuple(p+q@Vector((rad*math.cos(i*2*math.pi/n),rad*math.sin(i*2*math.pi/n),0))) for p,rad in [(a,r),(b,r2)] for i in range(n)]
        self.poly(verts,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
    def crown(self,c,s,mat,seed=0,n=10):
        rng=random.Random(seed);x,y,z=c;rx,ry,rz=s
        verts=[(x,y,z-rz)]
        for ring in range(1,5):
            phi=-math.pi/2+math.pi*ring/5
            for i in range(n):
                a=(i+.25*(ring%2))*math.tau/n;r=1+rng.uniform(-.06,.06)
                verts.append((x+math.cos(a)*math.cos(phi)*rx*r,y+math.sin(a)*math.cos(phi)*ry*r,z+math.sin(phi)*rz))
        verts.append((x,y,z+rz));faces=[]
        for i in range(n):faces.append((0,1+(i+1)%n,1+i))
        for row in range(3):
            for i in range(n):faces.append((1+row*n+i,1+row*n+(i+1)%n,1+(row+1)*n+(i+1)%n,1+(row+1)*n+i))
        for i in range(n):faces.append((1+3*n+i,1+3*n+(i+1)%n,len(verts)-1))
        self.poly(verts,faces,mat)
    def leaf(self,a,b,width,mat='Leaf'):
        a,b=Vector(a),Vector(b);side=(b-a).cross(Vector((0,0,1)))
        if side.length<.001:side=Vector((1,0,0))
        side.normalize();mid=a.lerp(b,.5);mid.z+=width*.2
        verts=[tuple(a),tuple(mid-side*width),tuple(b),tuple(mid+side*width),tuple(mid+Vector((0,0,width*.2)))]
        self.poly(verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(4,1,0),(4,2,1),(4,3,2),(4,0,3)],mat)

models={}
def save(name,m):models[name]=m
H=1.20601308346

# The imported Tree01 is already a good tiered pine; only missing biome forms are authored.
m=Model();m.rod((0,0,0),(0,0,3.65),.17,'Bark',r2=.055)
for i,(z,r) in enumerate([(1.2,1.10),(1.95,.91),(2.65,.67),(3.24,.40)]):
    m.rod((0,0,z-.35),(0,0,z+.60),r,'Leaf',n=12,r2=.05)
    m.rod((0,0,z-.18),(0,0,z+.65),r*.94,'Snow',n=12,r2=.02)
save('SnowPine',m)
m=Model();points=[(0,0,0),(.1,0,.8),(.28,0,1.7),(.44,0,2.6),(.48,0,3.1)]
for i,(a,b) in enumerate(zip(points,points[1:])):m.rod(a,b,.15-i*.018,'Bark',r2=.13-i*.018)
for j in range(9):
    a=j*math.tau/9;b=(.48+1.3*math.cos(a),1.3*math.sin(a),2.83)
    mid=(.48+.65*math.cos(a),.65*math.sin(a),3.35)
    m.leaf((.48,0,3.08),mid,.23);m.leaf(mid,b,.31)
for j in range(3):m.crown((.48+.18*math.cos(j*2.1),.18*math.sin(j*2.1),2.96),(.12,.12,.16),'Bark',j,n=6)
save('Palm',m)
m=Model();m.rod((0,0,0),(.10,0,2.50),.19,'Bark',r2=.10)
for j in range(7):
    a=j*math.tau/7;tip=(math.cos(a)*.98,math.sin(a)*.94,2.58)
    m.rod((.1,0,1.8),tip,.07,'Bark',r2=.026)
    m.crown(tip,(.70,.67,.75),'Leaf',j)
    for k in range(3):
        u=a+(k-1)*.27;x=math.cos(u)*1.24;y=math.sin(u)*1.24
        m.leaf((x*.82,y*.82,2.5),(x,y,.98+(j%3)*.12),.18)
for j in range(5):
    a=j*math.tau/5;m.rod((.08,0,.48),(math.cos(a)*.45,math.sin(a)*.45,.03),.07,'Bark',r2=.03)
save('Willow',m)
for charred in [False,True]:
    m=Model();mat='Metal' if charred else 'Bark'
    m.rod((0,0,0),(.10,0,2.8),.18,mat,r2=.045)
    for j in range(6):
        a=j*2.4;z=1.0+j*.23;mid=(.65*math.cos(a),.65*math.sin(a),z+.36);tip=(.94*math.cos(a),.94*math.sin(a),z+.8)
        m.rod((.05,0,z),mid,.09,mat,r2=.045);m.rod(mid,tip,.045,mat,r2=.008)
    save('CharredTree' if charred else 'DeadTree',m)

m=Model()
for j in range(13):
    a=j*2.4;r=.14+(j%4)*.085;x=math.cos(a)*r;y=math.sin(a)*r
    m.leaf((x,y,0),(x+math.cos(a)*.18,y+math.sin(a)*.18,.42+(j%4)*.09),.065)
save('TallGrass',m)
m=Model()
for j in range(7):
    a=j*math.tau/7
    for k in range(1,5):
        t=k/5;x=math.cos(a)*t*.63;y=math.sin(a)*t*.63;z=.45*math.sin(t*2.1)
        for side in [-1,1]:m.leaf((x,y,z),(x+math.cos(a+side*.85)*.25*(1-t*.6),y+math.sin(a+side*.85)*.25*(1-t*.6),z-.03),.07)
save('Fern',m)
m=Model()
for j in range(7):
    a=j*math.tau/7;x=math.cos(a)*.37;y=math.sin(a)*.37;h=.19+(j%3)*.065
    m.rod((x,y,0),(x,y,h),.038,'Plaster',n=6);m.crown((x,y,h),(.15,.15,.08),'Flower',j,n=8)
save('MushroomRing',m)
m=Model()
for j in range(9):
    a=j*2.4;x=math.cos(a)*.29;y=math.sin(a)*.29;h=.64+(j%3)*.12
    m.rod((x,y,0),(x+.06,y,h),.018,'Leaf',n=5);m.rod((x+.06,y,h*.78),(x+.06,y,h),.043,'Bark',n=6)
    m.leaf((x,y,.12),(x+math.cos(a)*.28,y+math.sin(a)*.28,.53),.04)
save('Reeds',m)
m=Model()
for j in range(8):
    x=-.44+j*.125;y=math.sin(j*2.3)*.10;h=.10+(j%3)*.035
    m.leaf((x-.035,y,0),(x+.06,y+.04,h),.045)
save('GrassLip',m)

# Outdoor adapters fit a single two-unit grid cell. Existing barrel/crate/bench are reused.
m=Model()
for x in [-.78,.78]:m.box((x,0,.35),(.13,.13,.70),'Timber');m.crown((x,0,.73),(.095,.095,.08),'Timber',n=6)
for z in [.26,.56]:m.box((0,0,z),(1.6,.09,.105),'Timber')
save('Fence',m)
def flowerbox(m,c=(0,0,0),width=.95):
    x,y,z=c;m.box((x,y,z+.12),(width,.30,.24),'Timber');m.box((x,y,z+.24),(width+.04,.34,.06),'Timber')
    for j in range(5):
        u=x+(j-2)*width*.16;m.crown((u,y,z+.33),(.13,.12,.13),'Leaf',j,n=6);m.crown((u,y-.025,z+.43),(.065,.065,.055),'Flower',j,n=6)
m=Model();flowerbox(m);save('FlowerBox',m)
m=Model();m.rod((0,0,0),(0,0,.84),.065,'Timber');m.box((0,0,.85),(.43,.43,.34),'Door');m.box((0,-.23,.92),(.29,.025,.038),'Metal');m.box((.26,0,1.03),(.04,.04,.30),'Timber');m.box((.32,0,1.14),(.13,.04,.10),'Flower');save('Mailbox',m)
m=Model()
for x in [-.58,.58]:m.box((x,0,.61),(.13,.13,1.22),'Timber')
board_start=len(m.v)
m.box((0,0,1.00),(1.43,.12,.50),'Timber');m.box((0,-.073,1.00),(1.25,.035,.34),'Door');m.box((0,0,1.30),(1.56,.25,.10),'Roof')
# Tilt the writing surface toward the elevated gameplay camera; posts stay upright.
a=math.radians(-55)
for i in range(board_start,len(m.v)):
    x,y,z=m.v[i];z-=1.0;m.v[i]=(x,y*math.cos(a)-z*math.sin(a),1.0+y*math.sin(a)+z*math.cos(a))
save('Signboard',m)
for name,size in [('BoulderSmall',(.55,.49,.44)),('BoulderLarge',(.82,.70,.67))]:
    m=Model();m.crown((0,0,size[2]*.83),size,'Stone',14,n=9);save(name,m)

# All facades are 2m wide, with a 1.6H door and 2.2H eave. The door panel
# has a true opening; the door slab remains a separate visual inside that opening.
eaves=H*2.2;door_h=H*1.6
for kind in ['Wall','Window','Door']:
    m=Model()
    if kind=='Door':
        for x in [-.76,.76]:m.box((x,0,eaves/2),(.48,.18,eaves),'Plaster')
        m.box((0,0,(door_h+eaves)/2),(1.06,.18,eaves-door_h),'Plaster')
        m.box((0,.012,door_h/2),(.98,.12,door_h),'Door')
        for x in [-.54,.54]:m.box((x,-.065,door_h/2),(.105,.19,door_h+.07),'Timber')
        m.box((0,-.065,door_h+.015),(1.18,.19,.12),'Timber');m.box((0,-.17,.065),(1.25,.52,.13),'Stone')
        for x in [-.75,.75]:m.rod((x,-.10,1.72),(x,-.47,2.14),.045,'Timber',n=6)
        m.box((0,-.30,2.17),(1.73,.70,.12),'Roof');m.box((.31,-.075,.92),(.09,.07,.09),'Metal')
        m.box((.76,-.22,1.75),(.19,.20,.30),'Glow');m.box((.76,-.22,1.92),(.25,.25,.07),'Metal')
    else:
        m.box((0,0,eaves/2),(2,.18,eaves),'Plaster')
        if kind=='Window':
            m.box((0,-.12,1.49),(.92,.10,.91),'Glass')
            for x in [-.50,.50]:m.box((x,-.18,1.49),(.11,.15,1.04),'Timber')
            for z in [1.0,1.49,1.98]:m.box((0,-.18,z),(1.10,.15,.10),'Timber')
            m.box((0,-.18,1.49),(.07,.15,.94),'Timber');flowerbox(m,(0,-.28,.55),1.12)
        else:
            m.rod((-.83,-.12,.52),(.83,-.12,2.35),.065,'Timber',n=4)
    for x in [-.94,.94]:m.box((x,-.045,eaves/2),(.12,.24,eaves+.04),'Timber')
    for z in [.28,eaves-.04]:m.box((0,-.075,z),(2.04,.23,.15),'Timber')
    for x in [-.75,-.25,.25,.75]:m.box((x,-.02,.13),(.49,.25,.25),'Stone')
    save('Facade'+kind,m)
m=Model();m.box((0,0,eaves/2),(.20,.20,eaves),'Timber');save('CornerPost',m)
m=Model();m.box((0,0,.1),(2,.30,.20),'Stone');save('StoneBase',m)
m=Model();m.box((0,0,.075),(1.08,.29,.15),'Roof');save('RoofEave',m)
m=Model();m.rod((0,-.52,0),(0,.52,0),.095,'Roof',n=8);save('RoofRidge',m)
m=Model();m.box((0,0,.41),(.39,.42,.82),'Stone');m.box((0,0,.85),(.50,.53,.15),'Stone');m.box((0,0,.935),(.29,.30,.025),'Metal');save('Chimney',m)
m=Model();m.box((0,0,.30),(.88,.62,.60),'Plaster');m.box((0,-.34,.33),(.53,.10,.38),'Glass')
for x in [-.31,.31]:m.box((x,-.35,.33),(.075,.12,.46),'Timber')
m.poly([(-.55,-.42,.62),(0,-.42,.97),(.55,-.42,.62),(-.55,.41,.62),(0,.41,.97),(.55,.41,.62)],[(0,3,4,1),(1,4,5,2),(0,1,2),(5,4,3)],'Roof');save('Dormer',m)
for service in ['Inn','Shop','Trainer']:
    m=Model();m.rod((0,.04,.46),(0,-.48,.46),.038,'Metal',n=6);m.box((0,-.48,.09),(.69,.09,.57),'Timber');m.box((0,-.535,.09),(.57,.025,.45),'Door')
    if service=='Inn':
        m.box((0,-.56,.06),(.35,.035,.12),'Glow')
        for x in [-.18,.18]:m.box((x,-.56,.035),(.045,.035,.22),'Glow')
        m.box((-.09,-.56,.15),(.13,.035,.07),'Glow')
    elif service=='Shop':
        m.box((0,-.56,.075),(.29,.035,.22),'Glow');m.rod((-.09,-.565,.20),(.09,-.565,.20),.025,'Glow',n=6)
    else:
        m.rod((-.13,-.56,-.05),(.13,-.56,.28),.028,'Glow',n=6);m.rod((-.13,-.56,.25),(.13,-.56,-.05),.028,'Glow',n=6)
    save('Service'+service,m)

# Grid cliff pieces use the same height-field contact contract as the TWC six-set.
# Rounded terraces and three darker strata replace the old pyramidal slope.
def cliff(mask):
    m=Model();n=8;verts=[]
    for y in range(n+1):
        for x in range(n+1):
            px=x/n-.5;py=y/n-.5
            distances=[.5-py if not mask&1 else 2,.5-px if not mask&2 else 2,.5+py if not mask&4 else 2,.5+px if not mask&8 else 2]
            d=min(distances);t=max(0,min(1,d/.26));t=t*t*(3-2*t)
            z=.42*t
            # Terracing retains exact edges and top planes; no gaps between neighbors.
            if 0<t<1:z=.42*(.80*t+.20*round(t*4)/4)
            verts.append((px,py,z))
    for y in range(n):
        for x in range(n):
            a=y*(n+1)+x;ids=(a,a+1,a+n+2,a+n+1)
            z=sum(verts[i][2] for i in ids)/4
            m.poly([verts[i] for i in ids],[(0,1,2,3)],'Leaf' if z>.395 else 'Stone')
    return m
for name,mask in [('CliffSingle',0),('CliffEnd',1),('CliffStraight',5),('CliffOuter',9),('CliffTee',13),('CliffFill',15)]:save(name,cliff(mask))
m=Model();m.crown((0,0,.11),(.48,.46,.23),'Stone',14,n=10);save('RockCap',m)

report={'roles':roles,'colors':colors,'models':[]}
for index,(name,m) in enumerate(models.items()):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(m.v,[],m.f)
    for mat in materials:mesh.materials.append(mat)
    for polygon,material in zip(mesh.polygons,m.m):polygon.material_index=material
    mesh.update();mesh.calc_loop_triangles()
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    obj.location=((index%8)*4,(index//8)*4,0)
    # Export polygon corners so the authored bevels keep their normals.
    vertices=[];normals=[];shades=[];uv=[];sub=[[] for _ in roles]
    maxheight=max((v[2] for v in m.v),default=1)
    for triangle in mesh.loop_triangles:
        poly=mesh.polygons[triangle.polygon_index];ids=[]
        for vi in triangle.vertices:
            p=mesh.vertices[vi].co;n=poly.normal;ids.append(len(vertices))
            vertices.append({'x':p.x,'y':p.y,'z':-p.z});normals.append({'x':n.x,'y':n.y,'z':-n.z})
            shade=.76+.24*max(0,min(1,p.z/max(.01,maxheight)))
            # Dark undersides and root contact, baked into vertex color independently of scene lighting.
            shade*=.88+.12*max(0,n.z)
            shades.append({'r':shade,'g':shade,'b':shade,'a':1});uv.append({'x':p.x,'y':p.y})
        sub[poly.material_index].extend([ids[0],ids[2],ids[1]])
    sockets=[]
    if name=='FacadeWall':
        sockets=[{'kind':'Ornament','center':{'x':-.46,'y':-.095,'z':-(eaves-.5)},'width':.55,'height':.50}]
    elif name=='FacadeDoor':
        sockets=[{'kind':'Service','center':{'x':-.82,'y':-.10,'z':-1.65},'width':.92,'height':.90}]
    record={'id':name,'vertices':vertices,'normals':normals,'colors':shades,'uv':uv,'sockets':sockets,'parts':[{'role':roles[i],'triangles':t} for i,t in enumerate(sub) if t]}
    report['models'].append(record)
(DEST/'Meshes.json').write_text(json.dumps(report,separators=(',',':')))
bpy.ops.wm.save_as_mainfile(filepath=str(DEST/'Diorama.blend'),copy=True)
print(json.dumps({'models':len(models),'triangles':{r['id']:sum(len(p['triangles'])//3 for p in r['parts']) for r in report['models']},'source':str(DEST)}))
