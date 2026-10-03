# Biome dungeons

Campaign runs outside `repeatable-0` and `story-0` use biome layouts. The two starters
use the original BSP/throne layouts. `DungeonSaveData.UseBiomeLayout` records that gameplay
choice; old save schemas are not migrated. Core layout options still distinguish the two
generation algorithms for deterministic caching.

`DungeonLayoutProfile.Options` is the shared Core entry point. It chooses each regular axis independently: 32, 34–38, 38–42, 44–48, then 52–56. Entry/exit floors use 16, 18, 20, 22, then 24 tiles. Tile scale and campaign floor ranges are unchanged. Entry/exit roles retain throne-floor gameplay behavior and have no encounters, hazards, or blocking scenery.

| Biome | Structure | Corridor brush width |
|---|---|---|
| Grassland | Rectangular chambers, courtyard galleries and connecting loops | 2–3 |
| Forest | Organic chambers with winding branches | 1–3 |
| Desert | Rectangular side chambers and a three-tile axial hall | 2–4 |
| Water | Dry platforms with trimmed corners and linked causeways | 2–3 |
| Mountain | Irregular chambers and narrow branching tunnels | 1–2 |
| Tundra | Chamfered ice rooms with repeated loop connections | 2–3 |
| Marsh | Organic chamber clusters linked to nearby chambers | 1–3 |
| Volcanic | Broad diamond-shaped halls and narrow fractures | 1–3 |

Width ranges describe the corridor carving brush; intersections and connections into rooms can be wider. Rectangular room anchors are entirely walkable, while organic extensions are represented only by the floor mask.

Layout, placement, scenery and cosmetic streams are separate. A skin override never selects a layout. Runs persist `LayoutVersion`, `LayoutTier` and `LayoutBiome` alongside the visual selection. `UseBiomePresentation` distinguishes later Grassland from the starter presentation. Runtime TWC assets are cloned and every Core action receives identical options before blueprints execute. `SetCustomRandomSeed` writes the asset seed, so configuration reads that value rather than the previous generation's `currentSeed`.

## Scenery and rewards

Density is `clamp(sqrt(walkable / 322.13671875), 1, 2)`. The denominator is the mean of legacy 32×32 seeds 0 through 255, protected by a regression test. Enemy, supply and gathering counts round away from zero and retain their existing caps. Targets per reference floor are two containers, six destructibles and three hazards. Placement can reduce scenery counts when no safe cells remain.

All props avoid the party's three-tile spawn clearance and existing placements. Blocking props are accepted only when every remaining floor cell stays reachable with diagonal corner rules. A reserved route, including diagonal support cells, remains free of hazards and blocking props. Containers receive item/gold allocations first; rewards are subtracted from the floor's scaled budget. Each prop stores an immutable loot roll; the runtime commits its opened state before creating a drop. Full inventory leaves the drop on the floor through the existing pickup behavior.

Face an adjacent container and use the normal attack/interaction button to open it. Destructibles block movement but never sight. Their HP is `20 + 10 × tier`. Basic, ranged, selected, missile, area, piercing and multi-hit damage can affect them. Raw damage uses neutral defense, guaranteed hits and no criticals. Character effects such as healing, statuses, displacement, retaliation and experience do not apply to scenery. The target-selection menu includes eligible props without adding them to character lists or AI target searches.

Hazards deal rounded-up 10% maximum HP on entry. Character action execution detects movement, swaps and displacement onto hazard cells; remaining on a hazard does not trigger it again. Pathfinding assigns hazards a substantial cost. Their geometry and minimap markers remain visible after exploration. Floor transitions clear the interaction references and destroy owned geometry. Mid-floor state is intentionally not saved.

## Preview and verification

The dungeon explorer has tier, role and legacy/biome controls. The generator inspector offers the same options. Both configure the gameplay generator and show scenery. The console explorer and CLI resolve campaign biome profiles; CLI JSON includes profile metadata and scenery.

Unity menu commands:

- `Tools/Eternal Enigma/Tests/Run Biome Layouts`: profile persistence, cache and reskin agreement, plus preview/performance export.
- `Tools/Eternal Enigma/Tests/Run Biome Scenery`: gameplay integration including loot, damage, hazards and transitions.
- `Tools/Eternal Enigma/Tests/Run Biome Regression`: scenery, skills, movement, inventory targeting and existing dungeon theme tests.

Core sweeps cover 24 seeds × eight biomes × five tiers × three roles, generate every floor twice, and check connected masks, boundaries, room anchors, safe stairs, spawn clearance, reward conservation, repeatability and increasing mean floor area. Legacy dungeon tests run alongside these checks.

[Preview images and measured costs](Art/Previews/BiomeLayouts/README.md) include 56×56 regular floors and tier-4 entry/exit floors for every biome. Measurements are editor CPU timings on this workstation, not player frame-time guarantees. The geometry timing includes blueprint generation, mesh building and scenery creation. Forest currently has substantially more triangles than the other themes because of its existing tile assets.

Current Core audit: **340 passed, 0 failed**. Earlier failure counts are superseded.
Unity validation uses the current save schema, explicit visual selection, and profile/cache
agreement. See [test harness](../Assets/Tests/README.md) for current results and commands.
