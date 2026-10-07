"""Read-only GUID and literal-path audit. Run again immediately before cleanup."""
from pathlib import Path
import json, re, sys

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'
guid_pattern = re.compile(r'guid:\s*([0-9a-f]{32})')
owners, refs, texts = {}, {}, {}
for path in ASSETS.rglob('*.meta'):
    match = guid_pattern.search(path.read_text(encoding='utf-8-sig', errors='replace'))
    if match:
        owners[match[1]] = path.with_suffix('').relative_to(ROOT).as_posix()
extensions = {'.asset', '.prefab', '.unity', '.mat', '.controller', '.overridecontroller', '.meta', '.cs', '.json', '.asmdef', '.shader', '.cginc', '.guiskin', '.spriteatlas'}
for folder in (ASSETS, ROOT / 'ProjectSettings'):
    for path in folder.rglob('*'):
        if not path.is_file() or path.suffix.lower() not in extensions:
            continue
        content = path.read_text(encoding='utf-8-sig', errors='replace')
        rel = path.relative_to(ROOT).as_posix()
        if path.suffix == '.cs':
            texts[rel] = content
        for guid in set(guid_pattern.findall(content)):
            if path.suffix != '.meta' or owners.get(guid) != rel[:-5]:
                refs.setdefault(guid, []).append(rel)

candidates = [
    'Tiles', 'Prefabs/Dungeon/Dungeon.prefab', 'Prefabs/Dungeon/_TileWorldCreator_Dungeon.prefab',
    'Prefabs/Dungeon/Enemy.prefab', 'Art/Torch.mat', 'Scripts/DungeonGenerator.cs',
    'Prefabs/Dungeon/Enemies/Enemy_MushroomSmile.prefab', 'Prefabs/Dungeon/Enemies/Enemy_LargeSlime.prefab',
    'Art/DungeonTexture', '_Recovery', 'unity-wave-function-collapse',
    'Art/RPGHero', 'Art/3D Props - Adorable Items/Adorable 3D Items',
    'RPG Tiny Fantasy World 01 PA', 'RPGMonsterWave4Polyart',
    'Art/EnvironmentKit/Models', 'Art/EnvironmentKit/Prefabs',
    'TileWorldCreator/Demo', 'TileWorldCreator/Tiles', 'TileWorldCreator/VillageLSystemAsset.asset',
    'RPG_skills_and_abilities/RPG_skills_and_abilities.unity',
]
for path in ASSETS.rglob('*'):
    if path.is_dir() and path.name in {'SciFi', 'CliffIsland', '2DIsland', 'Prototype', '6-Tiles', 'V2 Dungeon', '4-Tiles'}:
        candidates.append(path.relative_to(ASSETS).as_posix())
for path in ASSETS.glob('Overworld/Biome*.png'):
    candidates.append(path.relative_to(ASSETS).as_posix())
candidates.append('Overworld/BiomeRoad.mat')
for path in ASSETS.glob('RPG_skills_and_abilities/All*.png'):
    candidates.append(path.relative_to(ASSETS).as_posix())
for path in (ASSETS / 'Art/EnvironmentKit/Prefabs').iterdir():
    if path.is_dir() and path.name != 'Grassland':
        candidates.append(path.relative_to(ASSETS).as_posix())
for path in (ASSETS / 'Art/KennyNL/Castle Kit/Models').iterdir():
    if path.suffix.lower() in {'.obj', '.fbx'}:
        candidates.append(path.relative_to(ASSETS).as_posix())
for path in ASSETS.rglob('*.meta'):
    target=path.with_suffix('')
    if target.suffix in {'.zip','.unitypackage'} and not target.exists():
        candidates.append(target.relative_to(ASSETS).as_posix())
report = []
for candidate in sorted(set(candidates)):
    prefix = 'Assets/' + candidate
    path = ROOT / prefix
    if not path.exists() and not Path(str(path)+'.meta').exists():
        continue
    members = {g: p for g, p in owners.items() if p == prefix or p.startswith(prefix + '/')}
    external = {p: sorted(r for r in refs.get(g, []) if r != prefix and not r.startswith(prefix + '/') and r != prefix + '.meta') for g, p in members.items()}
    external = {p: r for p, r in external.items() if r}
    literals = {p: matches for p, t in texts.items() if not p.startswith(prefix + '/')
                if (matches := sorted(set(re.findall(r'"[^"\n]*' + re.escape(prefix) + r'[^"\n]*"', t))))}
    files = list(path.rglob('*')) if path.is_dir() else [path] if path.exists() else [Path(str(path)+'.meta')]
    report.append({'path': prefix, 'assets': len(members), 'bytes': sum(p.stat().st_size for p in files if p.is_file()),
                   'externalGuidReferences': external, 'literalPathReferences': literals,
                   'resources': any('/Resources/' in p for p in members.values())})
destination = ROOT / 'Docs/Art/Previews/Diorama/ReferenceAudit.json'
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(report, indent=2), encoding='utf-8')
for entry in report:
    print(f"{entry['path']}: {entry['assets']} assets; {len(entry['externalGuidReferences'])} externally referenced; {len(entry['literalPathReferences'])} script paths; {entry['bytes']/1048576:.1f} MiB")

if '--verify-cleanup' in sys.argv:
    manifest = json.loads((destination.parent / 'CleanupManifest.json').read_text(encoding='utf-8'))
    verification = []
    for group in manifest:
        remaining = [p + suffix for p in group['paths'] for suffix in ('', '.meta') if (ROOT / (p + suffix)).exists()]
        dangling = {path: refs[guid] for guid, path in group['guids'].items() if refs.get(guid)}
        verification.append(dict(group=group['name'], remaining=remaining, danglingReferences=dangling))
    (destination.parent / 'CleanupVerification.json').write_text(json.dumps(verification, indent=2), encoding='utf-8')
    invalid = [g['group'] for g in verification if g['remaining'] or g['danglingReferences']]
    print('Cleanup verification:', 'PASS' if not invalid else 'FAIL ' + ', '.join(invalid))
    if invalid:
        sys.exit(1)
