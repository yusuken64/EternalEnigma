"""Blender -b <source> --python verify_blend.py -- <report.json>"""
import bpy, hashlib, json, struct, sys
from pathlib import Path
report={}
for obj in bpy.data.objects:
    if obj.type!='MESH': continue
    mesh=obj.data
    raw=b''.join(struct.pack('<3f',*v.co) for v in mesh.vertices)
    raw+=b''.join(struct.pack('<i',len(p.vertices))+b''.join(struct.pack('<i',i) for i in p.vertices) for p in mesh.polygons)
    report[obj.name]={'vertices':len(mesh.vertices),'polygons':len(mesh.polygons),'geometry':hashlib.sha256(raw).hexdigest(),'matrix':[list(r) for r in obj.matrix_world]}
Path(sys.argv[sys.argv.index('--')+1]).write_text(json.dumps(report,sort_keys=True))
