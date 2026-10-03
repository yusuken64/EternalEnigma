import bpy, json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
result = []
for obj in bpy.data.objects:
    if obj.type != 'MESH': continue
    result.append({'name':obj.name, 'vertices':len(obj.data.vertices), 'polygons':len(obj.data.polygons),
        'uv': sorted(set(tuple(round(v,4) for v in u.uv) for u in obj.data.uv_layers.active.data))[:20] if obj.data.uv_layers.active else [],
        'matrix':[list(r) for r in obj.matrix_world], 'bounds':[list(v) for v in obj.bound_box]})
(root/'Temp/painted-blend-inspection.json').write_text(json.dumps(result,indent=2))
print('Materials:',[(m.name,[(n.name,n.image.filepath if n.image else '') for n in m.node_tree.nodes if n.type=='TEX_IMAGE']) for m in bpy.data.materials if m.use_nodes])
