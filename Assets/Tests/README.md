# Game test harness

`node Tools/unity-mcp.mjs harness CampaignKeys` checks unique capability rewards, shared
campaign-specific names in reward/menu/action UI, repeat-claim behavior, gate choices and
required return trips for seeds 42, -1 and 0. The EditMode `CampaignGenerationTests` cover
the imported generator and the new gate visuals. These fixtures use isolated save stores.

`node Tools/unity-mcp.mjs harness OpeningPreparation` exercises the new-journey lock,
0G bread confirmation and cancellation, active lesson choice, both errand orders,
early Save and quit, failed morning writes, reload/defeat persistence, and every authored
hero's affordable first lesson. Screenshots are written to `Temp/OpeningPreparation`.
`harness OpeningRegressions` covers the affected campaign travel, sleep, town services,
economy and party-menu fixtures. Both use isolated save stores.

Retained post-cleanup results record 379 Core passes, 285 EditMode passes, and
323 PlayMode passes with four explicit skips and no failures. See the
[diorama verification reports](../../Docs/Art/DioramaStyle.md#verification-and-reproduction)
for dates and scope; these are saved results, not a fresh run of the current checkout.
Save fixtures cover the current slot schema, item stock, campaign state and explicit
checkpoint restore. Town integration compares Core layout options/layers and services. The MCP dependency uses the committed metadata-fixed archive documented
in [Packages/McpUnityPatch.md](../../Packages/McpUnityPatch.md).

The EditMode assembly also includes `EternalEnigma.Tests.CoreIntegration.CampaignGenerationTests`.
It generates seed 42 through the imported core DLL, checks its fingerprint against
the headless golden value, validates the result and starts a campaign session.
Rebuild the DLL with **Tools > Eternal Enigma > Core > Build and Import DLL**
after changing `Core/`. The standalone core tests and 1,000-seed CI sweep live in
the separate solution; see [campaign generation](../../Docs/CampaignGeneration.md).
`CampaignOverworldTests` also verifies the grid-to-TileWorldCreator adapter,
template isolation and real town-marker instantiation. See
[overworld grid setup](../../Docs/OverworldGrid.md).

Town district coverage is available under **Tools > Eternal Enigma > Tests > Run Town
Dungeons EditMode / PlayMode**. It exercises the shared town template, variant persistence,
cache separation, overworld town silhouettes, cardinal doorway movement, street-to-interior
combat, roof cutaways, loot, gate transitions, boss clearance and generated mesh cleanup.
See [town district verification](../../Docs/BiomeDungeons.md#town-districts) for the current
Core sweep and Unity results. These runs use the harness's isolated save storage.

## Finale

`node Tools/unity-mcp.mjs harness Finale` checks production mimic protection,
adjacent reveal and reveal-turn behavior, reserved roster exclusion, region movement
restrictions, bosses at each biome's last building reveal, both stair transitions, resource preservation, terrain cleanup,
bedroom arrival, control/camera restoration, explicit saves, completed continuation
and retreat. Test fixtures may position actors and resolve combat to isolate these
contracts; they do not constitute an autoplay acceptance run.

`node Tools/unity-mcp.mjs harness FinaleLifecycle` checks that finale saves are
rejected, defeat restores the explicit checkpoint, completed slots support ordinary
dungeon returns and quitting, and ordinary chest mimics still reveal in the finale.
It also checks that debug survivability and infinite strength can be disabled.

The manual `FinaleControls` suite checks stop/takeover and restoration of saves,
input, animation mode and playback speed. `FinalePerformance` measures 300 production
commands and compares incremental minimap/fog pixels with a full redraw.
`FinaleValidation` combines these with lifecycle and legacy floor-transition checks.
`FinaleLoading` checks that the loading screen renders before two-map planning,
covers every finale floor through camera readiness (with and without animations),
and releases correctly for homecoming and ordinary town returns. Loading-screen
captures are saved as `Temp/FinaleDebug/loading-floor-<1..2>.png`.
Use `node Tools/unity-mcp.mjs harness <suite>`; these manual cases do not run in
ordinary batches. See [performance measurements](../../Docs/Autoplay.md#finale-performance-measurement).

Core `FinaleTests` sweeps ten campaign seeds (including no-Boat parties) for two-map
determinism, reachable enemies and mimics, distinct region ownership, the complete
ten-family roster, child accounting, locked movement/warps and both required stairs.
Reveal-order checks reserve each region's boss for its last building and preserve all families.
The full Core suite currently contains 423 tests. The latest manual verification
includes a passing five-floor direct run, an accepted full-campaign debug run and
seven passing finale validation cases; see the [recorded evidence](../../Docs/Autoplay.md#recorded-finale-verification).

The separate **Tools > Eternal Enigma > Tests > Run FinaleAutoplay** suite is explicit
and manual. Invoke `node Tools/unity-mcp.mjs harness FinaleAutoplay` through Unity MCP;
the client allows up to one hour. It uses the isolated direct-start fixture with
production movement, combat, reveals, gates and stairs, and requires waking with
control restored. It checks pause/resume and original-save restoration. Ordinary
PlayMode batches do not run this full autoplay route.

Artifacts are written beneath `Logs/FinaleDebug`. Run
`node Tools/debug-autoplay.mjs verify-finale [batch-directory]` for independent
population/death, mimic, gate, stair and homecoming evidence checks. A direct run is
excluded from balance and full-campaign acceptance; normal campaign `verify-run`
uses the same finale audit and additionally requires a normal campaign start.
The audit's negative cases can run without Unity:
`node --test Tools/tests/finale-audit.test.mjs`.

Use **Tools > Eternal Enigma > Finale > Build Enemy Prefabs** to reproduce the
existing Wave 4 mimic adapters without changing vendor assets. The models use the
same combat scale as ordinary enemies and retain one-tile footprints.
**Capture Combat Scale** renders pairs with a normal skeleton for visual comparison
under `Temp/FinaleDebug/Scale`. See
[autoplay](../../Docs/Autoplay.md) and
[campaign generation](../../Docs/CampaignGeneration.md#reverse-world-finale).

## Skills

`SkillRegressionTests` runs the production dungeon, skill assets, menu, input
bindings and turn pipeline. It checks single-target damage/healing, healing caps,
self buffs, ally/enemy selection, dead and out-of-range targets, area effects,
untargeted effects, SP costs, passive/unknown skills, silence, status coexistence,
reapplication and expiry, and casting on behalf of another party member.

Skill authoring uses `Targeting` (`SelectedTarget`, `Self`, or `AllTargets`) and
`AreaRadius` (Chebyshev tile radius, zero for a single character). Existing assets
default to selected single-target behavior; self-only selectors cast immediately.
`Self` centers an area on the caster. `AllTargets` casts immediately on every
living character allowed by the team/range selector. Area effects retain the
selector's team and range restrictions, and charge SP once per cast, regardless
of recipient count. `Custom` range has no geometry implementation and therefore
offers no targets. AoE and untargeted tests use clones of production skills to
exercise these settings without changing the existing skill balance.

The main menu's **Test Dungeon** supplies all three allies with these nine demo
skills (1 SP each), plus 20 of each matching scroll in the shared bag:

| Demo skill / scroll | Demonstrates |
| --- | --- |
| Single Hit | Selected enemy, single-target damage |
| Ally Mend | Selected ally or self, healing |
| Area Burst | Selected enemy, one-tile AoE excluding allies |
| Enemy Pulse | Untargeted damage to all visible enemies |
| Party Mend | Untargeted healing of all visible allies |
| Self Focus | Immediate self-targeted Anger status |
| Inspect Item | Any inventory item, including equipped items |
| Inspect Weapon | Weapons only, including equipped weapons |
| Arrow Shot | Eight-direction missile, 8-tile range |

The opening room includes three stationary 500-HP practice targets, partly injured
allies, and 100 SP per ally. Allies hold position. A training sword and 20 Wooden
Arrows are also provided. Inspect effects display the selected item's stock or
weapon stats; they do not enchant or consume that target. Scroll use consumes one
scroll with no SP cost. **R / left shoulder** opens skills, **Q / west button**
opens the inventory. Later floors retain normal encounters.

The content is stored in `Assets/Resources/DemoDungeon/Loadout.asset` with skill,
scroll, and effect subassets. It is applied only through Test Dungeon, and does
not enter normal random loot tables. Name lookups support these definitions for
save restoration. `node Tools/unity-mcp.mjs harness Demo` checks both clean/existing
save launches and invokes every supplied demo skill and usable item.

Inventory skills use `Targeting = InventoryItem`. Configure
`InventoryTargetSelector.ItemType` as `AnyItem`, `Equipment`, or `Weapon`;
`IncludeEquipped` optionally adds the caster's equipped items to the shared party
bag. Weapon filtering includes main-hand/two-handed weapons and off-hand swords,
and excludes shields/accessories. Character team/range and area radius do not
apply to this mode.

Add `InventorySkillEffect` implementations to the skill's `ActionEffects`.
Override `CanTarget(caster, item)` for effect-specific restrictions (such as
unidentified items or an enchantment cap), and `Bind(caster, item)` to create a
fresh `GameAction` operating on that exact runtime item. Do not mutate shared
item-definition assets. Identification/enchantment rules belong to those effects;
this mode supplies selection, validation and dispatch without changing the item
or save-data model. Empty/mixed character-effect lists offer no eligible items.

Casting opens a filtered inventory picker with the usual keyboard/controller/mouse
controls. Confirm casts on the chosen copy; Back returns to the skill list without
spending SP or a turn. Ownership, eligibility, status restrictions and SP are
checked again on confirm/execution. Item-skill animations play on the caster.
`InventorySkillTargetingTests` covers filters, duplicate-name item identity,
equipped-item inclusion, cancellation, stale targets, costs, mouse confirmation,
and restoring normal inventory actions. `harness Skills` runs both skill fixtures.

Usable item definitions also expose `Targeting`, `TargetSelector`, `AreaRadius`,
and `InventoryTargetSelector`. Existing items default to `Self`. Character-targeted
uses dispatch `ItemEffectDefinition` to each selected recipient; inventory-targeted
uses bind the `InventoryEffects` list of `InventorySkillEffect` templates. Using an
item invokes the same picker and confirmation flow as a skill. A successful use
consumes one stack unit (or the single consumable) and one turn, regardless of AoE
size, without charging skill SP or requiring a learned skill. Cancel and invalid
source/target checks consume nothing. A nested item picker preserves the original
inventory and Use menu when backing out.

`Missile` targeting is available on both skills and usable items. Set `MissileRange`
in tiles and `MissileProjectilePrefab` for its visual. Aim with **WASD/arrows,
D-pad, or stick**, including diagonal combinations, then use the normal confirm or
cancel controls. Aim starts in the user's facing direction; the indicator previews
the endpoint. Shots stop at the first living character, wall, blocked diagonal
corner, or range limit. Team/range filters determine whether the first character
receives effects; excluded characters still block the shot. Missile effects are
single-target (`AreaRadius` does not create splash damage). Confirmed empty shots
still consume ammunition/skill SP and a turn. Wooden Arrows and Spell Bolt now use
this mode, preserving their existing 40-tile range and projectile visuals.

Dungeon controls: **R / left shoulder** opens or closes skills; **WASD, arrows,
D-pad or left stick** selects a skill/target; **Enter, Space / south button**
confirms; **Escape / east button** goes back. Self/untargeted skills cast on the
first confirmation. Targeted skills require a second confirmation and can be
cancelled without spending SP or a turn. Native 3D targeting also supports mouse
preview, left-click confirmation and right-click cancellation. Opening with a
held click requires a release before confirmation; HUD clicks and invalid targets
cannot cast. Terminal presentation retains keyboard/controller target selection.
**Period / left stick press** waits; **F / right stick press** toggles Full Control
without spending a wait action. Target selection enables its component
only while needed, and disables it after confirm/cancel. Gamepad/keyboard/mouse
tests inject device events through the real Input System bindings; they do not
test physical hardware. The current main-menu Test Dungeon uses the nine-skill
DemoDungeon loadout described above; harness scenarios supply their own skills.

Sight coverage: `DungeonSightTests` verifies wall occlusion, blocked diagonal
corners, symmetric range limits, room/corridor classification, and immediate
completion of skipped visual effects. `SightPlaybackTests` advances from the
entry room to a generated floor and checks shared AI/skill visibility, camera
filtering, fog following displayed positions, and offscreen damage/status/death
completion. `FogVisibilityTests` checks explored fog opacity and renderer hiding.

Dungeon sight uses an eight-tile Chebyshev radius in open room areas (tiles in an
open 2x2 square), and a one-tile corridor radius (two for large units). Walls are
visible endpoints and block tiles behind them; either blocked side prevents
diagonal corner sight. All living allies share fog knowledge. Explored terrain
remains under 50% fog, but live enemies and items require current sight. Animation
playback also requires camera relevance; controlled-player actions and effects on
that player remain animated. Movement batches consider sight before and after
movement. These tests validate behavior, not measured frame-time improvements.

Run these tests with Unity 6000.2.7f2, outside an existing Play Mode session.

Movement coverage: `GridMovementTests` (EditMode) checks diagonal policies,
bounds, invalid steps, reusable searches, movement costs, and facing offsets.
`MovementRegressionTests` (PlayMode) loads both real scenes and injects sampled
movement/hold-position values into the existing input decision methods, checking
turn-in-place, completed movement, and town trail recording. It does not
simulate physical keyboard/controller bindings. Recompile scripts before running
the harness after adding tests, and confirm the new fixtures appear in the XML.

`GridMovement` owns terrain step rules shared by town, dungeon, A* and BFS.
Town uses its generated occupancy masks, including furniture, and allows corner cutting; dungeon and
searches require both diagonal side cells to be open. Occupancy, large-unit
hallway behavior, turns, party following and interactions remain in mode-specific
actions. Coordinate conversion uses the generator's cell size (currently 2).
Dungeon position searches fall back to their supplied origin when no suitable
cell is reachable; this fallback does not guarantee an unoccupied placement.
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
- `LoadTown(save)` runs the real town load path, including generation.
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

## UI audit

Run these Editor menu commands sequentially after compilation, outside Play Mode:

- **Tools > Eternal Enigma > Tests > Run UI Audit Captures** runs `UIScreenAuditTests`
  and `DungeonDockTests`: main menu, slots, hero picker, settings, town/overworld
  services and party docks, dungeon dialogs, and guarded mouse targeting. Screenshots
  cover 960×600, 1280×720, 1920×1080 and 2560×1080; dungeon adds 1280×800.
  `Temp/UIScreenAudit` contains screenshots and text bounds/font/overflow JSON;
  `Temp/DungeonDock` contains dungeon screenshots.
- **Tools > Eternal Enigma > Tests > Run UI Audit Controls** runs selection,
  settings, device switching, controller flows, party menus, opening/main-menu
  regressions and focused dungeon, town, shop, trainer and overworld guardrails.
  Coverage includes one visible close control, hero identity, wrapped rows, active
  tabs, pointer Options access in town/overworld/dungeon, modal focus restoration, and
  separate Wait / Full Control stick bindings.
- **Tools > Eternal Enigma > Tests > Run UI Audit Devices** focuses on supported
  keyboard/mouse and gamepad switching, cursor/prompts, controller navigation,
  and preventing incomplete template device schemes from taking control.
- **Tools > Eternal Enigma > Tests > Run UI Audit Fades** checks paused and
  interrupted fades, duplicate requests, input blocking, ready-before-reveal
  scene travel, and opaque-before-mutation floor advance/reset. Scene traces and
  frames are in `Temp/UIScreenAudit/Transitions`; floor traces and frames are in
  `Temp/FloorTransitions`.
- **Tools > Eternal Enigma > Tests > Run UI Audit Animations** runs hero animation
  and transfer checks, including potion playback for every armed stance,
  restoration of the equipped idle pose, dash clips during Lunge/Shadow Step,
  ordinary walking displacement, and the real home sleep/save flow.
- **Run UI Audit Followup** provides the focused controller, overworld, terminal,
  and current homecoming/save checks used during this audit.

For Unity MCP, call `execute_menu_item` with
`{"menuPath":"Tools/Eternal Enigma/Tests/Run UI Audit Captures"}` or the corresponding
`Run UI Audit Controls` path. The project client accepts the same call via
`node Tools/unity-mcp.mjs execute_menu_item @path/to/arguments.json`.
These audit names are not currently in the client's `harness` suite allowlist.
Wait for the new run ID to finish in `Temp/HarnessResults/PlayMode.json`; NUnit XML
is written alongside it. Review captures as well as assertions. See
[UI behavior and styling](../../Docs/BamaoUI.md#shared-gameplay-dock).

## MCP / command line

**Run AllyAI** includes the authored-skill and sight/search regressions in
`AllyAiRegressionTests` as well as the existing policy and class-party suites.
**Run Ally AI Intent** runs the expanded effect classification and budget tests.
**Run Ally AI Dependencies** covers sight playback, movement, skill movement,
casting/piercing, downed allies, summons and dungeon control. **Run Autoplay**
provides the separate explicit autoplay integration checks.
Sight playback fixes its animation mode for the fixture and restores it afterward.
Manual autoplay tests temporarily enable the hidden developer canvas and restore
the user's terminal presentation preference after checking the 3D menu inputs.

The search fixture uses disposable runtime terrain masks to exercise exact walls,
doors, corridors, blocked routes and dead ends without modifying scenes or assets.
Authored skill instances execute real effects and charging actions, checking SP,
ammunition, food, immunity, recipient eligibility and restoration.

The project installs a pinned MCP Unity package. Its bridge is localhost-only,
auto-starts on port 8091, and leaves package-installation permission disabled.
The auth token stays in ignored `Library/McpUnity`; do not commit it.

After Unity finishes importing, the project-local client can address this editor
without depending on a host's cached connection to a different Unity project:

```powershell
node Tools/unity-mcp.mjs get_scene_info
node Tools/unity-mcp.mjs harness EditMode
node Tools/unity-mcp.mjs harness PlayMode
node Tools/unity-mcp.mjs harness Skills
node Tools/unity-mcp.mjs harness Town
```

These commands invoke **Tools > Eternal Enigma > Tests** and wait for the matching
run ID in `Temp/HarnessResults/{mode}.json`. NUnit XML is written alongside it.
The local results survive MCP disconnects at Play Mode transitions. The client
returns nonzero for errors, failed/empty runs, or a ten-minute timeout. A timeout
does not automatically stop Unity; use **Tools > Eternal Enigma > Tests > Cancel Run**.

For generic MCP calls with JSON arguments, PowerShell hosts that strip quotes can
use a JSON file: `node Tools/unity-mcp.mjs run_tests @Tools/harness-playmode.json`.
Prefer the `harness` commands above for reliable Play Mode result collection.

`harness Town` checks Continue, dungeon return, direct town loading, caller-supplied
configuration, custom building dialogs, shop stock and purchase validation,
immediate capture of training changes in memory, donation unlocks, shared equipment
menus, party safeguards, and victory/defeat inventory transfer. Explicit save tests
separately verify persistence; training and travel do not overwrite checkpoints. See [Town authoring](../../Docs/Town.md).
The town holds the transition fully opaque while generating, then initializes
the party, snaps the camera to the controlled hero, and starts the reveal. The
transition tests observe coverage during generation and camera position/orientation
when the world becomes ready.

## Assembly boundary

Gameplay now lives in `EternalEnigma.Game`; editor drawers and DOTween modules
have separate assemblies. The generated input wrapper moved to `Scripts/Input`
with its GUID preserved and its generator path updated. Scene UnityEvents,
managed-reference data and editor class identifiers were migrated to the new
assembly name. Tests have friend access to internal gameplay methods.

The harness commands run the entire corresponding test assembly. Audit regressions
cover equipment displacement, two-handed removal, inventory restoration, town
state captured without overwriting the explicit checkpoint on return to menu, floor labels, and declining then
reopening the stair prompt. The stair test injects a sampled attack edge into the
production decision method; it does not validate physical keyboard bindings.

The active weapon catalog has unique names, enforced by `WeaponCatalogTests`.
Construct a specific variant from its exact definition when a test depends on its
model or stats. Save restoration remains name-based, so keep new catalog names
unique. See [weapons](../../Docs/Weapons.md).

**Tools > Eternal Enigma > Tests > Build Windows Player** performs a development
build of all enabled build scenes to `Builds/AuditVerification`. Its result is
written to `Temp/HarnessResults/Build.txt`. Run it outside Play Mode after tests
finish. This checks player compilation and packaging, not gameplay in the executable.
