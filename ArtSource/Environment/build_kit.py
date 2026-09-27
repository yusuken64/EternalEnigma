"""Run inside Blender. Low-poly environment kit; one palette material per mesh."""
import bpy, math, json, os
from mathutils import Vector

ROOT = 'C:/Users/yusuk/Documents/GitHub/EternalEnigma'
OUT = ROOT + '/Assets/Art/EnvironmentKit'
scene = bpy.data.scenes.new('EE Environment Kit')
bpy.context.window.scene = scene
palette = [
    (.16,.09,.06), (.40,.23,.12), (.83,.70,.48), (.42,.44,.43),
    (.22,.36,.28), (.34,.50,.36), (.20,.37,.15), (.45,.59,.23),
    (.92,.62,.16), (.20,.24,.26), (.07,.10,.12), (1.0,.80,.37),
    (.63,.23,.15), (.90,.92,.84), (.45,.36,.23), (.28,.61,.62)]
palettes = {}
for biome in ['Grassland','Desert','Water','Mountain','Forest','Tundra','Marsh','Volcanic']:
    p=list(palette)
    changes={
      'Desert':{2:(.91,.76,.48),3:(.69,.47,.27),4:(.54,.23,.14),5:(.78,.38,.19),6:(.23,.40,.25),7:(.46,.61,.28),14:(.73,.56,.34)},
      'Water':{2:(.78,.77,.61),3:(.49,.56,.57),4:(.16,.39,.48),5:(.26,.57,.61),6:(.21,.47,.32),7:(.42,.66,.38),14:(.57,.48,.31)},
      'Mountain':{2:(.72,.70,.58),3:(.43,.46,.51),4:(.27,.32,.42),5:(.43,.49,.58),6:(.22,.36,.30),7:(.40,.52,.38)},
      'Forest':{2:(.71,.66,.43),3:(.39,.45,.36),4:(.30,.24,.16),5:(.45,.35,.20),6:(.10,.30,.18),7:(.27,.46,.23)},
      'Tundra':{2:(.81,.82,.75),3:(.50,.59,.66),4:(.71,.81,.86),5:(.91,.96,.97),6:(.20,.38,.38),7:(.61,.77,.73),14:(.67,.73,.74)},
      'Marsh':{2:(.65,.68,.48),3:(.36,.42,.38),4:(.31,.27,.42),5:(.45,.39,.54),6:(.29,.39,.17),7:(.51,.58,.25),14:(.33,.30,.21)},
      'Volcanic':{2:(.55,.48,.40),3:(.25,.26,.30),4:(.37,.15,.14),5:(.60,.23,.16),6:(.33,.25,.23),7:(.53,.31,.19),14:(.25,.22,.23),15:(1,.38,.10)}
    }.get(biome,{})
    for i,c in changes.items(): p[i]=c
    image=bpy.data.images.new('Palette_'+biome,64,64,alpha=True)
    pixels=[]
    for y in range(64):
        for x in range(64): pixels.extend((*p[(y//16)*4+x//16],1))
    image.pixels=pixels; image.filepath_raw=OUT+'/Textures/Palette_'+biome+'.png'; image.file_format='PNG'; image.save()
    palettes[biome]=image
mat=bpy.data.materials.new('EnvironmentPalette'); mat.use_nodes=True
node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
node.inputs['Roughness'].default_value=.9
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=palettes['Grassland']
mat.node_tree.links.new(tex.outputs['Color'],node.inputs['Base Color'])

class Model:
    def __init__(self,name): self.name=name; self.v=[]; self.f=[]; self.c=[]
    def shape(self,verts,faces,color):
        start=len(self.v); self.v.extend(verts)
        for face in faces: self.f.append(tuple(start+i for i in face)); self.c.append(color)
    def box(self,x,y,z,sx,sy,sz,c):
        a=x-sx/2; b=x+sx/2; d=y-sy/2; e=y+sy/2; f=z; g=z+sz
        self.shape([(a,d,f),(b,d,f),(b,e,f),(a,e,f),(a,d,g),(b,d,g),(b,e,g),(a,e,g)],
            [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],c)
    def cone(self,x,y,z,r,height,c,n=5,top=0,angle=0):
        v=[(x+r*math.cos(angle+i*math.tau/n),y+r*math.sin(angle+i*math.tau/n),z) for i in range(n)]
        if top==0:
            v.append((x+.06*r,y-.08*r,z+height)); faces=[tuple(reversed(range(n)))]+[(i,(i+1)%n,n) for i in range(n)]
        else:
            v += [(x+top*math.cos(angle+i*math.tau/n),y+top*math.sin(angle+i*math.tau/n),z+height) for i in range(n)]
            faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        self.shape(v,faces,c)
    def roof(self,x,y,z,w,d,h,c=4):
        self.shape([(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x+w/2,y+d/2,z),(x-w/2,y+d/2,z),(x,y-d/2,z+h),(x,y+d/2,z+h)],
            [(0,1,4),(2,3,5),(0,4,5,3),(1,2,5,4),(3,2,1,0)],c)
    def quad(self,vertices,c): self.shape(vertices,[(0,1,2,3)],c)
    def finish(self):
        mesh=bpy.data.meshes.new(self.name); mesh.from_pydata(self.v,[],self.f); mesh.update()
        obj=bpy.data.objects.new(self.name,mesh); scene.collection.objects.link(obj); mesh.materials.append(mat)
        uv=mesh.uv_layers.new(name='PaletteUV')
        for poly,c in zip(mesh.polygons,self.c):
            for i in poly.loop_indices: uv.data[i].uv=((c%4+.5)/4,(c//4+.5)/4)
        mesh.calc_loop_triangles()
        obj['triangle_count']=len(mesh.loop_triangles); obj['purpose']='Static miniature; palette UV; base pivot; no collider'
        return obj

models=[]
def house(name,kind):
    m=Model(name); h=.66 if kind!='inn' else .9
    m.box(0,0,0,.90,.78,.12,3); m.box(0,0,.12,.82,.71,h-.12,2)
    m.roof(0,0,h,1.02,.91,.36,4); m.box(0,0,h+.32,.07,.94,.045,5)
    for x in [-.37,.37]: m.box(x,-.365,.12,.055,.035,h-.12,0)
    m.box(0,-.37,.18,.82,.04,.045,1); m.box(0,-.37,h-.08,.82,.04,.055,0)
    m.box(.1,-.375,.12,.2,.03,.31,0); m.box(.1,-.396,.14,.15,.015,.26,1)
    for x in [-.23,.29]:
        m.box(x,-.379,h-.29,.14,.024,.16,0); m.box(x,-.396,h-.27,.10,.012,.12,11)
    m.box(.25,.16,h,.12,.13,.30,3); m.box(.25,.16,h+.28,.16,.17,.045,9)
    if kind=='inn':
        m.box(0,-.38,.49,.83,.04,.055,0)
        m.box(-.5,-.33,.5,.06,.06,.40,1); m.box(-.4,-.33,.85,.24,.05,.05,1)
        m.box(-.50,-.33,.65,.24,.045,.19,8)
        m.cone(-.50,-.355,.66,.065,.1,11,6) # bold gilded inn sign
    if kind=='shop':
        m.box(0,-.56,.36,.84,.25,.04,1)
        for x in [-.38,.38]: m.box(x,-.64,0,.04,.04,.55,0)
        for i in range(4): m.roof(-.315+i*.21,-.48,.52,.205,.42,.05,12 if i%2 else 13)
        for x in [-.22,0,.22]: m.box(x,-.54,.40,.15,.15,.1,8 if x==0 else 6)
    if kind=='trainer':
        m.box(-.5,-.32,0,.06,.06,.95,0)
        m.quad([(-.47,-.33,.95),(-.20,-.33,.88),(-.27,-.33,.67),(-.47,-.33,.71)],12)
        m.box(.47,-.35,0,.1,.1,.37,1); m.box(.47,-.36,.18,.3,.07,.06,1)
    return m.finish()
for n,k in [('House','house'),('Inn','inn'),('Shop','shop'),('Trainer','trainer')]: models.append(house(n,k))
m=Model('Shrine'); m.box(0,0,0,.85,.8,.12,3);m.box(0,0,.12,.6,.6,.12,3);m.box(0,0,.24,.30,.3,.35,2);m.cone(0,0,.59,.27,.48,15,5);m.cone(0,0,.96,.13,.16,8,5);models.append(m.finish())
m=Model('DungeonPortal')
for x in [-.37,.37]:
    m.box(x,0,0,.26,.45,.7,3);m.box(x,0,.7,.3,.50,.1,2)
m.box(0,0,.65,.64,.48,.18,3);m.roof(0,0,.82,.95,.58,.21,9)
m.box(0,.1,0,.48,.045,.65,10);m.box(0,-.24,0,.90,.38,.055,14);models.append(m.finish())
m=Model('Gate')
for x in [-.40,.40]:
    m.box(x,0,0,.20,.27,.72,3);m.box(x,0,.7,.27,.32,.11,2);m.cone(x,0,.81,.16,.14,3,4,angle=math.pi/4)
for x in [-.24,-.08,.08,.24]: m.box(x,0,.04,.045,.07,.59,9)
m.box(0,0,.55,.65,.08,.065,1);m.box(0,0,.18,.65,.08,.055,1)
m.box(0,-.07,.27,.23,.08,.23,8);m.box(0,-.116,.32,.045,.007,.10,10)
models.append(m.finish())
m=Model('Wall');m.box(0,0,0,1,.25,.48,3);m.box(0,0,.46,1,.30,.07,2)
for x in [-.37,0,.37]:m.box(x,0,.53,.18,.27,.16,3)
models.append(m.finish())
m=Model('Mountain');m.cone(-.15,0,0,.49,.78,3,5);m.cone(.24,.11,0,.32,.55,9,5);m.cone(-.15,0,.58,.14,.2,13,5);models.append(m.finish())
m=Model('Tree');m.cone(0,0,0,.065,.35,1,5,top=.045);m.cone(0,0,.20,.32,.43,6,6);m.cone(.10,.04,.40,.23,.28,7,5);models.append(m.finish())
m=Model('Pine');m.cone(0,0,0,.05,.25,1,4,top=.04);m.cone(0,0,.16,.29,.38,6,5);m.cone(0,0,.38,.21,.32,7,5);models.append(m.finish())
m=Model('Cactus');m.box(0,0,0,.12,.12,.56,6);m.box(-.13,0,.23,.25,.10,.09,7);m.box(-.23,0,.23,.09,.09,.19,6);m.box(.13,0,.35,.22,.09,.08,7);m.box(.21,0,.35,.08,.09,.19,6);models.append(m.finish())
for name,col in [('Rock',3),('SnowRock',13),('Basalt',9)]:
    m=Model(name);m.cone(-.09,0,0,.24,.24 if name!='Basalt' else .43,col,5,top=.12);m.cone(.17,.08,0,.14,.16,3,4);models.append(m.finish())
m=Model('Reeds')
for x,y,h in [(-.13,0,.35),(0,.08,.43),(.14,-.04,.30)]: m.box(x,y,0,.025,.025,h,6);m.box(x,y,h-.06,.06,.05,.11,1)
models.append(m.finish())
m=Model('Mushrooms')
for x,y,s in [(-.13,0,1),(.13,.03,.7)]:m.cone(x,y,0,.04*s,.19*s,2,4,top=.025*s);m.cone(x,y,.15*s,.17*s,.11*s,12,6)
models.append(m.finish())
m=Model('Flowers')
for x,y in [(-.12,0),(.12,.10),(0,-.13)]:m.cone(x,y,0,.06,.12,6,4);m.cone(x,y,.10,.075,.055,11,5)
models.append(m.finish())
m=Model('DeadTree');m.box(0,0,0,.06,.06,.53,1);m.box(-.09,0,.22,.22,.04,.055,1);m.box(-.18,0,.22,.04,.04,.15,1);m.box(.08,0,.37,.2,.04,.05,1);models.append(m.finish())

# A true two-triangle tile: detail is a small shared texture, not stones per cell.
m=Model('Paving');m.quad([(-.5,-.5,0),(.5,-.5,0),(.5,.5,0),(-.5,.5,0)],14);obj=m.finish()
for poly in obj.data.polygons:
    for i,uv in zip(poly.loop_indices,[(0,0),(1,0),(1,1),(0,1)]):obj.data.uv_layers.active.data[i].uv=uv
models.append(obj)

# Painted broad cobbles; tileable joints and quiet dirt, 128px shared albedo.
im=bpy.data.images.new('MedievalPaving',128,128,alpha=True); pixels=[]
for y in range(128):
    row=y//32
    for x in range(128):
        xx=(x+(row%2)*16)%128; col=xx//32
        edge=min(xx%32,31-xx%32,y%32,31-y%32)
        variation=((row*17+col*11)%5)*.018
        c=(.30,.27,.21) if edge<2 else ((.49+variation,.45+variation,.35+variation) if edge>4 else (.42,.38,.30))
        pixels.extend((*c,1))
im.pixels=pixels;im.filepath_raw=OUT+'/Textures/MedievalPaving.png';im.file_format='PNG';im.save()

for i,obj in enumerate(models): obj.location=((i%5)*1.7,(i//5)*1.8,0)
scene.world=bpy.data.worlds.new('Environment Kit World');scene.world.color=(.16,.16,.16)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=12
        area.spaces.active.region_3d.view_location=(3.4,2.5,0)
        area.spaces.active.shading.color_type='MATERIAL'
for obj in models:
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    position=obj.location.copy();obj.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+obj.name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
    obj.location=position
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Environment/EnvironmentKit.blend')
print(json.dumps({o.name:o['triangle_count'] for o in models},indent=2))
