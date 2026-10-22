# Input-only chaos runner

Use **Tools > Eternal Enigma > Chaos > Open** in the Editor, or the development
player command line below. The runner is opt-in and cannot run alongside autoplay.
Runtime chaos code is excluded from non-development players. It does not use the
ability lab or autoplay's commands, invulnerability, resource replenishment, or
navigation repair.

## Run and inspect

```powershell
node Tools/chaos-runner.mjs init Temp/Chaos/my-options.json
node Tools/chaos-runner.mjs editor Temp/Chaos/my-options.json
node Tools/chaos-runner.mjs status Logs/Chaos
node Tools/chaos-runner.mjs checklist Logs/Chaos
node Tools/chaos-runner.mjs verify Logs/Chaos
```

The Editor launcher reads `Temp/Chaos/options.json`; the CLI copies the requested
options there, then launches through Unity MCP. A Play Mode transition can drop the
MCP response. The CLI confirms a fresh report with matching options before accepting
such a launch. If it still fails, check `latest.json`, `heartbeat.json`, and the
Editor's Play Mode state before retrying. Exit Play Mode after a run before launching another.

Build and launch a Windows development player:

```powershell
node Tools/chaos-runner.mjs build
node Tools/chaos-runner.mjs player Builds/Chaos/EternalEnigma-Chaos.exe Temp/Chaos/my-options.json
```

Build results are written to `Temp/Chaos/build.txt`. The player also accepts
`--chaos-options <absolute-options-path>` directly. Give it an explicit `ReportRoot`.
The CLI creates a separate launch directory and monitors a heartbeat belonging to
the child PID. It records `watchdog.json` and terminates only that child if startup
has no heartbeat for 180 seconds or its heartbeat stops for 75 seconds. A fourth
`player` argument overrides the heartbeat deadline. Direct launches do not have an
external watchdog. Temporary heartbeat read failures retain the last valid timestamp;
they cannot extend the stall deadline or immediately terminate a healthy player.
No `-batchmode` or `-nographics`: screenshots and production UI
need rendered frames.

Options are JSON fields on `ChaosOptions`:

| Field | Default / meaning |
| --- | --- |
| `Seed`, `RunCount` | 42, 1; separate hero, inventory and input streams |
| `DurationSeconds` | 300 per smoke run, divided among matching scene fixtures |
| `Mode` | 0 = smoke; 1 = full checklist |
| `Device` | 0 = mixed; 1 = keyboard; 2 = mouse; 3 = controller |
| `ActionTimeoutSeconds` | 10 |
| `LoadTimeoutSeconds` | 60 |
| `RecoverySeconds` | 10 |
| `ScenarioFilter`, `AbilityFilter` | Case/scene substring; ability filter applies to full mode |
| `ReportRoot` | `Logs/Chaos` in Editor; persistent data path in players |
| `ScenarioDirectory` | Additional `*.scenario.json` files and referenced fixtures |
| `ReplayDirectory`, `ResumeDirectory` | Mutually exclusive source report directories |
| `QuitWhenFinished` | true in players; ignored in Editor |

Smoke loads randomized town, dungeon, and overworld save fixtures through the
production Continue menu. It alternates directed settings/party-menu flows with
seeded taps, holds, opposing directions, mouse clicks/scrolls, stick movement and
device switches. A game exception ends the attempt with failure evidence. The
configured duration is not proof that the run lasted five minutes. `verify`
rejects short, interrupted, divergent, or failed attempts.
Developer actions and process-exit buttons are excluded, including random pointer
coordinates that raycast onto those controls.

## Fixture boundary and isolation

Only fixture preparation can write starting state. Every scenario begins at the
main menu with a fresh in-memory save store. It selects unique heroes from
`DefaultTown.AllyCatalog`, preserves each prefab's legal classes, varies party size
and order, and rotates the initially controlled hero. Full-mode fixture selection
prefers heroes/classes with less observed participation. Consecutive fixture
compositions differ when a legal alternative exists.

Items come from the production item manager and town shop catalog, excluding
progression items and demo-only inventories. Stock respects `StackMax`, the bag
respects `Inventory.DefaultCapacity`, and equipment respects class proficiency and
slot conflicts. Required equipment and inventory are reserved before random
selection. Unresolvable prerequisites fail explicitly; they are never replaced by
random choices. A targeted fixture can guarantee its controlled hero, rank, level,
HP, Mana, equipment and inventory. These guarantees are labelled in the manifest.

`fixture.json` contains exact saved hero IDs/classes/ranks/loadouts, inventory,
controlled hero, seeds, Unity random state, canonical save JSON, and a hash. Replay
loads those values instead of regenerating them. The campaign identity is derived
from the fixture seed rather than the normal new-campaign GUID.
Generated fixtures carry the current save-format version. Before navigating
Continue and Slot 1, preparation verifies that the production save reader can
load the raw fixture; unsupported fixtures fail immediately with a diagnostic.
Before closing a scenario trace, the runner closes any persistent Settings menu
through recorded input so its focus scope cannot leak into the next fixture.

After preparation, the runner queues `KeyboardState`, `MouseState`, and
`GamepadState` events through production bindings. UI navigation observes authored
navigation and moves focus through inputs; it never calls `Select`, `onClick`, a
gameplay command, teleportation, or resource repair. Normal game actions remain
responsible for subsequent state changes.

Settings route through `GamePreferences`, which uses PlayerPrefs normally and an
empty in-memory dictionary during chaos. Personal preference/save keys are never
written, including if the process crashes. Device settings, enabled physical
devices, held controls, cursor configuration, audio values, animation/full-control
overrides and runtime settings are restored during cleanup. The isolated save
store stays installed through scene destruction. Editor script reload is deferred
until cleanup. Forced process termination cannot run cleanup callbacks, but its
virtual devices are process-local and personal keys were never modified.

## Coverage and full mode

The checklist is generated from class entries and their supported ranks, hero and
trainer skills, production skill resources, dungeon-clear passives and item skill
effects. It includes active, passive and class-passive cases, targeting variants,
cancel/invalid/range/status/resource restrictions, delayed casts, and multiple
targets where applicable. Production item flows and feature flows are separate.
Demo-only skills are recorded separately and do not satisfy production coverage.
Hero/class participation is also separate: preparation/ownership does not count;
observed movement or accepted gameplay commands do.

Full mode executes declared scenarios and leaves missing cases unverified. A small
built-in baseline checks **Heal at every supported rank using keyboard/controller**
with a wounded, prepared healer, verifying an actual release, HP increase, exact Mana
cost, a turn, and returned control. Other cases require authored input scenarios
and effect oracles. This includes other active effects, passives, exploration,
probabilistic effects, specialized target fixtures, mouse world targeting, defeat
and endgame. The checklist is exhaustive over the enumerated definitions; the
current scenario library is not exhaustive. It cannot produce a full pass while
those cases are missing.

Additional scenario files have `Version: 1`, an exact checklist `CaseId`, `Entry`,
`Prerequisites` or `FixturePath`, a `DeadlineSeconds`, `Steps`, and `Assertions`.
Steps contain a visible `TargetLabel` to navigate to, a raw `ChaosInput`, or
`WaitScene`/`WaitForControl`. Input device must match the case. Targets must be
unambiguous. Raw inputs record keys and pad buttons by enum name, pointer position,
mouse buttons, scroll, both sticks, hold frames and settle frames.

Assertions explicitly classify effect, cost, turn, and passive trigger evidence.
Available numeric metrics include HP/Mana/hunger, max HP/Mana, strength/defense,
position, ammunition, status count, pending charge, actor count, gold, floor,
accepted commands, turns and cast starts/releases. Comparisons are `EqualDelta`,
`MinimumDelta`, `MaximumDelta`, `Changed`, and `Equal`. Passive checks require a
`PairedFixturePath`, input-trigger evidence and a `ComparePaired` effect comparison.
Unmapped effects need a new read-only oracle, not an assumed pass. A released cast
without a verified effect, selected skill without execution, or passive ownership
without a trigger/comparison cannot satisfy the corresponding success case.

```powershell
node Tools/chaos-runner.mjs checklist Logs/Chaos 'Active/class:healer/Heal/1/'
node Tools/chaos-runner.mjs verify Logs/Chaos --full
node Tools/chaos-runner.mjs resume <report-directory> Temp/Chaos/resume-options.json
node Tools/chaos-runner.mjs replay <report-directory> Temp/Chaos/replay-options.json
```

Launch generated replay/resume options with `editor` or `player`. Both refuse
different build/content/fixture versions. Resume also requires unchanged scenario
and fixture files. Each full-mode case seals its own evidence and artifact hashes,
so completed cases can survive an interrupted suite. Incomplete cases restart from
preparation. Previous evidence is copied into the new report's `resumed` directory.
Replay compares pre-input and post-input checkpoints and reports divergence,
including timing/generation differences; it never repairs state to match a trace.
Editor and player identities are separate and cannot share passing coverage.

## Diagnostics and validation

Each report contains the checklist, missing-case reasons, participation, build and
content identity, issues and SHA-256 artifact inventory. Each scenario contains
its fixture, pre-dispatch `inputs.jsonl`, post-dispatch `checkpoints.jsonl`, and
read-only command/cast observations. Logs include Unity errors and stack traces.
`save.json` is the last isolated persisted save; checkpoints also preserve live
state. `final.png` captures failure state before teardown. A suspected lock gets
its own screenshot before bounded input-only recovery; recovered navigation
failures remain failures, distinct from unrecovered softlocks.

Run checks through Unity MCP menu items **Tests > Run Chaos EditMode** and
**Tests > Run Chaos PlayMode**. Results are written to `Temp/Chaos/tests-*.xml` and
`tests-*.txt`. CLI tests use:

```powershell
node --test Tools/tests/chaos-runner.test.mjs
```

See `Docs/ChaosValidation.txt` for the performed checks and discovered game defects.
