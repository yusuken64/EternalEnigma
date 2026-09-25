# The Core ↔ TWC barrier

`EternalEnigma.Core` (`Core/EternalEnigma.Core/EternalEnigma.Core`) is a netstandard2.1
library with zero `UnityEngine` or `TWC` references. It generates campaigns, the
overworld grid, and (per the ongoing Core migration) town and dungeon layouts as plain,
deterministic C# - testable with `dotnet test`, runnable headless from
`EternalEnigma.Campaign.Explorer`/`EternalEnigma.Campaign.Cli`, no Unity required.

TileWorldCreator (TWC, `Assets/TileWorldCreator/`) is a third-party Unity Asset Store
plugin that turns boolean "blueprint layer" masks into tiles, meshes and prefabs in a
scene. It has its own procedural actions (L-System roads, Cellular Automata,
`BSPDungeon`, etc.) that are neither deterministic across runs in a way Core's tests can
rely on, nor usable outside Unity.

Between these two is a **barrier**: Core code never references `UnityEngine` or `TWC`,
and nothing on the Unity/TWC side ever links against Core's *source* - only two narrow,
well-defined things are allowed to cross it. Every new adapter (the Town/Dungeon work in
this repo's ongoing Core migration included) must go through exactly these two
crossings, not invent a third.

## What crosses the barrier

### 1. The compiled DLL - the only source-level connection

Unity does not have a project reference to `EternalEnigma.Core.csproj`. Instead, it
imports the *compiled* `EternalEnigma.Core.dll` as a plugin at
`Assets/Plugins/EternalEnigma.Core/EternalEnigma.Core.dll`. The only way that file is
produced or updated is `Tools > Eternal Enigma > Core > Build and Import DLL`
(`Assets/Scripts/Editor/CoreDllImporter.cs`), which:

1. Runs `dotnet build EternalEnigma.Core/EternalEnigma.Core.csproj --configuration Release --framework netstandard2.1` outside the `Assets/` folder (in `Temp/CoreDllImport`), so a failed or partial build can never corrupt the imported plugin.
2. Copies the resulting DLL into `Assets/Plugins/EternalEnigma.Core/` only if its bytes changed.
3. Re-imports it as a Unity plugin and marks it compatible with the Editor and any platform.

Any change to Core - a new generator, a renamed type, a new namespace - is invisible to
Unity until someone runs that command. Unity-side adapter code (see below) can be
written and reasoned about against Core's source, but it will not actually compile
inside the Editor until the DLL is rebuilt.

### 2. The `bool[,]` mask + `CampaignLayerAction` - the only generation-level connection

Core generators return plain data: `OverworldGrid`, and (per the migration) `TownGrid`/
`DungeonGrid` - each just a `Dictionary<string, GridLayer>` of named boolean masks plus
some metadata (locations, rooms, spawn points). Core never touches a `TileWorldCreator`
or any TWC type; it has no idea TWC exists.

A small Unity-side adapter component - `Assets/Scripts/Overworld/CampaignOverworld.cs`
is the reference implementation, with `Assets/Scripts/Town/CampaignTown.cs`/
`Assets/Scripts/CampaignDungeon.cs` following the same pattern for town/dungeon - is the
only thing that sits on both sides:

```csharp
// Unity-side adapter (CampaignOverworld.cs), never inside Core:
var grid = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(Width, Height));
foreach (var layer in generatedAsset.mapBlueprintLayers)
{
    var mask = grid.Layers[binding.CoreLayer].ToArray(); // plain bool[,], copied out of Core
    layer.stack = new List<TileWorldCreatorAsset.BlueprintLayerData.ActionStack>
        { new("Campaign mask", new CampaignLayerAction(mask)) };
}
creator.ExecuteAllBlueprintLayers();
```

`CampaignLayerAction` (`Assets/Scripts/Overworld/CampaignLayerAction.cs`) is the actual
crossing point: it is a `TWCBlueprintAction` that TWC's pipeline treats like any of its
own built-in actions (Cellular Automata, L-System, `BSPDungeon`), except it does no
generation itself - `Execute` just clones and returns the mask it was constructed with:

```csharp
public bool[,] Execute(bool[,] map, TileWorldCreator creator) => (bool[,])cells.Clone();
```

Because this class is generic over "any `bool[,]`", it is reused as-is for every domain
- overworld, town, dungeon - rather than writing a new TWC action per domain.

## What must never cross

- **No `UnityEngine`/`TWC` type in Core.** Not even `Vector3Int` - Core has its own
  Unity-free `GridPoint` (`Core/EternalEnigma.Core/EternalEnigma.Core/World/GridPoint.cs`)
  for exactly this reason.
- **No generation logic in the Unity-side adapter.** An adapter's job is: call a Core
  generator, get back masks, hand them to TWC via `CampaignLayerAction`. If an adapter
  starts making its own placement/random decisions, that logic has leaked across the
  barrier and belongs back in Core, where it can be tested and used headlessly.
- **No direct Core → TWC calls, ever.** Core does not know TWC exists. The adapter is
  the only thing that imports both `EternalEnigma.Core.*` and `TWC.*` namespaces.

## Where to look

| Concern | File |
|---|---|
| Reference adapter (overworld) | `Assets/Scripts/Overworld/CampaignOverworld.cs` |
| The reusable mask→TWC action | `Assets/Scripts/Overworld/CampaignLayerAction.cs` |
| DLL build/import | `Assets/Scripts/Editor/CoreDllImporter.cs` |
| Unity-free grid coordinate type | `Core/EternalEnigma.Core/EternalEnigma.Core/World/GridPoint.cs` |
| Overworld generator contract | `Docs/OverworldGrid.md` |
| Town/dungeon generator contracts (per the Core migration) | `Docs/TownLayout.md`, `Docs/DungeonLayout.md` |

Running the console explorer (`EternalEnigma.Campaign.Explorer`) or the test suite never
needs Unity - that is the barrier working as intended. If a change to Core ever requires
Unity to be open to verify it, something has crossed the barrier that shouldn't have.
