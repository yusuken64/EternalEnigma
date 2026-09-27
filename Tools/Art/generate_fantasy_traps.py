"""Execute in Blender MCP; build an isolated, editable fantasy trap collection."""
from pathlib import Path
ROOT = Path('C:/Users/yusuk/Documents/GitHub/EternalEnigma')
# Reuse the established bevelled mesh, palette UV, and FBX export pipeline only.
exec((ROOT/'Tools/Art/generate_dungeon_props.py').read_text().split('# Individual planks')[0])
from mathutils import Matrix
scene.name = 'EE Fantasy Traps'
SOURCE = ROOT/'ArtSource/FantasyTraps'
SOURCE.mkdir(parents=True, exist_ok=True)

def beam(name, a, b, width, color):
    a,b=Vector(a),Vector(b)
    obj=box(name,(0,0,0),(width,width,(b-a).length),color,0)
    rot=(b-a).to_track_quat('Z','Y').to_matrix()
    for v in obj.data.vertices: v.co=(a+b)/2+rot@v.co
    return obj

def hoop(name, center, radius, width, color, segments=24):
    x,y,z=center
    for i in range(segments):
        a=i*math.tau/segments; b=(i+1)*math.tau/segments
        beam(name,(x+radius*math.cos(a),y+radius*math.sin(a),z),(x+radius*math.cos(b),y+radius*math.sin(b),z),width,color)

def base(color=4):
    box('Octagonal mechanism bed',(0,0,.045),(1.04,1.04,.09),2,.07)
    rings('Inset pressure seal',(0,0,.09),[(0,.46),(.035,.46)],color,12)
    for x in [-.43,.43]:
        for y in [-.43,.43]: rings('Brass fastener',(x,y,.09),[(0,.035),(.032,.035)],3,6)

def rune(color, count=6):
    hoop('Engraved magic ring',(0,0,.14),.36,.025,color)
    for i in range(count):
        a=i*math.tau/count
        beam('Radial rune',(.22*math.cos(a),.22*math.sin(a),.15),(.30*math.cos(a+.17),.30*math.sin(a+.17),.15),.035,color)

for name in ['Arrow','IronArrow','RollingLog','FallingRock','Bomb','Warp','Pitfall','Updraft','IceSlick','Disarm','ShadowBind','Hallucination','Summoning','Transformation','WitheringHex','ClumsyJinx','Blight','Scorch']:
    base(1 if name in ['Arrow','RollingLog','Pitfall'] else 4)
    if name in ['Arrow','IronArrow']:
        metal=name=='IronArrow'
        box('Launcher stock',(0,0,.25),(.17,.72,.19),2 if metal else 0)
        beam('Bow left',(-.4,.1,.3),(0,.24,.3),.07,4 if metal else 1)
        beam('Bow right',(0,.24,.3),(.4,.1,.3),.07,4 if metal else 1)
        beam('Taut bowstring',(-.4,.1,.3),(0,-.22,.3),.018,3)
        beam('Taut bowstring',(0,-.22,.3),(.4,.1,.3),.018,3)
        beam('Loaded bolt',(0,-.4,.38),(0,.38,.38),.035,4)
        for x in ([-.28,.28] if metal else [0]):
            p=rings('Arrowhead',(x,.35,.38),[(0,.08),(.2,.001)],4,3)
            pivot=Vector((x,.35,.38))
            for v in p.data.vertices: v.co=pivot+Matrix.Rotation(-math.pi/2,3,'X')@(v.co-pivot)
        if metal:
            for y in [-.3,.3]: box('Reinforcement',(0,y,.42),(.32,.075,.045),3)
    elif name=='RollingLog':
        obj=rings('Bark cylinder',(0,0,0),[(0,.22),(.95,.22)],0,12)
        for v in obj.data.vertices: v.co=Vector((-.475,0,.34))+Matrix.Rotation(math.pi/2,3,'Y')@v.co
        for x in [-.35,.35]:
            for a in range(12):
                t=a*math.tau/12
                beam('Iron barrel band',(x,.23*math.cos(t),.34+.23*math.sin(t)),(x,.23*math.cos(t+.52),.34+.23*math.sin(t+.52)),.04,2)
        for y in [-.32,.32]: box('Release rail',(0,y,.18),(1,.09,.18),1)
    elif name=='FallingRock':
        for x,y,h,r in [(-.21,.08,.56,.23),(.23,.12,.4,.2),(.04,-.23,.29,.17)]:
            rings('Fractured rubble',(x,y,.13),[(0,r),(.12,r*1.2),(h,r*.5),(h+.04,.025)],4,5)
        for a in range(3): beam('Crack',(0,0,.15),(.4*math.cos(a*2),.4*math.sin(a*2),.15),.025,2)
    elif name=='Bomb':
        rings('Iron explosive vessel',(0,0,.13),[(0,.17),(.12,.29),(.35,.26),(.44,.1)],2,16)
        hoop('Brass seam',(0,0,.35),.285,.035,3)
        beam('Fuse',(0,0,.57),(.1,0,.74),.04,1)
        rings('Glowing fuse',(.1,0,.71),[(0,.055),(.1,.001)],7,5)
    elif name in ['Warp','Summoning']:
        rune(5 if name=='Warp' else 7,8)
        for i in range(4):
            a=i*math.pi/2
            rings('Runic focus',(.33*math.cos(a),.33*math.sin(a),.14),[(0,.07),(.28,.05),(.39,.001)],5 if name=='Warp' else 7,5)
        if name=='Warp': hoop('Portal horizon',(0,0,.27),.23,.045,5)
        else:
            for i in range(5):
                a=i*math.tau/5; b=(i+2)*math.tau/5
                beam('Summoning star',(.23*math.cos(a),.23*math.sin(a),.16),(.23*math.cos(b),.23*math.sin(b),.16),.028,7)
    elif name=='Pitfall':
        box('Dark pit',(0,0,.14),(.79,.79,.04),2)
        for side in [-1,1]:
            for y in [-.26,0,.26]:
                beam('Hinged planks',(side*.43,y,.16),(side*.18,y,.37),.19,1)
            box('Hinge',(side*.44,0,.2),(.075,.78,.06),3)
    elif name=='Updraft':
        rune(5)
        for y in [-.22,0,.22]: box('Wind vent',(0,y,.19),(.58,.07,.07),2)
        for i in range(3):
            a=i*math.tau/3
            for j in range(9):
                t=a+j*.32; u=t+.32; r=.2+j*.006
                beam('Spiral air vane',(r*math.cos(t),r*math.sin(t),.2+j*.045),(r*math.cos(u),r*math.sin(u),.245+j*.045),.025,5)
    elif name=='IceSlick':
        rings('Frozen sheet',(0,0,.13),[(0,.4),(.045,.41)],5,12)
        for x,y,h in [(-.23,.16,.44),(.23,.22,.29),(.21,-.2,.22)]: rings('Ice shard',(x,y,.15),[(0,.1),(h,.001)],5,4)
        for i in range(3): beam('Ice scoring',(-.24,-.17+i*.12,.20),(.14,-.04+i*.12,.20),.018,4)
    elif name in ['Disarm','ShadowBind']:
        rune(5 if name=='Disarm' else 7)
        for side in [-1,1]:
            for i in range(5): hoop('Chain link',(side*(.08+i*.065),0,.19+i*.04),.055,.02,2,10)
            hoop('Shackle',(side*.34,0,.43),.13,.055,3 if name=='Disarm' else 2,16)
        if name=='Disarm': beam('Suspended blade',(-.17,-.2,.21),(.21,.22,.45),.07,4)
    elif name=='Hallucination':
        rings('Censer',(0,0,.14),[(0,.16),(.12,.24),(.25,.16),(.3,.1)],3,12)
        for x,y,h in [(-.26,.12,.2),(.25,.1,.27),(.13,-.27,.15)]:
            beam('Fungal stem',(x,y,.15),(x,y,.15+h),.04,1)
            rings('Spotted fungal cap',(x,y,.15+h),[(0,.13),(.06,.15),(.14,.001)],7,10)
        for j in range(4): rings('Incense coil',(.025*j,0,.46+j*.07),[(0,.08-j*.013),(.06,.03)],5,7)
    elif name=='Transformation':
        rings('Pedestal',(0,0,.13),[(0,.26),(.08,.27),(.12,.15),(.34,.15),(.39,.3)],3,8)
        rings('Transmutation crystal',(0,0,.52),[(0,.12),(.18,.2),(.4,.001)],5,5)
        rune(3)
    elif name=='WitheringHex':
        rings('Cursed obelisk',(0,0,.13),[(0,.22),(.1,.23),(.55,.13),(.76,.001)],2,4)
        for z in [.3,.43,.56]: beam('Glowing curse',(-.08,-.17,z),(.08,-.17,z+.055),.025,7)
        rune(7)
    elif name=='ClumsyJinx':
        rings('Jester crown',(0,0,.14),[(0,.19),(.18,.2)],3,8)
        for i in range(3):
            a=i*math.tau/3
            beam('Jester prong',(.12*math.cos(a),.12*math.sin(a),.28),(.3*math.cos(a),.3*math.sin(a),.56),.09,7 if i%2 else 5)
            rings('Jester bell',(.3*math.cos(a),.3*math.sin(a),.53),[(0,.065),(.09,.001)],3,8)
        for x,y in [(-.3,-.26),(.24,-.3),(.32,.24)]: rings('Scattered coin',(x,y,.13),[(0,.075),(.025,.075)],3,10)
    elif name=='Blight':
        rune(6)
        for i in range(7):
            a=i*math.tau/7
            beam('Diseased root',(.38*math.cos(a),.38*math.sin(a),.15),(.1*math.cos(a+.5),.1*math.sin(a+.5),.28),.075,0)
            rings('Blight growth',(.2*math.cos(a),.2*math.sin(a),.22),[(0,.065),(.13,.09),(.22,.001)],6,6)
    elif name=='Scorch':
        for x in [-.3,-.15,0,.15,.3]: box('Furnace grate',(x,0,.22),(.065,.75,.07),2)
        for x,y,h in [(-.22,.12,.25),(0,-.12,.43),(.23,.15,.31)]:
            rings('Ember flame',(x,y,.23),[(0,.09),(.10,.12),(h,.001)],7,5)
            rings('Golden core',(x,y-.025,.23),[(0,.065),(.16,.001)],3,5)
    export('Trap'+name)

# Separate flying meshes: no floor plate or mechanism travels with the projectile.
for name in ['ArrowProjectile','IronArrowProjectile','LogProjectile']:
    if name=='LogProjectile':
        obj=rings('Flying bark log',(0,0,0),[(0,.22),(1.2,.22)],0,12)
        for v in obj.data.vertices: v.co=Vector((-.6,0,.24))+Matrix.Rotation(math.pi/2,3,'Y')@v.co
        for y,z in [(-.17,.12),(.17,.12),(0,.45)]: beam('Bark ridge',(-.55,y,z),(.55,y,z),.025,1)
        for x in [-.61,.61]:
            for radius in [.07,.14,.20]:
                for i in range(16):
                    a=i*math.tau/16; b=(i+1)*math.tau/16
                    beam('End grain',(x,radius*math.cos(a),.24+radius*math.sin(a)),(x,radius*math.cos(b),.24+radius*math.sin(b)),.009,1)
    else:
        beam('Arrow shaft',(-.55,0,.1),(.4,0,.1),.045,2 if name=='IronArrowProjectile' else 1)
        obj=rings('Forged arrow tip',(.33,0,.1),[(0,.12),(.3,.001)],4,4)
        pivot=Vector((.33,0,.1))
        for v in obj.data.vertices: v.co=pivot+Matrix.Rotation(math.pi/2,3,'Y')@(v.co-pivot)
        for side in [-1,1]: beam('Feather fletching',(-.55,side*.1,.1),(-.29,0,.1),.055,3 if name=='IronArrowProjectile' else 7)
    export(name)

for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=14
        area.spaces.active.region_3d.view_location=(3,4,.1)
        area.spaces.active.region_3d.view_rotation=Vector((1,-2,5)).to_track_quat('Z','Y')
bpy.data.libraries.write(str(SOURCE/'FantasyTraps.blend'),{scene})
(SOURCE/'geometry.json').write_text(json.dumps(models,indent=2))
print(json.dumps(models))
