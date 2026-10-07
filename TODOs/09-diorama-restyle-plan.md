# Diorama restyle and art asset integration

One task covers the TWC environment restyle, imported asset integration, and the art audit findings and cleanup below. Feel, input-prompt and weapon work already assigned to priorities 03/04/05 stays in those tasks; its audit evidence is retained here.

## Execution status (2026-10-07)

User requested all phases, a resumable status record, and a commit after each phase with automatic continuation. Existing TODO consolidation edits are preserved. **No further WebGL builds until final validation.**

### Current checkpoint

All phase implementations and in-scope audit fixes are committed in **`cab69fce`**. Before/After captures are complete and reviewed. **All 50 failures from the pre-cleanup PlayMode run passed focused followups; the clean full EditMode suite passed 285/0.** Reports are preserved as `Verification/PreCleanupPlayMode.*`, `PreCleanupEditMode.*` and the followup files below. All eleven cleanup groups are now removed, and the frozen-GUID verification passed. The full post-cleanup EditMode suite passed 285/0 (206 seconds); the full PlayMode suite passed **323/0**, with four expected manual-only skips, in 45m48s (run `19ef3e5a05cd46bdac0d1f0e2bca433d`). All eleven cleanup groups are committed (`02a12dc6` through `8e435a9e`); Windows player validation passed; the final integrated WebGL build and existing fit-build runtime check are next.

| Phase | Status | Evidence / remaining gate |
|---|---|---|
| 0 — fit and scale | Implemented; final WebGL runtime check pending | H = 1.206013; environment and ten Wave 4 adapters; lit materials/atlas correction; fit tests 2/0; Windows fit samples; style guide and task 07 handoff. Existing fit WebGL build succeeded, 0 errors / 11 warnings. |
| 1 — ground | Implemented and visually reviewed | Nine dry channels, water/bridge priority, one TWC ground owner, ten seamless 1024² textures, town border/approach. Ground + campaign tests 7/0. |
| 3 — foliage/props | Implemented and visually reviewed | 15 source-preserving adapters, biome tree picker, cover, route clearance, reused TownInteriors furniture, authored bed. Vegetation tests 5/0. Same-depth reference comparison accepts broadleaf 2.85H / pine 3.1H. |
| 4 — houses | Implemented and visually reviewed | Modular facades/service sockets, 1.6H doors, 2.2H eaves, overhang/ridges/chimneys/dormers; in-place 9×9 rooms and per-building roof hiding retained. Roof + sleep/save-slot tests 3/0. |
| 2 — terrain | Implemented and visually reviewed | Six cliffs plus concave adapters, eight palettes, rock caps, cyan shore, budgeted tree walls. Terrain 14/14 captures and seam/determinism checks passed. |
| 5 — settlements | Implemented and visually reviewed | Shared house miniatures, grounded Kenney tower/gate/flag adapters, tilted readable signboards and one paving owner. FinalSigns 3/3 merged into After. |
| 6 — items | Implemented and visually reviewed | Per-item prefab/icon overrides, all 11 fallbacks, separate currency pickup, atlas/lit/glass materials, corrected floor poses and half-cell pivots. Item/UI gameplay 5/0; nine floor sheets and three icon sheets reviewed. |
| Audit fixes | Implemented; final regression/build gate pending | Framed NPC portraits, stable class sprites, heading fallback, UI atlas, GameUISkin autoplay panel, result icons, lit enemies, empty Ally anchors, stripped build previews, migrated TWC sample dependencies, WebGL texture limits, shop goods and five town critters. |
| Cleanup | Complete; eleven group commits | Eleven groups removed, about 1.26 GiB of source files. `CleanupCompleted.csv` records exact counts/bytes; `CleanupVerification.json` confirms no remaining targets or retained GUID references. Post-cleanup EditMode 285/0 and PlayMode 323/0, four manual-only skips. All eleven group commits are recorded below and in `CleanupCommits.csv`. |
| Final validation | In progress | Core 379/0, post-cleanup EditMode 285/0 and PlayMode 323/0. Windows build/run passed with three reviewed captures and zero runtime errors/error materials. Final WebGL build/run and fit runtime remain. |

### Completed evidence and corrections

- [x] Portable Blender source roots; `ArtSource/Diorama/build.py`, `.blend` and deterministic JSON retain 35 authored meshes. Only selected vendor FBX Read/Write settings change: 15 environment models and three Kenney models; lists are in `Fit/Selected*FBXs.txt`.
- [x] Before/After **54/54 views** across all eight biomes, with three matching camera framings. [Local review](../Docs/Art/Previews/Diorama/Review.html) and `Stats.csv` retain the comparison. FinalTowns includes all five critters, varied counter goods, and unobscured service doors; corrected sign views are merged into After.
- [x] Same-seed builds match: town **322 meshes / 1,703,422 triangles / 5,990 batched parts**; overworld **1,361 meshes / 2,352,142 triangles / 13,078 parts**. Forest **234,042 triangles / 517 trees**; every tree chunk stays below 96,000 triangles. No art colliders and one ground owner. `Verification/Determinism.txt`.
- [x] Final Core suite **379 passed / 0 failed / 0 skipped**, 3m19s: `Verification/CoreFinal.trx`.
- [x] Focused followups **22/0** (class content, interiors, item bounds, dungeon themes); autoplay manual checks **4/0**; latest item/UI gameplay **5/0** (currency, override/fallback drop and throw, portrait reset, key/mimic, result icons).
- [x] Post-cleanup full PlayMode **323 passed / 0 failed / 4 skipped**, 45m48s: `Verification/PostCleanupPlayMode.*`. The four explicit autoplay checks already passed separately (4/0).
- [x] Post-cleanup full EditMode **285 passed / 0 failed / 0 skipped**, 206 seconds: `Verification/PostCleanupEditMode.*` (run `dc50a486a2bb41e4bfd8f12776df572c`).
- [x] Pre-cleanup full EditMode **285 passed / 0 failed**, `Verification/PreCleanupEditMode.*`. The earlier portrait failure incorrectly expected square portraits; their source is 512×640. Corrected the width-only assertion; runtime `FaceCamDisplay` applies/restores the packed sprite's UV rectangle.
- [x] Currency regression repaired: `GoldPickup.prefab` retains the `Gold` behaviour separately from the enum's inventory fallback. All eleven category bindings are refreshed, and dropped model children again use the logical cell-center offset `(1,1)`. Five gameplay checks pass after the repair.
- [x] Windows fit benchmark: RTX 2060 / D3D11, 1280×800, twelve instances, 45 warmup + 180 samples; monster medians 0.75–2.04 ms, trees 0.35–0.36 ms, empty baseline 0.275 ms. These samples include synchronous GPU readback and are not normal backbuffer FPS. `Fit/WindowsBenchmark.json`.

All evidence paths above are relative to `Docs/Art/Previews/Diorama/`. [DioramaStyle.md](../Docs/Art/DioramaStyle.md) records sources, palettes, scale, authoring menus and the final-player protocol.

### Final player evidence

- [x] Windows development build: **Succeeded, 0 errors, 160 warnings**, 55.5 seconds, **510,795,584 bytes** (`Verification/WindowsBuild.txt`). Captured warning lines are existing obsolete-API, unused-field and member-hiding compiler warnings.
- [x] Windows player: production Town ? Dungeon ? Overworld, **no runtime errors and no unsupported/error materials**. All three 1280?800 captures reviewed. RTX 2060 / D3D11; readback-protocol median/p95 milliseconds: Town **3.65/4.84**, Dungeon **1.99/4.28**, Overworld **3.97/5.69**. See `WindowsPlayerValidation.json` and `Windows_{Town,Dungeon,Overworld}.png`; these are not ordinary FPS measurements.
- [x] `WindowsArtInclusion.csv`: environment pack **1,012,892 packed bytes / 16 sources**; owned diorama art **16,224,057 / 388**; Wave 4 runtime content **0** (task 07 handoff). Baked TownPreview, removed TWC tiles, per-texture Adorable folder and RPGHero all **0**.
- [ ] Final integrated WebGL build/run, existing isolated fit WebGL runtime check, and final evidence commit.

### Active regression findings

Repairs compile without errors; the town preview has been rebuilt with `TownRoofTileOutput` in its matching script file. `TimedBuffStatusEffect.PreventsMenu` now permits actions instead of throwing; deliberate-input detection excludes synthetic stick-direction buttons and detects new keys while another key is held (including a null-key guard). The harness reports live progress/failure details and has **Run Failed PlayMode** to rerun exact failed names from its last XML. Followups completed **33/17**, **15/2**, **0/2**, then **1/1**, preserved as `Verification/RegressionPass1.*` through `RegressionPass4.*`; the final cursor check is **1/0** in `Verification/CursorFollowup.*`. An intervening run was cancelled after exposing the missing null-key guard. Environment rebuilds pass using stable XY placement (marker Z bobs). All originally failing cases are now resolved by these followups.

The remaining cursor check found `CursorManager` disabled in `Common.unity`. It is now enabled, with the authoring pass preserving that setting. Editing the open scene produced Unity's external-change modal; the computer-use skill reloaded the saved scene after MCP timed out. The timed-out test request was already queued, so it was not dispatched again.

The mouse regressions exposed a real UI bug: an empty option-preview panel covered dungeon picker rows. `PartyMenuPicker` now hides the entire panel when descriptions are absent; authoring places dungeon previews beside the dock and disables their raycasts. Both mouse pickers and the UI audit pass, and `Verification/PickerAfter.png` was visually reviewed. The two remaining test corrections distinguish repeated marker names by placement and use an unbound stick click to change prompts without entering a town.

Fixture repairs cover authored UI setup, campaign-slot navigation, explicit save checkpoints, proficient equipment and equipped ammunition, bag-only inventory rows, charging skill completion, status-effect timing, mouse clicks at rect centers, and new ground/settlement ownership. Imported bow definitions are cloned before teardown; bow selection excludes ammunition; melee smoke checks guarantee hit chance; movement fallback selects trap-free space. Mimic fixtures initialize vitals; production HUD tests use a campaign rather than the sandbox that hides it; the custom non-interior building has an empty shop catalog. These changes retain real interaction/persistence assertions and require a green rerun before completion.

Historical failures already resolved include stale class/skill expectations checked against HEAD, an invalid save-slot focus precondition, arrow thickness bounds, the wall-decoration adjacency probe, a too-strict gate width ratio, and a test teardown that unloaded the final scene. TurtleShell now uses a project-owned non-looping hit override, retaining its vendor clip/controller. Apparent result-icon clipping was a capture aspect-ratio issue; the capture now explicitly uses 1280:720.

### Cleanup and landing

The first post-cleanup compile exposed a C# dependency outside the GUID/path audit: `DungeonGenerator.cs` also defined `BSPNode`, `BSPRect` and `BSPPosition`, which the retained legacy tile-grid API references. Those unchanged shared types now live in `Assets/Scripts/Dungeon/Game/BSPNode.cs`. The initial EditMode run was cancelled; compilation now succeeds and the restarted full EditMode suite passed 285/0. The shared-type body is unchanged. GUID/path verification does not replace the compile gate.

`Tools/diorama-reference-audit.py` covers the two new packs and retained assets; `ReferenceAuditBeforeCleanup.json` freezes its findings. `CleanupManifest.json` stores exact paths and GUIDs for eleven groups, including the two unused enemy prefabs. No orphan archive metadata files remain to remove. Read-only build-report prefix filters and guarded historical audit/migration paths are documented exceptions. `Tools/Invoke-DioramaCleanup.ps1` validates normalized paths beneath `Assets` before a named group is removed. Post-cleanup `--verify-cleanup` checks missing targets and retained GUID references.

The already-committed task 05 work supplies dungeon theme lists, TownInteriorCatalog fallback, palette materials and music fields. Task 09 preserves those assets; its dungeon change keeps compact themed props instead of the larger overworld tree picker and raises the outdoor per-piece budget to 250. Clip, sound, animation, footstep, status and VFX polish remain with priority 05. Wave 4 spawn/boss registration remains with task 07. Farts/FartScene are retained.

**Landing adjustment:** phase implementation followed 0 → 1 → 3 → 4 → 2 → 5 → 6, but shared TWC assets, catalogs and authoring APIs were integrated before the user's per-phase commit instruction. The coupled implementation landed in `cab69fce`; cleanup retains one commit per nonempty group after its final gates. Each remaining phase is committed and followed automatically by the next. Existing TODO consolidation edits remain in the working tree.

### Cleanup commit checkpoints

| Group | Commit | Tracked files |
|---|---|---:|
| legacy-tilemap | `02a12dc6` | 517 |
| twc-samples | `2dee505b` | 942 |
| rpg-hero-placeholder | `68ce971c` | 89 |
| unused-enemy-prefabs | `3fd7360f` | 4 |
| adorable-per-texture | `65130951` | 1639 |
| environment-duplicates | `f8af6528` | 1096 |
| legacy-biome-swatches | `01afd637` | 24 |
| recovery-scenes | `d341a17a` | 5 |
| unused-wfc | `1742ddc1` | 22 |
| skill-contact-sheets | `e1a783d6` | 18 |
| kenney-duplicate-models | `8e435a9e` | 142 |

### Resume order

1. Full post-cleanup suites are complete: EditMode 285/0; PlayMode 323/0 and four expected skips. The implementation checkpoint is `cab69fce`; deletion and frozen-GUID verification are complete. Do not regenerate the cleanup manifest after deletion.
2. Clean full reports are saved as `Verification/PostCleanupEditMode.*` and `PostCleanupPlayMode.*`. Confirm Unity has finished test cleanup before changing assets.
3. Cleanup commits are complete (`02a12dc6` through `8e435a9e`); Windows build/run and visual checks passed; continue with final WebGL validation.
4. **Final stage:** Windows passed. Build/run the final integrated WebGL player and run the existing fit WebGL build. Save three production-scene captures, frame samples, shader-error counts and per-pack build inclusion.
5. Update this checkpoint with final results and commit IDs, regenerate the review index, and retain only intended font fallback changes rather than generated font-atlas noise.

## Context
The art references (`ArtRefs/`, Pokémon BDSP style) look like a lit, handcrafted miniature: soft-edged dirt paths, blended ground, tiered cliffs, dense rounded foliage, oversized iconic props and storybook houses. The game currently looks like a flat tile map:
- hard 90° cell edges between surfaces
- grey flagstone roads everywhere
- 28–34-triangle crystal-like trees, scattered sparsely
- tiny props
- cube-built facades

**Keep:** the Polyart assets (`Assets/Art/RPGTinyHeroWavePolyart`, `RPGMonsterBundlePolyart`) stay untouched and set the scale and style anchor. Everything else is restyled to match them.

**Focus areas:** ground, prop size, houses, overworld.

**Hard constraints:**
- Environment geometry must be produced through TileWorldCreator (TWC) layers.
- Core masks still decide gameplay; art adds no collision.
- Generation stays deterministic for a given seed.
- Towns keep their in-place 9×9 rooms with roofs that hide when the hero enters.

**Decisions from the user:**
- Houses keep the in-place rooms; restyle only.
- Test budgets may be raised.

**Current sourcing approach:** evaluate the imported environment pack first, then author missing pieces and adapters in Blender via MCP.

**Decisions after the plan audit:** use project-owned Built-in lit material variants; keep a distinct ground look for every biome; replace overworld rendering surface by surface; allow Read/Write import-setting changes only on selected vendor FBXs needed for TWC batching; redesign facade sign/decor placement; hand Wave 4 enemy integration to the encounter task; use lit materials for Adorable items.

## Imported Dungeon Mason assets (integration pending)
Commit `2d16e05d` imported the vendor assets; no project TWC layers or dungeon enemy prefabs were changed in that commit. Import is complete, but selection, adaptation and gameplay integration are still open.

- **Environment:** `Assets/RPG Tiny Fantasy World 01 PA/` contains 229 art prefabs: 45 building/utility/deco, 24 ground padding, 23 land mass, 14 mountain, 66 river/road/lake/fall, 40 rock and 17 tree/plant. It also includes two scene-setting prefabs. The earlier 34-piece waterways count was incomplete. The main material is named `DefaultPBR.mat`; check its actual appearance beside the Polyart hero before adopting it as the style source.
- **Monsters:** `Assets/RPGMonsterWave4Polyart/` contains ten character prefabs: BlueSlime, BoneDragon, CastleMonster, Crawler, FlameKnight, FlowerMonster, IceGolem, LittleDemon, SkeletonMage and StarFish. It also contains animation clips/controllers and Polyart materials. Treat them as candidate dungeon enemies and boss visuals; they are not yet registered in our enemy authoring or spawn pools. Measure their triangle counts and WebGL frame time before choosing floor bands or boss roles.
- Keep vendor meshes, prefabs and materials unchanged except for documented Read/Write settings on the selected FBXs used by TWC batching. Put collider-free, correctly oriented prefab variants and Built-in material variants in project-owned folders.

**Phase 0 fit spike (record results in `Docs/Art/DioramaStyle.md`):**

1. **Grid fit:**
     - Land masses and mountains are freeform diorama pieces, not cell tiles.
     - Check whether ground paddings, roads and mountain pieces tile on a 2-unit cell, or can be cut into Edge/Outer/Inner/Fill sets for TWC tile layers.
     - Freeform pieces can only be used as hash-placed cosmetics or as border/backdrop dressing.
2. **Axes:** Unity Y-up prefabs versus our XY ground with −Z up. Placement layers must apply the same −90° X correction that `BiomeDecorationAuthoring` uses.
3. **Collision and mesh access:** many imported environment prefabs contain colliders, and the checked FBX import settings have `isReadable: 0`. Remove colliders in project-owned variants. Enable Read/Write on the specific vendor FBXs selected for `EnvironmentBatch`, document that list, and leave other vendor import settings alone.
4. **Biome tinting:**
     - One shared atlas means per-biome palettes are done by atlas swap (snow, ash, marsh variants) or a tint/hue shader.
     - `PaintedEnvironmentAuthoring` material resets must not touch these materials.
5. **Houses:** the 9×9 in-place rooms and hide-on-enter roofs must still work. Check whether building meshes can be split into facade and roof parts; otherwise keep our facade cells and borrow only roofs, trims and dressing.
6. **Budgets:** measure triangles per prefab (LOD0 and LOD1) against the triangle budgets in `OverworldCosmetics` and the limits in `EnvironmentKitTests`. The wind shader must compile for WebGL on the Built-in pipeline.
7. **Material compatibility:** Unity MCP currently resolves both `DefaultPBR.mat` and Wave 4 `DefaultPolyart.mat` to `Hidden/InternalErrorShader` in this Built-in project. Make project-owned Built-in lit variants before the fit captures. Validate every selected environment and Wave 4 material in Unity and WebGL, including special water and foliage effects.

**Candidate uses, subject to the fit spike:**
  - **Phase 1:** keep `PaintedGroundMesh` and the splat shader, and paint the splat textures from the bundle atlas palette so ground matches the props. Ground paddings and roads may become edge-trim pieces instead of Blender grass-lip trims.
  - **Phase 2:** the bundle's mountains and rocks replace the Blender cliff kit wherever they fit the grid. Its water and falls shaders feed the coastline work.
  - **Phase 3:** the bundle's `Tree01`–`05` supply the main town and overworld trees; its plants and rocks replace most Blender foliage and props. Blender fills missing biome-specific forms and adapters.
  - **Phase 4:** source roofs, trims, signs and dressing from the bundle's buildings and deco, within the in-place room constraint above.
  - **Phase 5:** overworld settlements and landmarks use the bundle's buildings at overworld scale.

## Key facts from exploration
- **Custom TWC build layer pattern** (`EnvironmentSmartTileLayer.cs`, `OverworldOceanLayer.cs`, `TownEnvironmentLayer.cs`):
  - Class shape: `[Serializable, ActionName] class X : TWCBuildLayer`.
  - `Clone()` copies every field.
  - `Execute` reads `creator.GetGeneratedBlueprintMap(guid[+"_UNSUBD"])`, emits through `EnvironmentBatch`, and always increments `creator.executedBuildLayersCount` in `finally`.
  - Renderers without an `EnvironmentMeshOwner` are hidden (`OverworldBiomeRenderer.cs:70-72`).
- **Overworld broad ground bypasses TWC.** `OverworldBiomeRenderer.Draw` (`:75-132`) builds hard-edged per-biome quads after the build completes. Open work in `TODOs/08-twc-biome-styling-plan.md` already asks to move this into a TWC build action with one rendering owner.
- **Overworld blueprint masks** come from `CampaignOverworld.Apply` (`CampaignOverworld.cs:65-126`) through `CampaignLayerAction` and `SmartEnvironmentMasks.World`.
- **Town:**
  - Ground comes from `TownEnvironmentLayer.cs:31-34,70` (Paving + `Kit.Ground`).
  - Facades are cube boxes in `TownHouseTiles.cs`, called from `EnvironmentSmartTileLayer.cs:56-111`.
  - Roofs come from `TownRoofTileLayer.cs:44-89`.
  - The layers are rewired at runtime by `CoreTownLayerGenerator.Configure`.
  - Cell = 2 world units; XY ground, −Z up.
- **Prop scales:**
  - `TownEnvironmentLayer.cs:40,56,89` (`size*.45` props, `.85–1` trees)
  - `BiomeDecorations.cs:45` (`size*.8` lamps)
  - Catalog TownScale .8 / OverworldScale .45
  - `OverworldCosmetics.cs:20-21,71` (density 150/25 per 1000, 48 per chunk, 120k triangles)
- **Hero height is roughly 0.9–1.0 world units (about ½ cell).** This is estimated and must be measured.
- **Textures:**
  - `PaintedEnvironmentAuthoring.WriteSurface` makes 1024² seamless PNGs, and tests enforce this.
  - Re-running `Materials()` resets biome material colours and scales.
- **Blender pipeline:**
  - `ArtSource/BiomeDecorations/build.py` runs through Blender MCP, but `ROOT` is hard-coded to a different machine path.
  - Imports go through `BiomeDecorationAuthoring` (−90° X).
  - Kit meshes live in `Assets/Art/EnvironmentKit/Meshes/*.asset`; keep GUIDs and Read/Write enabled.

## Phase 0: Prerequisites and scale ladder
1. **Use Blender MCP when authoring missing meshes.** Make `build.py` resolve `ROOT` from the script location instead of the hard-coded path, and apply the same fix to the TownInteriors `author.py`.
2. **Measure Polyart hero bounds** in Unity (via `Tools/unity-mcp.mjs`) and write `Docs/Art/DioramaStyle.md` with:
   - the style rules from the earlier style guide
   - a scale ladder in hero heights (H): fence 0.6H, barrel/crate 0.5–0.6H, sign 1.1H, bench 0.5H, lamp 1.8H, door 1.6H, eaves 2.2H, broadleaf tree 2.5–3H, pine 3–3.5H, boulder 0.6–1H. Treat the tree numbers as starting targets and adjust them from same-depth gameplay-camera comparisons with the player in `ArtRefs/Untitled2.png`, `Untitled3.png`, `Untitled7.png` and `Untitled8.png`.
   - the palette per biome, using warm yellow-green grass and tan dirt as the base
3. **Capture a baseline** with the existing `PaintedEnvironmentCapture` (Before set) using the gameplay camera.
4. **Evaluate the imported environment pack:** run the fit spike above, place 3–4 candidate pieces next to the Polyart hero in one capture, then record the source choice for each phase in `Docs/Art/DioramaStyle.md`.
   - Include the most suitable `Tree01`–`05` canopy shapes beside the hero, measured at the same ground depth. Record their native bounds, chosen scale multipliers and player-to-tree height ratio from the gameplay camera against the reference photos; identify any missing pine form.
5. **Evaluate Wave 4 separately:** capture its ten monsters beside the existing enemies with the Built-in material variants, check animations/materials/colliders, measure LOD triangle and WebGL costs, and pass the shortlist to `TODOs/07-overworld-encounters-plan.txt` for enemy prefab, spawn-band and boss integration.

## Phase 1: Ground as TWC layers (largest visual win)
1. **Shared builder `PaintedGroundMesh`** (new, `Assets/Scripts/Environment/`):
   - Builds chunked (32×32) ground meshes. Keep a distinct ground appearance for Grassland, Desert, Water, Mountain, Forest, Tundra, Marsh and Volcanic, plus dirt paths and town cobble. Specify the per-biome surface mapping, texture/channel packing, transition weights and priority where biome, road, bridge, plaza and water masks overlap. If one chunk needs more than four textures, split it into compatible submeshes or use a shader that supports the required inputs; do not merge biome looks to meet a four-channel limit.
   - Writes per-vertex splat weights, with vertices on a ½-cell grid so edges blend over about half a cell.
   - Applies hash-based edge jitter so borders are organic and deterministic.
2. **Shader `PaintedGround.shader`** (built-in pipeline surface shader, matte):
   - Blends the textures required by the chosen chunk/submesh strategy by vertex weight.
   - Breaks transition edges with a noise mask.
   - Adds a darker rim where dirt meets grass.
   - Handles water separately.
3. **Overworld layer `OverworldGroundLayer : TWCBuildLayer`:**
   - Bind all eight Core biome masks to semantic ground inputs in `CampaignTerrain.asset` (or reuse their existing blueprint layers directly), plus a path input from Core Roads. Keep bridges and water explicit in the surface-priority table. Town cobble comes from town masks.
   - `CampaignOverworld.Apply` fills any new inputs from the Core biome and Road layers without changing the Core grid.
   - Transfer one surface at a time from `OverworldBiomeRenderer`/Smart Roads to its TWC owner. Record ownership for landscape, playable biome, roads, bridges, in-grid water, forest and mountain surfaces at each phase; disable the old draw for each transferred surface so no gaps or overlapping floors remain.
   - Preserve the existing half-cell XY offset and surface height ordering during the transfer. Keep a `RenderedSurfaces` equivalent for tests until the last old surface is retired.
   - Add the selected textures, materials, source meshes, surface mapping and TWC template/build-layer configuration to the cache identity in `OverworldTerrainCache` so a changed art style forces a rebuild.
4. **Roads become dirt paths:**
   - Overworld roads and town alleys use the Dirt splat channel instead of the flagstone `SmartRoad` quarter tiles.
   - Town `MainRoads` and plazas keep a cleaner, warm cobble flagstone, so paving is reserved for landmarks.
   - The Smart/Roads layer keeps its masks but emits only grass-lip edge trim pieces (Blender) along path borders.
5. **Town:** `TownEnvironmentLayer` uses `PaintedGroundMesh` with the town masks (Parks → grass, Alleys → dirt, MainRoads → cobble), replacing its floor Paving + `Kit.Ground`. Preserve the four-cell border floor, the exit approach, boundary walls and gate visuals in the same build layer.
6. **New painted textures:**
   - Grass (yellow-green with clover variation), dirt, cobble, sand, snow, ash, marsh, and a saturated cyan water texture.
   - All 1024² and seamless, made through the `ArtSource/PaintedEnvironment` → `WriteSurface` flow.

## Phase 2: Overworld terrain silhouettes
1. **Cliff kit (imported mountains where grid fit passes; Blender for missing adapters):**
   - Stratified, rounded-strata cliff pieces with a grass lip on top: Edge, Outer, Inner and Fill for each biome tint.
   - These replace the six-terrain prefabs on `Smart/Mountains Base/Tier 2/Tier 3` (keep `HeightScale 1` so the prefab path is used).
   - Summit pieces become rounded rock caps.
2. **Coastline:** foam band and shallows tint in `SmartShoreline.shader`, a cyan `Ocean.mat`, and scattered sea-rock props (cosmetic only, never on walkable cells).
3. **Forest cells:**
   - Replace the raised pyramid cells (`OverworldBiomeRenderer.cs:58,101-107`) with a new `Cosmetic/Tree Walls` TWC build layer that places dense, grid-aligned tree clusters on Core `Trees` cells.
   - Use small jitter and alternate pine and broadleaf trees by biome.

## Phase 3: Foliage and props (imported candidates plus Blender gaps)
1. **Use the imported trees and fill biome gaps:**
   - Select the best silhouettes from `Tree01`–`05` as the primary trees in `TreeModelPicker`, `TownEnvironmentLayer`, `OverworldCosmetics` and the new tree-wall layer. If the pack lacks a convincing pine, author only that missing form in Blender. Keep the selected pack trees' mesh identity; make project-owned orientation/material variants and use only selected Read/Write import changes as needed for batching.
   - Scale these trees so their visible height relative to a nearby player matches the reference photos from the same camera angle. Apply one measured `DioramaScale` rule across town, overworld props and tree walls, with an explicit context multiplier only if the cameras require it. Check trunk width, canopy width, ground contact and route clearance as well as height.
   - Author only missing forms after the fit spike: SnowPine, Palm, Willow/Mangrove, Dead and Charred trees.
   - For new Blender meshes, bake a dark-to-light gradient and occlusion, and keep a base-centred pivot in the XY / −Z convention.
2. **Ground cover:** test imported `Grass01`–`07` and `Flower01`–`05` at gameplay scale; author missing tall-grass clump, fern, mushroom ring and reeds.
3. **Town dressing: reuse first, author only the gaps.**
   - **Reuse:** `Resources/TownInteriors` already has Barrel, Crate, Sack, Bench, Basket, Luggage, Lantern, Plant, PerchSign, Banner, Dummy and Target, plus ambient birds. They are XY/−Z, use the shared palette, and are batched by `TownInteriorRendering.cs:38-41`. Place them outdoors through `TownInteriorCatalog` from `TownEnvironmentLayer`, scaled by the Phase 0 ladder.
   - **Check the imported pack first:** `WoodFence01`–`05`, `SignPost01`–`06`, `WoodBarrel01` and rocks may cover some gaps. Author only missing fence adapters, flower box, mailbox, readable two-post signboard and boulder sizes in Blender.
   - **Related:** replace the runtime cube bed in `HomeBed.cs:26-39` (which uses `Shader.Find("Standard")`) with `TownInteriors/Bed.prefab`.
   - **Optional Adorable markers** (see the asset audit below): exclamation/question mark over service NPCs; hen, duck and butterfly as ambient critters on Park cells.
4. **Placement (deterministic hash, protected cells unchanged):**
   - Overworld (`OverworldCosmetics.Plan`):
     - tufts along path edges
     - noise-driven flower and tall-grass patches on Ground cells
     - boulder clusters near cliffs
     - raise the caps: about 160 props per chunk, triangle budget set from measurements
   - Town (`TownEnvironmentLayer`):
     - fences around Parks edges
     - dressing clusters beside doors (barrel, crate, flower box) on cells that are already non-walkable or decorative only
     - the border becomes a dense tree wall
5. **Scale:** apply the Phase 0 ladder everywhere through one `DioramaScale` table, replacing `size*.45`, `.8`, and the catalog TownScale/OverworldScale.

## Phase 4: Houses (same Core footprints, in-place rooms)
1. **Facade kit (extract usable imported building parts; Blender for missing modular pieces):**
   - plaster/timber wall panel
   - framed window panel with flower box
   - door panel with step, awning and lamp
   - corner post
   - stone base course
   - per-biome tint through the existing palettes
2. **`TownHouseTiles.Facade`:** swap the cube boxes for these meshes while keeping the cell rules (door on the south face when the cell south is a door, windows at `(x+y)%2==0`). Facade height follows the ladder (eaves about 2.2H). Replace the old `BiomeDecorationSurfaceSet` facade faces with faces/sockets derived from the new meshes.
3. **Roofs (`TownRoofTileLayer` / `RoofTile` / `GableTile`):**
   - chunky shingle roof with a thick overhanging eave, ridge cap and light edge highlight
   - add chimneys and dormers from a deterministic per-building hash
   - a bold roof colour per biome
   - keep: one roof per building, no colliders, hide when the hero is in the room
4. **Service identity and decoration placement:** redesign facade sockets for the new wall, window, door and awning shapes. Place a large hanging sign or emblem per service (Inn, Shop, Trainer) through the new sockets. Verify signs and wall decorations neither float nor clip, and keep the roof hide-on-enter behavior.

## Phase 5: Overworld settlements and landmarks
1. Replace the POI plaza cubes (`OverworldScene.cs:188-210`) and `Smart/Houses` boxes with miniature house clusters built from the Phase 4 kit at overworld scale.
2. Replace the stretched-plank signpost in `BiomeRoadSigns` with a readable imported `SignPost` variant or an adapted two-post signboard.

## Phase 6: Item visuals from the Adorable pack
**Context:**
- Items shown on the floor and in menus come from 11 shared `DroppedItemVisual` categories (`Scripts/Dungeon/Item/DroppedItemVisual.cs`), not from the items themselves.
- Each category is one prefab in `Prefabs/Dungeon/DroppedItems/`, listed in `TileWorldDungeon.prefab` `DroppedItemPrefabs`.
- Ten of these prefabs use models from `Assets/Art/3D Props - Adorable Items` ([LAYERLAB, Asset Store 31085](https://assetstore.unity.com/packages/3d/props/3d-props-adorable-items-31085), Built-in pipeline only). Sword uses `Art/RPGHero/Meshes/Sword.fbx` instead.
- Menu icons are renders of those prefabs: `UnifiedPresentationAuthoring.cs:19-25` builds `GamePresentationProfile.ItemIcons[11]`.
- So many items look alike:
  - all 6 potions look identical
  - all 3 spells look identical
  - Bread, Charred Bread and Spoiled Bread (`Resources/TrapFood`) all show the waffle
  - Timber shows the waffle too
  - wands show as a **ring**
  - spears and greatswords show the one-handed sword
- The pack has about 190 models (`Adorable 3D Items/FBX`), and most are unused.

**Goal:** every item reads as itself, using Adorable models wherever one fits.

1. **Per-item override (code):**
   - Add an optional `DroppedItemPrefab` (DroppedItem) and `Icon` (Sprite) to `ItemDefinition`. When unset, the item falls back to today's category, so existing items stay unchanged.
   - Use the override first at:
     - `DungeonPlacement.cs:56` (placement check)
     - `TileWorldDungeon` spawn
     - `ActionDialog.cs:39-40`
     - `PartyMenuContexts.cs:36` (icon)
   - `MaterialCatalog.Create` (`Scripts/Dungeon/Item/MaterialCatalog.cs:49-62`) builds its definitions in code, so set the override there too.
   - This is better than growing the enum, which is serialized by index in every item asset and sized as `new Sprite[11]` in the icon authoring.
2. **Prefabs:**
   - Make one `DroppedItem` prefab per new model in `Prefabs/Dungeon/DroppedItems/`, copying `Bread.prefab`'s setup (model as a child, same pivot and lift, no colliders).
   - Give each model child an authored rotation and offset so its recognizable face reads from the dungeon gameplay camera. Check the same prefab through the menu-icon capture camera; use a separate icon pose or camera framing where one pose cannot serve both views. Apply this to the ten existing dropped items as well as every new one. Keep those transforms in project-owned prefabs, and capture each item at its final floor scale.
   - Use tinted material variants where one model serves several items.
   - Use the **`Adorable 3D Item_Atlas`** prefabs (5 shared materials). Today every reference points at the per-texture `Adorable 3D Items` variant (212 materials), which can't batch; see the asset audit below. Move the existing 10 dropped items, the bag icon and `Enemy_ChestMonster` over as well.
   - Check the bag icon, `Enemy_ChestMonster`, town shop-counter goods and any Adorable markers or critters from their own gameplay and icon camera angles before accepting their orientation.
   - The pack materials are Unlit/Texture. Make one lit material variant in our own folder, matching the character lighting decision, rather than editing the vendor folder.
3. **Mapping (starting point; confirm each one in a capture):**

   | Item(s) | Today | Adorable model |
   |---|---|---|
   | Bread | waffle | keep `waffle` (the pack has no bread model) |
   | Charred Bread / Spoiled Bread | waffle | `waffle` + dark / green tint variants |
   | Potion | potion_red | keep `potion_red` |
   | SP Potion | potion_red | `potion_blue` |
   | Antidote | potion_red | `jar` (green tint) |
   | Sleep Potion / Frailty Potion | potion_red | `potion_blue` purple tint / `potion_red` dark tint |
   | Oblivion Draught | potion_red | `ink` |
   | Bolt / Explosion / Warp Spell | book2 | `book2` yellow / red / violet tints, or `book` for one |
   | Timber | waffle | `log` |
   | Healing Herb | potion_red | `leaf` |
   | Iron Ore | treasure chest | `jewel` (grey tint) |
   | Trap Parts | key | `wheel` or `magnet` |
   | Gold | gold coin bag2 | keep |

   The pack has no wand, spear, greatsword or arrow models. For those, use the matching Tiny Hero weapon meshes (`Art/RPGTinyHeroWavePolyart/Mesh/Weapons`) in the same way `Sword.prefab` uses the RPGHero sword. This fixes wands showing as a ring.
4. **Icons:**
   - Extend `UnifiedPresentationAuthoring` to also render each override prefab to `Resources/UI/ItemIcons/` and assign it to `ItemDefinition.Icon`.
   - Keep the category icons as the fallback.
5. **Scale and shading:** set floor items to the Phase 0 ladder (pickups about 0.4–0.5H) through `DioramaScale`, so they don't fight the restyled props. Use project-owned Built-in lit material variants for the Adorable atlas models and check their appearance beside the hero and enemies.
6. **Tests:**
   - Every `ItemDefinition` resolves to a dropped prefab and an icon, either its override or its category.
   - No two items sold in the same shop share both a model and a tint.
   - Existing `DungeonPlacement` and `ActionDialog` paths still pass with no override set.
   - Review the floor pickup and menu icon captures for every mapped item: the silhouette, facing, tint and scale must identify the item without clipping or hiding its key feature.

## Out of scope (follow-ups)
- Full lighting and shadow redesign (the character/item material mismatch in the audit remains in scope)
- Post-processing
- Camera zoom
- Full dungeon theme redesign (the audited biome wall-texture fix remains in scope)
- KennyNL interior props, and remodeling the Adorable meshes to Polyart proportions

## Tests to update
- **`EnvironmentKitTests`:** model count of 68, ≤300 triangles, trees ≤120 triangles / ≤1.05 footprint, cosmetic budgets → new values.
- **`OverworldSixTerrainTests`:** "exactly 5" layers and the prefab facing checks → new cliff kit.
- **`PlayMode/OverworldSceneTests`:**
  - replace the `RenderedSurfaces` checks with `OverworldGroundLayer` owner checks
  - fix the stale distinct-`_Color` assertion
- **`EnvironmentPlaygroundTests`:**
  - gallery count: `Kit.Models.Length + 48`
  - Houses `BuildingMaterial` check, which is already stale
- **`TownAndPropVisualTests`:** prop ≤1.45 → ladder limits.
- **Tree scale checks:** selected pack trees are used in town and overworld, and their world bounds follow the measured player-height ladder without entering protected routes or door approaches.
- **`PaintedEnvironmentTests`:** new textures must pass the 1024/seamless check.
- **Ground ownership tests:** for each migration phase, assert one visible owner per surface, distinct biome materials, road/bridge/water precedence, half-cell alignment, and town border/gate coverage.
- **Material validation:** selected imported prefabs must resolve to working Built-in shaders in Unity and WebGL; none may show `Hidden/InternalErrorShader`.
- **Must still pass unchanged:**
  - Core town tests (`TownDetailTests`, `TownLayoutTests`, `TownInteriorTests`)
  - `TownGameplayTests` (roofs)
  - `BiomeDecorationTests` sign rules
- **`CampaignOverworldTests`:** update for the new template blueprint bindings while preserving Core mask topology, movement and same-seed output.

## Verification
1. EditMode and PlayMode suites via `Tools/harness-editmode.json` / `harness-playmode.json` through `Tools/unity-mcp.mjs`, plus `dotnet test` for `Core`.
2. **Same-seed determinism:** build the overworld and a town twice and compare cosmetic and ground triangle counts.
3. **Captures:**
   - Re-run `PaintedEnvironmentCapture` (After set), "Art > Rebuild Town Preview" and the UnifiedPresentation gameplay-camera captures.
   - Compare side by side with `ArtRefs/` for: blended path edges, a tree wall framing the route, hero-to-door and hero-to-prop scale, roof read, terraced cliffs. Include same-depth player-and-tree views in town, an overworld route, and a dense forest; compare their on-screen height and canopy spacing with `Untitled2.png`, `Untitled3.png`, `Untitled7.png` and `Untitled8.png` before accepting the tree multipliers.
4. **Performance:** frame samples (`UnifiedPresentation/FrameSamples.txt` flow) on the Windows and WebGL builds; triangles per chunk within the new budgets.
5. **Order of landing:** Phase 0 material/asset gates → 1 → 3 → 4 → 2 → 5. Each phase lands as its own commit, with tests green and captures updated. Phase 6 can follow the Phase 0 lighting decision and scale ladder; its prefab and icon orientation captures are required before it lands.

## Asset audit findings and cleanup

> Pending findings and supporting evidence. Checkmark glyphs below mark findings,
> not completed fixes. Tasks overlapping priorities 03/04/05 are tracked there;
> the restyle and asset cleanup work belongs to this task.
> Revalidate historical dependency counts before asset cleanup.

### Scope and method
**Scope:** third-party and generated art or audio packs under `Assets/` as of the audit, before commit `2d16e05d`. Read-only; no assets were changed. The newly imported `RPG Tiny Fantasy World 01 PA` and `RPGMonsterWave4Polyart` packs have not been included in the GUID/reference or build-weight findings below.

**Method:**
- A GUID reference index over all YAML assets and `ProjectSettings`, plus a grep of scripts for asset paths.
- One deep-dive per pack group: characters and VFX, UI/2D/audio, environment, props.
- Spot-checked claims are marked ✔.

**Related tasks** (findings already covered there are not repeated):
- `TODOs/04-weapon-availability-plan.md`
- `TODOs/03-input-prompts.md`

**Post-import audit:** before any cleanup or build-size claim is applied to the new packs, extend the GUID index to both folders and measure their references, import settings, shaders, triangle counts and build inclusion. Their fit and integration gates are in the diorama phases above.

### Remaining high-value findings

The original audit IDs are retained below so earlier references remain traceable. Code fixes that have landed since the audit are recorded after this table.

| Audit ID | Finding | Fix | Effort | Value |
|---|---|---|---|---|
| 4 | **NPC portraits are never shown.** `TownNpcDefinition.Portrait` is set for 7 NPCs, but `TownNpc.Greet` (`Town/TownNpc.cs:13`) shows text only. Bamao `QUEST/avatar_bg_*` frames are unused. | Show the portrait in the `TownMenu.ShowMessage` greeting, framed. | S | High |
| 5 | **UI confirm/cancel sounds are assigned but never played.** `Confirm`/`Decline`/`Denied` are set in `Scenes/Common.unity`, but the only UI sound is the hover in `NavigationHandler.cs:54`. | Play them on submit, cancel and invalid actions (`MenuUIInputModule` / `GameUISkin.Button`). | S | High |
| 6 | **Unused hero animations with ready hooks.** Victory, LevelUp, Defend, Dizzy, Combo, Dash, DrinkPotion and Greeting are all in the pools but never played (only 5 of 13 actions are used). | LevelUp at `MovementAction.cs:570`; Victory on floor clear; Defend for Guard/Parry/Bulwark; Dizzy for Stun/Confusion; Combo/Dash for Double Strike, Whirlwind, Lunge and Shadow Step; DrinkPotion on potion use. | M | High |
| 7 | **VFX sameness.** 20 of 31 statuses use one aura, `AuraSimpleShadow` (`CombatEffectAuthoring.cs:137`), and skills share one effect per family. Fitting MagicArsenal families are unused: Curse, DoT, Shields, Enchant, Walls, Pillar Blast, Beams, Slash, Charge, Orbital. | Add per-status and per-skill overrides in the authoring script and wrap only the prefabs that get picked. | M | High |
| 8 | **Footsteps.** `Sounds/RPG_Essentials_Free/12_Player_Movement_SFX` has 12 step/jump/landing clips; none are used. | Add a footstep per surface or biome on move: dungeon (`MovementAction`), town player, overworld. | M | High |
| 9 | **Lighting mismatch between characters.** ✔ The hero `DefaultPolyart.mat` uses Standard (lit, `_EMISSION`). The enemy `PolyartDefault.mat` and every Adorable item material use built-in shader 10752 (Unlit/Texture, per two audits). Enemies and floor items ignore scene lighting. | Pick one lighting model before the diorama restyle (add a lit material variant in our own folder). | S | Medium–High |

### Completed since the original audit
- The unused `ImportedEffects` catalog field and its authoring assignment are gone. Recheck build contents before quoting the former 517-prefab size impact.
- `HeroAnimator.MatchesClip` now excludes held poses, start/maintain clips and clips from the wrong stance. Priority 05 still owns verification of serialized hero animation pools.
- `PaintedEnvironmentAuthoring` now maps dungeon boundary materials to Masonry, MossMasonry, IceMasonry or BasaltEmbers by biome. Verify the generated material assets before closing the visual check.
- `StatusVisualProfile.Icon` is populated on status assets, and `DungeonPartyCard` displays those icons with a text fallback. Priority 05 can retain any remaining presentation polish.

### Characters and VFX
- **Enemy animations.** `Enemy.cs:178-219` plays only Idle, Walk, Attack, GetHit and Die. Every monster also has Taunt, Victory, Dizzy, SenseSomethingStart and IdleBattle. Hooks: wake from dormant (`Enemy.cs:157`), the Taunt status, Stun, and a hero going down. (M / Med)
- **Mask-tint is unused in gameplay.** Every monster has a `*PAMaskTint.prefab` (`RPGMonsterBundlePolyart/CommonStuffs/Prefab/Wave0x`). The shader already works in the main menu.
  - Golem and StingRay are each spawned in two floor bands with the same prefab, which are natural slots for tinted elite or biome variants.
  - The hero `PolyartMaskTint.shader` could also give per-recruit colour variety; the 24 `Ally_MC*` looks are fixed. (M / Med)
- **Dead enemies.** `Enemy_MushroomSmile` and `Enemy_LargeSlime` are never spawned. LargeSlime looks like a duplicate of `Enemy_Slime_Big`. Spawn or delete them. (S / Low)
- **RPGHero placeholder ships.** `Prefabs/Dungeon/Ally.prefab` nests `RPGHeroHP` (a 2048² hand-painted texture), only for it to be destroyed by path (`Ally.cs:321`, `AllyGenerator.cs:37`).
  - Replace it with an empty anchor of the same name.
  - Delete the unused `Prefabs/Dungeon/Enemy.prefab`.
  - `DroppedItems/Sword.prefab` uses Unity's default material.
  - After Diorama Phase 6, the whole RPGHero pack (52 MB) can go. (S / Med)
- **Skill icons are reused.** 201 skills use 131 icons; the `blue/9954` fallback (`CombatEffectAuthoring.cs:211`) covers 15 skills. (S / Med)
- **Double skill audio (needs a listen test).** 120 catalog VFX prefabs contain play-on-awake `AudioSource`s, and skills also play `CastSound` (`SkillAction.cs:95`, `CastSpellAction.cs:20`). Choose one source per skill. The unused MagicArsenal Cast/Impact wavs are per element. (S–M / Med)
- **Hygiene:**
  - `AllyGenerator.RemoveAllTransitions` edited the vendor controllers in place; re-importing the pack would undo it.
  - There are two arrow visuals: `Projectile_Arrow` and `Cyclops_BigArrow`.
  - Orphan `.zip.meta` / `.unitypackage.meta` files.
  - Vendor demo scripts still compile. (S / Low)

### UI, 2D, audio
- **Music.** The dungeon uses one track for every theme and boss. `Ambush Transition.mp3` and Bamao `Guitar-Gentle.wav` are unused. Add a music field per `DungeonThemeCatalog` theme, plus a boss/ambush cue. (S / Med)
- **Music import settings.** 11 mp3s are Decompress On Load at quality 1.0. Add a Standalone override: Streaming at about 0.5. Short SFX could use ADPCM. (S / Med)
- **Player-facing IMGUI.** The autoplay panel and the "Stop autoplay?" prompt (`Common/AutoplayRunner.cs:725-790`) use `GameSkin.guiskin`, which falls back to built-in Arial. Rebuild them with `GameUISkin`. (M / Med)
- **2D item icons.** Bamao `Shop/icon_*` (about 40: swords, shields, armour, mana potions, bread, meat, cheese, keys, bombs, crystals) can fill `ItemDefinition.Icon` where Diorama Phase 6 has no 3D model. (S / Med)
- **Button-prompt glyphs.** None exist in the project. `InputPromptsTODO` needs an external CC0 set (e.g. Kenney Input Prompts) built into a TMP sprite asset. (M / Med)
- **Fonts.** The Bamao "Magical Neverland" font is applied only by an editor pass (`Editor/GameUIButtonAuthoring.cs:133`) to labels named "title"/"header", and has no fallback. Apply `GameUITheme.HeadingFont` everywhere, and add a fallback font. (S / Low–Med)
- **Sprite atlases.** There are none. Atlas the roughly 57 used Bamao sprites, `Resources/UI` and the portraits. (S / Low–Med)
- **Class icons** load by display name (`MainMenu/ProtagonistClassPicker.cs:74`); renaming a class breaks the icon. Use a Sprite field instead. (S / Low)
- **Unused audio:**
  - Battle `Claw`, `Bite`, `Block`, `Encounter` are assigned but never played.
  - All 12 Bamao UI/object sounds are unused; "Coin Pop" fits `Gold.cs:13`.
  - `LevelUp` reuses `48_Speed_up_02`.

### Environment
- **Thin dungeon decorations.** `DungeonPresentation.Decorate` (`:118-147`) uses `kit.Models` only, with a cap of 16 per chunk and 120 triangles. Several themes get only `Rock`.
  - Unused fits: `DeadTree`, `SnowRock`, `Shrine`, `Flowers`, `MountainSpires`/`Ridge`, `Willow`.
  - The palette-batched TownInteriors furniture also fits: Barrel, Crate, Sack, Candles, Books, Lantern, Sconce, Banner, Chest, ArcanePedestal, Lectern.
  - Let `Decorate` resolve ids from `TownInteriorCatalog`, raise the cap to about 250 triangles, and give each theme 4–6 ids. (M / High)
- **Town dressing already exists.** Diorama Phase 3 plans to author barrel, crate, bench and signboard in Blender, but TownInteriors already has Barrel, Crate, Sack, Bench, Basket, Lantern, Plant, PerchSign, Banner and birds (XY/−Z, palette). Reuse them in `TownEnvironmentLayer`; author only the fence, flower box and mailbox.
  - `HomeBed.cs:26-39` builds the bed from cubes plus `Shader.Find("Standard")` at runtime, although `TownInteriors/Bed.prefab` exists. (S–M / High)
- **Baked previews in build scenes.** `Town.unity` references 124 `EnvironmentKit/TownPreview/Chunk*.asset` meshes (41 MB of YAML), even though `Town.cs:301-305` rebuilds the town at runtime. `DungeonScene.unity` embeds 54 baked meshes that use TWC demo materials. Tag them `EditorOnly` or strip them in a build preprocessor. (S / Med–High)
- **TileWorldCreator samples:**
  - About 190 MB is completely unreferenced: `Version 2 Tiles/SciFi`, `CliffIsland`, `2DIsland`, `Prototype`, `Version 3/6-Tiles`.
  - `V2 Dungeon` and `V3 4-Tiles` are reached only through legacy preset slots and inactive layers, but they pull 18 realistic 1024² demo textures into the build (also used by `Art/MainMenu/*_mat_Dungeon_*`).
  - Repoint those slots, then delete. `DungeonThemeTests:86` needs updating. (S / Med)
- **WebGL texture memory.**
  - `EnvironmentKit/Textures/Buildings_{8 biomes}.png` are 2048² with no WebGL override, and `Kit.asset` loads all 8 atlases.
  - `PaintedEnvironment/BiomeDecorations.png` and `DungeonProps.png` are 2048² as well.
  - Add 1024 WebGL overrides, or move to one atlas plus a tint (this fits Diorama's atlas-swap idea). (S–M / Med)
- **EnvironmentKit duplicates.** 476 biome prefabs (`Prefabs/<Biome>/`, everything except Grassland) are used only by `EnvironmentPlayground.unity`. 68 `Models/*.fbx` have been copied into `Meshes/*.asset`. Archive them after updating the playground and `EnvironmentKitTests`. (S / Low–Med)
- **Overworld markers** (`Overworld/{Town,Gate,Dungeon,Landmark}.prefab`) are primitives combined with kit meshes. This is evidence for Diorama Phase 5.

### Props
- **The wrong Adorable variant is used.** Every reference points at `Adorable 3D Items` (one material and texture per model: 212 materials, 143×512² + 54×1024²). The identical `Adorable 3D Item_Atlas` (5 materials, 3×2048² atlases) is unused. Repoint the dropped items, `UnifiedPresentationAuthoring.cs:18` (bag) and `Enemy_ChestMonster` to the atlas prefabs with one lit material variant in our own folder, then drop the other folder. **This changes Diorama Phase 6 step 2.** (S–M / High)
- **Adorable native scale varies widely** (dropped-item root scale ranges from 1.14 to 6.9). Use the `DioramaScale` table when it lands. (Med)
- **Quest/interaction markers.** Adorable `exclamation mark` / `question mark` as bobbing markers over service NPCs and doors; no marker system exists today. (S / High)
- **Themed shop counters.** Goods are only `Basket`/`Equipment`/`Potions` (`Core/.../TownInteriorGenerator.cs:73`). Rotate in cake, waffle and egg (bakery) and potion_red, potion_blue and jar (consumables). (M / High)
- **Results screen.** Use a trophy, star or treasure chest icon on `GameOverScreen.cs:62` (text only today). (S / Med)
- **Ambient critters.** Use species the town lacks (hen, duck, butterfly, bee, ladybug). Skip cat, dog, sheep and rabbit, which duplicate the animated NPCs. (M / Med)
- **Skip the Adorable furniture.** It duplicates the authored interiors and would break palette batching.
- **KennyNL is Castle Kit 1.2 (CC0).** It is used for the main menu castle and some town building prefabs; the Shop prefab's renderers are hidden by `TownBuildingManager.cs:34-53`. Its towers, walls, flags and siege engines fit overworld castle landmarks or boss-floor dressing (Diorama Phase 5). Clean up the fbx/obj duplicates and the orphan `.unitypackage.meta`. (M / Med)

### Safe deletions (0 references found)
**Repo/import weight only** (they don't ship today):
- `Tiles/` (237 files, 80 MB: a legacy 2D tilemap pack)
- `Prefabs/Dungeon/Dungeon.prefab`, `_TileWorldCreator_Dungeon.prefab`, `Art/Torch.mat`, `Scripts/DungeonGenerator.cs`
  - Keep `Scripts/Dungeon/Tile/*Tile.cs`; `Dungeon.cs:192-222` uses `InteractableTile`.
- `Art/DungeonTexture/` (old blueprint PNGs + PSD)
- `_Recovery/*.unity` (crash-recovery scenes)
- `unity-wave-function-collapse/` (8 scripts, unused, but compiled into Assembly-CSharp)
- `Overworld/Biome*.png` (11 swatches) and `Overworld/BiomeRoad.mat`; drop `BiomeRoad` from `PaintedEnvironmentAuthoring` too
- The unreferenced TWC samples listed above (about 190 MB)
- `RPG_skills_and_abilities` contact sheets (`All.png` 7000×4281 + six at about 3150²; 555 MB source)
- `Sounds/Farts/*` and `FartScene.unity`: **not safe.** FartScene is the splash scene loaded by `Common.cs:62` (`LoadScene(1)`); confirm with the user before touching it.

Re-run the GUID index before deleting anything, and delete in one commit per pack with EditMode and PlayMode suites green.

### Audit triage within the combined task
1. Verify the generated biome wall materials, then address NPC portraits and the Ally placeholder. Priority 05 owns the clip, sound, animation, footstep, status and VFX work recorded above.
2. Handle build hygiene after rechecking references: preview meshes, TWC demo slots, import settings and safe deletions.
3. Include the lighting material decision in Phase 0, TownInteriors and Kenney candidates in Phases 3–5, and the Adorable atlas/markers/counters in Phase 6. Check WebGL texture overrides with the performance pass.
