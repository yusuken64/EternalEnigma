"""Temporarily stage HEAD art for matched captures; restore exact working bytes afterward.

Only the tracked art files changed by this pass are staged, never scripts, scenes or
new files. Refuses to overwrite an existing backup. Run restore even if capture fails.
"""
from pathlib import Path
import json, shutil, subprocess, sys
root=Path(__file__).resolve().parents[2]
backup=root/'Temp/PaintedEnvironmentBaseline'
manifest=backup/'manifest.json'
if sys.argv[1]=='stage':
    if manifest.exists(): raise RuntimeError('Restore the pending baseline before staging another one.')
    files=subprocess.check_output(['git','-c','core.safecrlf=false','diff','--name-only'],cwd=root).decode().splitlines()
    files=[p for p in files if p.startswith(('Assets/Art/','Assets/Resources/','Assets/Overworld/'))
           and p.endswith(('.mat','.asset','.png','.png.meta')) and '/TownPreview/' not in p]
    for p in files:
        dest=backup/p;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/p,dest)
    manifest.write_text(json.dumps(files,indent=2))
    try:
        for p in files: (root/p).write_bytes(subprocess.check_output(['git','show','HEAD:'+p],cwd=root))
    except BaseException:
        for p in files: shutil.copyfile(backup/p,root/p)
        manifest.unlink()
        raise
    print('Staged original art for capture:',len(files),'files. Restore afterward.')
elif sys.argv[1]=='restore':
    files=json.loads(manifest.read_text())
    for p in files: shutil.copyfile(backup/p,root/p)
    manifest.unlink()
    print('Restored exact working art:',len(files),'files.')
else: raise ValueError('Expected stage or restore')
