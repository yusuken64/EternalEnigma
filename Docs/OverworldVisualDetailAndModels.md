# Overworld visual detail plan and model request list

Reviewed 2026-09-22. Companion to [BlenderModelGuide.md](BlenderModelGuide.md).

For the preferred TWC-owned styling architecture and generation integration roadmap, see [TWCBiomeStylingIntegrationPlan.md](TWCBiomeStylingIntegrationPlan.md). That plan selects the TWC-centered option over the separate decoration pass suggested below; this document remains the demo audit and model backlog.

**Yes: the overworld can gain substantial visual interest without changing gameplay.** Start with clustered trees and rocks on existing blocked land, a sparse layer of small ground details, shoreline accents, and better silhouettes for existing location markers. Keep the current inexpensive floor renderer and campaign topology.

This document is an implementation proposal and Blender production list. No scene, generation rules, navigation, or runtime code were changed. Evidence comes from the serialized demo scenes, their referenced TWC assets, and local implementation code; the demos were not opened or play-tested during this review. Proposed densities and budgets require camera and performance validation.

## Current overworld layers and constraints

| Existing part | Observed behavior | Consequence for visual detail |
|---|---|---|
| `Assets/Overworld/CampaignTerrain.asset` | Two blueprint layers, Parks and Roads, and two corresponding `InstantiateTiles` build layers. References ParkPreset and RoadPreset. | There is currently no authored overworld tree, rock, shore, or small-prop build layer. |
| `Assets/Scenes/Overworld.unity` | Explicit bindings: `Ground → Parks`, `Roads → Roads`. Orthographic camera size is 13 in the serialized scene. | Other campaign masks are available in the grid but are not automatically imported into this TWC template. Judge detail at gameplay camera size. |
| `CampaignOverworld.Apply` | Clones the template, preserves blueprint GUIDs, replaces **every** blueprint action stack with a campaign mask, and empties unbound layers. | Adding demo noise/select/subtract actions to the template alone will not survive application. Presentation-derived masks need an explicit integration path. |
| `OverworldBiomeRenderer.Build` | Draws landscape, biome/water, mountain/tree ridges, roads, and bridges in 32×32-cell chunks; then disables renderers beneath `creator.worldObject`. | New TWC object renderers would also be hidden. Change the visibility policy or keep decoration outside that hierarchy. Do not simply re-enable all existing TWC floors. |
| `OverworldTerrainCache` | Owns the TWC world and biome surfaces across scene transitions; tracks their generated meshes. Restore checks campaign context and template identity. | Decoration needs explicit ownership, hide/restore, cleanup, and rebuild rules. Editing a template in place is not currently an art-version invalidation mechanism. |
| Grid and transform convention | Cell size 2; XY gameplay plane; half-cell visual alignment; negative Z is raised relief in the biome renderer. | Verify object bases, rotations, and offsets using a small placement test. Do not directly copy the demos' usual ground-plane settings. |

The existing `Trees` and `Mountains` masks represent blocked features, while `Biome/Forest` and `Biome/Mountain` can contain walkable ground. Use the correct mask; a biome name alone does not authorize a large obstructive-looking prop.

## What the TWC demos teach us

All five demo scenes present in this checkout were inspected through their scene serialization and referenced assets. The reusable ideas are the relationship between layers, restrained repetition, and separation of base surfaces from accents.

| Demo and source | Actual authored evidence | Useful overworld adaptation |
|---|---|---|
| `02_Village.unity` / `VillageAsset.asset` | Houses, roofs, roads, parks, trees, clouds, and streetlights. Trees use Add → Shrink → random Select (serialized weight 0.4). Tree object layer enables scale/rotation variation and two scattered children with radius 1. | Grove interiors with breathing room around edges; a few silhouette variants instead of one tree at every cell. Use bounded clusters on existing blocked terrain. |
| Same village demo | Clouds use RandomNoise → Expand → Smooth and a cloud object layer with five children/radius 2. Scene contains `CloudSphere(Clone)` objects. | Broad patches read more naturally than uniform scatter. Apply the clustering idea to crowns, scrub, or ground tint. Visible clouds are optional and lower priority because they can obscure the map. |
| Same village demo | Separate roads and streetlight build layers; the StreetLights blueprint contains Subtract and edge Select actions that are **disabled** in this serialized asset. | Road-edge accents are a useful design idea, but the stored demo is not proof of a finished working edge-only selection recipe. Build and validate our own exclusion mask. |
| `02_Village_LSystem.unity` / `VillageLSystemAsset.asset` | Roads start from LSystem; houses derive from Add/Expand/Subtract/random Select. Roofs, parks, and trees are separate. Tree build layer merges objects and enables scale/rotation variation. | Use separate terrain, canopy, and settlement silhouettes; use clustered static rendering where appropriate. Do not import its road generator into the campaign. This asset also serves the project's town and has project-specific additions. |
| `07_Dungeon.unity` / `DungeonAsset.asset` | Separate dungeon, floor, carpet, columns, and torchlight layers. Carpet is derived through inversion/shrinking; columns use rule selection. Scene contains separate floor/carpet/torch clusters. | Keep broad base colors, then add inset patches and selected boundary details. Ruins and rock accents should respond to space/edges instead of covering every floor cell. |
| `07_Dungeon_Game.unity` / `DungeonGameDemoAsset.asset` | Separate doors and chests; doors use rule selection plus `RemoveNeighbours` radius 6. Chests use random Select (0.039), `RemoveNeighbours` radius 4, and subtraction. | Sparse, separated visual points of interest and explicit exclusions. Borrow the placement pattern for rubble or ruins, not the interactable chest/door behavior. |
| `01_RuntimeEditor.unity` / `RuntimeEditorAsset.asset` | Two paint blueprint/build layers referencing `CliffPreset`; first layer has disabled CellularAutomata and enabled Paint. | A layered cliff/shore vocabulary can give land a stronger edge silhouette. Keep elevations cosmetic; do not copy editable terrain behavior into campaign navigation. |

Additional installed assets worth reusing are the CliffIsland cactus/stones/decal prefabs, Park tree, house/roof kit, and WoodenBridge tiles. Their presence is verified; full overworld fit, material cost, and camera appearance are not yet approved.

### TWC implementation details to account for

- `InstantiateObjects` supports object merging, optional mesh colliders, shadow settings, local/global transforms, random transforms, and optional child scatter. Its prefab-list choice is used for scattered children; do not assume the primary prefab is automatically picked from that list. Use separate variant masks/layers or a dedicated decoration selector.
- Random scale is added to existing scale in the inspected instantiation path. Entering 0.8–1.2 as if it were an absolute multiplier can produce the wrong result. Preview the effective transform.
- Child scatter uses an unconstrained radius before placement; an accepted anchor does not guarantee that all children remain off paths. Validate every final footprint. Check the active XY branch's offsets and rotations with a test patch.
- `RemoveNeighbours` is useful inspiration, but its implementation is not a strict circular minimum-distance sampler. Do not treat the demo radii as a guaranteed spacing contract. Use explicit footprint/distance checks for our placement policy.
- Blueprint Select/Pick actions initialize Unity's random state from the TWC seed. New visual variation should use its own deterministic random stream and leave gameplay random state unchanged.

## Recommended presentation layers

These names and masks are **proposed**, not currently available `CampaignLayerBinding` inputs. Generate them from immutable copies of campaign masks in a presentation pass.

| Proposed layer | Source / placement | Visual purpose | Initial density proposal |
|---|---|---|---|
| `Decor/Canopy` | Existing blocked Trees, intersected with suitable landscape biome and visibility clearances | Broadleaf, conifer, marsh or dead-tree clusters | Sample 15–25% of eligible cells; reject overlap and cap each cluster to 1–3 trees. |
| `Decor/RockMasses` | Existing blocked Mountains / suitable blocked landscape | A few angular silhouettes replacing the uniform ridge look | Sample 10–20% of eligible cells; 1 mass per accepted anchor. |
| `Decor/LowGround` | Safe dry biome floor away from route/marker buffers | Small grass, pebbles, flowers, snow crust, dry scrub | Sample 2–5% of eligible cells, low height and small footprint; keep most floor quiet. |
| `Decor/Shore` | Derived water/land boundary, excluding crossings and gate approaches | Reeds, flat stones, subtle bank/foam accents | Sparse accents on 10–20% of eligible boundary cells; shore surface strips can be continuous. |
| `Decor/Roadside` | Ring beside existing Roads, excluding roads themselves and protected approaches | Occasional milestone, low shrub or rock cluster | Approximately 1 accepted accent per 8–12 eligible road cells; avoid regular rows. |
| `Decor/BiomeAccents` | Sparse, biome-specific eligible background | Cactus, basalt, icy outcrop, mushroom/log group | About 1 group per 25–50 eligible cells, with local caps. |
| `Decor/LocationDress` | Around existing location anchors, on safe background cells | Small ruin, fence remnant, foundation or rubble grouping | At most 1–2 groups per location; leave marker and approach visible. |
| Existing marker visuals | Existing Town/Dungeon/Landmark/Gate anchors and state | Readable destination silhouettes | Replace visible mesh children only; no additional destinations or triggers. |

Sample rates apply to eligible cells, not the whole map. Apply spacing and per-chunk caps after sampling. Do not stack every applicable layer at each anchor. Prefer a hierarchy of a few large shapes, some medium accents, and very little tiny detail.

Low ground detail can also be texture/UV/color variation on the existing floor meshes. That gives variety without a model or GameObject per patch. Shore foam and broad ground patches are primarily material/mesh-strip work, not reasons to commission high-poly terrain.

### Keep the changes strictly visual

1. Never modify `Ground`, `Walkable`, roads, water requirements, gate footprints, location positions, route graph, capabilities, or `CanStep` to accommodate decoration. Do not introduce new interactive landmarks disguised as scenery.
2. Separate **large background props** from **tiny walkable-floor accents**. `Reserved` includes Ground plus a sealing halo; subtracting it from every layer would eliminate almost all walkable-floor decoration. Use it conservatively for large background props, and use explicit route/location exclusions plus very low height for floor accents.
3. Starting clearances: protect a two-cell neighborhood around routes/locations and a five-cell neighborhood around gates, plus bridge/warp approaches. These are proposed visual clearances consistent with existing generation intent; narrow them only after screen-space review. Reject a prop when its full projected footprint/canopy crosses the protected area, not just when its origin does.
4. Decorative prefabs have no movement/interaction scripts, Rigidbody, NavMeshObstacle, trigger, or collider. `addMeshCollider=false` does not remove a collider already inside a prefab: inspect children too. Exclude scenery from interaction raycasts where applicable.
5. Keep water visually water and bridges visually traversable. Do not scatter stepping-stone chains, ice bridges, fake roads, closed-looking gates, or loot-like chests where they imply gameplay that does not exist.
6. Preserve marker color/icon identity and actor visibility. Tall props on the camera-facing side need stronger exclusions or a tested visual fade; adding many invisible colliders would not solve occlusion.
7. Derive randomness from campaign seed + stable decoration-layer ID + cell coordinates + art version. Density or variant changes must not change campaign generation or other layers' placements.

## Integration proposal

The safest first implementation is a dedicated **overworld decoration presentation pass** using TWC's layering ideas and existing prefabs, while leaving the current campaign-mask adapter intact. Own a separate decoration root so the existing blanket TWC renderer suppression cannot hide it. Add that root and its generated meshes to terrain cache ownership explicitly.

If editor-authored TWC build layers are preferred, extend the presentation adapter to register derived decoration masks separately from immutable gameplay bindings, and narrow `OverworldBiomeRenderer` suppression to the replaced surface layers. Preserve build-layer GUID connections and both subdivided and `_UNSUBD` caches. This is a code change, not just a new layer checkbox.

For either route:

- Build static details once per campaign/art configuration, not each frame or whenever a gate opens. Keep dynamic gate/marker state separate from static decoration.
- Batch/merge compatible opaque props by material into bounded spatial chunks, or use a verified instanced rendering path. Keep useful culling; do not merge the entire overworld into one mesh. Merely sharing a material does not prove draw-call batching.
- Start with the existing 32-cell chunk partition for bookkeeping, then profile whether smaller decoration chunks cull better. Default tiny ground detail to no shadows and no lights; reserve shadows for prominent silhouettes. Do not copy point lights/particle systems from dungeon demo torch prefabs wholesale.
- Add decoration ownership to cache restore, hide, clear, and rebuild paths. Include an explicit art configuration/version invalidation mechanism so changing assets or density cannot leave stale cached scenery.
- First compare a representative camera view before/after, including dense forest, shore, narrow gate, bridge, and location approach. Profile the target build before expanding densities.

## Needed models: production backlog

Follow the companion guide's low-poly, super-deformed proportions and export rules. Counts below are **requested variants**, and triangle ranges are **proposed LOD0 budgets per variant**, not measurements of existing assets. One shared opaque material is preferred; two are acceptable for large trees/markers. Color-only biome changes should reuse geometry/material parameters instead of multiplying unique models.

`P0` = first visual pass; `P1` = biome identity; `P2` = later polish. “Reuse/adapt” means inspect the existing asset first and generate only the missing quality or variation. “New” means a requested addition; it is not a claim that no vaguely similar prop exists anywhere in bundled packs.

### P0: first useful overworld set

| ID / asset family | Variants | Triangles each | Footprint / height in cells | Production decision and placement |
|---|---:|---:|---|---|
| OW-01 Broadleaf tree | 3 | 500–1,000 | Crown 0.8–1.4; height 1–1.8 | Reuse/adapt Park Tree first; add squat, leaning, and forked silhouettes for blocked groves. |
| OW-02 Boulder cluster | 3 | 120–350 | 0.4–0.9; height 0.2–0.5 | Reuse/adapt CliffIsland stones; broad, tall, and broken groups. Low variants for distant roadside accents. |
| OW-03 Rocky outcrop / ridge mass | 3 | 300–800 | 0.8–1.5; height 0.5–1.2 | New chunky wedge, split peak, and stepped mass; blocked rock land only. No new traversable cliff topology. |
| OW-04 Low shrub / grass clump | 3 | 40–160 | 0.15–0.35; height 0.05–0.15 | New opaque grouped foliage; no individual leaf cards. Tiny floor accents, larger silhouette kept off paths. |
| OW-05 Pebble / ground cluster | 2 | 30–100 | 0.15–0.35; height at most 0.08 | Adapt OW-02 or new low mesh; break up ground sparingly without appearing to block a cell. |
| OW-06 Shore reed / bank-stone group | 2 | 80–250 | 0.25–0.6; height 0.1–0.35 | New one reed and one stone group; never obscure bridge landings or gate channels. |
| OW-07 Town marker miniature | 1 | 800–1,500 | About 1; height 0.6–1 | Adapt existing house/roof kit into one compact cluster. Replace only the current town marker visual; add no satellite town. |
| OW-08 Dungeon entrance marker | 2 | 600–1,200 | About 1; height 0.5–1 | New cave-mouth and ruined-arch silhouettes, retaining existing dungeon marker semantics/identity. |

**P0 total: 19 requested variants across 8 families**, several supplied by adapting existing models. Prototype with existing tree/stone/house assets before commissioning all variants. This is the best first batch for Blender generation.

### P1: biome identity and location dressing

| ID / asset family | Variants | Triangles each | Placement and design brief |
|---|---:|---:|---|
| OW-09 Conifer | 2 | 350–750 | Forest/mountain/tundra blocked vegetation. Broad tiered crown; reuse snow tint/overlay, avoid a separate high-detail snow tree. |
| OW-10 Dead / marsh tree | 2 | 200–550 | One bare crooked tree and one squat marsh crown; sparse blocked wetland background. |
| OW-11 Cactus / dry scrub | 2 | 100–400 | Adapt CliffIsland cactus plus one low scrub variant. Desert background, no spikes fine enough to shimmer. |
| OW-12 Basalt / volcanic outcrop | 2 | 250–650 | Clustered angular columns and cracked mass on blocked volcanic land. Emission optional and restrained; no point lights per rock. |
| OW-13 Snow / ice outcrop | 2 | 150–450 | Tundra background forms with broad facets. No ice across navigable water implying a new crossing. |
| OW-14 Fallen log / stump group | 2 | 100–300 | Forest/marsh background. Keep logs away from routes so they do not suggest climb/jump obstacles. |
| OW-15 Mushroom / flower patch | 2 | 60–180 | Oversized simple caps/petals; small grouped accents. Avoid collectible appearance and no interaction behavior. |
| OW-16 Broken wall / column ruin | 3 | 180–500 | Reuse/adapt dungeon masonry: wall remnant, broken column, rubble base. Sparse dressing beside existing dungeon/landmark locations. |
| OW-17 Road milestone / empty signpost | 2 | 80–220 | Low readable roadside punctuation; no directional text that conflicts with procedural route destinations. |
| OW-18 Landmark monument visual | 2 | 400–900 | Compact standing-stone and small shrine silhouettes for existing landmark anchors only. Preserve current marker identity and reward behavior. |

**P1 total: 21 variants across 10 families.** Restrict large variants to blocked background and use the clearances above. Material reuse should connect each biome set to the shared hero palette.

### P2: modular edge and crossing polish

| ID / asset family | Pieces | Triangles each | Reuse and constraints |
|---|---:|---:|---|
| OW-19 Bank / cliff trim kit | 5 | 80–350 | Straight, inner corner, outer corner, end, short transition. Adapt CliffIsland vocabulary; exact two-unit final seam convention, cosmetic height only. Retain existing land/water mask. |
| OW-20 Bridge dressing kit | 3 | 150–450 | Deck overlay, rail section, landing end. Inspect existing WoodenBridge kit first. Deck follows existing Bridges cells; no new bridge placement or changed water access. Rails must not obscure actors or suggest blocked exits. |
| OW-21 Fence / settlement remnant | 2 | 80–200 | Short straight and broken-end piece for background beside existing town markers. No full enclosure across a route. |

**P2 total: 10 pieces across 3 families.** Do not commission full replacement terrain sets yet. First prove that inexpensive object silhouettes, ground variation, and marker improvements supply enough interest.

### Shared Blender delivery requirements

- Use the existing hero as a scale reference. A cell is two Unity units after import/placement; the footprint values above are final visible bounds, not raw FBX dimensions. Keep canopy bounds in the manifest.
- Provide a ground-contact origin, consistent orientation, applied mesh scale, UVs for a shared palette/atlas, and opaque materials. Terrain trim additionally needs exact seam anchors; markers keep the existing game-facing root.
- Supply `.blend`, FBX, textures when needed, and a manifest of triangles, used materials, footprint, height, bounds, variants, and suggested biome/layer. Use names such as `EE_OW_TreeBroadleaf_A`.
- Static props require no armature. No embedded colliders, lights, particles, gameplay scripts, or arbitrary animation unless explicitly requested for a later pass.
- For prominent repeated trees/outcrops, provide simplified LODs only when profiling/camera scale warrants them; tiny clusters need no automatic LOD chain. Preserve a recognizable outline at reduced size.
- Render a small mixed patch beside a hero, not just a close-up turntable. Verify the prop still reads when its complete silhouette is only a few dozen pixels high.

## Validation before shipping a visual pass

- Compare the same seed before/after: grid masks, routes, gate footprints, location IDs/positions, `CanStep`, and boat/warp behavior must be identical.
- Compare visual placement across rebuilds and town/dungeon returns. No duplicate objects, stale cache, missing decorations, leaked generated meshes, or reseeding of gameplay randomness.
- Confirm no new collider, trigger, obstacle, interactable, or raycast target slipped in through reused prefabs.
- Test every biome and narrow approaches in the actual camera; keep actors, markers, closed/open gates, water, and bridges legible.
- Record frame time, draw calls, triangle/renderer counts, memory, and generation time on a specified target/resolution/quality. Compare a dense view and a wide overview to the undecorated baseline; the asset budgets alone are not performance acceptance.

## Source locations

- [Village demo](../Assets/TileWorldCreator/Demo/02_Village/02_Village.unity) and [Village asset](../Assets/TileWorldCreator/Demo/02_Village/VillageAsset.asset).
- [L-system village demo](../Assets/TileWorldCreator/Demo/02_Village/02_Village_LSystem.unity) and [project village asset](../Assets/TileWorldCreator/VillageLSystemAsset.asset).
- [Dungeon demo](../Assets/TileWorldCreator/Demo/07_Dungeon/07_Dungeon.unity) and [Dungeon game demo](../Assets/TileWorldCreator/Demo/07_Dungeon/07_Dungeon_Game.unity).
- [Runtime editor demo](../Assets/TileWorldCreator/Demo/01_RuntimeEditor/01_RuntimeEditor.unity).
- [Campaign adapter](../Assets/Scripts/Overworld/CampaignOverworld.cs), [biome renderer](../Assets/Scripts/Overworld/OverworldBiomeRenderer.cs), and [terrain cache](../Assets/Scripts/Overworld/OverworldTerrainCache.cs).
- [TWC object builder](../Assets/TileWorldCreator/Code/Actions/Instantiation/InstantiateObjects.cs), [Select](../Assets/TileWorldCreator/Code/Actions/Modifiers/Select.cs), and [RemoveNeighbours](../Assets/TileWorldCreator/Code/Actions/Modifiers/RemoveNeighbours.cs).
- [Campaign terrain template](../Assets/Overworld/CampaignTerrain.asset), [overworld behavior and masks](OverworldGrid.md), and [Blender modeling guide](BlenderModelGuide.md).
