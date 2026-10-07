"""Build reviewable cleanup groups from the fresh GUID/path reference audit; never deletes."""
from pathlib import Path
import json, re

ROOT = Path(__file__).resolve().parents[1]
report = json.loads((ROOT / 'Docs/Art/Previews/Diorama/ReferenceAudit.json').read_text())
by_path = {entry['path']: entry for entry in report}
groups = {
    'legacy-tilemap': ['Assets/Tiles', 'Assets/Prefabs/Dungeon/Dungeon.prefab', 'Assets/Art/Torch.mat',
                       'Assets/Scripts/DungeonGenerator.cs', 'Assets/Art/DungeonTexture'],
    'twc-samples': ['Assets/TileWorldCreator/Demo', 'Assets/TileWorldCreator/Tiles',
                    'Assets/Prefabs/Dungeon/_TileWorldCreator_Dungeon.prefab'],
    'rpg-hero-placeholder': ['Assets/Art/RPGHero', 'Assets/Prefabs/Dungeon/Enemy.prefab'],
    'unused-enemy-prefabs': ['Assets/Prefabs/Dungeon/Enemies/Enemy_MushroomSmile.prefab',
                             'Assets/Prefabs/Dungeon/Enemies/Enemy_LargeSlime.prefab'],
    'adorable-per-texture': ['Assets/Art/3D Props - Adorable Items/Adorable 3D Items'],
    'environment-duplicates': [path for path in by_path if path.startswith('Assets/Art/EnvironmentKit/Prefabs/')]
                              + ['Assets/Art/EnvironmentKit/Models'],
    'legacy-biome-swatches': [path for path in by_path if path.startswith('Assets/Overworld/Biome')],
    'recovery-scenes': ['Assets/_Recovery'],
    'unused-wfc': ['Assets/unity-wave-function-collapse'],
    'skill-contact-sheets': [path for path in by_path if path.startswith('Assets/RPG_skills_and_abilities/')],
}
models = ROOT / 'Assets/Art/KennyNL/Castle Kit/Models'
duplicates = []
for obj in sorted(models.glob('*.obj')):
    fbx = obj.with_suffix('.fbx')
    if not fbx.exists():
        continue
    obj_path, fbx_path = obj.relative_to(ROOT).as_posix(), fbx.relative_to(ROOT).as_posix()
    obj_refs, fbx_refs = by_path[obj_path], by_path[fbx_path]
    if not obj_refs['externalGuidReferences'] and not obj_refs['literalPathReferences']:
        duplicates.append(obj_path)
    elif not fbx_refs['externalGuidReferences'] and not fbx_refs['literalPathReferences']:
        duplicates.append(fbx_path)
groups['kenney-duplicate-models'] = duplicates
groups['orphan-archive-metadata'] = [p for p in by_path if Path(p).suffix in {'.zip','.unitypackage'} and not (ROOT/p).exists()]

def inside(path, prefixes):
    return any(path == prefix or path == prefix + '.meta' or path.startswith(prefix + '/') for prefix in prefixes)

result = []
for name, paths in groups.items():
    if not paths:
        continue
    external, literals = {}, {}
    guids = {}
    for path in paths:
        entry = by_path[path]
        target = ROOT / path
        metas = list(target.rglob('*.meta')) if target.is_dir() else []
        root_meta = Path(str(target) + '.meta')
        if root_meta.exists():
            metas.append(root_meta)
        for meta in metas:
            match = re.search(r'^guid: ([0-9a-f]{32})', meta.read_text(encoding='utf-8-sig'), re.M)
            if match:
                guids[match[1]] = meta.with_suffix('').relative_to(ROOT).as_posix()
        for target, references in entry['externalGuidReferences'].items():
            remaining = [r for r in references if not inside(r, paths)]
            if remaining:
                external[target] = remaining
        for source, references in entry['literalPathReferences'].items():
            if not inside(source, paths):
                literals[source] = references
    guarded = {p: refs for p, refs in literals.items() if p in {
        'Assets/Scripts/Editor/TWCSampleAudit.cs', 'Assets/Scripts/Editor/OverworldTerrainAudit.cs',
        'Assets/Scripts/Editor/DioramaAuditAuthoring.cs', 'Assets/Scripts/Editor/DioramaCleanupAuthoring.cs',
        # Read-only build-report prefix filters prove the removed packs no longer ship.
        'Assets/Tests/Editor/HarnessPlayerBuild.cs'}}
    literals = {p: refs for p, refs in literals.items() if p not in guarded}
    result.append(dict(name=name, paths=paths, guids=guids, external=external, literals=literals, guardedMigrationOrArchivedAuditPaths=guarded,
                       bytes=sum(by_path[p]['bytes'] for p in paths)))
    print(name, f'{sum(by_path[p]["bytes"] for p in paths)/1048576:.1f} MiB',
          'external:', sorted({r for refs in external.values() for r in refs}), 'scripts:', list(literals))
(ROOT / 'Docs/Art/Previews/Diorama/CleanupManifest.json').write_text(json.dumps(result, indent=2))
