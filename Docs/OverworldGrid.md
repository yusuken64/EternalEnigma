# Campaign-to-overworld grid

The core now converts a campaign definition into an immutable, walkable grid with
named boolean layers and location/route/lock coordinates. The optional
`CampaignOverworld` Unity component imports those masks into TileWorldCreator.

```csharp
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

var campaign = CampaignGenerator.Generate(42);
var grid = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(256, 256));
bool[,] ground = grid.Layers[OverworldLayers.Ground].ToArray();
GridPoint start = grid.PlayerStart;
GridPoint town = grid.Locations["town-0"];
```

Every layer uses `[x, y]` indexing with exactly `Width Ã— Height` cells. `ToArray()`
returns an independent copy, so TWC cannot modify the core's movement model.
`grid.Routes` maps walking route IDs to contiguous, ordered grid paths from the
campaign route's `From` to `To`. Warp routes instead contain exactly two landing
pad coordinates; no terrain is carved between them. `grid.Warps` identifies these
nonlocal links. `grid.Locks` maps physical gated routes to their footprints.

## TileWorldCreator setup

### Playable Overworld scene

Use **Tools > Eternal Enigma > Launch Overworld Sandbox**, or open
`Assets/Scenes/Overworld.unity` and press Play. Both use the scene's
`CampaignOverworld.Seed` (42 by default). Normal New Game / Continue navigation
explicitly selects campaign mode. There is no separate test scene.

Both modes render the same `CampaignContext.Grid`, with identical location seeds,
starting enclosure, capabilities and physical gates. Sandbox owns a temporary
save object; writes and clears are suppressed and exiting restores the previous
player save object. `OverworldSandboxControls` is enabled only for sandbox mode.
Its buttons claim eligible rewards, select companions at town markers, and
simulate a victory at the current dungeon marker through `CompleteDungeon`.
An ordinary reward claim cannot obtain the starting key.

The opening has two gates. Clear `story-0` inside `town-0` to unlock the town
exit. Clear `repeatable-0` in the surrounding town area to receive a separate
key, then use it at the physical `starter-exit` gate to reach `checkpoint-0`.
Town exits are scene gates; `grid.CanStep` also enforces departure from the town
marker for sandbox/explorer movement. Interior nodes share their town tile and
have one-point graph route projections, with no separate overworld marker.
Boat, diagonal movement and later warps cannot bypass either opening gate.

Move with WASD, arrows, or a gamepad's left stick/D-pad. Enter / gamepad A enters
a town or dungeon in campaign mode, claims a landmark reward, or opens an adjacent
gate. Campaign party selection is inside towns; sandbox selection is at town
markers. Green markers are towns, red markers dungeons, gold markers other
locations, and purple bars closed
gates. Acquiring a key or capability leaves physical gates visible and blocked.
Stand directly beside a gate and press Enter / gamepad A (or its HUD button) to
use the matching key or capability. Opening displays a confirmation; permanent
gates stay open, while area gates still require current capabilities. Boat-only
water crossings remain automatic sailing checks. Progress is in memory for the current Play session. Location markers
do not launch combat or load the existing Town/Dungeon scenes.

Actor roots retain Town's cell-corner coordinates (`cell * cellSize`). Terrain,
markers and camera focus use the half-tile center offset, matching the Town hero's
visual child; this offset does not change grid movement or gate checks.

The scene is included in build settings; the main-menu flow is unchanged.
`Tools > Eternal Enigma > Overworld > Set Up Scene` authors the setup into an empty
Overworld scene and refuses to replace an existing controller. For a mesh preview
outside Play Mode, use `Generate And Build Overworld` on `CampaignOverworld`.

`node Tools/unity-mcp.mjs harness Overworld` loads the authored scene in Play Mode,
checks terrain construction, hero movement, camera following and closed gates,
and writes a camera render to `Temp/OverworldScene/play-preview.png`.

### Custom scenes

1. Use a dedicated GameObject with **TileWorldCreator** and **CampaignOverworld**.
   Keep the existing town generator on its own object.
2. Assign a TWC asset to the adapter's **Template**, or use the asset already
   assigned to its TileWorldCreator component. Set seed and grid dimensions.
3. Configure the template's tile/object build layers and their prefabs. Map core
   layer names to template blueprint names in **Layer Bindings**. For example,
   `Ground â†’ Floor`, `Roads â†’ Road`, and `Towns â†’ SettlementPositions`.
   With no bindings, every core layer is imported under its own name. Explicit
   bindings avoid building TWC caches for metadata masks that need no rendering.
4. On the CampaignOverworld component's context menu, choose **Generate Campaign
   Layers** to inspect masks, or **Generate And Build Overworld** to run the
   template's build layers as well.

For a campaign produced elsewhere:

```csharp
OverworldGrid grid = overworldComponent.Generate(campaign);
overworldComponent.BuildMeshes();

// Or import an existing grid with a particular progress snapshot:
overworldComponent.Apply(grid, session.HeldCapabilities,
    new HashSet<string>(session.ResolvedLocks));
```

The adapter clones the asset, preserves matching blueprint GUIDs and existing
build-layer links, replaces blueprint action stacks with deterministic masks, and
runs TWC's normal blueprint pipeline. Both subdivided tile maps and `_UNSUBD`
object-placement maps are populated. Merely assigning `layer.map` would not
populate those build caches.

Unmapped template blueprint layers receive empty masks so old town/dungeon
generation does not leak into the overworld. Authored assets are never changed.
The generated clone is temporary; edit the template to author persistent art.
Without a template, generation still creates inspectable layers, but mesh building
requires authored build layers/prefabs. Removing the adapter restores the previous
TWC asset and blueprint-map cache. Unity's global random state is preserved during
blueprint generation. TWC's own build-layer visual randomness is separate.

## Layers

| Layer | Meaning |
|---|---|
| `Ground` | All potential walkable ground, including conditional gate tiles. Use for floor/terrain rendering. |
| `Walkable` | Conservative mask with all locks closed and no Boat; adapter can produce a capability/resolution snapshot. Do not treat it as permanently authoritative. |
| `Roads` | Carved routes, including the portions crossing gates. Rendering roads must not create new walkable tiles. |
| `Mountains`, `Trees` | Blocked-background decoration; these differ from the walkable mountain/forest biomes. |
| `Water` | All visible water, including the blocked outer border and navigable inland water. |
| `NavigableWater` | Water within `Ground`; always requires Boat, including when adjacent locks are resolved. |
| `Bridges` | Dry road/causeway tiles crossing a water region. No Boat needed. |
| `Biome/<name>` | Grassland, Desert, Water, Mountain, Forest, Tundra, Marsh, Volcanic. Mutually exclusive masks partitioning `Ground`. |
| `Reserved` | Ground plus a one-tile sealing halo. Decoration must not carve or block these cells. This is metadata, not a prefab placement layer. |
| `Towns`, `StoryDungeons`, `RepeatableDungeons`, `FinalDungeon`, `Converters`, `Landmarks`, `Secrets` | One-tile placement markers by location kind. These are entrances/anchors, not building footprints or interiors. |
| `PlayerStart` | Exactly one marker at the start town. |
| `Locks`, `AreaLocks`, `ObstacleLocks`, `InteractionLocks` | Physical lock footprints. Resolve requirement details through their route IDs. |
| `Region/<region-id>` | Non-overlapping ownership masks partitioning the ground. They describe placed ground, not full geographic region polygons. |

## Movement and gates

```csharp
bool legal = grid.CanStep(fromCell, toCell, session.HeldCapabilities,
    new HashSet<string>(session.ResolvedLocks));
bool[,] currentWalkable = grid.CreateWalkableLayer(session.HeldCapabilities,
    new HashSet<string>(session.ResolvedLocks));
GridLock gate = grid.LockAt(toCell); // null outside a lock footprint
```

Movement is eight-way, one tile per step, with sealing corners: diagonal movement
requires both orthogonal side cells to be passable. Lock tiles use the campaign's
same DNF requirement evaluator. Obstacle and interaction resolutions can latch;
area gates always require the currently held capabilities. Gate footprints span
short passes of two to four tiles through sealed territory boundaries.

These queries do not mutate progression. A movement/interaction controller must
record permanent resolutions through its progression logic. The existing town
`WalkableMap` is not an overworld controller: its Houses/Trees mask and permissive
corner policy cannot represent these gates. The dedicated `OverworldScene`
controller adds `OverworldGates` interaction state to these queries and records
latched gates when the player explicitly opens them. The console explorer uses
the same interaction state. Keys remain in inventory after use.

## Geometry and validation

Grid generation **version 10** shapes a continuous landmass before laying roads.
Seeded smooth coordinate noise bends a weighted territory partition; A is given
extra space for required returns. The loose geographical guide remains:

```text
. F E
A B D
. . C
```

Consecutive progression stages share short sealed passes, including B–C.
Additional stages occupy separate territories inside F. Ungated graph components
share countryside; gated rewards occupy irregular protected pockets with one
entrance. Return destinations remain physically in A. Destinations use seeded
minimum-distance sampling (11 tiles, 6 for the opening pair), with clearances from boundaries.

Roads use deterministic cardinal terrain pathfinding and stay grid-aligned. Each open area has a minimum
spanning network under Manhattan distance plus one additional edge when it has
at least three destinations. Campaign routes remain valid navigation records;
only gate routes and the local network become visible roads. B–D/E/F remain
keyed warps. Closed-gate components and sealed diagonal corners are checked by
the existing graph-to-grid validator. Failed constraints produce diagnostics
through at most 64 deterministic retries; there is no corridor-layout fallback.

The default remains 256×256, with supported dimensions from 248 through 1024.
`AreaExpansionRadius` is source-compatible (0–12, default 6) and now controls
boundary variation strength. Zero produces smooth, broad territories.
Landscape biome masks (`Landscape/<biome>`) cover impassable land as well as
floor, separately from gameplay biome masks and `Ground`. The renderer uses
existing biome materials for continuous terrain and shallow raised barriers.

Biomes are sampled without replacement using stream 103, with grassland reserved
for the starting region. `RegionBiomes` describes region identity; `BiomeAt` gives
the actual floor biome. Water regions retain dry islands and causeways around
sampled locations and navigation routes. Boat-only area gates are water crossings. Every
navigable water tile requires Boat, even with resolved gates; the outer water
border remains impassable. Boat providers and required non-Boat routes remain dry.

`CampaignRoute.HasGate` includes keyed shortcuts even though their capability
requirement is empty; `IsWarp` distinguishes them from physical gate footprints.
Each warp starts locked from both ends. Collect its
key at the landmark in D, E or F using Enter / gamepad A or **Collect key**.
In the playable simulations, `OverworldGates.CollectKey` only adds the key to
inventory. Return to either warp pad and press Enter / A to use it and open the
passage; warp travel remains a separate action. Opening records the passage in
the resolved-route set, allowing travel in both directions without consuming the
key. The abstract campaign solver still combines collection and resolution for
reachability analysis. `grid.TryWarp` validates the current endpoint, unlock state and destination
before returning the landing position. Walking remains restricted to adjacent cells.
The console shows O for warp gates and K/k for uncollected/collected key sites;
press V at a gate to choose a destination. Unity uses cyan pads and explicit
**Warp to biome** HUD buttons. HUDs list keys and the required return objective.
With all three keys, every biome is at most two biome transitions away.
Keys and gate resolutions remain session-local. Actual water still requires Boat.
The core also retains support for far-side and capability shortcut kinds.

The validator rejects occupied-floor aspect ratios above 1.5 and shortcuts that
save less than 25% of shortest endpoint travel with all capabilities
and all reachable permanent unlocks available. The comparison blocks only the
shortcut's own gate or warp, leaving other shortcuts usable. Warp activation costs
one step in this comparison. This detects redundant loops
and terrain that erases their travel benefit.

Unity's `OverworldBiomeRenderer` draws textured, chunked floor meshes using the
existing half-tile alignment. Edit `Assets/Overworld/Biome*.mat` to change the
palette. **Tools > Eternal Enigma > Overworld > Apply Biome Floors** assigns the
materials to an existing scene while preserving material edits.

`OverworldGridValidator` removes gate footprints and compares the remaining tile
components against the campaign's open-route components. Each gate must connect
exactly its intended components. Gate footprints must be connected and cannot
touch other gates, which rules out diagonal interactions between gate states.
The validator also checks location placement, every realized route's steps, and
requires at least 30% of each dry region's floor to have a fully open 5×5 neighborhood.
This establishes graph/grid connectivity equivalence for combinations of gate
states under the shared sealing rule, rather than checking just one spoiler path.
It does not assert that arbitrary prefab colliders or later TWC modifiers preserve
that topology; runtime movement must continue to use the core grid.

Tests compare actual flood-fill reachability for sampled capability/resolution
states, deliberately carve a gate bypass and ensure rejection, check deterministic
layers and defensive copies, and exercise too-small/cyclic inputs. Unity tests
check asset isolation, layer GUIDs, both TWC cache formats and real prefab placement
through `InstantiateObjects`.

## Inspect or export without Unity

From `Core/`:

```powershell
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --grid --output ../Temp/OverworldPreview
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed -500 --count 1000 --grid
```

The export directory contains campaign JSON, overworld JSON and an SVG preview.
JSON layers use rows of `0`/`1` strings: array index is y, character index is x.
Rows run from y=0 upwards; the SVG flips y for display and includes location-ID
hover labels. Exports are diagnostics, not a save/restore format. CI also validates
1,000 campaign/grid pairs and retains the sweep log.

Terrain variation uses independent seeded smooth noise for water, vegetation and
rock. Grassland has lakes, groves and rocky patches; forest favors trees, mountain
and volcanic regions favor rock, marsh favors water and vegetation, and desert
and tundra use sparser features. Trees and rock are impassable; inland water
requires Boat. Two-tile route/location buffers and five-tile gate halos preserve
access and short approaches. Disconnected scraps are absorbed into solid terrain.
Gated pockets use periodic angular noise with multiple scales to vary their
outlines, while their sealing barriers and single entrances remain validated.

Version 10 targets regions about 25% smaller in travel scale: a reduced land
footprint on the same canvas, shorter destination spacing, and fewer separate
reward sites. Scene gates have no physical lock footprint; connectivity validation
projects their town-area routes into the same terrain component while runtime
departure checks preserve the logical gate. Exact distances vary by seed.
