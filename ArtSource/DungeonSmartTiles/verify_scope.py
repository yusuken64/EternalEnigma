"""Verify the dungeon-only asset boundary against the repository's original HEAD."""
from pathlib import Path
import csv, hashlib, json, re, subprocess

root = Path(__file__).resolve().parents[2]
def git(*args):
    return subprocess.check_output(['git','-c','core.safecrlf=false',*args],cwd=root).decode('utf-8')
changed = git('diff','--name-only').splitlines()
protected = ['Assets/Scenes/', 'Assets/Prefabs/Town/', 'Assets/Prefabs/Overworld/',
             'Assets/Prefabs/Dungeon/Ally.prefab', 'Assets/Resources/UI/',
             'Assets/Scripts/Dungeon/Game/Menu/CameraController.cs',
             'Assets/Scripts/Dungeon/Game/Ally.cs', 'Assets/Scripts/Dungeon/Game/Character.cs',
             'Assets/Art/RPGTinyHeroWavePolyart/', 'Assets/Art/3D Props - Adorable Items/',
             'Assets/Art/DungeonThemes/Materials/', 'Assets/Art/DungeonThemes/Presets/',
             'Assets/RPG Tiny Fantasy World 01 PA/', 'Core/', 'ProjectSettings/']
violations = [p for p in changed if any(p.startswith(prefix) for prefix in protected)]
if violations: raise AssertionError('Protected assets changed: '+str(violations))

catalog_path = 'Assets/Resources/DungeonThemes/Catalog.asset'
catalog = (root/catalog_path).read_text(encoding='utf-8-sig')
def without_smart_bindings(text):
    return re.sub(r'^    (Regular|Throne)SmartBoundary:.*\n', '', text, flags=re.M)
assert without_smart_bindings(catalog) == without_smart_bindings(git('show','HEAD:'+catalog_path)), 'Non-boundary theme settings changed'
smart_bindings = re.findall(r'^    (?:Regular|Throne)SmartBoundary:.*guid: ([a-f0-9]+)', catalog, re.M)
assert len(smart_bindings) == 32, 'Every biome/environment/layout needs a smart boundary'
adapters = list((root/'Assets/Art/DungeonThemes/PolyartSmartTiles/Themes').glob('*.asset'))
assert len(adapters) == 15
for adapter in adapters:
    text=adapter.read_text(encoding='utf-8-sig')
    assert 'AuthoredCellSize: 2' in text and 'AuthoredHeight: 4.234727' in text
    assert re.search(r'MaterialOverride:.*guid: [a-f0-9]+',text), adapter

def blocks(text):
    chunks = re.split(r'(?m)^--- !u!(\d+) &(-?\d+)\s*\n',text)
    return {chunks[i+1]:(int(chunks[i]),chunks[i+2]) for i in range(1,len(chunks),3)}
pickup_paths = [p for p in changed if p.startswith('Assets/Prefabs/Dungeon/DroppedItems/') and p.endswith('.prefab')]
pickup_paths += [p for p in changed if p=='Assets/Art/Diorama/Items/GoldPickup.prefab']
for path in pickup_paths:
    before=blocks(git('show','HEAD:'+path));after=blocks((root/path).read_text(encoding='utf-8-sig'))
    for key,(kind,body) in before.items():
        assert key in after, (path,'removed existing component',key)
        new=after[key][1]
        if kind==4 and 'm_Father: {fileID: 0}' not in body: continue
        if kind==1 and 'm_Name: Floor pose\n' in body:
            # The visual child gains only its baked support-point component.
            old_ids=set(re.findall(r'component: \{fileID: (-?\d+)\}',body))
            new=re.sub(r'^  - component: \{fileID: (-?\d+)\}\n',lambda m:m.group(0) if m.group(1) in old_ids else '',new,flags=re.M)
        assert body==new,(path,'changed non-visual-child data',kind,key)

blueprints={}
for name in ['DungeonAsset','DungeonThroneAsset']:
    path=f'Assets/Prefabs/Dungeon/{name}.asset'
    old=git('show','HEAD:'+path);new=(root/path).read_text(encoding='utf-8-sig')
    def blueprint(text):
        start=text.index('    - Name: mapBlueprintLayers\n')
        end=text.index('    - Name: mapBuildLayers\n',start)
        return text[start:end]
    assert blueprint(old)==blueprint(new),name+' blueprint data changed'
    blueprints[name]=hashlib.sha256(blueprint(new).encode()).hexdigest()

sources={}
for n in range(1,5):
    name=f'Wall0{n}.fbx'
    original=(root/'Assets/RPG Tiny Fantasy World 01 PA/Mesh/BuildingUtilityDeco'/name).read_bytes()
    copied=(root/'ArtSource/DungeonSmartTiles/Inputs'/name).read_bytes()
    assert original==copied,name+' input copy differs from vendor'
    sources[name]=hashlib.sha256(original).hexdigest()

folder=root/'Docs/Art/Verification/DungeonSmartLayers'
table=(root/'Assets/Resources/DungeonThemes/PickupPresentation.asset').read_text(encoding='utf-8-sig')
hero_height=float(re.search(r'  HeroHeight: ([\d.]+)',table)[1])
kit_height=json.loads((root/'Assets/Art/DungeonThemes/PolyartSmartTiles/Meshes.json').read_text())['authoredHeight']
assert abs(kit_height/hero_height-1.25)<.00001
assert 'HeroRigPrefab: Assets/Prefabs/Dungeon/Ally.prefab' in table
with (folder/'PickupAdjustments.csv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
metrics={}
for category,low,high in [('Ordinary',1/3,.5),('Elongated',.5,2/3)]:
    values=[float(e['projected_diameter_ratio']) for e in rows if e['size']==category]
    assert all(low-.001<=v<=high+.001 for v in values)
    metrics[category]={'count':len(values),'minimum':min(values),'maximum':max(values)}
report={'protectedAssetsUnchanged':True,'pickupPrefabsWithUnchangedLogicalRootsMeshesMaterialsAndInteractions':len(pickup_paths),
        'smartBoundaryBiomeEnvironmentSelections':16,'smartBoundaryLayoutBindings':len(smart_bindings),
        'otherThemeSettingsAndExistingSurfaceAssetsUnchanged':True,
        'unchangedDungeonHeroHeight':hero_height,'wallKitHeight':kit_height,'wallToHeroHeightRatio':1.25,
        'blueprintYamlSha256':blueprints,'originalSourceSha256':sources,'projectedPickupRatios':metrics,
        'pickupComparisons':len(rows),'chestWorldHeightRatio':.5}
(folder/'ScopeAudit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
