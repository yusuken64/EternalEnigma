# Diorama style and fit evidence

Phase 0 measurements captured in Unity 6000.2.7f2 on 2026-10-06. Implementation progress and remaining acceptance gates are tracked in [the task plan](../../TODOs/09-diorama-restyle-plan.md).

## Scale and camera

The actual `Ally_MC01` prefab, with its active equipment and sampled idle pose, measures **H = 1.206013 world units** vertically. The Core cell is 2 units, or 1.658H. This replaces the earlier 0.9–1.0 estimate. Ground is XY and height extends toward negative Z. Imported Y-up meshes need a −90° X adapter; characters additionally face the gameplay camera.

Use one height ladder in all three environment contexts. Camera zoom remains unchanged. Height is measured from the visible base to the top; normalize by measured asset bounds, rather than applying one multiplier to differently sized source meshes.

| Form | Hero heights | World units |
|---|---:|---:|
| Fence | 0.6 | 0.724 |
| Barrel/crate | 0.55 | 0.663 |
| Sign | 1.1 | 1.327 |
| Bench | 0.5 | 0.603 |
| Lamp | 1.8 | 2.171 |
| Door | 1.6 | 1.930 |
| Eaves | 2.2 | 2.653 |
| Broadleaf tree | 2.85 | 3.437 |
| Pine | 3.1 | 3.739 |
| Boulder | 0.6–1.0 | 0.724–1.206 |
| Floor pickup | 0.45 | 0.543 |

The four reference images (`Untitled2`, `Untitled3`, `Untitled7`, `Untitled8`) call for overlapping canopies, readable trunks and clear routes. Compare nearby ground-contact points at the same depth. Height alone is insufficient: a broadleaf canopy at 2.85H is roughly 3 units wide. Keep it outside route and door clearances. The integrated town/route/forest comparisons retain these production multipliers; evidence is linked below.

## Measured source selection

Raw measurements, original shader names, mesh paths, native bounds, collider counts and animation names are in [Measurements.json](Previews/Diorama/Fit/Measurements.json). Triangle counts include active renderer instances at the specified LOD. They are not frame-time estimates.

| Tree source | Native Y height | Native X width | LOD0 / LOD1 triangles | 2.85H fit multiplier | Form and intended use |
|---|---:|---:|---:|---:|---|
| Tree01 | 4.57885 | 2.99478 | 366 / 208 | 0.75065 | Tiered pine; use 3.1H (0.8165) for primary pine |
| Tree02 | 3.34977 | 2.80354 | 280 / 159 | 1.02608 | Squat pine; secondary cluster silhouette |
| Tree03 | 4.19266 | 3.70159 | 1038 / 588 | 0.81980 | Rounded fruit tree; primary broadleaf |
| Tree04 | 4.01798 | 3.29594 | 1020 / 696 | 0.85544 | Open, yellow broadleaf; seasonal accent |
| Tree05 | 5.40083 | 2.88419 | 1007 / 814 | 0.63641 | Cactus; desert accent, not a broadleaf |

The pack already supplies pine forms. Author only the missing snow treatment, palm, willow/mangrove, dead/charred forms, cover and modular adapters in Blender. Preserve selected vendor mesh identities. Town and overworld batching can choose a measured source LOD; it must never combine both levels.

[Same-depth environment group](Previews/Diorama/Fit/Environment_Group.png): hero, Tree01, Tree03, WoodFence01, SignPost01. Individual tree comparisons and [TreeScale.csv](Previews/Diorama/Fit/TreeScale.csv) retain the tested multipliers. The CSV projects the vertical centerline; visible canopy silhouette also includes depth and must be inspected in the images.

GroundPadding01_1 is **11.525 × 16.928** units on its ground plane, with a 0.206 height. RoadA01 is **4.821 × 13.990**, with a 0.0446 height. Mountain01 is **9.018 × 9.044 × 3.750**. These are freeform pieces. Uniformly scaling them to a cell leaves irregular edges; they cannot be the continuous walkable floor or a complete cliff tile set.

The 45 building/utility/deco prefabs contain walls, gates, bridges, a watchtower and dressing, but no complete storybook house or modular facade/roof set. Preserve the Core house footprints and author the missing modular facades and roofs. WatchTower01 (5712/3922 triangles) is a landmark candidate.

| Phase | Source decision |
|---|---|
| 1 ground | Project-owned painted ground mesh and textures through TWC. Freeform padding/roads are unsuitable as cell fills. |
| 2 terrain | Blender modular strata adapters; imported rocks for accents/backdrops. Retain project-owned water/shore effects on Built-in. |
| 3 foliage/props | Imported Tree01/02/03/04 and cactus Tree05; selected grass/flowers/rocks; TownInteriors furniture; Blender only for missing forms. |
| 4 houses | Blender modular facades/roof details; imported fence/sign/trim shapes where they fit. |
| 5 landmarks | Phase 4 miniatures plus imported watchtower/utility and existing Kenney castle pieces where suitable. |
| 6 items | Adorable atlas variants with project-owned lit materials, plus Tiny Hero weapon meshes for absent item forms. |

## Materials and palette

Use project-owned Built-in lit materials under `Assets/Art/Diorama/Materials`. Vendor materials resolve to `Hidden/InternalErrorShader` in this project and must not be used directly. The environment atlas works with Standard; foliage uses the project-owned Built-in `Diorama Foliage` shader. It bends only elevated canopy vertices in world XY. Ground-contact vertices stay fixed. Imported colliders are removed only in adapters.

All ten Wave 4 prefab renderers currently resolve to the environment `DefaultPBR` atlas, even though the pack contains its own `DefaultPolyart`. The project adapters explicitly bind the Wave 4 albedo texture. This was verified by recapturing the blue slime after the correction. The vendor prefab/material files remain unchanged.

Keep matte surfaces, rounded silhouettes and larger readable details. Use saturated roof colors, warm plaster/timber and quiet floor textures so the Polyart characters remain readable. Add hue variation through project materials and atlas variants, preserving vendor UVs.

| Biome | Ground | Plant/rock notes | Roof accent |
|---|---|---|---|
| Grassland | Warm yellow-green grass, tan dirt | Fresh rounded foliage, warm grey stones | Terracotta |
| Desert | Golden sand | Cactus/palm, ochre stone | Coral/clay |
| Water | Saturated cyan water, pale shore | Reeds, coastal rock | Teal blue |
| Mountain | Cool slate and sparse meadow | Pine, stratified stone | Slate blue |
| Forest | Deeper moss green with leaf litter | Dense pine/broadleaf clusters | Forest teal |
| Tundra | Blue-shadowed ivory snow | Snow pine and pale rocks | Berry red |
| Marsh | Olive moss and muted wet earth | Willow, reeds, mushrooms | Plum |
| Volcanic | Cool charcoal ash with restrained ember accents | Charred/dead forms, basalt | Burnt orange |

## Wave 4 handoff

All candidates have animated skinned meshes and no source colliders. No LODGroup is provided. The figures below are full visible triangle counts, including attached weapons. Floor-band and boss registration belongs to task 07; no spawn pool has been changed here.

| Candidate | Triangles | Animation clips | Provisional visual role |
|---|---:|---:|---|
| BlueSlime | 3320 | 16 | Common slime candidate |
| BoneDragon | 8772 | 16 | Large flying boss candidate (`FlyFWD` locomotion) |
| CastleMonster | 16201 | 17 | Boss/rare encounter; high mesh cost |
| Crawler | 4668 | 17 | Common ground enemy candidate |
| FlameKnight | 8018 | 16 | Fire elite candidate |
| FlowerMonster | 4622 | 16 | Forest/marsh candidate |
| IceGolem | 16498 | 17 | Ice boss/rare elite; high mesh cost |
| LittleDemon | 7610 | 18 | Elite candidate |
| SkeletonMage | 5612 | 16 | Caster candidate |
| StarFish | 4252 | 15 | Coastal candidate |

Each `Wave4_*.png` compares the existing slime, the hero and the candidate. Their candidate heights are normalized to 1.4H for the fit comparison; gameplay size and roles remain subject to the encounter task and measured performance.

## Verification and reproduction

- Baseline: **54/54 views**, [Before](Previews/Diorama/Before/) with `Stats.csv` and `CaptureState.txt`.
- Core baseline: **379 passed, 0 failed**, prior to runtime art integration.
- Unity menus: `Tools / Eternal Enigma / Diorama / Audit Imported Assets`, `Capture Fit Candidates`, `Capture Before`, `Resume Before`, `Capture After`.
- Focused adapter/material and animation tests: `Tools / Eternal Enigma / Tests / Run Diorama Fit`.
- Isolated benchmark: `Build Fit Windows` / `Build Fit WebGL`; 12 instances per candidate, fixed camera, 45 warmup frames and 180 measured frames, including an empty baseline. Results identify the device, graphics API and resolution. The runtime logs `DIORAMA_BENCHMARK_JSON` and writes `DioramaBenchmark.json` to the persistent data directory.
- Focused fit tests: **2 passed / 0 failed**. Corrected Windows GPU benchmark: `Previews/Diorama/Fit/WindowsBenchmark.json`, RTX 2060/D3D11 at 1280×800; twelve monsters median 0.75–2.04 ms, twelve trees 0.35–0.36 ms, empty baseline 0.275 ms. Includes animation/update and synchronous GPU readback, not full-game frame time.
- The isolated WebGL fit build and final integrated WebGL build completed with zero errors. The final [WebGL build report](Previews/Diorama/Verification/WebGLBuild.txt) records **12 warnings**, **233,526,160 bytes** and **6:17.99** duration; packed art is listed in `Verification/WebGLArtInclusion.csv`. No retained browser-runtime report closes the integrated or isolated-fit validation gates.
- Selected Read/Write changes are listed in `Fit/SelectedReadableFBXs.txt` (15 environment meshes) and `Fit/SelectedCastleFBXs.txt` (three Kenney models). No character vendor meshes, materials, prefabs or clips are edited.
- Same-depth town, road and forest views have been reviewed against `ArtRefs/Untitled2.png`, `Untitled3.png`, `Untitled7.png` and `Untitled8.png`. Broadleaf **2.85H** and pine **3.1H** remain the production multipliers. The rendered canopy is roughly three hero heights, with the road rectangle clear; snow pine uses the same scale and blue-white ground keeps its outline visible. See `After/Town_*_HeroTree.png` and `Terrain/Overworld_{Road,Forest}_HeroTree.png`.
- `Fit/Landmarks.png` records the corrected Kenney XY adapters. Source root transforms remain intact under a normalized wrapper. Gate and flag turn toward the gameplay camera, and `towerSquare` supplies the complete tower rather than a roof-only module.
- Full post-cleanup suites: **EditMode 285/0**, **PlayMode 323/0 with four explicit skips** (the four manual autoplay checks passed separately). Final Core: **379/0**. Raw reports are in `Verification/`.
- Windows development build and production-scene validation passed: **0 build errors**, no runtime errors and no unsupported/error materials in Town, Dungeon or Overworld. All three player captures were reviewed. The build is **510,795,584 bytes**; its 160 warnings and captured compiler-warning lines are retained with the report.
- **Pending:** browser runtime validation of the built integrated WebGL player and isolated fit benchmark.

### Authoring and review entry points

The local [Before/After review](Previews/Diorama/Review.html) selects all 54 views and three matching camera framings. The [item review](Previews/Diorama/Items/Review.html) retains floor and menu views. Test reports and player measurements live in `Previews/Diorama/Verification/`; the latest Core report is `CoreFinal.trx` (379 passed).

Reproduction order, under `Tools / Eternal Enigma` in Unity:

1. `Diorama / Build Ground Style` imports the ten authored ground textures.
2. Run `ArtSource/Diorama/build.py` through Blender MCP, then `Diorama / Import Blender Kit` and `Integrate Vegetation`.
3. `Diorama / Integrate Terrain` and `Integrate Settlements` update the shared TWC template and normalized landmark adapters.
4. `Diorama / Integrate Items` authors the individual and fallback models, icons and currency binding. `Repair Pickup Bindings` independently restores all eleven category references, the separate `Gold` behaviour and the cell-center pivot contract.
5. `Diorama / Apply Art Audit Fixes` reapplies owned material/UI repairs when needed. The one-time dependency cleanup is complete; [CleanupCompleted.csv](Previews/Diorama/CleanupCompleted.csv), [CleanupCommits.csv](Previews/Diorama/CleanupCommits.csv) and [CleanupVerification.json](Previews/Diorama/CleanupVerification.json) retain its results. `Tools/diorama-reference-audit.py` can audit new candidates; do not treat the historical cleanup manifest as new pending work.
6. `Art / Rebuild Town Preview`, followed by the Diorama capture menus, refreshes the saved comparison evidence. `python Tools/diorama-review.py` rebuilds its local index.

Dropped-item roots retain logical cell-corner positions; their model children are centered at `(1,1)` inside the two-unit grid. Decorative props are centered at the origin for their placement layers. Currency and inventory-category Gold use separate behaviour prefabs. NPC portraits use `Image`; the saved hero portrait `RawImage` applies the sprite's atlas UV rectangle and restores its previous UVs when released.

To reproduce development-player validation, use `Tests / Build Windows Player` and `Tools/Invoke-DioramaPlayerValidation.ps1`. The final `Tests / Build Presentation WebGL` build has already succeeded; run `node Tools/diorama-webgl-benchmark.mjs --integrated` against it to close the remaining browser gate, rebuilding only if the matching local output is unavailable or code/assets changed. Running the same Node script without `--integrated` exercises the existing isolated fit build without rebuilding it. Each report describes the GPU-readback measurement protocol; compare its samples separately from ordinary backbuffer FPS.

When changing platforms, select `Tests / Restore Windows Build Target` or `Tests / Select WebGL Build Target` first, then wait for script compilation and domain reload to finish before invoking the build menu. An `Unknown` report with zero duration/bytes is not a completed build.

Windows integrated samples (RTX 2060 / D3D11, 1280×800, 45 warmup + 180 samples with synchronous readback): Town median/p95 **3.65/4.84 ms**, Dungeon **1.99/4.28 ms**, Overworld **3.97/5.69 ms**. The packed-asset report contains **1,012,892 bytes / 16 sources** from Tiny Fantasy World and **16,224,057 bytes / 388 sources** from project-owned diorama art. Wave 4 remains absent from production until task 07 integration. Baked TownPreview meshes and the removed TWC, per-texture Adorable and RPGHero folders each contribute **zero packed bytes**.

## Ground channel and ownership contract

The TWC `OverworldGroundLayer` owns landscape, playable biome, forest floor, mountain floor, roads, town plazas, bridges and in-grid water. `OverworldBiomeRenderer` exposes the TWC owner through its compatibility handle and emits none of those old floors. `OverworldTreeWallLayer` supplies the forest boundary; mountain and coastline silhouettes retain their separate TWC layers. The ocean outside the grid retains its ocean layer.

Dry vertex channels are `Color.rgba = Grass/Sand/Mountain/Forest`, `UV1.xyzw = Snow/Marsh/Ash/Dirt`, `UV2.x = Cobble`. Nine independent 1024² textures preserve every dry biome. Water uses the separately animated cyan texture and bridges use their wood material. Priority: **bridge > explicit water > town cobble > road dirt > playable biome > landscape biome**. Coastal towns stand on sand; parks override town ground to grass, alleys to dirt and main roads to cobble.

Chunks are 32×32 cells, vertices lie on the half-cell grid, transition width is about half a cell. Coordinate hashing perturbs material sampling only; shared vertices keep exact positions across chunk edges. Z depths are dry `0.02`, water `0.045`, bridge `-0.005`. Cell extents remain `[x,x+1] × cellSize`, preserving the former half-cell actor alignment. Town ground includes the four-cell border and cobbled exit approach.
