# Environment playground

Open `Assets/Scenes/EnvironmentPlayground.unity` and enter Play Mode. This isolated scene does not load Common, create a campaign save, or appear in the shipping scene list. Buttons, sprites, labels, and persistent click bindings are authored in the scene.

| Control | View or action |
|---|---|
| 1 / Asset gallery | All 68 meshes with triangle labels, plus eight biome palettes |
| 2 / Overworld | Generate the production campaign TWC template at the selected seed |
| 3 / Town | Generate the production town TWC template and service models |
| 4 / Smart tile rules | Separate TWC instance containing editable corner, courtyard, junction, and three-tier cliff examples |
| B / Next biome | Cycle the town palette; rebuild the visible town |
| Next seed | Increment the seed and regenerate the current generated view |
| R | Rebuild the current view with the same seed |
| World overview | Frame the entire overworld and surrounding ocean |
| WASD / mouse wheel | Pan / zoom |

The generators stay enabled when changing views; only generated geometry is hidden. Rebuilds replace owned chunk meshes. Production town/overworld scenes do not contain the playground status panel.

The saved `Town.unity` preview has also been regenerated: legacy house/roof/tree clusters were removed, and the current smart houses and tree mix are baked to persistent preview mesh assets. Playing the scene replaces those preview meshes with the current seed's generation.

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

The picker models use 28–120 triangles and fit within one cell. The gallery includes biome-colored single/group examples alongside the buildings.

## TWC integration

- `Assets/Overworld/CampaignTerrain.asset`: biome cosmetics, three mountain tiers and summit noise, house footprints, connected walls, bordered roads, surrounding water, ocean noise, and smart coastline.
- `Assets/TileWorldCreator/VillageLSystemAsset.asset`: biome ground and plants, smart house footprints, connected shop walls, and bordered roads including shop floors. Existing service prefabs retain gameplay scripts while using the new shop, trainer lodge, shrine, and entrance meshes. The inn service has its own bed sign and offers rest/checkpoint actions in the production town.
- `Assets/Art/EnvironmentKit/SmartTiles/RuleExamples.asset`: native Paint blueprint layers for deliberate edge cases. Edit these in the TWC inspector and rebuild the rule view.
- `SmartTiles/Mountain.asset`, `Summit.asset`, `House.asset`, `Road.asset`, and `Shoreline.asset` are native `TileWorldCreator4TilesPreset` assets. `Wall.asset` is a native six-tile preset. Their prefab children convert the authored XY meshes to TWC's canonical XZ orientation, so they are inspectable as normal TWC presets.
- `EnvironmentSmartTileLayer` consumes TWC's generated quarter-tile classifications and rotations directly, combining the authored pieces into chunks instead of instantiating thousands of temporary objects. Walls use TWC's six canonical connection orientations.
- Upper mountain tiers use native Add + Shrink blueprint stacks. Each tier rises 0.41 cells: the imported quarter-piece height is 0.82 and the quarter scale is half a cell. Smart borders meet at matching heights; the summit noise layer uses the highest plateau, inset one cell for solid support. Its seeded Perlin noise is split into small patches (up to 3 by 3 cells), then resolved through the Summit smart preset at 1.23 cells elevation. Frequency and threshold are editable in the TWC action inspector.
- The outer ocean is two independent TWC build layers: animated water and slow macro-noise variation. A separate coastline blueprint/build layer chooses native water-side shore pieces along land/water edges throughout the map. The outer map boundary omits the beach, joining the open ocean. In-map water shares the animated ocean material and world UV scale.

Ocean materials expose wave speed, contrast, noise tiling, drift, and colors. The shoreline shader exposes sand, foam, and shallow-water colors. Animation runs in the shaders; no per-frame material allocation or mesh rebuild is needed.

The original core terrain masks still control movement, boat requirements, locks, and campaign progression. Presentation layers do not add colliders or modify those masks. Town biome lookup uses campaign data without forcing generation of the lazy overworld grid.

## Geometry and textures

The initial audit is in `EnvironmentAudit.md`; measured imported counts are in `EnvironmentKitSpecs.md`.

- Base buildings: 188–292 triangles. Static, one submesh/material, no bones or colliders.
- Mountain quarter pieces: 2 triangles for fill, 8 for faceted edges/corners. Three tiers; broad exposed faces with dark rock facets.
- Summit smart quarters: 8 triangles each. Edge, outer corner, inner corner, and fill form joined rocky spikes with low saddles. Old standalone peak/ridge/spire meshes remain available in the gallery; production generation uses the smart summit layer.
- Building textures: eight 2048x2048 painted biome atlases with padded plaster, timber, stone, roof and foliage regions. Environment props share these textures through their existing materials. One material per model; texture detail adds no triangles.
- Smart house quarters: 8–108 triangles, including timber facades and windows.
- Bordered road quarters: 4–23 triangles. Raised curbs appear on exposed borders, with open joins through intersections. One cobble/curb atlas and one material.
- Connected wall pieces: 24–120 triangles. Isolated post, end, straight, corner, T, and cross.
- Shore pieces: 2 triangles. Interior fill is deliberately omitted.
- Surrounding ocean: 8 triangles for water and 8 for the noise overlay, plus the coastline tiles. Default margin: 128 cells.
- Painted paving, roads, biome ground, masonry and water surfaces are 1024×1024 with mipmaps and trilinear filtering. The animated ocean/shore shaders and macro-noise masks are retained. See [painted environment surfaces](PaintedEnvironment.md) for source art, UV mapping, capture protocol and verification.
- Cosmetics are capped at 48 props per 32-cell chunk and 120,000 triangles across the map. Summit geometry is a separate smart layer with 32 triangles per occupied cell. Paths, bridges, towns, locks, start cells, and location entrances are protected. Coordinate hashes keep decoration deterministic without consuming gameplay RNG.
- Meshes remain CPU-readable for combining. Result meshes are grouped by 32-cell area and shared material. Triangle budgets are not an FPS guarantee; profile target hardware at the intended zoom.

## Source and reimport

`ArtSource/Environment/EnvironmentKit.blend` retains the editable mesh source;
`manifest.json` records source counts. FBX exports and runtime mesh/preset assets live under
`Assets/Art/EnvironmentKit`. Completed Python construction scripts and the destructive kit
installer are removed. Edit the source models and committed Unity meshes/materials/presets
directly, preserving asset GUIDs and CPU readability needed by mesh combining.

The Town view now uses Core's detailed service layout at the selected seed, including
centered spawn/exit, residential plots, road hierarchy and biome prop cells. The camera frames
the generated dimensions. Service markers follow the same seeded slot order as production.

The sample inspection is in `TWCSampleAudit.md`. CliffIsland, ramps, and mixed-tileset scenes were inspected through Unity. The cached TWC package has no separately named river scene; its water, sand, cliff, and bridge layers supplied the relevant examples. Restored vendor demo files remain under the project's existing ignored Demo folder and are not required by the environment kit.

## Verification

Targeted editor tests cover model budgets, all 16 wall neighborhoods against actual mesh extents, mountain and summit seam heights, protected paths, small summit patch bounds, deterministic placement, and biome lookup. The playground Play Mode test cycles all eight town palettes, rebuilds both generators and the rule laboratory, checks mesh ownership and authored button bindings, checks ocean/shore layers, and compares rendered ocean frames to verify animation.

Test outcomes and any unrelated regression failures are recorded in `EnvironmentValidation.md`.

Saved Unity renders: [town wall detail](Previews/TownFacade.png), [smart mountain tops](Previews/MountainTops.png), [shoreline](Previews/Shoreline.png), [rule examples](Previews/SmartRules.png), and [biome palettes](Previews/BiomePalette.png).
