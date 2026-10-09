"""Run through Blender MCP. Only project-owned inputs/outputs are written.
Extracts the source masonry islands (including their atlas UVs), reshapes them,
and adds closed backing volumes, seam connectors, fills and inside-corner caps.
"""
import bpy, bmesh, math, json, os
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DEST = os.path.join(ROOT, 'Assets', 'Art', 'DungeonThemes', 'PolyartSmartTiles')
HEIGHT = 3.3877816 * 1.25
BODY = HEIGHT - .27
HALF = .30
os.makedirs(DEST, exist_ok=True)
scene = bpy.context.scene
kit = bpy.data.collections.get('Dungeon kit')
if kit:
    for obj in list(kit.objects): bpy.data.objects.remove(obj, do_unlink=True)
else:
    kit = bpy.data.collections.new('Dungeon kit'); scene.collection.children.link(kit)

def islands(name):
    obj = bpy.data.objects[name]
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0001)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.faces.index_update()
    uv = bm.loops.layers.uv.active
    seen, result = set(), []
    for v in bm.verts:
        if v in seen: continue
        part, queue = set([v]), [v]; seen.add(v)
        while queue:
            for edge in queue.pop().link_edges:
                n = edge.other_vert(next(q for q in edge.verts if q in part))
                if n not in seen: seen.add(n); part.add(n); queue.append(n)
        faces = sorted(set(f for q in part for f in q.link_faces),key=lambda f:f.index)
        points = [obj.matrix_world @ q.co for q in part]
        lo = Vector([min(p[i] for p in points) for i in range(3)])
        hi = Vector([max(p[i] for p in points) for i in range(3)])
        tris = [[(obj.matrix_world @ loop.vert.co, loop[uv].uv.copy()) for loop in f.loops] for f in faces]
        result.append((lo, hi, tris))
    bm.free()
    return result

sources = [islands(n) for n in ['LOD1', 'LOD1.001', 'LOD1.002', 'LOD1.003']]
stones = []
for source_index,parts in enumerate(sources):
    choices = [p for p in parts if .2 < (p[1]-p[0]).z < .8]
    ashlar = [p for p in choices if .48 < (p[1]-p[0]).z < .65 and min((p[1]-p[0]).x,(p[1]-p[0]).y)<.65 and max((p[1]-p[0]).x,(p[1]-p[0]).y)>1]
    if source_index<2 and ashlar: choices=ashlar
    stones.append(max(choices, key=lambda p: (p[1]-p[0]).x * (p[1]-p[0]).y))
moss = min(sources[0],key=lambda p:((p[0]+p[1])*.5-Vector((0,0,1.785))).length)
material = bpy.data.objects['LOD0'].data.materials[0]
# A neutral masonry texel from the source atlas, used only for the hidden mortar cores.
core_uv = tuple(stones[0][2][0][0][1])

def mesh_builder(name):
    return {'name': name, 'vertices': [], 'uvs': [], 'triangles': []}

def triangle(out, coords, uvs):
    index = len(out['vertices'])
    out['vertices'].extend([list(p) for p in coords]); out['uvs'].extend([list(p) for p in uvs])
    out['triangles'].extend([index, index+1, index+2])

def stone(out, src, center, size, rotation=0):
    lo, hi, tris = src; extent = hi-lo; mid = (hi+lo)*.5
    matrix = Matrix.Rotation(rotation, 3, 'Z')
    for tri in tris:
        points = [Vector(center) + matrix @ Vector([(p[i]-mid[i])*size[i]/max(.00001,extent[i]) for i in range(3)]) for p,uv in tri]
        triangle(out, points, [uv for p,uv in tri])

def prism(out, polygon, z0, z1):
    # Polygon must be counter-clockwise. Closed, manifold backing and exact shared border planes.
    vertices = [(x,y,z) for z in [z0,z1] for x,y in polygon]; n=len(polygon)
    for i in range(1,n-1):
        triangle(out,[vertices[0],vertices[i+1],vertices[i]],[core_uv]*3)
        triangle(out,[vertices[n],vertices[n+i],vertices[n+i+1]],[core_uv]*3)
    for i in range(n):
        j=(i+1)%n
        triangle(out,[vertices[i],vertices[j],vertices[n+j]],[core_uv]*3)
        triangle(out,[vertices[i],vertices[n+j],vertices[n+i]],[core_uv]*3)

def box(out, cx,cy, sx,sy, z0,z1):
    prism(out,[(cx-sx/2,cy-sy/2),(cx+sx/2,cy-sy/2),(cx+sx/2,cy+sy/2),(cx-sx/2,cy+sy/2)],z0,z1)

def pillar(out, variant):
    box(out,0,0,.49,.49,0,HEIGHT-.06)
    # Source capitals and ashlar, all reshaped inside a .72-wide central footprint.
    courses = max(1,round((HEIGHT-.32)/.5))
    bands = [(0,.14,.72)] + [(.13+j*(HEIGHT-.29)/courses,.14+(j+1)*(HEIGHT-.29)/courses,.58) for j in range(courses)] + [(HEIGHT-.17,HEIGHT,.70)]
    for j,(bottom,top,width) in enumerate(bands):
        stone(out, stones[(variant+j)%4], (0,0,(bottom+top)/2),(width,width,top-bottom))
    stone(out,moss,(0,0,BODY*.52),(.595,.595,.08))

def arm(out, turn, variant):
    angle=-turn*math.pi/2
    def rot(p): return (math.cos(angle)*p[0]-math.sin(angle)*p[1], math.sin(angle)*p[0]+math.cos(angle)*p[1])
    poly=[rot(p) for p in [(-.245,.22),(.245,.22),(.245,1),(-.245,1)]]
    prism(out,poly,0,BODY-.06)
    seam=[rot(p) for p in [(-HALF,.99),(HALF,.99),(HALF,1),(-HALF,1)]]
    prism(out,seam,0,BODY)
    # Keep stone courses proportionate at the measured dungeon-hero wall height.
    rows=max(1,round(BODY/.52))
    for row in range(rows):
        z0=row*BODY/rows; z1=(row+1)*BODY/rows
        split = [(.23,1.0)] if (row+variant)%2==0 else [(.23,.60),(.60,1.0)]
        for i,(a,b) in enumerate(split):
            x,y=rot((0,(a+b)/2))
            stone(out,stones[(row+variant+i)%4],(x,y,(z0+z1)/2),(.62,b-a+.008,z1-z0+.008),angle)
    # Clip all arms at the exact tile edge after generation. The core is closed and has
    # a constant .60 x 1.24 cross section, shared by both variants and every connector.

def finish(out, index):
    verts=out['vertices']
    for p in verts:
        p[0]=max(-1,min(1,p[0]));p[1]=max(-1,min(1,p[1]));p[2]=max(0,min(HEIGHT,p[2]))
        if out['name'] not in ('QuadrantFill','SolidFill','ConcaveCorner') and abs(p[0])>=.99999:
            p[1]=max(-HALF,min(HALF,p[1]));p[2]=min(BODY,p[2])
        if out['name'] not in ('QuadrantFill','SolidFill','ConcaveCorner') and abs(p[1])>=.99999:
            p[0]=max(-HALF,min(HALF,p[0]));p[2]=min(BODY,p[2])
    mesh=bpy.data.meshes.new(out['name']);mesh.from_pydata(verts,[],[out['triangles'][i:i+3] for i in range(0,len(out['triangles']),3)])
    mesh.uv_layers.new(name='Source atlas')
    for loop in mesh.loops: mesh.uv_layers.active.data[loop.index].uv=out['uvs'][loop.vertex_index]
    mesh.materials.append(material);mesh.update()
    obj=bpy.data.objects.new(out['name'],mesh);kit.objects.link(obj)
    obj.location=(index%5*3,index//5*3,0)
    obj['authored_cell_size']=2;obj['authored_height']=HEIGHT
    obj['source']='Project-owned copies of Polyart Wall01–04; source UVs retained'
    # Export exact XY/-Z Unity coordinates; reflection reverses triangle winding.
    data={'name':out['name'],'vertices':[{'x':p[0],'y':p[1],'z':-p[2]} for p in verts],
          'uv':[{'x':p[0],'y':p[1]} for p in out['uvs']],
          'triangles':[v for i in range(0,len(out['triangles']),3) for v in (out['triangles'][i],out['triangles'][i+2],out['triangles'][i+1])]}
    return data

exports=[]
for index,(name,turns,variant) in enumerate([('Single',[],0),('End',[0],0),('StraightA',[0,2],0),('StraightB',[0,2],1),('Corner',[0,1],1),('Junction',[0,1,2],0),('Cross',[0,1,2,3],1)]):
    out=mesh_builder(name);pillar(out,variant)
    for turn in turns:arm(out,turn,variant)
    exports.append(finish(out,index))
out=mesh_builder('QuadrantFill')
box(out,.65,.65,.70,.70,0,BODY)
exports.append(finish(out,7))
out=mesh_builder('ConcaveCorner')
prism(out,[(.285,.285),(.52,.285),(.285,.52)],0,BODY)
exports.append(finish(out,8))
out=mesh_builder('SolidFill')
box(out,0,0,2,2,0,BODY-.04)
# Quiet cap courses retain the source's stone shading without a forest of interior posts.
for x in [-.5,.5]:
    for y in [-.5,.5]:stone(out,stones[0],(x,y,BODY-.04),(1,1,.08))
exports.append(finish(out,9))
with open(os.path.join(DEST,'Meshes.json'),'w') as f:json.dump({'authoredHeight':HEIGHT,'authoredCellSize':2,'meshes':exports},f,separators=(',',':'))
for obj in scene.objects:
    if obj.name not in kit.objects:obj.hide_set(True);obj.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource','DungeonSmartTiles','DungeonSmartTiles.blend'))
print([(m['name'],len(m['triangles'])//3) for m in exports])
