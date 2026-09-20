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
    EternalEnigma.Core.Tests/     net10.0 xUnit tests
      Architecture/              Engine boundary and target-framework checks
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

## Next implementation

The domain folders currently document ownership; they contain no gameplay stubs.
Start with a capability manifest, normalized alternative lock requirements and
one evaluator, followed by a hand-authored progression fixture with completion and
circular-dependency tests. Add tests under matching domain folders as behavior is
implemented. Resolve proof-model questions in
[`Procedural_RPG_Spec_Review.md`](../Docs/Procedural_RPG_Spec_Review.md) before
claiming a general world-solvability guarantee.
