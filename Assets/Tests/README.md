# Game test harness

Run these tests with Unity 6000.2.7f2, outside an existing Play Mode session.
Open **Window > General > Test Runner**. Run `SaveStoreTests` in EditMode and
`HarnessSmokeTests` and `AuditRegressionTests` in PlayMode, plus
`EquipmentRegressionTests` in EditMode. PlayMode fixtures are editor-only and load the
production scenes through `EditorSceneManager`; they are not player tests.

```csharp
[PrebuildSetup(typeof(HarnessSceneBootstrap))]
[PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public class MyScenarioTests
{
private GameTestHarness harness;

[UnitySetUp]
public IEnumerator SetUp()
{
    harness = new GameTestHarness();
    yield return harness.LoadDungeon(new TestScenario {
        AllyName = "Rowan",
        Seed = 12345,
        StartFloor = 1,
        EndFloor = 5,
        HP = 7,
        Items = new string[0]
    });
}

[UnityTearDown]
public IEnumerator TearDown() => harness.Cleanup();
}
```

## Setup and actions

- `LoadDungeon(scenario)` loads Common, supplies a real ally prefab, and loads
  DungeonScene. It waits for initialization and the floor announcement animation.
  The first floor is the game's normal throne room. No synthetic test room is used.
- `LoadOverworld(save)` runs the real overworld load path, including generation.
  It deliberately does not compensate for missing inventory restoration.
- `TestScenario.Items` uses **ItemDefinition.ItemName**, not asset filenames.
  StartingItems are excluded unless `IncludeStartingItems` is true. The game still
  applies its usual starting stats and passive skills; optional HP/SP override vitals.
- `AddItem(name)`, `PlaceAlly(tile)` and `PlaceBesideStairs()` establish explicit
  starting conditions. Placement is a teleport, not a movement/interaction test.
- `SpawnEnemy(prefabName, tile)` loads a prefab from `Assets/Prefabs/Dungeon/Enemies`
  and waits for its AI initialization; use a filename without `.prefab`.
- `ExecuteAction(action)` uses `Ally.SetAction`, the production turn pipeline and
  animations. It does not synthesize keyboard input.
- `UseItemThroughMenu(item)` opens the inventory and invokes the real inventory-row
  and Equip/Use button UnityEvents, then waits for the turn. This checks UI wiring,
  item effects and equipment behavior, but not keyboard/controller navigation.
- `WaitForIdle()` and `WaitUntil(predicate, description)` fail with a timeout
  rather than waiting indefinitely. Default timeout is 60 seconds per wait.

## Isolation

Every scenario installs an in-memory `ISaveStore` **before loading Common**.
The actual JsonUtility save/load path is used, but PlayerPrefs is never read,
overwritten or deleted by a harness scenario. Always use UnityTearDown so the
previous store is restored even after an assertion fails. Nested save-store
scopes must be disposed in reverse order.

The prebuild/postbuild attributes suppress the game's automatic editor scene
bootstrap before Play Mode begins, and restore its previous setting afterward.
Keep these attributes on new PlayMode fixtures, including when domain reload is
disabled. If a test is forcibly aborted, use **Tools > Eternal Enigma > Tests >
Restore Normal Startup** after stopping it, or restart Unity to clear the
session-only override.

Generation settings are cloned before applying seeds, avoiding edits to TWC
assets. Cleanup unloads scenario scenes, destroys newly created persistent roots
and generation clones, and restores Unity's random state. It refuses to start
inside an already-running Common session. Run tests sequentially.

A fixed seed controls generation, not every gameplay decision: code that uses
`Guid.NewGuid()` for random selection is not seeded. Regression tests needing
specific combat encounters should explicitly spawn those enemies.

## MCP / command line

The project installs a pinned MCP Unity package. Its bridge is localhost-only,
auto-starts on port 8091, and leaves package-installation permission disabled.
The auth token stays in ignored `Library/McpUnity`; do not commit it.

After Unity finishes importing, the project-local client can address this editor
without depending on a host's cached connection to a different Unity project:

```powershell
node Tools/unity-mcp.mjs get_scene_info
node Tools/unity-mcp.mjs harness EditMode
node Tools/unity-mcp.mjs harness PlayMode
```

These commands invoke **Tools > Eternal Enigma > Tests** and wait for the matching
run ID in `Temp/HarnessResults/{mode}.json`. NUnit XML is written alongside it.
The local results survive MCP disconnects at Play Mode transitions. The client
returns nonzero for errors, failed/empty runs, or a four-minute timeout. A timeout
does not automatically stop Unity; use **Tools > Eternal Enigma > Tests > Cancel Run**.

For generic MCP calls with JSON arguments, PowerShell hosts that strip quotes can
use a JSON file: `node Tools/unity-mcp.mjs run_tests @Tools/harness-playmode.json`.
Prefer the `harness` commands above for reliable Play Mode result collection.

## Assembly boundary

Gameplay now lives in `EternalEnigma.Game`; editor drawers and DOTween modules
have separate assemblies. The generated input wrapper moved to `Scripts/Input`
with its GUID preserved and its generator path updated. Scene UnityEvents,
managed-reference data and editor class identifiers were migrated to the new
assembly name. Tests have friend access to internal gameplay methods.

The harness commands run the entire corresponding test assembly. Audit regressions
cover equipment displacement, two-handed removal, inventory restoration, town
progress saved through the return-to-menu button, floor labels, and declining then
reopening the stair prompt. The stair test injects a sampled attack edge into the
production decision method; it does not validate physical keyboard bindings.

Several catalog definitions share an ItemName. Tests requiring a specific weapon
must construct it from its exact definition instead of using AddItem(name).
The existing name-based save format still cannot distinguish these variants.

**Tools > Eternal Enigma > Tests > Build Windows Player** performs a development
build of all enabled build scenes to `Builds/AuditVerification`. Its result is
written to `Temp/HarnessResults/Build.txt`. Run it outside Play Mode after tests
finish. This checks player compilation and packaging, not gameplay in the executable.
