"""Run through Blender MCP in an isolated scene. Source coordinates: XY floor, Z up, front -Y.
Exports editable rigs, FBX and lossless Unity mesh/weight manifests; never edits purchased assets.
"""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector
ROOT=Path(EE_PROJECT_ROOT) if 'EE_PROJECT_ROOT' in globals() else Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/TownInteriors'; EXPORT=ROOT/'Assets/Art/TownInteriors/Models'
OUT.mkdir(parents=True,exist_ok=True);EXPORT.mkdir(parents=True,exist_ok=True)
COLORS=['442937','713e35','a35c3d','d49a50','f5ddb0','fff2cc','267c81','55b6a6','79529a','b38cc5','629747','b6cf68','3e4358','7376ac','a1aae0','bd434b','e77949','ffd33f','428cc0','9cccea','e16ba1','edaec2','805a3e','b89b69','aaa68c','e9e3d1','c4ad80','dbb954','3d332d','9fb8a1','edab72','f4cfa6']
def rgb(s):return tuple(int(s[i:i+2],16)/255 for i in (0,2,4))
scene=bpy.data.scenes.new('EE_TownInteriors_Authoring');bpy.context.window.scene=scene
scene.render.fps=24
palette=bpy.data.images.new('EE_TownPalette',width=len(COLORS)*16,height=64)
pixels=[]
for y in range(64):
 for x in range(len(COLORS)*16):
  c=rgb(COLORS[x//16]);shade=(.78,.94,1.0,1.06)[y//16]
  pixels.extend([min(1,v*shade) for v in c]+[1])
palette.pixels=pixels;palette.filepath_raw=str(EXPORT.parent/'Palette.png');palette.file_format='PNG';palette.save();palette.pack()
mat=bpy.data.materials.new('EE_Town_PaintedPalette');mat.use_nodes=True
node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');node.inputs['Roughness'].default_value=.85
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=palette;mat.node_tree.links.new(tex.outputs['Color'],node.inputs['Base Color'])
class Model:
 def __init__(self,name,animated=False,bird=False):self.name=name;self.v=[];self.f=[];self.colors=[];self.weights=[];self.animated=animated;self.bird=bird
 def mesh(self,v,f,c,bone='Body'):
  clean=[]
  for face in f:
   ids=[]
   for i in face:
    if not ids or (Vector(v[i])-Vector(v[ids[-1]])).length_squared>1e-14:ids.append(i)
   if len(ids)>1 and (Vector(v[ids[0]])-Vector(v[ids[-1]])).length_squared<1e-14:ids.pop()
   if len(ids)>=3:clean.append(ids)
  offset=len(self.v);self.v.extend(v);self.f.extend([tuple(i+offset for i in face) for face in clean]);self.colors.extend([c]*len(clean));self.weights.extend([bone]*len(v))
 def box(self,p,s,c,bone='Body',angle=0):
  x,y,z=p;a,b,d=[t/2 for t in s];co=math.cos(angle);si=math.sin(angle)
  # Carved corner cuts keep furniture from reading as unmodified cubes. Tile seams stay exact.
  bevel=0 if self.name.startswith(('Carpet','Counter')) else min(a,b,d)*.28
  outline=[(-a+bevel,-b),(a-bevel,-b),(a,-b+bevel),(a,b-bevel),(a-bevel,b),(-a+bevel,b),(-a,b-bevel),(-a,-b+bevel)]
  vs=[(x+u*co-v*si,y+u*si+v*co,z+w) for w in (-d,d) for u,v in outline]
  self.mesh(vs,[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)],c,bone)
 def ball(self,p,s,c,bone='Body',n=16,r=10):
  v=[]
  for j in range(r+1):
   a=math.pi*j/r
   for i in range(n):
    b=2*math.pi*i/n;v.append((p[0]+s[0]*math.sin(a)*math.cos(b),p[1]+s[1]*math.sin(a)*math.sin(b),p[2]+s[2]*math.cos(a)))
  f=[]
  for j in range(r):
   for i in range(n):
    a=j*n+i;b=j*n+(i+1)%n;c1=(j+1)*n+(i+1)%n;d=(j+1)*n+i
    if j==0:f.append((a,d,c1))
    elif j==r-1:f.append((a,d,b))
    else:f.append((a,d,c1,b))
  self.mesh(v,f,c,bone)
 def cone(self,p,r1,r2,h,c,bone='Body',n=10):
  v=[(p[0]+r*math.cos(2*math.pi*i/n),p[1]+r*math.sin(2*math.pi*i/n),p[2]+z) for r,z in [(r1,0),(r2,h)] for i in range(n)]
  f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
  self.mesh(v,f,c,bone)
 def tube(self,pts,r,c,bone='Body',n=8):
  v=[]
  for k,p in enumerate(pts):
   tangent=Vector(pts[min(k+1,len(pts)-1)])-Vector(pts[max(0,k-1)])
   tangent.normalize();u=tangent.cross(Vector((0,1,0)))
   if u.length<.1:u=tangent.cross(Vector((1,0,0)))
   u.normalize();w=tangent.cross(u)
   for i in range(n):v.append(tuple(Vector(p)+r*(u*math.cos(i*2*math.pi/n)+w*math.sin(i*2*math.pi/n))))
  f=[tuple(reversed(range(n))),tuple(range((len(pts)-1)*n,len(pts)*n))]
  for k in range(len(pts)-1):
   for i in range(n):f.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i))
  self.mesh(v,f,c,bone)
 def finish(self):
  mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(self.v,[],self.f);mesh.materials.append(mat);mesh.update()
  uv=mesh.uv_layers.new(name='Painted palette')
  for polygon,c in zip(mesh.polygons,self.colors):
   shade=2 if polygon.normal.z>.3 else 1 if polygon.normal.z>-.5 else 0
   for li in polygon.loop_indices:uv.data[li].uv=((c+.5)/len(COLORS),(shade+.5)/4)
   polygon.use_smooth=len(polygon.vertices)==4 and self.animated
  obj=bpy.data.objects.new(self.name,mesh);scene.collection.objects.link(obj)
  bones=[];rig=None
  if self.animated:
   z=.3 if self.bird else .55
   bones=[('Root',(0,0,0),None),('Body',(0,0,z),'Root'),('Head',(0,0,z if self.bird else 1.1),'Body'),('Left',( -.25,0,z if self.bird else .85),'Body'),('Right',(.25,0,z if self.bird else .85),'Body'),('Eyes',(0,-.2,z if self.bird else 1.3),'Head')]
   arm=bpy.data.armatures.new(self.name+'_Rig');rig=bpy.data.objects.new(self.name+'_Rig',arm);scene.collection.objects.link(rig)
   bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
   for name,p,parent in bones:
    b=arm.edit_bones.new(name);b.head=p;b.tail=Vector(p)+Vector((0,0,.12))
    if parent:b.parent=arm.edit_bones[parent]
   bpy.ops.object.mode_set(mode='OBJECT')
   obj.parent=rig;mod=obj.modifiers.new('Reusable town rig','ARMATURE');mod.object=rig
   for name,_,_ in bones:
    group=obj.vertex_groups.new(name=name);ids=[i for i,b in enumerate(self.weights) if b==name]
    if ids:group.add(ids,1,'REPLACE')
   for clip in ['Idle','Greeting'] if not self.bird else ['Idle']:
    action=bpy.data.actions.new(self.name+'_'+clip);rig.animation_data_create();rig.animation_data.action=action
    for frame in range(1,98,4):
     phase=(frame-1)/96*math.tau
     for b in rig.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0);b.scale=(1,1,1)
     rig.pose.bones['Body'].location.y=.012*math.sin(phase)
     # Bone-local Y points up; local Z maps to gameplay's ground Y after conversion.
     rig.pose.bones['Head'].rotation_euler.z=.065*math.sin(phase)
     for name,sgn in [('Left',-1),('Right',1)]:rig.pose.bones[name].rotation_euler.z=sgn*(.12*max(0,math.sin(phase*2)) if self.bird else .035*math.sin(phase))
     if clip=='Greeting':rig.pose.bones['Right'].rotation_euler.z=.55+.18*math.sin(phase*3)
     rig.pose.bones['Eyes'].scale.y=.1 if frame in (45,49) else 1
     for b in rig.pose.bones:
      for path in ('location','rotation_euler','scale'):b.keyframe_insert(data_path=path,frame=frame,group=b.name)
    track=rig.animation_data.nla_tracks.new();track.name=clip;track.strips.new(clip,1,action)
   rig.animation_data.action=None
  mesh.calc_loop_triangles()
  # Lossless transport used by Unity: duplicate loop vertices to preserve hard/painted seams.
  vertices=[];uvs=[];weights=[];triangles=[];normals=[]
  for tri in mesh.loop_triangles:
   for li in reversed(tri.loops):
    vi=mesh.loops[li].vertex_index;p=mesh.vertices[vi].co;n=tri.normal
    triangles.append(len(vertices));vertices.append({'x':p.x,'y':p.y,'z':-p.z});normals.append({'x':n.x,'y':n.y,'z':-n.z})
    t=uv.data[li].uv;uvs.append({'x':t.x,'y':t.y});weights.append(next((i for i,b in enumerate(bones) if b[0]==self.weights[vi]),0))
  record={'name':self.name,'animated':self.animated,'bird':self.bird,'vertices':vertices,'normals':normals,'uv':uvs,'triangles':triangles,'weights':weights,
   'bones':[{'name':n,'position':{'x':p[0],'y':p[1],'z':-p[2]},'parent':next((i for i,b in enumerate(bones) if b[0]==par),-1)} for n,p,par in bones]}
  (EXPORT/(self.name+'.json')).write_text(json.dumps(record,separators=(',',':')))
  for o in bpy.context.selected_objects:o.select_set(False)
  obj.select_set(True)
  if rig:rig.select_set(True)
  bpy.context.view_layer.objects.active=rig or obj
  bpy.ops.export_scene.fbx(filepath=str(EXPORT/(self.name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=self.animated,bake_anim_use_all_actions=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
  manifest.append({'name':self.name,'triangles':len(mesh.loop_triangles),'materials':1,'animated':self.animated,'bird':self.bird,'bounds':[[min(p[k] for p in self.v) for k in range(3)],[max(p[k] for p in self.v) for k in range(3)]]})
  return rig or obj
manifest=[];assets={}

def npc(name,fur,cloth):
 m=Model(name,True);b=m.ball
 b((0,0,.68),(.27,.21,.38),cloth)
 for x in (-.15,.15):b((x,-.05,.17),(.145,.23,.17),fur);b((x,0,.35),(.13,.14,.23),fur)
 b((0,-.015,1.23),(.38,.29,.35),fur,'Head',20,14)
 for x,bone in [(-.32,'Left'),(.32,'Right')]:b((x,0,.76),(.13,.145,.26),cloth,bone);b((x,-.04,.56),(.13,.15,.13),fur,bone)
 # Painted muzzle and eyes: broad cream fields remain legible at gameplay scale.
 b((0,-.26,1.12),(.23,.1,.13),4,'Head',16,8)
 for x in (-.145,.145):
  b((x,-.27,1.29),(.095,.045,.11),5,'Head',12,8)
  b((x,-.314,1.285),(.047,.026,.073),28,'Eyes',12,8)
  b((x-.012,-.336,1.315),(.018,.01,.022),5,'Eyes',8,6)
 b((0,-.362,1.17),(.06,.04,.04),28,'Head',10,6)
 m.tube([(-.08,-.348,1.105),(0,-.367,1.08),(.08,-.348,1.105)],.013,28,'Head',6)
 if name in ('Cat','Dog','Merchant'):
  for x in (-.265,.265):
   m.mesh([(x-.12,0,1.4),(x+.12,0,1.4),(x*1.35,0,1.83),(x,-.15,1.43),(x,.12,1.43)],[(0,3,2),(3,1,2),(1,4,2),(4,0,2),(0,4,1,3)],fur,'Head')
   m.mesh([(x-.065,-.09,1.46),(x+.065,-.09,1.46),(x*1.22,-.04,1.72)],[(0,1,2)],21 if name!='Merchant' else 14,'Head')
 if name=='Cat':
  m.cone((0,0,1.49),.4,.28,.08,22,'Head',12);m.cone((-.04,.02,1.54),.3,.12,.25,22,'Head',10)
  m.tube([(-.14,.17,.36),(-.48,.2,.46),(-.57,.16,.8),(-.46,.13,1.0)],.10,4)
  for x,z in [(-.25,1.38),(-.22,1.44),(.26,1.4),(.21,1.46),(0,1.47)]:b((x,-.242,z),(.025,.02,.04),22,'Head',8,5)
  m.box((0,.23,.8),(.45,.20,.43),22);m.cone((0,.26,1.02),.12,.12,.16,29)
  m.tube([(-.18,-.23,.95),(.19,-.24,.55)],.026,3)
  b((-.05,.015,1.85),(.10,.055,.05),11,'Head',10,6)
 if name=='Dog':
  b((0,-.29,1.12),(.19,.17,.12),24,'Head');m.box((0,-.21,.88),(.35,.04,.15),6)
  for x in (-.22,.22):b((x,-.18,1.48),(.12,.07,.11),12,'Head',10,6)
  m.tube([(0,.17,.35),(.2,.31,.6),(.27,.25,.84)],.075,23)
 if name=='Bear':
  for x in (-.28,.28):b((x,0,1.52),(.13,.1,.15),25,'Head')
  # Thin but chunky eyeglass rims.
  for x in (-.145,.145):m.tube([(x+.115*math.cos(t*math.tau/12),-.323,1.29+.13*math.sin(t*math.tau/12)) for t in range(13)],.012,22,'Head',5)
  m.box((0,-.22,.66),(.35,.055,.37),4);m.cone((.19,-.265,.74),.036,.036,.09,27)
 if name=='Sheep':
  for i in range(9):
   a=math.pi*i/8;b((math.cos(a)*.29,-.03,1.43+math.sin(a)*.18),(.12,.16,.12),25,'Head',10,7)
  for x in (-.34,.34):b((x,0,1.3),(.15,.08,.08),24,'Head',10,6)
  m.box((0,-.21,.69),(.35,.05,.39),5);m.tube([(-.24,-.16,.94),(0,-.24,.91),(.24,-.16,.94)],.036,18);m.cone((0,-.27,.86),.06,.035,.075,27)
 if name=='Bunny':
  for x in (-.2,.2):
   b((x,.01,1.7),(.105,.095,.37),17,'Head');b((x,-.08,1.74),(.048,.026,.24),21,'Head',10,8)
  for i in range(5):a=i*math.tau/5;b((.22+.07*math.cos(a),-.11,1.63+.07*math.sin(a)),(.048,.028,.048),9,'Head',8,6)
  b((.22,-.145,1.63),(.035,.022,.035),27,'Head',8,6)
  m.box((0,-.205,.65),(.34,.07,.38),4)
 if name=='GuineaPig':
  for x in (-.3,.3):b((x,.02,1.46),(.13,.10,.11),26,'Head')
  m.cone((0,0,1.47),.43,.38,.08,8,'Head',12);m.cone((0,0,1.55),.31,.025,.45,9,'Head',10)
  m.tube([(-.44,-.10,.2),(-.44,-.10,1.31)],.025,22,'Left');b((-.44,-.10,1.37),(.12,.06,.12),17,'Left',5,4)
  m.cone((0,0,.17),.33,.23,.51,8);m.box((0,-.235,.85),(.15,.045,.10),27)
 if name=='Merchant':
  # Hood leaves the face open; its red back, heavy rim and tail match the source.
  b((0,.12,1.29),(.415,.23,.4),15,'Head',16,10)
  m.tube([(-.37,-.04,1.0),(-.4,-.04,1.38),(-.27,-.04,1.61),(0,-.04,1.66),(.27,-.04,1.61),(.4,-.04,1.38),(.37,-.04,1.0)],.035,3,'Head')
  m.cone((0,.08,.28),.34,.23,.59,15);m.tube([(.13,.17,.36),(.42,.21,.5),(.48,.15,.72)],.1,13)
  for x in (-.24,.24):b((x,-.24,1.14),(.05,.025,.025),21,'Head',8,5)
 return m

def bird(name,color):
 m=Model(name,True,True);b=m.ball
 b((0,0,.28),(.255,.215,.25),color,'Head',16,10)
 b((0,-.178,.22),(.175,.067,.16),4 if name!='BirdBlue' else 19,'Head',12,7)
 for x,bone in [(-.245,'Left'),(.245,'Right')]:
  b((x,0,.27),(.09,.13,.055),6 if name=='BirdYellow' else color,bone,8,5)
  if name=='BirdYellow':b((x,-.01,.30),(.06,.10,.017),7,bone,8,4)
 for x in (-.09,.09):
  b((x,-.20,.33),(.079,.038,.087),5,'Head',10,6)
  if name=='BirdRed':m.tube([(x-.04,-.239,.33),(x,-.249,.349),(x+.04,-.239,.33)],.012,28,'Eyes',5)
  else:b((x,-.239,.326),(.027,.016,.034),28,'Eyes',8,5)
  m.tube([(x,-.05,.045),(x,-.11,.015),(x+.04,-.12,.015)],.014,17 if name!='BirdYellow' else 24,'Root',5)
 m.mesh([(-.035,-.219,.275),(.035,-.219,.275),(0,-.30,.245),(0,-.225,.215)],[(0,2,1),(0,3,2),(1,2,3),(0,1,3)],16 if name=='BirdBlue' else 17,'Head')
 if name=='BirdBlue':
  b((0,.005,.448),(.18,.16,.095),20,'Head',10,6)
  m.tube([(0,0,.48),(-.025,0,.57),(0,0,.61)],.028,20,'Head',6)
 elif name=='BirdRed':
  m.tube([(-.09,0,.49),(-.08,0,.55),(0,0,.57),(.055,0,.535),(.01,0,.52)],.024,15,'Head',6)
  for x in (-.08,.08):b((x,-.13,.454),(.025,.019,.027),7,'Head',8,4)
 else:
  for x in (-.045,0,.045):b((x,-.09,.485),(.022,.025,.055),28,'Head',8,4)
 return m

def prop(name):
 m=Model(name);box=m.box;b=m.ball
 def legs(h=.62):
  for x in (-.29,.29):
   for y in (-.29,.29):box((x,y,h/2),(.095,.095,h),1)
 def slab(z=.65):box((0,0,z),(.82,.78,.10),3);box((0,0,z+.055),(.76,.72,.025),2)
 if name in ('Table','Chair','Stool','Bench','Bedside','Lectern'):
  h=.4 if name in ('Chair','Stool','Bench') else .6;legs(h);slab(h)
  if name=='Chair':box((0,.29,.7),(.67,.08,.48),2);box((0,.28,.94),(.74,.12,.07),3)
  if name=='Lectern':box((0,.1,.79),(.7,.5,.1),2);box((0,.08,.86),(.46,.32,.045),4)
  if name=='Bedside':box((0,0,.39),(.63,.64,.32),2);b((0,-.34,.4),(.035,.03,.035),27,n=8,r=5)
 elif name in ('Cupboard','Shelf','Bookcase','Rack','ShopDisplay','KeyBoard'):
  h=1.25 if name!='KeyBoard' else .7
  box((-.36,0,h/2),(.10,.42,h),1);box((.36,0,h/2),(.10,.42,h),1);box((0,.18,h/2),(.65,.07,h),2)
  for z in (.1,.5,.9,h):box((0,0,z),(.83,.48,.09),3)
  if name=='Cupboard':box((0,-.2,.64),(.63,.08,1.02),2)
  if name=='Bookcase':
   for z in (.17,.57,.97):
    for i in range(5):box((-.26+i*.13,-.03,z+.09),(.09,.25,.18),[8,6,15,4,22][i])
  if name in ('Rack','KeyBoard'):
   for x in (-.22,0,.22):m.tube([(x,-.23,.28),(x,-.23,.74)],.018,27);b((x,-.23,.78),(.05,.02,.055),27,n=8,r=4)
 elif name in ('Chest','Crate','Luggage'):
  box((0,0,.29),(.72,.60,.56),2)
  for x in (-.3,.3):box((x,0,.32),(.075,.65,.61),3)
  for z in (.08,.54):box((0,-.31,z),(.72,.055,.06),3)
  if name!='Crate':b((0,-.33,.36),(.075,.025,.065),27,n=8,r=5)
 elif name in ('Barrel','Sack','Basket'):
  m.cone((0,0,.05),.25,.32,.28,22 if name=='Barrel' else 3);m.cone((0,0,.33),.32,.25,.28,22 if name=='Barrel' else 4)
  for z in (.12,.5):m.cone((0,0,z),.29,.29,.055,12 if name=='Barrel' else 3)
  if name=='Basket':
   for x in (-.14,.12):b((x,0,.59),(.12,.19,.08),3,n=12,r=7)
 elif name=='Bed':
  legs(.26);box((0,0,.28),(.81,.94,.12),1);box((0,0,.37),(.76,.87,.13),4);box((0,-.13,.45),(.77,.56,.06),6);box((0,.27,.48),(.55,.23,.11),5)
  for y in (-.44,.44):box((0,y,.5 if y>0 else .28),(.84,.075,.55 if y>0 else .23),2)
 elif name=='Hearth':
  box((0,.17,.55),(.8,.25,1.1),1)
  for x in (-.29,.29):box((x,-.04,.29),(.20,.50,.55),24)
  box((0,-.04,.61),(.83,.53,.17),3);box((0,-.06,.08),(.83,.59,.12),24)
  for x in (-.12,.12):m.cone((x,-.10,.15),.07,0,.27,16)
 elif name=='Plant':
  m.cone((0,0,0),.2,.27,.35,2);m.cone((0,0,.30),.28,.28,.08,3)
  for i in range(5):
   a=i*math.tau/5;b((.12*math.cos(a),.12*math.sin(a),.5+(i%2)*.12),(.12,.1,.22),10 if i%2 else 11,n=10,r=6)
 elif name in ('Potions','Cookware','Tableware','Candles','Books','Equipment'):
  if name=='Books':
   for i in range(3):box((0,0,.045+i*.065),(.37,.45,.055),[8,4,6][i])
  elif name=='Equipment':
   box((0,0,.035),(.40,.60,.04),4);box((0,0,.09),(.085,.5,.045),24);box((0,-.17,.12),(.30,.055,.05),27)
  else:
   for x,c in [(-.18,8),(.04,6),(.2,15)]:
    if name=='Candles':m.cone((x,0,0),.045,.045,.23,4);m.cone((x,0,.24),.025,0,.075,17)
    elif name=='Tableware':m.cone((x,0,0),.07,.10,.13,4)
    else:b((x,0,.13),(.10,.10,.12),c if name=='Potions' else 12,n=10,r=7);m.cone((x,0,.20),.04,.04,.09,3)
 elif name in ('Dummy','Target','ArcanePedestal'):
  m.cone((0,0,.02),.3,.25,.08,1);m.cone((0,0,.08),.05,.05,.82,3)
  if name=='Dummy':b((0,0,.75),(.19,.16,.3),26);b((0,0,1.14),(.15,.13,.15),26);box((0,0,.91),(.73,.08,.08),3)
  elif name=='Target':
   for y,s,c in [(0,.32,3),(-.035,.25,4),(-.07,.16,15),(-.10,.065,27)]:b((0,y,.91),(s,.035,s),c,n=16,r=8)
  else:m.cone((0,0,.25),.25,.2,.43,24);box((0,0,.72),(.57,.57,.1),3);m.cone((0,0,.78),.12,0,.3,8)
 elif name=='PerchSign':
  box((0,0,.41),(.12,.12,.82),1);box((0,0,.69),(.76,.16,.34),2);box((0,0,.92),(.84,.44,.08),3)
  box((0,-.095,.69),(.62,.02,.20),4)
  for x in (-.28,.28):m.tube([(x,-.1,.57),(x,-.1,.82)],.018,27)
 elif name in ('Window','Banner','Frame','Sconce','Lantern'):
  if name=='Window':
   box((0,0,.65),(.8,.10,.84),1);box((0,-.065,.65),(.59,.03,.64),6)
   for x in (-.34,0,.34):box((x,-.09,.65),(.06,.055,.78),3)
   box((0,-.14,.22),(.94,.35,.095),3)
  elif name in ('Banner','Frame'):
   box((0,0,.64),(.62,.06,.82),3);box((0,-.04,.64),(.50,.035,.68),8 if name=='Banner' else 4);b((0,-.067,.64),(.12,.018,.15),27,n=6,r=4)
  else:
   box((0,.04,.5),(.16,.12,.65),1);box((0,-.12,.7),(.3,.3,.08),3);box((0,-.12,.43),(.25,.25,.08),3);box((0,-.12,.57),(.18,.18,.2),17)
 elif name.startswith('Carpet') or name.startswith('Counter'):
  counter=name.startswith('Counter');z=.69 if counter else .012
  box((0,0,z/2), (1,1,z),2 if counter else 6)
  if counter:box((0,0,.71),(1,1,.02),3)
  edge=name.split('_')[1]
  if edge in ('Edge','Outer'):
   box((0,.46,z+.006),(1,.08,.012),3)
  if edge=='Outer':box((.46,0,z+.006),(.08,1,.012),3)
  if edge=='Inner':box((.46,.46,z+.006),(.08,.08,.012),3)
 return m

def build():
 for name,fur,cloth in [('Cat',4,29),('Dog',23,6),('Bear',25,1),('Sheep',24,6),('Bunny',17,10),('GuineaPig',26,8),('Merchant',13,15)]:assets[name]=npc(name,fur,cloth).finish()
 for name,c in [('BirdBlue',18),('BirdRed',16),('BirdYellow',17)]:assets[name]=bird(name,c).finish()
 for name in ['Table','Chair','Stool','Bench','Cupboard','Shelf','Chest','Crate','Barrel','Bed','Bedside','Hearth','Cookware','Plant','ShopDisplay','Basket','Potions','Equipment','Sack','KeyBoard','Luggage','Tableware','Dummy','Target','Rack','Bookcase','Lectern','ArcanePedestal','Window','Banner','Frame','Sconce','Lantern','Candles','Books','PerchSign']+[k+'_'+s for k in ('Carpet','Counter') for s in ('Fill','Edge','Inner','Outer')]:assets[name]=prop(name).finish()
 (OUT/'manifest.json').write_text(json.dumps({'palette':COLORS,'assets':manifest},indent=2))
 (EXPORT.parent/'manifest.json').write_text(json.dumps({'palette':COLORS,'assets':manifest},indent=2))
 for i,(name,obj) in enumerate(assets.items()):obj.location=(i%9*1.7,i//9*2.3,0)
 scene.frame_set(1);scene.frame_start=1;scene.frame_end=97
 bpy.data.libraries.write(str(OUT/'TownInteriors.blend'),{scene},fake_user=True)
 print('Exported',len(manifest),'assets; triangle budgets:',[(a['name'],a['triangles']) for a in manifest if a['animated']])
build()
