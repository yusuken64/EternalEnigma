"""Verify runtime authored meshes differ from HEAD only in texture coordinates."""
import json, re, subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[2]
snapshots=json.loads((root/'ArtSource/PaintedEnvironment/OriginalUVs.json').read_text())['meshes']
def parse(text):
    channels=[tuple(map(int,m)) for m in re.findall(r'- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)',text)]
    count=int(re.search(r'm_VertexCount: (\d+)',text)[1])
    data=bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)',text)[1])
    assert all(s==0 and fmt==0 for s,o,fmt,d in channels if d)
    stride=max(o+4*d for s,o,f,d in channels if d)
    uv=channels[4]
    assert uv[3]==2
    non_uv=b''.join(data[i*stride:i*stride+uv[1]]+data[i*stride+uv[1]+8:(i+1)*stride] for i in range(count))
    indices=re.search(r'm_IndexBuffer: ([0-9a-f]+)',text)[1]
    return count, non_uv, indices
verified=[]
for mesh in snapshots:
    path=mesh['path']
    before=subprocess.check_output(['git','show','HEAD:'+path],cwd=root).decode()
    after=(root/path).read_text()
    assert parse(before)==parse(after), 'Non-UV mesh change: '+path
    verified.append(path)
result={'verifiedMeshes':len(verified),'positionsNormalsTangentsIndicesUnchanged':True,'meshes':verified}
(root/'Docs/Art/Verification/PaintedEnvironmentMeshAudit.json').write_text(json.dumps(result,indent=2)+'\n')
print('Verified',len(verified),'meshes: vertex counts, positions, normals, tangents and index buffers unchanged.')
