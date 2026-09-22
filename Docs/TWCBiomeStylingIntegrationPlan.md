# TWC-owned biome styling: generation integration plan

Status: proposed implementation; no runtime or scene changes made. Written 2026-09-22.

## Intended outcome

**Campaign generation determines what the world means; TWC determines how it looks.** Feed validated campaign masks into explicitly identified TWC source layers. Preserve artist-authored blueprint stacks that derive biome surfaces, transitions, vegetation, rocks, and accents. Let TWC build layers choose tile presets, materials, prefabs, variation, height, and rendering settings.

An artist should be able to replace the forest canopy, change desert ground variation, add shoreline reeds, or adjust mountain silhouettes through the overworld TWC style asset, then rebuild the same campaign seed without changing a route, gate, location, or movement result.

This plan selects the TWC-centered integration option from [OverworldVisualDetailAndModels.md](OverworldVisualDetailAndModels.md). It supersedes that document's suggested separate decoration pass as the preferred long-term architecture. Its visual constraints and model backlog still apply. Follow [BlenderModelGuide.md](BlenderModelGuide.md) for generated assets.

## Ownership boundaries

| Owner | Responsibilities | Must not do |
|---|---|---|
| Core campaign/grid generation | Region layout and biome assignment; topology; ground/water/blocked feature masks; roads; locations; gates; progression and movement | Depend on Unity, TWC assets, mesh appearance, or decoration density. |
| Unity campaign-to-TWC adapter | Copy source masks into the runtime style instance; supply semantic exclusion masks; validate dependencies, dimensions, and style contract; coordinate build lifecycle | Replace every authored blueprint stack or embed per-biome aesthetic recipes in C#. |
| TWC blueprint layers | Derive visual masks using copied source layers; select boundaries/interiors; create safe variation and sparse accents | Write results back to the core grid or become movement authority. |
| TWC build layers/presets | Ground rendering, modular edges, objects, material/prefab variants, transforms, shadows, and batching | Add gameplay interactions, obstacles, or new destinations as a side effect of decoration. |
| Existing marker/gate presentation | Position and state of existing destinations, gate open/closed visuals, selection/readability | Be merged into static scenery in a way that loses identity or state updates. |

The style system consumes the generated biome assignment. It does not rerun biome geography using TWC noise. Noise can select moss, grass, stone, or other purely visual variants **inside** the assigned biome.

## Current behavior to change

1. `CampaignTerrain.asset` only has Parks/Roads blueprint and tile build layers. `Overworld.unity` binds Ground to Parks and Roads to Roads.
2. `CampaignOverworld.Apply` clones the template but overwrites **all** blueprint action stacks, including unbound layers. Unbound layers receive empty masks. This prevents authored styling dependencies from working.
3. The adapter's post-build loop casts every blueprint action to `CampaignLayerAction` and clears all failure flags. That assumption must be removed before mixed imported/authored actions are supported.
4. `OverworldBiomeRenderer` chooses biome surfaces and relief in C#, then disables every renderer under the TWC world. Thus TWC cannot currently own the visible result.
5. Terrain caching owns TWC cluster meshes and separate biome surfaces; cache reuse only checks context/template identity. It needs style-version invalidation and ownership of any new build output.
6. TWC uses GUID references between layers, ordered blueprint execution, and shared Unity random state in several actions/build paths. These need explicit validation and deterministic integration.

## Asset layout and authoring contract

Introduce a project-owned `OverworldStyleProfile` ScriptableObject that references **one composed TWC template for the entire overworld**. Use that template for all biome families so shared road/shore/exclusion layers can reference each other directly. Do not create a separate TWC world per biome in the first implementation.

Proposed profile fields:

| Field | Purpose |
|---|---|
| Stable style ID and explicit revision | Deterministic identity and runtime cache invalidation. |
| TWC template reference | Source of all artist-authored blueprint/build layers and presets. |
| Source bindings: semantic key → blueprint GUID | Identifies the only layers the adapter may replace with imported data. Names are display labels, not binding identity. |
| Source requirement: required / optional | Missing required mask fails validation; optional mask becomes an explicit empty array of the correct dimensions. |
| Presentation mask definitions | Derived protection inputs such as road, gate, location, and crossing clearances; generic semantics, not biome aesthetics. |
| Build-layer role metadata keyed by GUID | Classifies surface, transition, low detail, tall background, or dynamic visual output for validation and migration. |
| Placement policy references | Footprint clearance, collider/script restrictions, and density caps per role. Artists may strengthen safety exclusions, not bypass required protections. |
| Seed salt and build schema version | Reproducible styling without perturbing campaign randomness. |

Materials, prefab choices, tile weights, and visual densities belong in TWC build/blueprint settings. Do not duplicate the same choices in profile fields and TWC layers. Start with one style profile; a later alternate art theme should swap profiles while preserving the source contract.

Suggested new asset locations: `Assets/Overworld/Styles/DefaultOverworldStyle.asset` and `Assets/Overworld/Styles/DefaultOverworldTWC.asset`. Keep the existing template available during migration. Proposed class/asset names in this plan are not existing APIs.

## Layer organization

Use readable naming prefixes, with binding and dependency identity stored as GUIDs. Source layers remain executable so TWC generates their caches, but have **no build layers** and no artist modifiers attached. Show them as imported/read-only in the project authoring UI.

### 1. Imported sources: `Source/*`

| Source family | Campaign input | Use |
|---|---|---|
| `Source/Ground` | Ground | Stable potential floor, including conditional cells. |
| `Source/Biome/<biome>` | Biome masks | Gameplay-floor biome membership. |
| `Source/Landscape/<biome>` | Landscape masks | Background as well as floor biome membership. |
| `Source/Trees`, `Source/Mountains` | Blocked feature masks | Large vegetation/rock anchors. Distinct from walkable forest/mountain biomes. |
| `Source/Water`, `Source/NavigableWater` | Visible and navigable water | Surface styling and crossing exclusions; never infer access from material. |
| `Source/Roads`, `Source/Bridges` | Existing route/crossing masks | Route overlays and bridge visuals. |
| `Source/Reserved` | Existing reservation mask | Conservative large-background protection; includes Ground and its halo. |
| `Source/Locations`, `Source/Locks`, `Source/WarpPads`, `Source/PlayerStart` | Masks or explicit coordinates from the grid/context | Stable presentation protection inputs. Locations/warp pads may need adapter-created unions; do not assume each proposed source name already exists in `grid.Layers`. |
| Optional `Source/Region/<id>` | Region masks | Later local visual identity; import only when referenced to avoid unnecessary caches. |

Import the biome enum values supported by the generator: Grassland, Desert, Water, Mountain, Forest, Tundra, Marsh, and Volcanic. An absent biome in a particular seed is a valid empty layer.

Do not use capability-dependent `Walkable` as the base for static biome art: earning Boat or opening a gate must not reshuffle vegetation or replace the whole terrain. Keep capability/lock state in existing gameplay and dynamic marker visuals.

### 2. Protection and shared derivations: `Protect/*`, `Shared/*`

- `Protect/Routes`: roads and movement approaches expanded by the configured visual clearance.
- `Protect/Locations`: union of existing locations, start, warp pads, and their visible approach areas.
- `Protect/Gates`: stable lock footprints and approaches, independent of open/closed state.
- `Protect/Crossings`: bridge landings and water/warp approach space.
- `Shared/ShoreCandidates`: land/water adjacency derived from source masks, not from a new shoreline generator.
- `Shared/RoadsideCandidates`: a ring around the existing road footprint, minus roads and protected approaches.

Decide one producer for each protection mask: either adapter-supplied semantic masks or mandatory validated TWC derivations. Prefer adapter-supplied masks for invariant gameplay approaches; allow additional artist-authored exclusions in TWC. Do not maintain two subtly different exclusion algorithms.

### 3. Artist blueprint layers: `Style/<biome>/*`

For each biome, expose only the layers that add value: Base, GroundVariation, BlockedFeatures, LowDetail, ShoreAccents, and RareAccents. Keep shared roads/bridges in `Style/Shared/*`. Do not create unused layers simply to fill a fixed matrix.

Example **forest** recipe, expressed as set operations rather than claimed drop-in TWC commands:

```text
ForestBase        = Source/Landscape/Forest
ForestFloor       = Source/Biome/Forest
ForestTreeSpace   = Source/Trees INTERSECT Source/Landscape/Forest
ForestSafeTrees   = ForestTreeSpace MINUS Protect/TallObjects
ForestCanopy      = spaced_sample(shrink(ForestSafeTrees), canopy_density)
ForestLowSpace    = ForestFloor MINUS Protect/LowDetail
ForestLowDetail   = sparse_sample(ForestLowSpace, ground_density)
ForestShore       = Shared/ShoreCandidates INTERSECT Source/Landscape/Forest
ForestShoreAccent = sparse_sample(ForestShore MINUS Protect/Crossings)
```

Create the equivalents using TWC Add, Subtract, Shrink, Select, rule selection, and overlap actions where their actual semantics fit. A fresh layer starts empty. Inspect action implementations before translating formulas: the bundled `Overlap` sets matching cells true but does not clear other already-true cells, so it is not automatically a safe final intersection on a populated layer. Add a small project-owned exact intersection/clamp action if needed.

Reapply the allowed-area mask after noise, expansion, smoothing, or offsetting. Apply a mandatory footprint/visibility guard after transforms during object placement too; a safe anchor alone cannot contain a large crown or scattered child.

### 4. Artist build layers: `Build/*`

| Output | TWC mechanism | Artist controls |
|---|---|---|
| Broad biome floors/water | Project-owned chunked surface build action, integrated into TWC | Material, UV scale/variation, shallow visual relief, shadow policy, layer ordering. |
| Shore/bank/cliff trims | `InstantiateTiles` and matching 4-tile presets where suitable | Edge/corner geometry, preset weights, material, offsets; exact seam fit. |
| Roads/bridges | Existing tile/6-tile builders when their topology and cost fit; cheap surface builder for simple overlays | Route materials, modular join style, edge treatment. Footprint remains campaign-derived. |
| Trees/rocks/accents | `InstantiateObjects` for safe fixed placements; project-owned constrained object action where needed | Prefab variants, density masks, transforms, clustering, merging, shadows. |
| Ground microvariation | Surface build action or sparse low-detail object layer | Palette/UV variation without thousands of separate patch objects. |

`InstantiateTiles` already supports weighted tile presets and merging. `InstantiateObjects` has more limited primary-prefab variation and unconstrained child scatter; use explicit variant layers initially. Add a constrained variant/scatter action only where that removes a real authoring limitation. Its prefab selection and variation settings must remain visible in TWC.

Keep extensions in project-owned code against the TWC action interfaces, with working Clone/serialization/editor support. Avoid a second C# biome recipe engine behind nominal TWC layers. If a vendor hook is required, keep it narrowly scoped and documented.

## Efficient floors should also be styled through TWC

Move the reusable mesh-emission logic from `OverworldBiomeRenderer` into the proposed chunked surface build action. It consumes the assigned blueprint mask and authored settings; it must not enumerate biome names or choose materials from a hardcoded biome switch.

Retain the current cheap geometry as the baseline: four vertices/two triangles per ordinary cell, optional shallow relief where appropriate, bounded chunk meshes, shared materials, and no collider or shadow by default. TWC controls which layers use this builder and which use full tile prefabs. Rich style does not require replacing every floor cell with a detailed FBX.

Define a surface coverage/ordering policy in the template. A full landscape base can underlie gameplay-floor surfaces deliberately, but random ground variants should partition or selectively overlay that base instead of creating many duplicate coplanar layers. Specify depth offsets for XY rendering and test z-fighting. Roads, water, bridge decks, and trim must have explicit precedence.

During migration, assign each surface role exactly one renderer owner. Either the legacy renderer owns that role or TWC does. Remove blanket TWC renderer suppression immediately for migrated output. Retire the legacy renderer only when TWC has parity for all biome, water, road, bridge, and blocked-feature visuals.

## Generation and build lifecycle

1. Generate and validate the immutable campaign/grid with the existing core pipeline.
2. Select the style profile; validate its source bindings, build references, layer roles, and supported dimensions/orientation.
3. Clone the TWC template into a runtime instance with independent mutable stacks/actions/maps. Confirm nested serialized data is actually isolated; do not assume a top-level clone proves independence. Preserve authored layer GUIDs inside the instance.
4. Replace **only bound source layers** with imported `CampaignLayerAction` instances using copied arrays. Preserve authored style stacks, active flags, and parameters. Reject unclassified layers rather than silently emptying arbitrary authored content.
5. Resolve dependencies, require sources before consumers, and reject cycles, missing GUIDs, or references to unavailable outputs. Initially validate the authored list order; do not silently reorder layers and change random results. Regenerate all referenced source/style caches, including `_UNSUBD` where required.
6. Execute blueprint layers. Report actual failures with the layer/action name. Treat expected empty source/decoration masks as valid without erasing genuine error flags. Remove the existing blanket action cast/failure reset.
7. Validate final surface coverage and allowed placement domains. Execute TWC build layers with deterministic visual random handling and final transformed-footprint checks for objects.
8. Wait for build completion, register generated resources and root ownership, then publish the finished terrain and cache it. Do not signal scene readiness or cache a partial build.
9. On failure, clean the partial runtime output, restore global/random and adapter state, and keep the last valid terrain or an explicit fallback. Do not leave half-old/half-new layers visible.

Source layers are runtime data injection points. Artist edits to style layers survive every campaign application. Invalid profiles must fail before destructive replacement of the displayed world.

## Randomness, rebuilds, and caching

Derive visual seeds from campaign seed, stable style ID, stable blueprint/build-layer GUID, and an explicit variation salt. Keep cache revision separate from seed salt: a material-only edit should invalidate rendering without unnecessarily moving every tree. Use a stable hash, not process-dependent string hashing or list indices.

Audit the execution path end-to-end. Several bundled actions reset Unity random state from `twcAsset.randomSeed`, so setting a per-layer custom seed alone does not establish independent streams. Builders also consume Unity random state. Provide a narrow execution wrapper or project action context that gives each layer its deterministic stream; test that enabling an unrelated layer cannot reshuffle other layers.

If execution yields across frames, save/restore Unity random state around each synchronous execution segment or use a local PRNG. A single save before an asynchronous build and restore at the end could overwrite other systems' random progress. Protect gameplay random state on success, failure, cancellation, and rebuild.

Expand the terrain cache key to cover campaign/grid identity or generation version, grid dimensions, style ID/revision, template/dependency content fingerprint when available, and renderer schema version. Use an explicit runtime revision in builds; editor validation can compute a dependency fingerprint that includes referenced presets/materials/prefabs. A changed template reference alone is insufficient.

Keep a single terrain result owner for the runtime template, maps/previews, generated mesh/material resources, and TWC world root. Destroy only generated resources, never shared imported mesh/material assets. Restore and hide the whole static styled world together. Gate state changes update existing dynamic visuals without rebuilding static biome placement.

## Artist workflow

1. Open an overworld style preview using a fixed campaign seed; expose source and protection masks as inspectable read-only layers.
2. Edit TWC blueprint stacks to change a biome's visual selection, or its build layers/presets to change appearance. Preview the selected layer and the combined world.
3. Rebuild styling against the same generated grid. No core regeneration is required for a purely artistic change.
4. Compare dense/sparse areas, biome boundaries, shores, roads, gate approaches, and location markers at the gameplay camera. Use the model budgets and backlog in the companion documents.
5. Save the authored profile/template/presets, update the style revision, and validate a representative seed suite. Runtime clones and generated previews are never saved over source assets.

The first editor tooling should be modest: Validate Style, Rebuild Style From Current Grid, show source/protection masks, and layer cost/instance counts. TWC remains the primary editing surface; avoid constructing a competing full biome editor.

## Delivery phases and completion criteria

| Phase | Work | Completion evidence |
|---|---|---|
| 1. Source/style separation | Profile contract, GUID bindings, independent runtime clone, dependency validation, preserve authored stacks, correct empty/error handling | Source masks exactly match the grid; a custom TWC derived layer survives repeated Apply; source/template data is unchanged. |
| 2. Forest/grassland vertical slice | Author base, tree-space, exclusions, low detail, and rock layers; enable visible TWC object output while retaining explicit legacy floor ownership | Same seed has stable TWC-styled objects, no blanket hiding, no gameplay change, and meaningful TWC-only appearance edits. |
| 3. TWC surface ownership | Add chunked surface build action; author material/UV/ordering settings; migrate each floor/road/water/bridge/relief role | Visual coverage parity and no duplicate surfaces; legacy biome renderer removable; all material/style selection lives in TWC assets. |
| 4. Remaining biomes and transitions | Desert, mountain, tundra, marsh, volcanic, water styling; shore and crossing masks; use prioritized model sets | Every generated biome has coverage and readable transitions; absent biomes produce no error; roads/gates stay clear. |
| 5. Cache and production hardening | Full ownership/invalidation, deterministic streams, cost reporting, preview tools, target-build profiling | Town/dungeon returns restore the same world; style changes rebuild correctly; repeat/failure paths leak no generated resources. |

Lifecycle, random-state isolation, and cleanup must be minimally correct in every phase; phase 5 completes broad regression coverage and tooling, rather than postponing those requirements until the end.

## Validation plan

- **Gameplay invariance:** for identical seeds, compare all source masks, region/biome assignments, routes, locations, gates, boat requirements, and movement results before/after styling. Core generation output must be unchanged.
- **Layer contract:** verify authored actions/active flags survive Apply; copied source arrays cannot mutate the grid; GUIDs survive cloning; source dependencies produce both required cache forms. Missing/cyclic references produce actionable errors.
- **TWC semantics:** test exact intersections, mask clamps, empty layers, and required surface coverage. Validate actual builder coordinate/subdivision behavior with a small labeled XY test patch and two-unit cells.
- **Determinism:** rebuild twice, switch scenes and return, toggle an unrelated optional layer, and rebuild after a material-only revision. Expected placement remains stable; Unity gameplay random state remains unchanged.
- **Safety:** validate complete transformed prop bounds and prefab contents, including nested colliders/scripts. Ensure protected approaches remain visible, and water/bridge/gate visual semantics remain correct.
- **Resources/cache:** repeated rebuilds, interrupted builds, invalid styles, template/preset edits, campaign change, and scene unload leave no duplicate roots or leaked runtime meshes. Shared assets survive cleanup.
- **Visual/performance:** compare multiple seeds containing all eight biomes, mixed boundaries, dense vegetation, narrow crossings, and marker approaches. Record target hardware, resolution, quality, generation time, memory, visible renderers, draw calls, and CPU/GPU frame time against the current baseline. Establish target budgets from these measurements before raising density.
- Run relevant campaign/overworld tests and the existing Overworld harness after implementation. Existing tests are starting points; this plan does not claim that they already cover the new style lifecycle.

## Primary files and related documents

- [CampaignOverworld.cs](../Assets/Scripts/Overworld/CampaignOverworld.cs): binding/application and lifecycle changes.
- [OverworldBiomeRenderer.cs](../Assets/Scripts/Overworld/OverworldBiomeRenderer.cs): migrate mesh building and remove blanket suppression.
- [OverworldTerrainCache.cs](../Assets/Scripts/Overworld/OverworldTerrainCache.cs): ownership and invalidation.
- [CampaignTerrain.asset](../Assets/Overworld/CampaignTerrain.asset): legacy template; migrate to a composed style template.
- [TileWorldCreator.cs](../Assets/TileWorldCreator/Code/TileWorldCreator.cs): verify execution order, completion, seed behavior, and cache production.
- [InstantiateTiles.cs](../Assets/TileWorldCreator/Code/Actions/Instantiation/InstantiateTiles.cs) and [InstantiateObjects.cs](../Assets/TileWorldCreator/Code/Actions/Instantiation/InstantiateObjects.cs): reuse builders and document any narrowly required extension points.
- [OverworldGrid.md](OverworldGrid.md), [OverworldVisualDetailAndModels.md](OverworldVisualDetailAndModels.md), and [BlenderModelGuide.md](BlenderModelGuide.md).
