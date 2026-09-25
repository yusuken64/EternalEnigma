# Dungeon floors and town interiors

The core generates a single dungeon floor or town interior as an immutable,
validated grid of named boolean layers, mirroring the overworld's model (see
[overworld grids](OverworldGrid.md)) but at building/room scale.
`EternalEnigma.Core.Generation.DungeonFloorGenerator.Generate(DungeonFloorOptions)`
returns a `DungeonFloor`; `EternalEnigma.Core.Generation.TownPlanGenerator.Generate(TownPlanOptions)`
returns a `TownPlan`. Both retry generation up to 32 times with independent
seed streams per attempt and validate the result
(`DungeonFloorValidator`/`TownPlanValidator`) before returning it.

```csharp
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

var floor = DungeonFloorGenerator.Generate(new DungeonFloorOptions(seed: 42, enemyCount: 10, goldCount: 5));
bool[,] floorMask = floor.Layers[DungeonLayers.Floor].ToArray();
GridPoint start = floor.Start;

var town = TownPlanGenerator.Generate(new TownPlanOptions(seed: 42));
bool[,] walkable = town.Layers[TownLayers.Walkable].ToArray();
```

## Status

The generation code in this document (`DungeonFloorGenerator`, `TownPlanGenerator`,
`CoreDungeonLayerGenerator`, `CoreTownLayerGenerator`, `CoreLayoutCache`, and the
rewired `Town.cs`/`WalkableMap.cs`/`TileWorldDungeon.cs`/`Game.cs`) is implemented
and, where it is dotnet-testable outside Unity, passing. Two things are not yet
true, and a developer picking this up should do them before relying on the
Unity side:

1. **The shipped `.asset` files have not been rewritten.** `Assets/Prefabs/Dungeon/DungeonAsset.asset`,
   `Assets/Prefabs/Dungeon/DungeonThroneAsset.asset` and
   `Assets/TileWorldCreator/VillageLSystemAsset.asset` still run their old
   vendored generator action stacks. Open Unity and run
   **Tools/Eternal Enigma/Core Layers/Rewrite Dungeon Assets** and
   **Tools/Eternal Enigma/Core Layers/Rewrite Town Asset**, then
   **Tools/Eternal Enigma/Core Layers/Verify Assets** to confirm all three
   assets now carry exactly one `CoreDungeonLayerGenerator`/`CoreTownLayerGenerator`
   action per core layer with matching options.
2. **None of this has been run inside Unity.** No EditMode, PlayMode or Town
   harness (`node Tools/unity-mcp.mjs harness EditMode|PlayMode|Town`) has
   exercised the rewired scripts against a live Unity session, because this
   environment has no Unity `Library/` folder. Treat the Unity-side wiring as
   implemented-but-unverified, not as tested-and-working, until those harnesses
   run and any resulting seed-dependent PlayMode test expectations are fixed.

## Layers

### `DungeonLayers`

| Layer | Meaning |
|---|---|
| `Floor` | Walkable cells; the only layer Unity gameplay reads directly for movement. |
| `Dungeon` | Wall mass; always the exact inverse of `Floor` (`Dungeon == !Floor`) for every cell. |
| `Carpet` | Decorative floor accent, a subset of `Floor` (twice-eroded room interior). |
| `Columns` | Subset of `Dungeon`, placed at the four corners of rooms at least 5x5. |
| `Torchlights` | Subset of `Dungeon`, placed periodically along room wall edges. |
| `Start` | Single-cell marker; always on `Floor`. |
| `Stairs` | Single-cell marker; always on `Floor`, always reachable from `Start`. |

### `TownLayers`

| Layer | Meaning |
|---|---|
| `Roads` | The vertical spine plus paths from every building door and a horizontal avenue. |
| `Houses` | Building footprints (3x4 bodies north of each door), minus any carved shop interior. |
| `Trees` | Scenery blockers scattered outside roads/buildings/the reserved corridor. |
| `Parks` | Decorative open-area rectangles; never overlap roads, houses, trees or shops. |
| `Roofs` | Copy of `Houses`, for build-layer rendering above the floor. |
| `Buildings` | One-cell door markers, in the same raster order as `TownPlan.BuildingSlots`. |
| `Allies` | One-cell ally spawn markers, in the same raster order as `TownPlan.AllySlots`. |
| `Dungeon` | Single-cell dungeon-entrance marker at the bottom of the road spine. |
| `ShopFloor` | Interior floor of carved shop rooms. |
| `ShopWalls` | Interior walls of carved shop rooms. |
| `Walkable` | Derived: `Walkable == !(Houses | Trees | ShopWalls)`. Unity's `WalkableMap` reads this directly. |

## Placements and rolls

`DungeonFloor.Enemies`, `.Gold`, `.Items` and `.Traps` are lists of `Placement`
(a cell plus an `int Roll` drawn from the same seeded stream as the position).
Unity picks which prefab to instantiate at a placement with `roll % count`
against its own prefab list, so the same seed always picks the same prefab as
long as the prefab list doesn't change order. `DungeonFloor.GatheringSites` is
placed afterward from its own seed stream (`900 + attempt`) so it never shifts
the other placements. `TownPlan.AllySlots` placements work the same way for
ally prefab selection.

## Throne floors

A floor is a throne floor when it is the first or last floor of its tier's
range (`CampaignContext.Floors(tier)`, e.g. tier 0 is floors 1..5, so floor 1
and floor 5 are both throne floors). Throne floors skip normal generation
entirely and use the fixed `ThroneRoomTemplate`: a 12x12 room (`GridRect(1, 1, 10, 10)`)
with `Start` at `(6, 4)` and `Stairs` at `(6, 9)`, no enemies/gold/items/traps.
`DungeonFloorOptions.Throne(seed)` produces the matching fixed `12x12`,
zero-placement options; the constructor throws if a throne floor's dimensions
or placement counts are anything else.

## Seeding

`CampaignContext.LocationSeed(campaignSeed, locationId, floor = 0)` hashes the
campaign seed, location id and floor number (FNV-1a over their string
concatenation) into a single deterministic `int` seed. The instance method
`CampaignContext.LocationSeed(location, floor)` uses the context's own campaign
seed. `CampaignContext.DungeonFloorOptionsFor(campaignSeed, locationId, floor, tier)`
combines that seed with the tier's throne-floor check to build the right
`DungeonFloorOptions`. `CampaignContext.DungeonFloor(locationId, floor)` and
`CampaignContext.Town(townId, shopFlags, allyCount)` are the campaign-aware
entry points that wrap `DungeonFloorGenerator.Generate`/`TownPlanGenerator.Generate`
with these seeds.

## Movement rules

`DungeonFloor.CanStep`/`.Neighbors` use `GridSteps` with `DiagonalRule.RequireOpenSides`:
a diagonal step is only legal when both orthogonal cells next to it are also
open, matching dungeon corridors that shouldn't let the player cut through a
wall corner. `TownPlan.CanStep` uses `DiagonalRule.AllowCornerCutting` instead,
matching open town squares where corner-cutting is fine. `WalkableMap.CanWalkTo`
(Unity) calls the equivalent `GridMovement.CanStep(..., DiagonalMovement.AllowCornerCutting)`
against the cached `TownPlan.Layers[TownLayers.Walkable]` mask.

## TWC integration

Two Unity classes generate a full core result and slice out one layer per TWC
blueprint action:

- `CoreDungeonLayerGenerator` (`Assets/Scripts/Generation/CoreDungeonLayerGenerator.cs`):
  one per dungeon blueprint layer, configured with `LayerName` (a `DungeonLayers`
  constant), `Throne`, and `EnemyCount`/`GoldCount`/`ItemCount`/`TrapCount`. Its
  `Execute` calls `CoreLayoutCache.GetDungeon(twc, Options(twc))` and merges
  that layer's cells into the TWC map, throwing if the layer's dimensions don't
  match the asset (a throne asset must be exactly 12x12).
- `CoreTownLayerGenerator` (`Assets/Scripts/Generation/CoreTownLayerGenerator.cs`):
  one per town blueprint layer, configured with `LayerName` (a `TownLayers`
  constant), `BuildingCount`, `ShopFlags`, `AllyCount`, and the party
  spawn/exit cells. Its `Execute` calls `CoreLayoutCache.GetTown(twc, Options(twc))`
  the same way. `CoreTownLayerGenerator.Configure(asset, TownConfiguration)`
  pushes the configuration's building count, per-building shop flags and party
  spawn onto every action in the asset, leaving `AllyCount` as authored.

`CoreLayoutCache` (`Assets/Scripts/Generation/CoreLayoutCache.cs`) caches one
generation result per `TileWorldCreator` instance (a `ConditionalWeakTable`
slot keyed by the component), regenerating only when the `DungeonFloorOptions`/`TownPlanOptions`
change — so every `CoreDungeonLayerGenerator`/`CoreTownLayerGenerator` action on
the same asset shares a single generation rather than re-running per layer.
`CoreLayoutCache.ClearResultFlags(asset)` clears the `mapResultFailed`/`resultFailed`
flags TWC sets on an all-false layer (a legitimately empty `Carpet` or
`ShopFloor` is not a failure) after `ExecuteAllBlueprintLayers` runs. There are
two separate dungeon TWC assets/`TileWorldCreator`s — a normal-floor one and a
throne one (`TileWorldDungeonGenerator.ThroneTileWorldCreator`) — because a
throne floor's fixed 12x12 size differs from a normal floor's configurable
size; `CoreDungeonLayerGenerator.Throne` distinguishes which template to use
per asset.

### Rewrite and verify menus

`Assets/Scripts/Editor/CoreLayerAuthoring.cs` provides three menu items under
**Tools/Eternal Enigma/Core Layers**:

- **Rewrite Dungeon Assets** rewrites every core dungeon layer in
  `Assets/Prefabs/Dungeon/DungeonAsset.asset` (`Throne = false`) and
  `Assets/Prefabs/Dungeon/DungeonThroneAsset.asset` (`Throne = true`) to a
  single `CoreDungeonLayerGenerator` action, preserving blueprint layer GUIDs.
- **Rewrite Town Asset** does the same for
  `Assets/TileWorldCreator/VillageLSystemAsset.asset` with `CoreTownLayerGenerator`,
  and adds any of `ShopFloor`/`ShopWalls`/`Walkable` the asset is missing.
- **Verify Assets** checks all three assets: exactly one action per core
  layer, of the right type, with `LayerName` matching the blueprint layer's
  own name; every core action in an asset sharing the same options (enemy/gold/item/trap
  counts for dungeons, building/shop/ally/spawn/exit for towns); throne assets
  (`mapWidth == 12`) having `Throne = true`; and every build layer's
  `assignedGenerationLayerGuid` matching a real blueprint layer GUID. It logs
  `"OK"` when clean, or one line per issue otherwise.

These three `.asset` files are Odin-serialized; never hand-edit their YAML —
always go through these menu items. See the **Status** section above: as of
this writing, none of the three assets have actually had these menu items run
against them yet.

### DLL re-import

Because `CoreDungeonLayerGenerator`/`CoreTownLayerGenerator` call into
`EternalEnigma.Core.Generation`/`.World` types, any core code change needs the
imported DLL refreshed before Unity picks it up: **Tools > Eternal Enigma >
Core > Build and Import DLL** (see `Core/README.md`'s "Import into Unity"
section). Repeat imports preserve the `.meta` GUID; there is no automatic
build on every script refresh.

## Console explorer usage

From the repository root:

```powershell
dotnet run --project Core/EternalEnigma.Core/EternalEnigma.Campaign.Explorer -- --seed 42 --town town-0
dotnet run --project Core/EternalEnigma.Core/EternalEnigma.Campaign.Explorer -- --seed 42 --dungeon story-0 --floor 2
```

Both print a static preview and imply `--snapshot`; `--dungeon` accepts an
optional `--floor <n>` (defaults to the dungeon's current floor) and fails if
the floor is outside the location's tier range. In interactive mode (no
`--town`/`--dungeon`/`--snapshot`, run from an actual terminal), choosing T or D
at startup or pressing Enter on an overworld town/dungeon marker switches to
the same `TownRenderer`/`DungeonRenderer` views; Enter opens a door/descends
stairs, R claims rewards, Esc returns to the overworld.

## CLI export

From `Core`:

```powershell
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --town town-0 --output ../Temp/TownPreview
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --dungeon story-0 --floor 1 --output ../Temp/DungeonPreview
```

Both require `--output` and write a `.json` (layers as rows of `0`/`1`
strings, plus rooms/placements/slot metadata) and an `.svg` preview per
location, alongside the campaign-sweep/`--grid` export described in
`Core/README.md`. `--dungeon`'s `--floor` defaults to 1.

## Determinism note

Dungeon floors and town plans are never saved — only the campaign seed,
location id and floor number are. Every visit regenerates the same layout
from `CampaignContext.LocationSeed`, so nothing needs to persist the grid
itself. Changing `DungeonFloorGenerator`/`TownPlanGenerator` (or the BSP/plot
parameters they use) changes the layout that an existing save's seed produces
on the next visit, but nothing breaks: there is no stored grid to invalidate,
and placements/rooms/slots are always recomputed from the current generator
against the current seed.
