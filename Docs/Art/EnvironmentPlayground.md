# Environment playground

Open `Assets/Scenes/EnvironmentPlayground.unity` and enter Play Mode. This isolated scene does not load Common, create a campaign save, or appear in the shipping scene list. Buttons, sprites, labels, and persistent click bindings are authored in the scene.

| Control | View or action |
|---|---|
| 1 / Asset gallery | Base kit mesh gallery with triangle labels and eight biome palettes |
| 2 / Overworld | Generate the production campaign TWC template at the selected seed |
| 3 / Town | Generate the production town TWC template and service models |
| 4 / Smart tile rules | Separate TWC instance containing editable corner, courtyard, junction, and three-tier cliff examples |
| B / Next biome | Cycle the town palette; rebuild the visible town |
| Next seed | Increment the seed and regenerate the current generated view |
| R | Rebuild the current view with the same seed |
| World overview | Frame the entire overworld and surrounding ocean |
| WASD / mouse wheel | Pan / zoom |

The generators stay enabled when changing views; only generated geometry is hidden. Rebuilds replace owned chunk meshes. Production town/overworld scenes do not contain the playground status panel.

The saved `Town.unity` preview uses persistent baked mesh assets. Playing the scene
replaces them with the current seed's generation. Rebuild the preview after changing
production bindings. The [diorama review](DioramaStyle.md) contains current foliage,
facade and terrain captures; older images below preserve the earlier base kit.

## Tree model picker

Both production TWC templates expose **Tree model picker / Weighted tree models** on their environment/cosmetic build layer. The shared asset is `Assets/Art/EnvironmentKit/TreeModels.asset`. Each entry has a prefab, weight, and allowed biome list; zero weight disables an entry. Choices, rotation, and scale are coordinate-seeded, so identical seeds rebuild identically without consuming gameplay RNG. The overworld's protection and triangle budgets still apply.

| Biome | Tree choices |
|---|---|
| Grassland / Forest | Standard, tall, wide broadleaf trees, pairs, three-tree groves; biome foliage colors |
| Mountain | Pines, pairs, three-pine groves |
| Tundra | Snow-capped pines, pairs, three-pine groves |
| Desert | Palms and cacti, single and paired |
| Water | Drooping willows, single and paired (town foliage; open ocean stays clear) |
| Marsh | Rooted mangroves and dead snags, single and paired |
| Volcanic | Charred trees with ember accents, single and paired |

The table describes biome roles, not the old compact mesh bindings. Production
uses measured Tiny Fantasy tree adapters and authored missing forms. Broadleaf
trees are 2.85 hero heights and pines 3.1; canopies can exceed one cell, so route
clearances matter. Source triangle counts and selected LODs are in
[diorama fit evidence](DioramaStyle.md#measured-source-selection).

## TWC integration

- `Assets/Overworld/CampaignTerrain.asset`: biome cosmetics, three mountain tiers and summit noise, house footprints, connected walls, bordered roads, surrounding water, ocean noise, and smart coastline.
- `Assets/TileWorldCreator/VillageLSystemAsset.asset`: biome ground and plants, smart house footprints, connected shop walls, and bordered roads including shop floors. Existing service prefabs retain gameplay scripts while using the new shop, trainer lodge, shrine, and entrance meshes. The inn service has its own bed sign and offers rest/checkpoint actions in the production town.
- `Assets/Art/EnvironmentKit/SmartTiles/RuleExamples.asset`: native Paint blueprint layers for deliberate edge cases. Edit these in the TWC inspector and rebuild the rule view.
- `SmartTiles/Mountain.asset`, `Summit.asset`, `House.asset`, `Road.asset`, and `Shoreline.asset` are native `TileWorldCreator4TilesPreset` assets. `Wall.asset` is a native six-tile preset. Their prefab children convert the authored XY meshes to TWC's canonical XZ orientation, so they are inspectable as normal TWC presets.
- `EnvironmentSmartTileLayer` consumes TWC's generated quarter-tile classifications and rotations directly, combining the authored pieces into chunks instead of instantiating thousands of temporary objects. Walls use TWC's six canonical connection orientations.
- Upper mountain tiers retain native Add + Shrink blueprint masks and summit noise. Production diorama strata adapters replace the original compact pieces; inspect the current preset and [diorama scale contract](DioramaStyle.md) before changing layer height or quarter-tile scale.
- The outer ocean is two independent TWC build layers: animated water and slow macro-noise variation. A separate coastline blueprint/build layer chooses native water-side shore pieces along land/water edges throughout the map. The outer map boundary omits the beach, joining the open ocean. In-map water shares the animated ocean material and world UV scale.

Ocean materials expose wave speed, contrast, noise tiling, drift, and colors. The shoreline shader exposes sand, foam, and shallow-water colors. Animation runs in the shaders; no per-frame material allocation or mesh rebuild is needed.

The original core terrain masks still control movement, boat requirements, locks, and campaign progression. Presentation layers do not add colliders or modify those masks. Town biome lookup uses campaign data without forcing generation of the lazy overworld grid.

## Geometry and textures

The [base kit table](EnvironmentKitSpecs.md) preserves the original per-mesh audit.
Current diorama source selection, scale, screenshots, packed assets and scene
measurements are in [DioramaStyle.md](DioramaStyle.md).

- TWC `OverworldGroundLayer` owns painted biome ground, roads, town plazas,
  bridges and in-grid water. Half-cell vertices blend biome channels inside
  32×32-cell chunks. Coastline and the outer ocean retain separate layers.
- The tree-wall layer has a 2,000,000-triangle map budget, 96,000 triangles per
  chunk and at most 192 trees per chunk. General cosmetics have separate limits
  of 600,000 triangles per map and 160 props per chunk.
- Paths, bridges, towns, locks, start cells and entrances remain protected.
  Coordinate hashing keeps cosmetic choices independent of gameplay RNG.
- Source meshes used for combining remain CPU-readable. Combined meshes are
  grouped by area/material and released with their generated owner.
- Source and runtime painted textures, UV mapping and earlier capture protocols
  remain documented in [painted environment surfaces](PaintedEnvironment.md).
  Use current diorama bindings when reproducing production output.

Geometry budgets and editor timings are not an FPS guarantee; profile the target
player at the intended zoom.

## Source and reimport

`ArtSource/Environment/EnvironmentKit.blend` retains the editable mesh source;
`manifest.json` records source counts. FBX exports and runtime mesh/preset assets live under
`Assets/Art/EnvironmentKit`. Completed Python construction scripts and the destructive kit
installer are removed. Edit the source models and committed Unity meshes/materials/presets
directly, preserving asset GUIDs and CPU readability needed by mesh combining.

The Town view now uses Core's detailed service layout at the selected seed, including
centered spawn/exit, residential plots, road hierarchy and biome prop cells. The camera frames
the generated dimensions. Service markers follow the same seeded slot order as production.

The historical [sample inspection](TWCSampleAudit.md) covers CliffIsland, ramps
and mixed tilesets. Temporarily restored vendor demos were removed during the
diorama cleanup; production uses committed project-owned presets.

## Verification

Targeted editor tests cover model budgets, all 16 wall neighborhoods against actual mesh extents, mountain and summit seam heights, protected paths, small summit patch bounds, deterministic placement, and biome lookup. The playground Play Mode test cycles all eight town palettes, rebuilds both generators and the rule laboratory, checks mesh ownership and authored button bindings, checks ocean/shore layers, and compares rendered ocean frames to verify animation.

Test outcomes and any unrelated regression failures are recorded in `EnvironmentValidation.md`.

Saved Unity renders: [town wall detail](Previews/TownFacade.png), [smart mountain tops](Previews/MountainTops.png), [shoreline](Previews/Shoreline.png), [rule examples](Previews/SmartRules.png), and [biome palettes](Previews/BiomePalette.png).
