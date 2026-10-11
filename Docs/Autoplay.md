# Manual playthrough and Debug autoplay

## Floor loot sweep

The Unity **Playthrough** window also offers **FloorLootSweep**. Selecting it defaults
to 10,000 floors, starting seed 42, 32× playback, 100,000 actions, 60 active minutes,
and five active minutes without meaningful progress per floor. Campaign remains
mode zero, including previously saved settings and the main-menu setup. Programmatic
callers can use `AutoplayOptions.LootSweep()` and change `RunCount` for short checks.

Each case loads the production dungeon scene, its cloned generation settings, and a
fresh isolated save. `manifest.json` is written before the first floor. It fixes the
case index, sequential seed, all generation options, combat difficulty, and profile.
Cases cycle through sorted supported theme/biome, interior/outdoor, tier 0–4 and
entry/regular/exit combinations, plus supported legacy layouts. Build and content
identifiers accompany the manifest and every attempt.
Reports also record the actual generated seed, dimensions, floor role, and floor
fingerprint. A mismatch against the manifest stops the batch.

The validation party starts at the normal entrance. The loot policy uses the shared
A*, combat policies, and real turn commands to walk, collect, open containers, break
reward props, and unlock or bash doors. Stairs do not exit during a sweep. The party
has debug survivability and the production Survey passive for harvesting. Currency
replenishment and deliberate ability coverage are disabled. Inventory capacity
expands before pickup, including companion pickups. Normal combat skills and cast
reporting remain enabled. The animation override is temporary and does not change
the user's saved animation preference.

Loot receipts record source identity, initial/current cells, expected quantity,
collector, action, resource values before/after, and removal. Collection requires
both pickup removal and the exact inventory/stock, gold, or key delta. Prop rewards
are declared before opening and linked to their spawned drops. New traversal drops
join the same ledger. Missing generated loot, missing reward drops, visual offsets,
invalid placement, and runtime recovery remain explicit failures. Initial inspection
does not move seeded loot. Valid positions are preserved; an invalid initial position
without a proven valid replacement is reported as a placement failure.

`Passed` and `Failed` advance only floor sweeps. A recorded collection failure does
not end the batch; game, automation, or reporting exceptions do. `batch.json` records
`FinalizedFloors`, `PassedFloors`, `FailedFloors`, and `AllFloorsPassed` separately from
its lifecycle state. A completed batch can contain failures. All sweep reports are
validation-only and ineligible for balance statistics. Reports include unresolved
loot, placement issues and recovery events; `entrance.png` and `gameplay-final.png`
are gameplay-camera samples alongside the ordinary `final.png` screenshot.

Set **Resume manifest** to an existing `manifest.json` to resume. Finalized `Passed`
or `Failed` cases with complete artifacts are skipped only when their manifest,
build and content identifiers match. Interrupted/stopped cases restart from generation
in new attempt directories. Previous attempts remain intact. Per-case reports rebuild
the aggregate, so an interrupted `batch.json` write cannot lose finalized work. A
changed build/content version reruns the whole manifest. Earlier-version passes never
satisfy acceptance. Pauses exclude time from run/stall limits and block advancement;
repeated movement through visited cells does not reset the stall clock.

For scripted manual launches, write the options JSON to
`Temp/FloorLootSweep/options.json` and invoke **Tools > Eternal Enigma > Playthrough >
Run Floor Loot Sweep From File** through Unity MCP. The ordinary **Run Floor Loot
Sweep** command uses the full 10,000-case defaults. Neither command is a test runner.

Focused verification:

```powershell
node Tools/unity-mcp.mjs harness FloorLootSweep
```

`EternalEnigma.Tests.AutoplaySweepTests` covers manifest distribution, serialization,
counts, seed mapping, mode-specific outcomes and version/artifact resume filtering.
The short PlayMode suite covers actual collections, container/key/prop rewards,
capacity, companion receipts, invalid placement, stop/resume, reporting failures,
cleanup and save restoration. These checks do not establish full-sweep acceptance:
that requires 10,000 distinct finalized passing cases on the final version.

`node Tools/floor-loot-sweep.mjs status [batch-directory]` reads progress and failures.
`node Tools/floor-loot-sweep.mjs verify [batch-directory]` independently checks all
10,000 cases, version identity, generated-floor fingerprints, artifacts, and each
collection's action receipt. It exits with a failure status for incomplete or failed
acceptance. With no directory argument it selects the latest sweep manifest.

## Campaign autoplay

Choose **Debug autoplay** in the main menu's developer controls to open its setup.
Enter a **starting seed** and **run count**, then choose **Start autoplay**. Defaults
are seed **42**, **1 run**, **1x**, with godmode, infinite strength, infinite resources,
and item/ability coverage enabled. The setup validates integer input and rejects
a last seed above `int.MaxValue`. Seeds may be negative.

The same runtime engine is available in a built app. The playback
overlay offers **0.5x, 1x, 2x, 4x, 8x, 16x and 32x**, plus **Pause/Resume**. Speed changes
apply to game time and bot pacing; changing speed while paused keeps it paused.
Use **Hide panel** or **F8** to collapse the debug panel; the small **Autoplay [F8]** button reopens it without pausing playback.
Move the pointer freely to reach the controls. Keyboard/controller input and mouse
clicks, scrolling or touch outside the playback panel pause the demo and ask whether
to stop autoplay. Choose **Take control (T / X)** to play the current session,
**Return to main menu (Enter / A)** to exit, or **Keep watching (Esc / B)** to resume
the prior playback state. Taking control waits for the current action to finish,
restores normal speed, hides the overlay, and disables autoplay cheats. It keeps
the current party, floor and inventory. The session still uses its isolated save;
returning to the main menu restores your normal game. Exiting restores the original player save; the demo has
its own save store for its entire lifetime.

For developer runs, open **Tools > Eternal Enigma > Playthrough > Open** in Unity.
This supports starting seed, run count, normal/debug options, item/ability coverage,
speed, time limit, stall timeout, pause/stop, and an optional goal of
visiting every generated destination and completing every dungeon once. The
default goal is the campaign's actual Finished flag. The editor retains its existing
4x and cheat defaults; both launchers default to one run. Neither launcher is invoked
by CI or by opening the game. Run Normal and Run Debug menu commands use seed 42.
Programmatic `AutoplayOptions` remain single-run by default; `DebugCoverage` is an
explicit option and only takes effect with `DebugPlaythrough`.

For a scripted campaign debug batch, write `AutoplayOptions` JSON to
`Temp/DebugAutoplay/options.json`, then invoke **Tools > Eternal Enigma > Playthrough >
Run Debug From File** through Unity MCP. Use `Mode: 0`, `DebugPlaythrough: true`,
`Seed` and `RunCount` to select the campaigns. The optional `NoAnimations: true`
temporarily disables dungeon animations and floor-introduction delays without
changing the saved animation preference. It still executes production turns,
travel, generation and progression. Accelerated playback scales the physics step
along with game time and restores it on takeover or exit. Script reloads are deferred
until the isolated session is cleaned up.

`node Tools/debug-autoplay.mjs status [batch-directory]` reports campaign progress
and every issue. `verify` requires at least 100 distinct sequential seeds, each with
a finalized Victory, the real campaign Finished flag, no warnings/errors, matching
build identifiers, and complete save, screenshot and action artifacts. Merely
launching a batch or reaching its time limits does not pass this verification.
`inspect <report.json>` prints the party, remaining enemies, stairs, and a local
walkability map for investigating a stopped campaign.

For concurrent local runs, **Build Debug Windows** builds the enabled production
scenes into `Builds/DebugAutoplay/DebugAutoplay.exe`. Development players accept
`--debug-autoplay-options <options.json>`; the options must name an explicit
`ReportRoot`. The player launches the same isolated campaign runner, ignores
watch-demo input prompts, and exits after finalization and save cleanup. It returns
zero only when every requested campaign wins without issues. Normal launches do
not start this runner.

The report verifier also accepts a directory containing `campaign-suite.json`:
`{"StartingSeed":43,"RequestedRuns":100,"Workers":["worker-1","worker-2"]}`.
Each worker's `ReportRoot` is its named subdirectory of that directory. Give the
workers disjoint consecutive seed ranges, such as 43–92 and 93–142. Verification
reads their actual reports and requires all 100 seeds to finish on one build;
the manifest alone never establishes a pass.

A batch starts a new isolated campaign for each seed `Seed + run index` (zero-based).
Victory, Defeat, Stalled and TimeLimit advance to the next seed. Unsupported,
GameError, AutomationError, manual stop, return to menu and player takeover stop
the batch. The final scene stays frozen for inspection. A paused batch cannot
start its next run, and playback speed persists between runs. Run number and
current seed appear in both playback interfaces. Taking control is available
until a finished campaign starts unloading; the control is disabled during unload.
The next campaign never resumes or reuses the previous campaign's progress.

Normal runs retain damage, consumable costs, gold limits and defeat. Debug runs
optionally protect all party members from HP loss, give melee/ranged attacks
infinite strength (lethal damage without integer overflow), and replenish gold, SP, hunger,
and preserve consumables used by the bot. Debug runs never grant progression
keys/capabilities or fabricate dungeon victories. Every debug run is marked
ineligible for balance statistics, including one with both cheat switches off.

The leader's policy reads the current campaign graph and deliberately knows the full
dungeon map and stair positions. Reports label this omniscient navigation. Companions
use the [ally strategies](Classes.md): personal sight, last-seen leader memory
and legal wandering when lost, including in Full Control. They never route to an
unseen leader's current position or swap the leader backward. Town
and dungeon routes reuse the game's `AStar.FindPath` (used by enemy pursuit), with
the existing character grid adapter for dungeons and a town walkability adapter.
The bot follows the route between turns, replanning when blocked, displaced, or
given a new objective. It walks through production movement code, opens eligible gates,
uses warps, changes capability companions in town, buys affordable supplies and
equipment upgrades, visits trainers, fights with the existing ally attack policy, uses restorative
items, and confirms the real stair/exit prompt. Content IDs, positions, number of
dungeons and floor counts are not encoded in the route. Supporting new mechanics
can still require policy changes. This is a baseline bot, not a claim of optimal
combat or a human-equivalent difficulty measurement; it casts skills through the
same ally skill AI as AI-controlled party members (revive, emergency heal, cure,
buff upkeep, crowd control, damage) and has no deliberate grinding strategy.
Shopping resolves offers through `TownServices.Catalog` and walks to each shop's
vendor (or its entrance when there is no interior). Purchases use `TownServices.Buy`,
honor stock, gold and party equipment proficiency, and equip through the production
equipment service. Debug coverage buys one of each previously untested eligible
item per run alongside useful upgrades and supplies. Rejected purchases are not
retried on the same town visit.

Training walks to the trainer and rebuilds `TrainerOffers.Build` after every
`TownServices.Learn` acquisition. Unlearned mastery takes priority, then new
abilities, then rank upgrades. Earned levels, prerequisites, skill points and gold
still apply. Subsequent town visits reconsider offers as the party progresses.

In debug coverage mode, both leaders and companions retain revival, emergency
healing and cure priorities, then prefer unexercised abilities with appropriate
targets and useful effects. Damage abilities need not outperform a basic attack
to be exercised, but use ordinary targeting, SP, arrows, cooldowns and actions.
The normal AI path is unchanged. Coverage never fabricates injuries, levels or
skill points, so conditional abilities can remain unexercised. Passive abilities
are listed separately from active casting coverage. Optional destination coverage
does not assert that every dialogue, shop offer or skill has been exercised.

Outcomes distinguish Victory, Defeat, Stalled, TimeLimit, Unsupported, GameError,
AutomationError, manual Stopped and PlayerControl. PlayerControl reports end at
the handoff and are excluded from balance statistics. There are no automatic retries or hidden
resources in normal mode. The final scene is frozen for inspection. Repeated
actions with the same position and enemy HP, missing paths, and lack of progress
produce a report rather than awarding success.

Each session creates a batch directory under `Logs/Playthroughs` in the editor or
`Application.persistentDataPath/Playthroughs` in a player. `batch.json` and readable
`summary.txt` link each seed and outcome to a unique run subdirectory. Summaries
update after every completed run and include purchase, training, cast and issue counts.
Each run contains:

- `report.json`: options, outcome, explanation, seed/generation fingerprint,
  app/Unity/build identifiers, elapsed time, actions, damage/healing, inventory,
  party and enemies, and completed/visited objectives. `report.json` also lists
  each actor's class, stable actor IDs, purchase and training attempts, learned ranks,
  ability coverage and unexercised reasons. `SkillUses.Selected` records selections,
  `Started` records SP-paid casts, `Casts` records resolved releases, and `Interrupted`
  records canceled, invalid or unfinished casts. A resolved release schedules its
  ordinary effects; it is not a guarantee of a hit or damage.
- `actions.jsonl`: chronological actions with location, floor, position, party
  actor ID, resources and elapsed time. Events include seed, run ID, run number,
  action number, scene, location and floor. Purchase/training events include costs,
  currencies, rank changes, stock and rejection reasons. Cast events share a cast
  ID and distinguish selection, start, completion and interruption. Unchanged skip
  reasons are deduplicated per actor/offer.
- `issues.jsonl`: warnings, errors, exceptions with stack traces, stalls and policy failures.
- `save.json`: the isolated campaign save using production serialization.
- `final.png`: final rendered state captured on completion/stop.

The report, save and logs are flushed and the screenshot is written before the
old campaign unloads. Its isolated save store stays installed through scene cleanup;
only leaving the session restores the original store, slot, player state and random
state. Final run saves are sealed against inspection or cleanup overwrites.
Reporting or transition failures stop advancement and expose the failure in the
playback status and available logs; `ArtifactsComplete` distinguishes complete sets.
Ending Unity Play Mode before screenshot finalization can leave an incomplete set.
The overlay displays the run folder; Unity's Playthrough window opens run and batch folders.

The save uses the game's existing interrupted-dungeon recovery. It is not a
mid-turn checkpoint or an exact combat replay. Use the frozen scene, report and
action log to inspect the failure. Comparing runs should include the policy/build
identifier as well as the seed; changed AI or content can change the result.

Manual integration checks are `[Explicit]` and also require a session flag set
only by the manual launcher. Ordinary test runs skip them even when Unity includes
explicit fixtures in an assembly-wide selection:

```powershell
node Tools/unity-mcp.mjs harness Autoplay
```

These short checks cover setup and playback controls, a two-seed time-limited batch,
pause/stop during advancement, reporting failures, shop/trainer restrictions, manual
takeover, the input prompt, save restoration, lethal normal-mode damage and optional
debug resource protection/infinite strength. **Run Autoplay EditMode** covers seed
ranges, outcomes, catalogs and trainer rules. **Run Autoplay Policies** covers normal
AI, debug coverage priorities and interrupted casts. Validation
reports are marked separately and excluded from balance data. They do not launch a full campaign
in automated test discovery.
