# Manual playthrough and Debug autoplay

For a one-click debug run, choose **Debug autoplay** directly below **Test Dungeon**
on the main menu. It starts seed 42 at 1x with godmode, infinite strength and infinite
resources enabled; playback controls remain available during the run.

The same runtime engine is available in a built app. The playback
overlay offers **0.5x, 1x, 2x, 4x, and 8x**, plus **Pause/Resume**. Speed changes
apply to game time and bot pacing; changing speed while paused keeps it paused.
Move the pointer freely to reach the controls. Keyboard/controller input and mouse
clicks, scrolling or touch outside the playback panel pause the demo and ask whether
to return to the main menu. Enter/A confirms, Escape/B cancels. Cancelling resumes
the prior playback state. Exiting restores the original player save; the demo has
its own save store for its entire lifetime.

For developer runs, open **Tools > Eternal Enigma > Playthrough > Open** in Unity.
This supports seed, normal/debug options, speed, time limit, stall timeout, pause/stop, and an optional goal of
visiting every generated destination and completing every dungeon once. The
default goal is the campaign's actual Finished flag. Neither launcher is invoked
by CI or by opening the game. Run Normal and Run Debug menu commands use seed 42.

Normal runs retain damage, consumable costs, gold limits and defeat. Debug runs
optionally protect all party members from HP loss, give melee/ranged attacks
infinite strength (lethal damage without integer overflow), and replenish gold, SP, hunger,
and preserve consumables used by the bot. Debug runs never grant progression
keys/capabilities or fabricate dungeon victories. Every debug run is marked
ineligible for balance statistics, including one with both cheat switches off.

The policy reads the current campaign graph and deliberately knows the full
dungeon map and stair positions. Reports label this omniscient navigation. Town
and dungeon routes reuse the game's `AStar.FindPath` (used by enemy pursuit), with
the existing character grid adapter for dungeons and a town walkability adapter.
The bot follows the route between turns, replanning when blocked, displaced, or
given a new objective. It walks through production movement code, opens eligible gates,
uses warps, changes capability companions in town, buys affordable supplies and
equipment upgrades, fights with the existing ally attack policy, uses restorative
items, and confirms the real stair/exit prompt. Content IDs, positions, number of
dungeons and floor counts are not encoded in the route. Supporting new mechanics
can still require policy changes. This is a baseline bot, not a claim of optimal
combat or a human-equivalent difficulty measurement; it currently has no active
skill selection or deliberate grinding strategy. Optional destination coverage
does not assert that every dialogue, shop offer or skill has been exercised.

Outcomes distinguish Victory, Defeat, Stalled, TimeLimit, Unsupported, GameError,
AutomationError and manual Stopped. There are no automatic retries or hidden
resources in normal mode. The final scene is frozen for inspection. Repeated
actions with the same position and enemy HP, missing paths, and lack of progress
produce a report rather than awarding success.

Each run writes a unique directory under `Logs/Playthroughs` in the editor or
`Application.persistentDataPath/Playthroughs` in a player:

- `report.json`: options, outcome, explanation, seed/generation fingerprint,
  app/Unity/build identifiers, elapsed time, actions, damage/healing, inventory,
  party and enemies, and completed/visited objectives.
- `actions.jsonl`: chronological actions with location, floor, position, party
  leader resources and elapsed time.
- `save.json`: the isolated campaign save using production serialization.
- `final.png`: final rendered state captured on completion/stop.

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

These short checks cover the input prompt, save restoration, lethal normal-mode
damage and optional debug resource protection/infinite strength. Validation
reports are marked separately and excluded from balance data. They do not launch a full campaign
in automated test discovery.
