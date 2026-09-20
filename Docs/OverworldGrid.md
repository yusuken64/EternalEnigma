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

Every layer uses `[x, y]` indexing with exactly `Width × Height` cells. `ToArray()`
returns an independent copy, so TWC cannot modify the core's movement model.
`grid.Routes` maps route IDs to contiguous, ordered grid paths from the campaign
route's `From` to `To`. `grid.Locks` maps gated route IDs to their footprints.

## TileWorldCreator setup

1. Use a dedicated GameObject with **TileWorldCreator** and **CampaignOverworld**.
   Keep the existing town generator on its own object.
2. Assign a TWC asset to the adapter's **Template**, or use the asset already
   assigned to its TileWorldCreator component. Set seed and grid dimensions.
3. Configure the template's tile/object build layers and their prefabs. Map core
   layer names to template blueprint names in **Layer Bindings**. For example,
   `Ground → Floor`, `Roads → Road`, and `Towns → SettlementPositions`.
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
| `Walkable` | Conservative mask with all locks closed; adapter can produce a capability/resolution snapshot. Do not treat it as permanently authoritative. |
| `Roads` | Carved routes, including the portions crossing gates. Rendering roads must not create new walkable tiles. |
| `Mountains`, `Trees`, `Water` | Disjoint blocked-background masks. Water currently forms an outer border; this is not navigable vehicle-zone generation. |
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
area gates always require the currently held capabilities. Area footprints span
three corridor tiles; other gates occupy one tile.

These queries do not mutate progression. A movement/interaction controller must
record permanent resolutions through its progression logic. The existing town
`WalkableMap` is not an overworld controller: its Houses/Trees mask and permissive
corner policy cannot represent these gates. This change supplies generation and
the renderer adapter, not a replacement player controller or automatic scene flow.

## Geometry and validation

Version 1 embeds the current campaign **tree** as separated branches with small
location clearings, shared junctions and reserved gate approaches. Seed streams
100/101 determine branch order and blocked-background decoration. Existing campaign
streams are unchanged. This is a conservative overworld layout foundation, not
finished natural geography, seas, docks, broad capability fields or populated towns.

Cyclic/parallel-route campaigns fail explicitly rather than silently dropping
routes or introducing crossings. A map too small for the layout reports its
minimum required dimensions. Dimensions are configurable from 16 to 1024 per axis;
default dimensions are 256×256. The generator validates the campaign first and
the finished grid before returning either to a renderer.

`OverworldGridValidator` removes gate footprints and compares the remaining tile
components against the campaign's open-route components. Each gate must connect
exactly its intended components. Gate footprints must be connected and cannot
touch other gates, which rules out diagonal interactions between gate states.
The validator also checks location placement and every realized route's steps.
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
