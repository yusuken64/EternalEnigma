"""Publish the review index only after the saved regression reports pass."""
from pathlib import Path
import csv, json, xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[2]
folder=root/'Docs/Art/Verification/DungeonSmartLayers'
edit=json.loads((folder/'EditMode.json').read_text())
play=json.loads((folder/'PlayMode.json').read_text())
for report in [edit,play]:
    assert report['state']=='Completed' and report['failed']==0 and report['skipped']==0
core=ET.parse(folder/'Core/DungeonSmartLayersCore.trx').find('.//{*}Counters').attrib
assert core['failed']=='0' and core['passed']==core['total']
audit=json.loads((folder/'ScopeAudit.json').read_text())
with (folder/'LivePickupRatios.csv').open(newline='',encoding='utf-8-sig') as f:
    live=list(csv.DictReader(f))
assert len(live)==141
for i in range(141): assert (folder/'Pickups'/f'{i:03}.png').is_file()
assert audit['smartBoundaryLayoutBindings']==32
assert (folder/'Biomes/Progress.txt').read_text().startswith('Complete: 32')
ordinary=audit['projectedPickupRatios']['Ordinary']
elongated=audit['projectedPickupRatios']['Elongated']

text=f"""# Dungeon smart layers verification

The implementation uses **4.234727-unit walls**, 1.25 times the unchanged dungeon hero's measured **3.3877816-unit** height, following the user's clarification. The two-unit grid, gameplay masks, seeds, placements, and interactions are retained.

| Check | Result |
| --- | --- |
| Core regression suite | {core['passed']} passed, 0 failed |
| Focused Unity Edit Mode suite | {edit['passed']} passed, 0 failed |
| Focused Unity Play Mode suite | {play['passed']} passed, 0 failed |
| Neighbor patterns | All 256 cardinal/diagonal combinations |
| Runtime presentation transitions | All 32 biome/environment/layout combinations |
| Smart boundaries | All 8 biomes, Interior/Outdoor, regular/throne |
| Pickup comparisons | 141, each on light and dark floors |
| Ordinary projected diameter / hero screen height | {ordinary['minimum']:.3f}–{ordinary['maximum']:.3f} |
| Elongated projected diameter / hero screen height | {elongated['minimum']:.3f}–{elongated['maximum']:.3f} |
| Chest vertical world height / hero world height | 0.500 |
| Pickup footprint | At most 1.64 units within a 2-unit cell |

The Edit Mode checks cover authored layer serialization, overrides, exclusions, independent rebuilds, disabling/hide/restore, deterministic variants, resource disposal, blueprint equivalence, map edges, isolated cells, concave corners, narrow strips, and thick masses. A GPU check renders a marker behind a full-height wall and confirms that the cutaway reveals it; disabling the mask occludes it again.

The Play Mode checks exercise real floor generation, fog, mounted decorations, pickup visibility, movement, pickup/drop/throw, currency amounts, keys, mimic disguise grounding, and measured item ratios against the live dungeon hero. Fog padding covers map-edge wall faces. Foreground cutaways and decoration support filtering leave walkable cells readable through the unchanged camera.

## Visual review

- [All 32 biome/layout captures](Biomes/Review.html) and [contact sheet](Biomes/ContactSheet.png)
- [Pickup category contact sheet](PickupCategories.png)
- [Every pickup on both floors](PickupReview.html)
- [Individual adjustment table](PickupAdjustments.csv)
- [Live gameplay ratio measurements](LivePickupRatios.csv)
- [Tile kit beside the dungeon hero](Kit_Gameplay.png)
- [Regular room](Regular_Gameplay.png) and [throne room](Throne_Gameplay.png)
- [Runtime regular room with fog/HUD](Runtime_Regular.png) and [runtime throne room](Runtime_Throne.png)
- [Runtime key and mimic](Runtime_Key_Mimic.png)

Comparison crops retain the production camera projection and put each item at the same ground depth as the assembled dungeon hero. The hero is sampled in its existing idle pose; animated selection-ring effects are inactive in the isolated review. Runtime captures show the actual game presentation. The key/mimic interaction screenshot uses the existing test's inspection framing; the 141 comparison crops supply the unchanged-camera size evidence.

## Scope and reproducibility

The [scope audit](ScopeAudit.json) verifies byte-identical blueprint YAML, vendor source copies, and unchanged logical roots, mesh/material references, and interaction components in 138 existing pickup prefabs. Hero assets/code, cameras, town/overworld assets, icons, vendor originals, and Core code are unchanged. Shared key/chest/bag props have separate dungeon adapters.

All sixteen biome/environment selections now share the smart kit geometry and height. Fifteen adapters reuse their existing dungeon boundary textures, tints and UV projection; Grassland Interior retains its Polyart atlas. Other catalog settings, existing materials and four-piece fallback assets are unchanged. Runtime transitions verify actual wall materials, height, cutaways and fog for each regular/throne combination.

Editable Blender sources, original FBX copies, and the modeling recipe are retained in `ArtSource/DungeonSmartTiles`. See [authoring instructions](../../../DungeonSmartLayers.md) for Unity menus and rebuild steps.

Reports: [Core TRX](Core/DungeonSmartLayersCore.trx), [Edit Mode XML](EditMode.xml), [Play Mode XML](PlayMode.xml). These are correctness and presentation checks, not a target-device performance benchmark.
"""
(folder/'Verification.md').write_text(text,encoding='utf-8')
print(folder/'Verification.md')
