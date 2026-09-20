# Eternal Enigma Core

Standalone progression and procedural-generation solution. No Unity installation,
editor session or license is needed to build and test it.

```text
Core/
  global.json                    .NET SDK selection (10.0, latest installed feature band)
  Directory.Build.props          Shared compiler settings; warnings are errors
  EternalEnigma.Core/
    EternalEnigma.Core.slnx
    EternalEnigma.Core/           netstandard2.1 library
      Capabilities/              IDs, manifests and capability sets
      Progression/               Lock requirements, graphs and state transitions
      Generation/                Seeded construction and world descriptions
      Validation/                Reachability and structural checks
      World/                     Grid coordinates, immutable layers and capability-aware movement
    EternalEnigma.Core.Tests/     net10.0 xUnit tests
      Architecture/              Engine boundary and target-framework checks
      Generation/                Seed sweeps, fingerprints, invalid campaigns, runtime completion
      Progression/               Lock algebra, party traversal and permanent progression
    EternalEnigma.Campaign.Cli/   Headless generation, validation and JSON export
    EternalEnigma.Campaign.Explorer/ Interactive console map exploration
```

Run from this `Core` directory so `global.json` selects the SDK:

```powershell
dotnet restore EternalEnigma.Core/EternalEnigma.Core.slnx
dotnet build EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release --no-restore
dotnet test EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release --no-build
```

Open `EternalEnigma.Core/EternalEnigma.Core.slnx` in an IDE with .NET 10 and SLNX
support. Test packages use explicit versions. Builds, intermediate files and test
results are excluded from Git. The separate `Core checks` workflow runs on core
changes without invoking the Unity build/deployment workflow's steps.

## Boundaries

The library targets .NET Standard 2.1 for the project's Unity consumer. Tests run
on .NET 10 and may use test-only packages; do not raise the library target to match
them. Shared settings use C# 10 because compilation happens outside Unity.

Keep Unity adapters under `Assets`, referencing the imported core library.
Do not reference Unity projects, engine assemblies,
ScriptableObjects, MonoBehaviours or scene state from the core. Architecture tests
inspect the compiled assembly dependency graph for engine references, including
transitive ones. They do not replace a Unity/IL2CPP integration test.

## Import into Unity

In Unity, choose **Tools > Eternal Enigma > Core > Build and Import DLL**, or
right-click in the Project window and choose **Eternal Enigma > Build and Import
Core DLL**. Run outside Play Mode with the .NET 10 SDK installed.

The command builds the library in Release for `netstandard2.1`, then copies it to
`Assets/Plugins/EternalEnigma.Core/EternalEnigma.Core.dll` and imports it with
Editor/player compatibility. New plugins use Unity's default Auto Reference setting;
leave it enabled in the DLL Inspector. Gameplay assemblies with
default precompiled-reference settings can use core types without an asmdef edit.
The build runs asynchronously, logs to the Unity Console, and times out after 60
seconds. A failed build leaves the previously imported DLL in place. Repeat
imports preserve its `.meta` GUID and avoid rewriting identical DLL bytes.

Commit the imported DLL and its `.meta` with corresponding core changes so fresh
Unity checkouts and player CI builds have the same library. Re-run the command
after changing core code; there is no automatic build on every script refresh.
Only the core DLL is copied; if runtime package dependencies are added later,
their Unity-compatible assemblies need an explicit import policy too. Test
assemblies and framework assemblies must never be copied into Assets.

Ordinary command-line Release builds still go to
`EternalEnigma.Core/EternalEnigma.Core/bin/Release/netstandard2.1/`; the import
command stages its build under the repository's ignored `Temp/CoreDllImport/`.

## Generate a campaign

To explore a generated campaign in a scrolling console window, run from the repository root:

```powershell
dotnet run --project Core/EternalEnigma.Core/EternalEnigma.Campaign.Explorer -- --seed 42
```

Use arrows or WASD to walk, Q/E/Z/C or the numpad for diagonals, and Esc to quit.
The camera follows `@`; `+` gates enforce the active party's capabilities.
`O` marks a warp gate; `K`/`k` marks an uncollected/collected key site.
Keys in biomes D, E and F permanently unlock B–D, B–E and B–F warps in both directions.
Stand on a warp gate and press V to choose a destination. Locked destinations show their key requirements.
The HUD lists collected keys and the required return objective.
Stand on a location marker and press Enter to collect its key or claim its rewards (simulated encounter
completion). Press P to equip or dismiss recruited companions while standing on a
town; up to three can be active. Press T to travel to a previously visited town.
Menus use up/down and Enter; Esc closes them. Obstacle and interaction gates stay
open after crossing; area gates always require their capability. The entire map is
visible without fog. Progress is in memory only; restarting resets the campaign.

Use `--snapshot` to print a static viewport in redirected output or CI, or `--help`
for controls. Interactive mode needs a terminal of at least 41 columns by 13 rows.
The console host owns tile-based progression and references the core library;
it does not require Unity or change the imported Unity DLL.

For generation/export only, run from `Core`:

```powershell
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --output ../Temp/CampaignPreview
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 0 --count 1000
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --grid --output ../Temp/OverworldPreview
```

Every generated campaign passes structural validation before it is returned.
The CLI exits nonzero on failure and reports a versioned content fingerprint.
JSON exports are diagnostic world descriptions, not player saves.

Library entry points are `CampaignGenerator.Generate(seed)`,
`CampaignValidator.Validate(campaign)` and `new CampaignSession(campaign)`.
See [campaign generation](../Docs/CampaignGeneration.md) for the implemented
contract, examples and the boundary between logical validation and future terrain
or combat validation.

`OverworldGridGenerator.Generate(campaign)` returns a validated grid with boolean
layers and placement metadata. `CampaignOverworld` imports these into TWC through
its component context menu. See [overworld grids](../Docs/OverworldGrid.md) for
layer definitions, setup, supported topology and movement queries.
