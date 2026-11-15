"""Run in Blender through MCP. Reuses the game's bevelled diorama modelling helpers.
Creates only the EE Lock Authoring scene; exports XY/-Z meshes for the Unity importer.
"""
import ast
import bpy
import json
import gzip
import math
import random
import traceback
from pathlib import Path
from mathutils import Vector


def build(project):
    dest = project / 'ArtSource' / 'Locks'
    dest.mkdir(parents=True, exist_ok=True)
    # Load the existing modeller without running its scene generation or exports.
    source = ast.parse((project / 'ArtSource/Diorama/build.py').read_text())
    model_class = next(n for n in source.body if isinstance(n, ast.ClassDef) and n.name == 'Model')
    roles = ['Plaster', 'Timber', 'Roof', 'Door', 'Glass', 'Leaf', 'Flower', 'Stone', 'Metal', 'Glow', 'Snow', 'Bark']
    colors = [(.91,.84,.68,1),(.30,.20,.14,1),(.70,.27,.17,1),(.34,.46,.48,1),(.36,.70,.79,1),
              (.39,.62,.18,1),(.92,.44,.32,1),(.57,.56,.49,1),(.17,.20,.22,1),(.99,.77,.33,1),(.90,.95,1,1),(.42,.30,.18,1)]
    scope = dict(math=math, random=random, Vector=Vector, roles=roles)
    exec(compile(ast.Module(body=[model_class], type_ignores=[]), 'diorama-model-helper', 'exec'), scope)
    Model = scope['Model']
    scene = bpy.data.scenes.get('EE Lock Authoring') or bpy.data.scenes.new('EE Lock Authoring')
    for obj in list(scene.objects):
        mesh = obj.data if obj.type == 'MESH' else None
        bpy.data.objects.remove(obj, do_unlink=True)
        if mesh is not None and mesh.users == 0: bpy.data.meshes.remove(mesh)
    materials = []
    for name, color in zip(roles, colors):
        material = bpy.data.materials.get('EE Lock ' + name) or bpy.data.materials.new('EE Lock ' + name)
        material.use_nodes = True
        shader = next(n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value = color
        shader.inputs['Roughness'].default_value = .82
        material.diffuse_color = color
        materials.append(material)
    models = {}

    def save(name, obstacle, frame=None, side_frame=None):
        models['Lock' + name] = [('Frame', frame), ('Closed barrier', obstacle)] if frame else [('Obstacle', obstacle)]
        if side_frame: models['Lock' + name].append(('Side frame', side_frame))

    def ring(m, c, radius, tube, material, n=16, plane='front'):
        # Segmented forged rings, wheels and chains keep a readable low-poly outline.
        for i in range(n):
            points = []
            for a in [i*math.tau/n, (i+1)*math.tau/n]:
                p = (math.cos(a)*radius, 0, math.sin(a)*radius) if plane == 'front' else (math.cos(a)*radius, math.sin(a)*radius, 0)
                points.append(tuple(c[j]+p[j] for j in range(3)))
            m.rod(*points, tube, material, n=6)

    def board(m, a, b, thickness=.06, material='Timber'):
        m.rod(a, b, thickness, material, n=4)

    def post(m, x, y, h, material='Timber'):
        m.box((x,y,h/2),(.16,.18,h),material)
        m.box((x,y,h+.035),(.22,.24,.07),material)

    def candle(m, x, y, h=.32):
        m.rod((x,y,0),(x,y,h),.05,'Plaster')
        m.crown((x,y,h+.055),(.035,.035,.075),'Glow',n=6)

    def wheel(m, c, radius=.4, material='Timber', paddles=False):
        ring(m,c,radius,.045,material)
        ring(m,c,radius*.83,.025,'Metal')
        for j in range(8):
            a=j*math.tau/8
            end=(c[0]+math.cos(a)*radius,c[1],c[2]+math.sin(a)*radius)
            board(m,c,end,.035,material)
            if paddles: m.box(end,(.15,.35,.09),'Timber')
        m.rod((c[0],c[1]-.16,c[2]),(c[0],c[1]+.16,c[2]),.08,'Metal')

    def rock(m, x,y,z, sx,sy,sz, seed=0, material='Stone'):
        # Broad, flat contact faces prevent rock walls and monoliths looking like floating crystals.
        rng=random.Random(seed);n=7;vertices=[]
        radii=[1+rng.uniform(-.08,.08) for _ in range(n)]
        for level,radius in [(-1,1.12),(-.62,1),(.55,.95),(1,.60)]:
            for i in range(n):
                angle=(i+.12)*math.tau/n
                vertices.append((x+math.cos(angle)*sx*radius*radii[i],y+math.sin(angle)*sy*radius*radii[i],z+sz*level))
        faces=[tuple(reversed(range(n))),tuple(range(3*n,4*n))]
        for j in range(3):
            for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
        m.poly(vertices,faces,material)

    def water(m, width=1.6, depth=1.05, frozen=False):
        m.box((0,0,.025),(width,depth,.05),'Glass')
        for i in range(4):
            m.box((-.5+i*.29,(-1 if i%2 else 1)*.26,.058),(.23,.02,.012),'Snow' if frozen else 'Glass')

    def steps(m, count=4):
        for i in range(count):
            m.box((0,-.45+i*.15,.08+i*.07),(.60,.17,.15),'Stone')

    def rune(m,x,y,z, scale=1):
        a=.10*scale
        for p,q in [((-a,0),(0,a)),((0,a),(a,0)),((a,0),(0,-a)),((0,-a),(-a,0))]:
            board(m,(x+p[0],y,z+p[1]),(x+q[0],y,z+q[1]),.015*scale,'Glow')

    for v in range(3):
        m=Model()
        # Unlike a single tiny terrain tile, these have a tall, solid climbing face.
        for i in range(3+v):
            x=-.58+i*1.16/(2+v);h=[1.1,1.42,1.24,1.0,1.33][(i+v)%5]
            rock(m,x,.08,h/2,.32,.38,h/2,10+i+v)
            m.box((x,-.28,h*.46),(.46,.18,.08),'Stone')
        if v==2:
            for i in range(5): m.box((-.56+i*.28,-.34,.14+i*.15),(.29,.34,.15),'Stone')
        save('Cliff'+str(v+1),m)

        m=Model();water(m,frozen=True)
        for i in range(3+v):
            x=-.58+i*1.16/(2+v);y=.20*math.sin(i*2.1)
            m.rod((x,y,.07),(x+.06,y,.22+(i%3)*.13),.24,'Snow',n=5,r2=.16)
            board(m,(x-.18,y-.18,.075),(x+.16,y+.13,.08),.013,'Metal')
        save('IceFloes'+str(v+1),m)

        m=Model();water(m);steps(m,3+v)
        if v==0:
            for x in [-.72,.72]:m.box((x,0,.13),(.19,1.15,.26),'Stone')
            m.box((0,.53,.13),(1.6,.18,.26),'Stone')
        elif v==1:
            for i in range(10):
                a=i*math.tau/10;m.box((math.cos(a)*.66,math.sin(a)*.46,.17),(.25,.24,.34),'Stone')
        else:
            for x in [-.63,.63]:post(m,x,.32,.88,'Stone')
            m.box((0,.32,.92),(1.42,.28,.14),'Stone')
        save('DrownedStair'+str(v+1),m)

        m=Model()
        for x in [-.63,.63]:
            rock(m,x,.04,.66,.20,.23,.66,19+v)
            rune(m,x,-.2,.85,.8)
        for i in range(5+v):
            x=-.48+i*.96/(4+v)
            m.box((x,.0,.65),(.08,.055,1.15-.08*(i%2)),'Glass')
            rune(m,x,-.05,.37+.14*(i%3),.45)
        if v==1:m.box((0,.04,1.35),(1.4,.25,.14),'Stone')
        if v==2: ring(m,(0,-.09,.65),.40,.03,'Glow')
        save('Veil'+str(v+1),m)

        m=Model();m.box((0,0,.015),(1.62,1.1,.03),'Metal')
        for x in [-.62,.62]:
            rock(m,x,0,.24,.30,.55,.25,30+v)
            for i in range(v+1):m.box((x,-.23+i*.21,.48),(.40,.13,.06),'Stone')
        post(m,.58,.25,1.1)
        ring(m,(.58,.13,.91),.12,.025,'Metal')
        if v==1:
            board(m,(-.64,-.35,.63),(-.64,.34,.63),.035)
            post(m,-.64,-.35,.7)
        if v==2:board(m,(.56,.2,1.05),(.30,-.18,.71),.02,'Timber')
        save('AnchorGap'+str(v+1),m)

        m=Model();water(m)
        for side in [-1,1]:
            for j in range(2+v):
                x=side*(.57-j*.08);m.box((x,0,.15),(.13,1.1,.09),'Timber')
            for y in [-.42,.42]:post(m,side*.70,y,.42)
        if v==1:board(m,(-.35,-.42,.18),(-.11,-.10,.06),.045)
        if v==2:board(m,(.35,.1,.18),(.08,.2,.08),.045)
        save('BrokenDock'+str(v+1),m)

    for v in range(4):
        m=Model();m.box((0,0,.09),(1.1, .8,.18),'Stone')
        if v==0:rock(m,0,0,.85,.33,.23,.76,40)
        elif v==1:
            for x in [-.36,.36]:m.box((x,.10,.69),(.23,.30,1.22),'Stone')
            m.box((0,.10,1.28),(.99,.37,.20),'Stone')
        elif v==2:
            for j in range(3):
                x=(j-1)*.34;rock(m,x,.08,.6+(j%2)*.24,.20,.22,.56+(j%2)*.24,41+j)
        else:
            m.rod((0,0,.16),(0,0,1.42),.30,'Stone',n=4,r2=.20)
            m.crown((0,0,1.47),(.18,.18,.22),'Glass',9,n=6)
        for j in range(3):rune(m,0,-.245,.4+j*.27,.8)
        ring(m,(0,0,.19),.43,.018,'Glow',plane='ground')
        save('Waystone'+str(v+1),m)

    for v in range(2):
        m=Model()
        m.box((0,0,.10),(1.45,.78,.20),'Stone')
        for x in [-.55,.55]:post(m,x,0,.9)
        m.rod((-.65,0,.75),(.65,0,.75),.10,'Metal')
        wheel(m,(0,-.18,.75),.43, 'Metal')
        if v==0:
            m.box((.46,-.35,.40),(.21,.4,.6),'Timber')
            board(m,(-.16,-.20,.65),(-.16,-.63,.16),.045,'Metal')
        else:
            for i in range(5):m.box((-.5+i*.25,.38,.42+i*.06),(.23,.45,.10),'Timber')
        save('Winch'+str(v+1),m)

        m=Model();water(m)
        for x in [-.63,.63]:post(m,x,.05,1.03)
        m.rod((-.73,0,.73),(.73,0,.73),.09,'Metal')
        wheel(m,(0,-.1,.70),.57,paddles=True)
        if v==1:
            m.box((.62,.22,.71),(.36,.37,1.4),'Stone')
            m.box((.62,.22,1.45),(.45,.46,.10),'Roof')
        save('Waterwheel'+str(v+1),m)

    for v in range(3):
        m=Model();m.box((0,0,.12),(1.35,.94,.24),'Stone')
        if v==0:
            m.box((0,.12,.40),(.7,.45,.56),'Stone')
            m.box((0,.12,.73),(.96,.65,.13),'Stone')
        elif v==1:
            for x in [-.48,.48]:post(m,x,.22,1.0,'Stone')
            m.box((0,.22,1.1),(1.26,.54,.16),'Roof')
            m.crown((0,.19,.60),(.17,.15,.29),'Glow',9,n=8)
        else:
            for i in range(7):
                a=i*math.tau/7;candle(m,.5*math.cos(a),.35*math.sin(a),.28+(i%3)*.12)
        if v!=2:
            for x in [-.47,.47]:candle(m,x,-.23,.48)
        save('Altar'+str(v+1),m)

    for v in range(2):
        m=Model();water(m)
        # Hollow clinker hull with seats, gunwales, oar and a mooring line.
        m.box((0,0,.14),(.92,.48,.10),'Timber')
        for y in [-.27,.27]:board(m,(-.48,y,.32),(.48,y,.32),.065)
        for x in [-.48,.48]:
            tip=(x*1.5,0,.36)
            for y in [-.27,.27]:board(m,(x,y,.32),tip,.065)
        for x in [-.28,.28]:m.box((x,0,.29),(.14,.58,.07),'Timber')
        board(m,(-.45,-.46,.25),(.47,.36,.40),.03)
        m.box((-.48,-.48,.25),(.15,.23,.045),'Timber')
        post(m,.70,.43,.6)
        board(m,(.70,.43,.5),(.5,.2,.34),.016)
        if v==1:
            post(m,0,.02,1.25)
            m.poly([(0,.03,.5),(0,.03,1.2),(.57,.03,.68)],[(0,1,2),(2,1,0)],'Plaster')
        save('Ferry'+str(v+1),m)

    for v in range(3):
        m=Model()
        for i in range(2+v):
            x=-.48+i*.96/(1+v);h=.50+(i%2)*.18
            m.box((x,.12,h/2),(.28,.20,h),'Stone')
            m.crown((x,.12,h),(.14,.10,.13),'Stone',i,n=8)
            board(m,(x,-.005,.22),(x,-.005,h+.02),.02,'Metal')
            board(m,(x-.07,-.005,h-.08),(x+.07,-.005,h-.08),.018,'Metal')
            m.box((x,-.22,.035),(.29,.42,.07),'Stone')
        if v==1:
            for x in [-.64,.64]:candle(m,x,-.30,.32)
        if v==2:
            m.box((0,-.29,.20),(.58,.40,.30),'Timber')
            m.box((0,-.29,.38),(.65,.46,.09),'Plaster')
        save('Graves'+str(v+1),m)

    for v in range(2):
        m=Model()
        for x in [-.42,.42]:post(m,x,0,1.38,'Stone' if v else 'Timber')
        m.box((0,0,1.4),(1.04,.42,.13),'Timber')
        m.poly([(-.62,-.34,1.44),(0,-.34,1.77),(.62,-.34,1.44),(-.62,.34,1.44),(0,.34,1.77),(.62,.34,1.44)],[(1,4,3,0),(2,5,4,1),(2,1,0),(3,4,5),(0,3,5,2)],'Roof')
        m.rod((0,0,.79),(0,0,1.21),.27,'Glow',n=12,r2=.12)
        m.rod((0,0,1.18),(0,0,1.36),.05,'Metal')
        ring(m,(0,0,.78),.27,.035,'Glow',plane='ground')
        m.crown((0,0,.74),(.06,.06,.08),'Metal',n=6)
        board(m,(.11,.02,1.20),(.18,-.19,.30),.014)
        if v:steps(m)
        save('Belfry'+str(v+1),m)

        m=Model()
        for i in range(3):
            a=i*math.tau/3;board(m,(math.cos(a)*.35,math.sin(a)*.35,0),(0,0,.92),.035)
        m.box((0,0,.96),(.48,.35,.09),'Timber')
        m.box((0,-.01,1.02),(.39,.27,.025),'Plaster')
        for x in [-.65,.65]:
            post(m,x,.12,.41)
            m.box((x,-.11,.34),(.07,.02,.12),'Flower')
        if v:
            board(m,(-.65,.12,.4),(.65,.12,.4),.009,'Metal')
        else:
            m.rod((-.24,0,1.10),(.24,0,1.10),.065,'Metal')
        save('Survey'+str(v+1),m)

    for v in range(3):
        m=Model();height=.25 if v==1 else .48
        m.box((0,0,height),(.94,.63,.13),'Timber')
        for x in [-.42,.42]:
            if v==1:
                for y in [-.27,.27]:post(m,x,y,.20)
            else:
                for y in [-.37,.37]:wheel(m,(x,y,.26),.23)
        m.box((-.27,0,height+.11),(.25,.48,.13),'Plaster')
        m.crown((-.24,-.02,height+.25),(.12,.12,.13),'Plaster',9,n=8)
        m.box((.17,0,height+.17),(.55,.51,.22),'Roof')
        for y in [-.27,.27]:board(m,(.4,y,height),(.81,y,height+.07),.025)
        if v==2:
            for x in [-.46,.46]:post(m,x,.29,1.1)
            m.box((0,.24,1.15),(1.12,.50,.08),'Plaster')
        save('SickCart'+str(v+1),m)

    for v in range(2):
        m=Model();frame=Model();side_frame=Model()
        for x in [-.58,.58]:post(frame,x,0,1.25)
        for x in [-.58,.58]:post(side_frame,x,0,.38)
        frame.box((0,0,1.30),(1.45,.40,.22),'Timber')
        for i in range(5):
            x=-.48+i*.24;rock(m,x,-.04,.34+.15*(i%2),.28,.31,.35+.14*(i%2),60+i+v)
        if v:
            for i in range(3):board(m,(-.57,-.32,.2+i*.26),(.57,-.32,.5+i*.26),.045)
        save('Mine'+str(v+1),m,frame,side_frame)

    # Six visibly different closed gates. Frames remain; only the barrier opens.
    for v in range(6):
        frame=Model();m=Model();side_frame=Model()
        stone=v in [1,4];h=1.18 if v!=5 else .83
        for x in [-.64,.64]:post(frame,x,0,h,'Stone' if stone else 'Timber')
        # Low posts keep the closure visible when the overworld camera sees the gate edge-on.
        for x in [-.64,.64]:post(side_frame,x,0,.36,'Stone' if stone else 'Timber')
        if v in [0,1,4]:frame.box((0,0,h+.1),(1.55,.32,.18),'Stone' if stone else 'Timber')
        if v==0:
            # Braced timber double doors.
            for i in range(8):m.box((-.5+i/7,.0,.53),(.13,.13,1.04),'Timber')
            for side in [-1,1]:board(m,(side*.05,-.10,.15),(side*.5,-.10,.91),.045,'Metal')
        elif v==1:
            # Spear-topped iron wicket.
            for i in range(9):
                x=-.53+i*.1325;m.rod((x,0,.02),(x,0,1.04),.026,'Metal',n=6)
                m.rod((x,0,1.02),(x,0,1.14),.055,'Metal',n=4,r2=0)
            for z in [.18,.70]:m.box((0,0,z),(1.16,.06,.055),'Metal')
        elif v==2:
            # Toll boom with a counterweight and painted bands.
            m.box((0,0,.64),(1.36,.16,.17),'Timber')
            for x in [-.45,0,.45]:m.box((x,-.09,.64),(.18,.02,.17),'Plaster')
            frame.box((.73,.0,.34),(.28,.34,.35),'Stone')
        elif v==3:
            # Uneven log palisade with cross braces.
            for i in range(7):
                x=-.51+i*.17;top=1.05+(i%2)*.17
                m.rod((x,0,.02),(x,0,top),.09,'Bark',n=6,r2=.07)
                m.rod((x,0,top),(x,0,top+.15),.07,'Bark',n=6,r2=0)
            for z in [.23,.72]:m.box((0,-.11,z),(1.2,.07,.09),'Timber')
        elif v==4:
            # Sealed stone double door with a metal escutcheon.
            for x in [-.28,.28]:
                m.box((x,0,.55),(.54,.20,1.1),'Stone')
                m.box((x,-.12,.56),(.31,.035,.60),'Door')
            rune(m,0,-.15,.79,1.3)
        else:
            # Hanging chain, padlock and warning seal.
            for i in range(15):
                x=-.57+i*.0815;z=.56+.55*x*x
                ring(m,(x,0,z),.065,.014,'Metal',n=8,plane='front' if i%2 else 'ground')
            m.box((0,-.06,.39),(.22,.16,.25),'Glow')
            ring(m,(0,-.05,.54),.10,.023,'Metal',n=12)
        if v!=5:
            m.box((0,-.11,.57),(.14,.08,.20),'Glow')
            m.box((0,-.158,.58),(.027,.016,.07),'Metal')
        save('Gate'+str(v+1),m,frame,side_frame)

    report={'roles':roles,'models':[]}
    for index,(name,pieces) in enumerate(models.items()):
        root=bpy.data.objects.new(name,None);scene.collection.objects.link(root)
        root.location=((index%8)*2.8,(index//8)*2.6,0)
        record={'id':name,'pieces':[]}
        for part,m in pieces:
            mesh=bpy.data.meshes.new(name+' '+part);mesh.from_pydata(m.v,[],m.f)
            for material in materials:mesh.materials.append(material)
            for polygon,mi in zip(mesh.polygons,m.m):polygon.material_index=mi
            mesh.update();mesh.calc_loop_triangles()
            obj=bpy.data.objects.new(name+' '+part,mesh);scene.collection.objects.link(obj);obj.parent=root
            if part == 'Side frame':
                obj.hide_render=True
                if obj.name in bpy.context.view_layer.objects: obj.hide_set(True)
            vertices=[];normals=[];shades=[];uv=[];sub=[[] for _ in roles]
            for tri in mesh.loop_triangles:
                poly=mesh.polygons[tri.polygon_index];ids=[]
                for vi in tri.vertices:
                    p=mesh.vertices[vi].co;n=poly.normal;ids.append(len(vertices))
                    vertices.append(dict(x=p.x,y=p.y,z=-p.z));normals.append(dict(x=n.x,y=n.y,z=-n.z))
                    shade=(.87+.13*min(1,max(0,p.z)/1.5))*(.94+.06*max(0,n.z))
                    shades.append(dict(r=shade,g=shade,b=shade,a=1))
                    # The existing painted masonry atlas's stone cell; other roles retain diorama colours.
                    u=(p.y+.9)/1.8 if abs(n.x)>abs(n.y) else (p.x+.9)/1.8
                    vv=(p.y+.9)/1.8 if abs(n.z)>.6 else p.z/1.9
                    uv.append(dict(x=(3+.06+u*.88)/4,y=(.06+vv*.88)/4))
                sub[poly.material_index].extend([ids[0],ids[2],ids[1]])
            record['pieces'].append(dict(id=part,vertices=vertices,normals=normals,colors=shades,uv=uv,
                parts=[dict(role=roles[i],triangles=t) for i,t in enumerate(sub) if t]))
        report['models'].append(record)
    with gzip.GzipFile(filename=str(dest/'Meshes.json.gz'), mode='wb', mtime=0) as output:
        output.write(json.dumps(report,separators=(',',':')).encode('utf-8'))
    previous_versions=bpy.context.preferences.filepaths.save_version
    try:
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(dest/'Locks.blend'),copy=True)
    finally: bpy.context.preferences.filepaths.save_version=previous_versions
    print(json.dumps({'models':len(models),'triangles':sum(len(part['triangles'])//3 for model in report['models'] for piece in model['pieces'] for part in piece['parts']), 'source':str(dest)}))


try:
    build(Path(EE_PROJECT_ROOT) if 'EE_PROJECT_ROOT' in globals() else Path(__file__).resolve().parents[2])
except Exception:
    traceback.print_exc()
    raise
