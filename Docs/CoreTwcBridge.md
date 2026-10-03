# Core and TileWorldCreator integration

`Core/EternalEnigma.Core/EternalEnigma.Core` targets .NET Standard 2.1 and has no Unity or TWC
references. It returns immutable `Campaign`, `OverworldGrid`, `TownPlan` and `DungeonFloor`
data, including masks, rooms, slots and seeded placements. Unity owns rendering and gameplay.

## Compiled boundary

Unity references `Assets/Plugins/EternalEnigma.Core/EternalEnigma.Core.dll`, not the Core
source project. **Tools > Eternal Enigma > Core > Build and Import DLL** builds Release
outside Assets, copies changed bytes and imports the plugin while preserving its GUID.
Command-line builds may copy the same Release DLL before Unity validation. Rebuild it whenever
Core's public data or behavior changes.

## Mask adapters

- `CampaignOverworld` imports overworld masks via `CampaignLayerAction` and configures presentation inputs.
- `CoreDungeonLayerGenerator` selects a named layer from `CoreLayoutCache.GetDungeon`.
- `CoreTownLayerGenerator` selects a named layer from `CoreLayoutCache.GetTown`.
- `CoreLayoutCache` shares a result across all layers on a creator and invalidates when options change.

These are distinct implemented adapters; town/dungeon do not use hypothetical `CampaignTown`,
`TownGrid` or `DungeonGrid` APIs. `GridLayer.ToArray()` provides an independent `[x,y]` mask.
Core `GridPoint` is converted to Unity coordinates at the boundary. TWC classifications,
presets and project build actions create presentation without becoming movement authority.

The committed dungeon/throne/village assets already contain Core-backed action stacks.
Completed rewrite tools are removed; **Core Layers > Verify Assets** remains. Edit
Odin-serialized TWC assets through Unity. Preserve blueprint GUIDs referenced by build layers.

Core tests/CLI/explorer run without Unity. Unity integration tests check imported API behavior,
mask/cache agreement, prefab wiring and presentation. Neither test layer replaces the other.
See [Core](../Core/README.md), [dungeon floors](DungeonFloor.md) and [overworld](OverworldGrid.md).
