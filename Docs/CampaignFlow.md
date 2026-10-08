# Campaign and sandbox flow

`Common` owns `CampaignContext`; `CampaignTravelService` validates travel, captures state
before transitions and blocks duplicate callbacks until the destination reports ready.
New Journey chooses a slot and authored hero, then starts at the bed in town-0.
See [campaign saves and artwork](CampaignSaves.md) for the explicit-save contract.

## Campaign travel

The overworld grid is lazy. Starting/continuing inside a town or its interior dungeon does
not generate it. The first overworld request creates it; `OverworldTerrainCache` retains
terrain, TWC data and biome meshes for later scene visits. Dynamic parties, markers and gates
reflect current state. Context/template changes release the cache, which is never serialized.

Town-0 contains story-0. Its completion unlocks the town exit with the Town gate key.
Repeatable-0 outside town awards the separate Town area key, used at starter-exit.
Interior dungeons have `ParentTownId`, share their parent town's overworld tile, and return
inside that town. Other victories return to the dungeon marker. Campaign defeat
restores the selected slot's last explicit save, including its saved town and position. Floor ranges by tier are 1-5, 5-10, 10-20, 20-30 and 30-40.

`StartDungeon` captures live town state in memory and selects the run's biome, tier and visuals.
The two starter locations use BSP/throne layouts. Seeds derive from campaign seed/location/floor.
Completion commits rewards to memory once per run. Final victory offers an explicit
**Save completed campaign** action. Retreat retains loot and progression in memory.

## Travel interactions

Overworld dungeon entry first shows a confirmation with campaign clear status. Town entry
and direct `CampaignTravelService.EnterLocation` calls remain immediate. Returns to town
place the leader on a free, walkable neighbor of its dungeon entrance (south, west, east,
then north), using the existing party arrival formation. Overworld victories keep their
original destination.

Physical locks open a chooser owned by `OverworldMenuManager`'s `DialogController`.
It displays the obstacle and all acquired capabilities and held keys, without recommending
a solution. Benched companions' capabilities remain visible and disabled. Description
focus supports up/down scrolling and right to actions; actions support keyboard/gamepad
navigation and Cancel/back. Wrong or repeated actions leave the chooser open.

Core's `LockSolutionEvaluator` tracks each alternative independently. A
`LockInteractionSession` revalidates the target and each typed capability/key action,
then commits a completed solution once. Partial progress belongs only to that chooser;
closing it or leaving the scene discards progress. No new save fields or resource costs
are introduced. Autoplay submits explicit actions through the same session API.

The overworld party menu's Capabilities tab lists learned capabilities, sources and
availability. Town greetings append the current town exit hint, or the next required
unopened lock. Hints describe alternative/paired requirements and acquisition locations
with generated biomes and eight-way geographic bearings. Waiting allies occupy their
tiles and require an adjacent, facing interaction to recruit.

Run **Tools > Eternal Enigma > Tests > Run Travel Interactions** for the relevant Unity
regressions; results are written to `Temp/HarnessResults/PlayMode.json` and `.xml`.

## Save schema

Three new PlayerPrefs keys contain independent campaign snapshots, roster progression,
active party, possessions, shops, playtime, save-point identity, awake arrival position and
precomputed visual summaries. The legacy key is untouched and unavailable for Continue.
Initial creation and explicit home/inn/completion saves are the only persistent writes.
Quit and defeat restore the selected slot without applying penalties. No nested checkpoint
or pre-run recovery JSON is stored. Campaign generation/fingerprint validation occurs on
Continue, never during slot browsing; overworld terrain remains lazy.

## Party and isolation

The protagonist stays selected with up to three companions. Paid recruits grant no traversal
capability. Campaign companions use `TownConfiguration.CampaignCompanions`, with deterministic
catalog fallback when no mapping is authored. Dungeon allies are rebuilt from active records.

Sandbox uses the same generation/progression with isolated memory state and simulated dungeon
completion. SaveSystem prevents writes/clears while sandbox is active. Autoplay and Test Dungeon
also isolate their save stores. See [autoplay](Autoplay.md).

Core validation: `dotnet test Core/EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release`.
Unity validation: `node Tools/unity-mcp.mjs harness Campaign`, `Overworld`, `Town` and `EditMode`.
Unity commands require the project open with its local MCP bridge active.
