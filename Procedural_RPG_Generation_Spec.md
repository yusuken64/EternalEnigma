# Procedural RPG generation: implemented contract

This document describes the current repository. It replaces the historical v5 proposal;
unimplemented narrative and presentation ideas are identified below rather than treated as guarantees.

## Architecture

`Core/EternalEnigma.Core` contains a Unity-free .NET Standard 2.1 library, .NET 10 tests,
a generation/export CLI and an interactive console explorer. Unity 6000.2.7f2 imports
the compiled library from `Assets/Plugins/EternalEnigma.Core`. Unity owns combat,
prefabs, input, UI, presentation and PlayerPrefs serialization.

Core returns immutable campaign, overworld, town and dungeon data. Grid masks use
`[x,y]`; adapters copy masks into TileWorldCreator. Rendering does not determine movement.
See [Core/TWC integration](Docs/CoreTwcBridge.md).

## Campaign

- `CampaignGenerator.Generate(seed)` validates the logical campaign before returning.
- Six ordered regions, five dungeon tiers, story and repeatable dungeons, capability sources,
  towns, landmarks, return objectives and keyed warps form the progression graph.
- The 21-capability vocabulary is defined in `Capabilities/Capability.cs`. A campaign activates
  a subset. Personal capabilities require an active companion; acquired utility and vehicle capabilities persist.
- Requirements are alternatives of capability sets. Obstacle and interaction locks latch open;
  area gates continue checking the active party. Keys are permanent and nonconsumable.
- The party is the protagonist plus at most three companions. Party changes occur in towns.
- The opening is town-0, its story-0 interior, repeatable-0 outside town, then starter-exit.
  Story victory unlocks the town exit; outdoor dungeon victory supplies the separate area key.
- One required return objective and optional return secrets reward revisiting earlier regions.
- Lock narration stores skin IDs, fictional descriptions and key display names after topology generation.
  Mechanical key IDs remain separate from their displayed names.

`CampaignContext` is Unity's progression authority. `CampaignSession` is the abstract graph
traversal API; it is not the Unity combat host. See [campaign generation](Docs/CampaignGeneration.md).

## Geography and interiors

`OverworldGridGenerator` realizes routes, territories, gates, water, features and locations.
Physical grid validation checks connectivity against the graph with closed corners and gate sealing.
The overworld generates lazily and is cached in memory during a campaign. Travel has no combat.

Dungeon floor ranges are 1-5, 5-10, 10-20, 20-30 and 30-40. Per-location/floor seeds
are stable hashes of campaign seed, location ID and floor. The two opening dungeons use
the original BSP/throne layouts. Other campaign dungeons use eight biome profiles with
tier-scaled dimensions, containers, destructibles, hazards and gathering. Entry/exit floors
are quiet transition rooms. Mid-floor combat state is not saved.

Core detailed towns assign the five service kinds (bakery, consumables, items, inn, trainer)
to seeded building slots. Unity's `CampaignTownLayout.Configure` consumes `TownLayout.SlotServices`
and maps them to authored building definitions, with one dungeon entrance and seven residential
slots. Town-0 turns the first residential slot into the home. Furnished interiors publish
furniture occupancy, vendor anchors and reserved interaction cells to both Core and Unity.

## Gameplay and persistence

Unity implements eight-direction turn-based dungeon combat, ranked class skills, equipment,
elements, statuses, ally AI, traps, shared visibility and action playback. New Journey selects
an authored hero and their fixed class combination. Class training spends skill points;
classless configured training retains its gold cost. The inn supports free rest and an explicit
checkpoint. See [game design](Docs/GameDesign.md), [classes](Docs/Classes.md) and [towns](Docs/Town.md).

Saves use only the current `GameSaveData`/`CampaignSnapshot` schema, without format versions,
old-key rewrites or migrations. Inventory and equipment use `ItemSaveData` including remaining
stock. Campaign content fingerprints remain integrity checks. Start a new game for incompatible
older data. Continue and defeat recovery load the selected slot's last explicit checkpoint;
quit discards unsaved campaign progress. Initial creation, home/inn saves and the completed-campaign
save are the persistent writes. See [campaign saves](Docs/CampaignSaves.md).

## Validation boundaries and remaining work

Core tests cover deterministic generation, class learning, logical completion, grid topology,
town/dungeon placement and progression restoration. They assume reachable combat can be won;
they do not establish game balance or certify Unity prefab behavior.

Remaining work includes generalized TWC style profiles/binding validation, encounter and
puzzle content, terminal-native menus, lock legibility tiers, graded wrong-capability feedback,
terrain-specific narration and chronicle/faction content. The town-service bridge and production
TWC ground renderer are implemented. No full plot, release balance or target-device performance guarantee is implied.
The old 64-rule proposal is not an acceptance claim for this build.
