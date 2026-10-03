# Campaign and sandbox flow

`Common` owns `CampaignContext`; `CampaignTravelService` validates travel, captures state
before transitions and blocks duplicate callbacks until the destination reports ready.
New Journey chooses an authored hero with fixed classes and starts inside town-0.

## Campaign travel

The overworld grid is lazy. Starting/continuing inside a town or its interior dungeon does
not generate it. The first overworld request creates it; `OverworldTerrainCache` retains
terrain, TWC data and biome meshes for later scene visits. Dynamic parties, markers and gates
reflect current state. Context/template changes release the cache, which is never serialized.

Town-0 contains story-0. Its completion unlocks the town exit with the Town gate key.
Repeatable-0 outside town awards the separate Town area key, used at starter-exit.
Interior dungeons have `ParentTownId`, share their parent town's overworld tile, and return
inside that town. Other victories return to the dungeon marker; ordinary defeats return
to the last town. Floor ranges by tier are 1-5, 5-10, 10-20, 20-30 and 30-40.

`StartDungeon` captures pre-run town state and explicitly selects visuals, biome, tier and
whether the run uses biome layouts. The two starter locations use BSP/throne layouts.
Other campaign runs use biome profiles. Seeds derive from campaign seed/location/floor.

Completion commits once per run. Final victory saves `Finished`, shows the victory screen
and disables Continue for that campaign. Retreat keeps loot without claiming victory.

## Current save schema

`SaveSystem` serializes `GameSaveData` directly with Unity `JsonUtility` into the `SaveData`
PlayerPrefs entry. `ISaveStore` supports isolated tests; nested scopes dispose in reverse order.
There are no schema versions, older-key rewrites, name-only inventory fallbacks or migrations.
Incompatible old saves require a new game.

| Data | Contents |
|---|---|
| `CampaignSnapshot` | Seed, identity, content fingerprint, scene/location/position, pending run, keys, gate resolutions, rewards, capabilities, party and completion |
| `TownSaveData` | Configuration/seed, gold, donations, item snapshots, active hero records, shops and restock cycle |
| `TownAllyData` | Stable ID, name, class IDs, learned skills/ranks, equipment, highest level, carried HP/SP |
| `DungeonSaveData` | Floor range, biome-layout choice, tier/biome, resolved visual selection, return-commit flag |
| `GameSaveData.Roster` | Full roster including benched heroes |
| `PreRunTownJson` | Town state used to recover an interrupted run |
| `InnSaveJson` | Non-nested full checkpoint taken at the inn |

`HasCampaign` checks for an actual fingerprint because JsonUtility can materialize an empty
nested snapshot for standalone test runs. Restoring a campaign regenerates its logical content
and verifies the fingerprint. Generator identifiers remain diagnostic metadata in generated
worlds, not fields in the save snapshot.

Continue recovers a pending run at its town/overworld entrance and restores pre-run town data
without granting victory or applying defeat losses. It does not restore floor combat state.
The defeat recovery button restores `InnSaveJson` when one exists; otherwise ordinary return
rules apply. Resting and explicitly saving at the inn are separate actions.

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
