# Diorama restyle: ground, props, houses, overworld (via TWC layers)

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
- Everything must be produced through TileWorldCreator (TWC) layers.
- Core masks still decide gameplay; art adds no collision.
- Generation stays deterministic for a given seed.
- Towns keep their in-place 9×9 rooms with roofs that hide when the hero enters.

**Decisions from the user:**
- Houses keep the in-place rooms; restyle only.
- Meshes are authored in Blender via MCP.
- Test budgets may be raised.

## Suggested environment source: Dungeon Mason "RPG Tiny Hero World Bundle Polyart" (pending purchase)
**Suggestion:** buy [RPG Tiny Hero World Bundle Polyart](https://assetstore.unity.com/packages/3d/environments/fantasy/rpg-tiny-hero-world-bundle-polyart-317855) and make its environment half the main source of meshes, instead of authoring most pieces in Blender.
- **Publisher:** Dungeon Mason, who also made our Polyart hero and monster packs.
- **Why Polyart, not PBR:**
  - The style anchor is the Polyart hero (`RPGTinyHeroWavePolyart`) and monsters (`RPGMonsterBundlePolyart`).
  - Their materials are flat-colour, with one shared texture.
  - Environment pieces from the [PBR version](https://assetstore.unity.com/packages/3d/environments/fantasy/rpg-tiny-hero-world-bundle-pbr-316370) (316370, about $80, 338 MB) would clash with them.
  - Do not buy the PBR version.
- **Contents:**
  - The PBR listing gives these counts; confirm the Polyart version matches.
  - **RPG Tiny Fantasy World:** 45 buildings and deco, 24 ground paddings, 23 land masses, 14 mountains, 34 rivers, roads, lakes and falls, 40 rocks, and 17 trees and plants.
  - **RPG Tiny Hero Wave Polyart:** we already own this. Check whether "RPG Tiny Fantasy World Polyart" is sold on its own, so we don't pay for it twice.
  - **Assets:** one universal base texture shared by all meshes; water, portal, fire and wind shaders; LOD0/LOD1; collision components.
  - Supports Built-in, URP and HDRP, Unity 2021.3+.
- **Monsters:** use the same rule. Buy [RPG Monster Wave 4 Polyart](https://assetstore.unity.com/packages/3d/characters/creatures/rpg-monster-wave-4-polyart-357900), not the PBR version (355036).
  - All 34 `Prefabs/Dungeon/Enemies` use `RPGMonsterBundlePolyart/CommonStuffs/Materials/PolyartDefault.mat`, which has an albedo and an emission texture.
  - Wave 4 Polyart uses the same one-material albedo + emission (512²) setup, plus a mask-tint material.
  - Wave 4 is heavier: 3.3k–16.5k triangles per monster, against 1.3k–7.5k for waves 1–3. Measure WebGL frame time on a full floor.
- **Why it fits:**
  - It is built to match the Tiny Hero proportions, so the Phase 0 scale ladder becomes mostly "measure and confirm" rather than "invent".
  - Its stylized forest, land masses, terraced mountains, roads and storybook buildings cover most of what the art references (`ArtRefs/`) ask for.
- **Fit risks to check before committing (one spike, Phase 0):**
  1. **Grid fit:**
     - Land masses and mountains are freeform diorama pieces, not cell tiles.
     - Check whether ground paddings, roads and mountain pieces tile on a 2-unit cell, or can be cut into Edge/Outer/Inner/Fill sets for TWC tile layers.
     - Freeform pieces can only be used as hash-placed cosmetics or as border/backdrop dressing.
  2. **Axes:** Unity Y-up prefabs versus our XY ground with −Z up. Placement layers must apply the same −90° X correction that `BiomeDecorationAuthoring` uses.
  3. **Collision:** remove the asset's colliders on import. Art adds no collision, and Core masks decide gameplay.
  4. **Biome tinting:**
     - One shared atlas means per-biome palettes are done by atlas swap (snow, ash, marsh variants) or a tint/hue shader.
     - `PaintedEnvironmentAuthoring` material resets must not touch these materials.
  5. **Houses:** the 9×9 in-place rooms and hide-on-enter roofs must still work. Their buildings are probably closed meshes, so either split them into facade and roof parts or keep our facade cells and borrow only roofs, trims and dressing.
  6. **Budgets:** measure triangles per prefab (LOD0 and LOD1) against the triangle budgets in `OverworldCosmetics` and the limits in `EnvironmentKitTests`. The wind shader must compile for WebGL on the Built-in pipeline.
  7. **Licence:** a Single Entity licence is enough for this project. Keep the vendor folder untouched (`Assets/Art/RPGTinyFantasyWorld…`) and put wrappers or prefab variants in our own folders, as we do for the Polyart packs.
- **What changes in the plan if adopted:**
  - **Phase 1:** keep `PaintedGroundMesh` and the splat shader, and paint the splat textures from the bundle atlas palette so ground matches the props. Ground paddings and roads may become edge-trim pieces instead of Blender grass-lip trims.
  - **Phase 2:** the bundle's mountains and rocks replace the Blender cliff kit wherever they fit the grid. Its water and falls shaders feed the coastline work.
  - **Phase 3:** the bundle's trees, plants and rocks replace most Blender foliage and props. Blender only fills gaps (fences, signboards, biome variants such as charred or snow trees).
  - **Phase 4:** source roofs, trims, signs and dressing from the bundle's buildings and deco, within the in-place room constraint above.
  - **Phase 5:** overworld settlements and landmarks use the bundle's buildings at overworld scale.
  - **Decisions:** "Meshes are authored in Blender via MCP" becomes "the bundle first, Blender for gaps and adapters".

## Key facts from exploration
- **Custom TWC build layer pattern** (`EnvironmentSmartTileLayer.cs`, `OverworldOceanLayer.cs`, `TownEnvironmentLayer.cs`):
  - Class shape: `[Serializable, ActionName] class X : TWCBuildLayer`.
  - `Clone()` copies every field.
  - `Execute` reads `creator.GetGeneratedBlueprintMap(guid[+"_UNSUBD"])`, emits through `EnvironmentBatch`, and always increments `creator.executedBuildLayersCount` in `finally`.
  - Renderers without an `EnvironmentMeshOwner` are hidden (`OverworldBiomeRenderer.cs:70-72`).
- **Overworld broad ground bypasses TWC.** `OverworldBiomeRenderer.Draw` (`:75-132`) builds hard-edged per-biome quads after the build completes. Open work in `Docs/TWCBiomeStylingIntegrationPlan.md` already asks to move this into a TWC build action with one rendering owner.
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
1. **Connect Blender MCP** to this session (it isn't connected now). Make `build.py` resolve `ROOT` from the script location instead of the hard-coded path, and apply the same fix to the TownInteriors `author.py`.
2. **Measure Polyart hero bounds** in Unity (via `Tools/unity-mcp.mjs`) and write `Docs/Art/DioramaStyle.md` with:
   - the style rules from the earlier style guide
   - a scale ladder in hero heights (H): fence 0.6H, barrel/crate 0.5–0.6H, sign 1.1H, bench 0.5H, lamp 1.8H, door 1.6H, eaves 2.2H, broadleaf tree 2.5–3H, pine 3–3.5H, boulder 0.6–1H
   - the palette per biome, using warm yellow-green grass and tan dirt as the base
3. **Capture a baseline** with the existing `PaintedEnvironmentCapture` (Before set) using the gameplay camera.
4. **If the Dungeon Mason bundle is bought:**
   - Import it and run the fit spike above (grid, axes, collision, tint, houses, budgets).
   - Place 3–4 bundle pieces next to the Polyart hero in one capture.
   - Decide per phase which pieces come from the bundle and which from Blender, and record the decision in `Docs/Art/DioramaStyle.md`.

## Phase 1: Ground as TWC layers (largest visual win)
1. **Shared builder `PaintedGroundMesh`** (new, `Assets/Scripts/Environment/`):
   - Builds chunked (32×32) ground meshes from up to 4 surface masks.
   - Writes per-vertex splat weights, with vertices on a ½-cell grid so edges blend over about half a cell.
   - Applies hash-based edge jitter so borders are organic and deterministic.
2. **Shader `PaintedGround.shader`** (built-in pipeline surface shader, matte):
   - Blends 4 painted textures by vertex weight.
   - Breaks transition edges with a noise mask.
   - Adds a darker rim where dirt meets grass.
   - Handles water separately.
3. **Overworld layer `OverworldGroundLayer : TWCBuildLayer`:**
   - Add one blueprint layer per surface group to `CampaignTerrain.asset`: `Ground/Grass`, `Ground/Sand`, `Ground/Dirt` (paths and roads), `Ground/Snow`, `Ground/Ash`, `Ground/Marsh`.
   - `CampaignOverworld.Apply` fills these from the Core biome and Road layers.
   - The new layer becomes the single rendering owner of the broad ground.
   - Reduce `OverworldBiomeRenderer` to the bridges and in-grid water it still needs, or remove its floor drawing, and keep a `RenderedSurfaces` equivalent for tests.
   - Add the style inputs to the cache key in `OverworldTerrainCache`.
4. **Roads become dirt paths:**
   - Overworld roads and town alleys use the Dirt splat channel instead of the flagstone `SmartRoad` quarter tiles.
   - Town `MainRoads` and plazas keep a cleaner, warm cobble flagstone, so paving is reserved for landmarks.
   - The Smart/Roads layer keeps its masks but emits only grass-lip edge trim pieces (Blender) along path borders.
5. **Town:** `TownEnvironmentLayer` uses `PaintedGroundMesh` with the town masks (Parks → grass, Alleys → dirt, MainRoads → cobble), replacing Paving + `Kit.Ground`.
6. **New painted textures:**
   - Grass (yellow-green with clover variation), dirt, cobble, sand, snow, ash, marsh, and a saturated cyan water texture.
   - All 1024² and seamless, made through the `ArtSource/PaintedEnvironment` → `WriteSurface` flow.

## Phase 2: Overworld terrain silhouettes
1. **Cliff kit (Blender):**
   - Stratified, rounded-strata cliff pieces with a grass lip on top: Edge, Outer, Inner and Fill for each biome tint.
   - These replace the six-terrain prefabs on `Smart/Mountains Base/Tier 2/Tier 3` (keep `HeightScale 1` so the prefab path is used).
   - Summit pieces become rounded rock caps.
2. **Coastline:** foam band and shallows tint in `SmartShoreline.shader`, a cyan `Ocean.mat`, and scattered sea-rock props (cosmetic only, never on walkable cells).
3. **Forest cells:**
   - Replace the raised pyramid cells (`OverworldBiomeRenderer.cs:58,101-107`) with a new `Cosmetic/Tree Walls` TWC build layer that places dense, grid-aligned tree clusters on Core `Trees` cells.
   - Use small jitter and alternate pine and broadleaf trees by biome.

## Phase 3: Foliage and props (Blender plus placement)
1. **Re-author trees:**
   - Pine: stacked rounded cones, about 250 triangles.
   - Broadleaf: puffy lumpy canopy with a visible trunk, about 300 triangles.
   - Also: SnowPine, Palm, Willow/Mangrove, Dead and Charred trees.
   - Bake vertex-colour gradient and occlusion (dark at the base, light at the top).
   - Keep the base-centred pivot and the XY / −Z convention.
2. **New ground cover:** tall-grass clump, flower bed patch, grass tuft, fern, mushroom ring, reeds.
3. **Town dressing: reuse first, author only the gaps.**
   - **Reuse:** `Resources/TownInteriors` already has Barrel, Crate, Sack, Bench, Basket, Luggage, Lantern, Plant, PerchSign, Banner, Dummy and Target, plus ambient birds. They are XY/−Z, use the shared palette, and are batched by `TownInteriorRendering.cs:38-41`. Place them outdoors through `TownInteriorCatalog` from `TownEnvironmentLayer`, scaled by the Phase 0 ladder.
   - **Author in Blender (missing):** fence segment (straight, corner, post), flower box, mailbox, two-post signboard (if `PerchSign` doesn't read at overworld scale), boulder (S/M/L).
   - **Related:** replace the runtime cube bed in `HomeBed.cs:26-39` (which uses `Shader.Find("Standard")`) with `TownInteriors/Bed.prefab`.
   - **Optional Adorable markers** (`Docs/Art/ArtAssetAudit.md`): exclamation/question mark over service NPCs; hen, duck and butterfly as ambient critters on Park cells.
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
1. **Facade kit (Blender):**
   - plaster/timber wall panel
   - framed window panel with flower box
   - door panel with step, awning and lamp
   - corner post
   - stone base course
   - per-biome tint through the existing palettes
2. **`TownHouseTiles.Facade`:** swap the cube boxes for these meshes while keeping the cell rules (door on the south face when the cell south is a door, windows at `(x+y)%2==0`). Facade height follows the ladder (eaves about 2.2H).
3. **Roofs (`TownRoofTileLayer` / `RoofTile` / `GableTile`):**
   - chunky shingle roof with a thick overhanging eave, ridge cap and light edge highlight
   - add chimneys and dormers from a deterministic per-building hash
   - a bold roof colour per biome
   - keep: one roof per building, no colliders, hide when the hero is in the room
4. **Service identity:** a large hanging sign or emblem per service (Inn, Shop, Trainer), placed through `BiomeDecorationPlacement` facade sockets (already defined in `BiomeDecorationAuthoring.cs:58-70`).

## Phase 5: Overworld settlements and landmarks
1. Replace the POI plaza cubes (`OverworldScene.cs:188-210`) and `Smart/Houses` boxes with miniature house clusters built from the Phase 4 kit at overworld scale.
2. Replace the stretched-plank signpost in `BiomeRoadSigns` with the new two-post signboard.

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
   - Use tinted material variants where one model serves several items.
   - Use the **`Adorable 3D Item_Atlas`** prefabs (5 shared materials). Today every reference points at the per-texture `Adorable 3D Items` variant (212 materials), which can't batch; see `Docs/Art/ArtAssetAudit.md`. Move the existing 10 dropped items, the bag icon and `Enemy_ChestMonster` over as well.
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
5. **Scale:** set floor items to the Phase 0 ladder (pickups about 0.4–0.5H) through `DioramaScale`, so they don't fight the restyled props. The Adorable cartoon shading stays as it is for now.
6. **Tests:**
   - Every `ItemDefinition` resolves to a dropped prefab and an icon, either its override or its category.
   - No two items sold in the same shop share both a model and a tint.
   - Existing `DungeonPlacement` and `ActionDialog` paths still pass with no override set.

## Out of scope (follow-ups)
- Lighting and shadows
- Post-processing
- Camera zoom
- Dungeon themes
- KennyNL interior props, and restyling the Adorable models' shading to Polyart (Phase 6 only maps and scales them)

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
- **`PaintedEnvironmentTests`:** new textures must pass the 1024/seamless check.
- **Must still pass unchanged:**
  - Core town tests (`TownDetailTests`, `TownLayoutTests`, `TownInteriorTests`)
  - `TownGameplayTests` (roofs)
  - `BiomeDecorationTests` sign rules
  - `CampaignOverworldTests` (template not modified)

## Verification
1. EditMode and PlayMode suites via `Tools/harness-editmode.json` / `harness-playmode.json` through `Tools/unity-mcp.mjs`, plus `dotnet test` for `Core`.
2. **Same-seed determinism:** build the overworld and a town twice and compare cosmetic and ground triangle counts.
3. **Captures:**
   - Re-run `PaintedEnvironmentCapture` (After set), "Art > Rebuild Town Preview" and the UnifiedPresentation gameplay-camera captures.
   - Compare side by side with `ArtRefs/` for: blended path edges, a tree wall framing the route, hero-to-door and hero-to-prop scale, roof read, terraced cliffs.
4. **Performance:** frame samples (`UnifiedPresentation/FrameSamples.txt` flow) on the Windows and WebGL builds; triangles per chunk within the new budgets.
5. **Order of landing:** Phase 1 → 3 → 4 → 2 → 5. Each phase lands as its own commit, with tests green and captures updated. Phase 6 depends on nothing else and can land at any time; only its scale step (6.5) waits for the Phase 0 ladder.
