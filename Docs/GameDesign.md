# Eternal Enigma — Game Design Document

## Game at a glance

**Genre:** Procedural fantasy role-playing game with grid exploration and turn-based, Mystery Dungeon-style combat.

**Platform and presentation:** Unity 3D. The game uses compact tile-based spaces, animated heroes and monsters, and themed towns and dungeons. The procedural campaign model is engine-independent; Unity presents its generated world and runs the moment-to-moment game.

**Player promise:** Explore a world whose routes and regions are shaped by a campaign seed, recruit a flexible party, gain abilities that change where you can travel, and venture into dangerous dungeons for story progress and rewards. The world should reward curiosity and make revisiting known places meaningful.

## Design pillars

- **Explore through fiction.** The player encounters cliffs, ruins, hazards, blocked roads, and other concrete places. Capabilities explain how the party overcomes them behind the scenes.
- **Progression opens the map.** New companions, permanent abilities, vehicles, and keys create routes into previously inaccessible areas and return visits to earlier regions.
- **Generated campaigns remain coherent.** The progression structure is generated and validated before its geography is presented. A seed changes the active abilities, route structure, sources, alternatives, and themes while preserving a completable campaign.
- **Party choice matters.** Some traversal abilities depend on an active companion, so players choose whom to bring. No single companion identity is required; equivalent providers support required routes.
- **Exploration and combat have distinct rhythms.** Overworld movement is for navigation and discovery. Dungeon actions advance the turn-based simulation and demand tactical decisions.
- **Defeat costs a run, not the campaign.** Campaign progression survives defeat. Dungeon supplies and carried wealth are subject to the game's run rules.

## Player experience and core loop

1. **Choose or begin a campaign.** Starting a new game establishes the protagonist, starting party, class choice, and campaign seed. Campaign generation lays out regions, routes, towns, dungeons, rewards, and capability sources.
2. **Explore the overworld.** Move across a tile grid, inspect destinations, resolve route obstacles, and find towns, dungeons, reward sites, and shortcuts. Overworld movement does not run dungeon combat simulation.
3. **Prepare in town.** Recruit or dismiss companions, choose the active party, shop, equip gear, train skills, and use town services. The protagonist can travel with up to three companions.
4. **Enter a dungeon.** Story dungeons advance the campaign; repeatable dungeons provide a way to earn experience and resources. Fight enemies, use skills and items, find treasure, navigate hazards, and reach the stairs.
5. **Claim rewards and unlock routes.** A completed encounter or dungeon can award companions, keys, or permanent capabilities. Use these to open new passages or return to earlier regions for optional rewards.
6. **Repeat until the final dungeon.** Complete the campaign's required progression and return objective, then enter the final dungeon. Final victory ends the campaign.

## World and campaign structure

A campaign is generated from a seed. The current campaign model has six ordered semantic regions (A–F), each associated with a distinct biome and landmark. It creates five progression tiers, a main progression spine, alternate routes, return opportunities, and keyed warps between later and earlier regions. Towns provide recruitment and fast travel; dungeon locations provide story encounters or repeatable runs. There are four story dungeons, a final dungeon, and repeatable dungeons across progression tiers in the current generator.

The generated capability vocabulary includes personal traversal skills, vehicles, and utility abilities. Personal skills are supplied by companions and remain available while a provider is in the active party. Vehicle and utility unlocks persist once earned. Campaigns activate only a subset of the capability pool; inactive abilities do not create requirements. Towns are placed across the biome chain, and companions that provide required abilities have redundant sources so one missed recruit does not strand the campaign.

Campaign keys permanently unlock paired warps. These shortcuts make return trips easier as the map opens. Optional return secrets reward revisiting earlier places; one return objective is part of required progression. The final dungeon is the terminal destination.

The overworld is a navigable grid with capability-aware obstacles and area gates. Town interiors and dungeon floors are generated as separate tile spaces. Layouts are deterministic from the campaign seed and location, so returning to a location in the same campaign recreates its layout.

## Dungeons and combat

Dungeons use turn-based grid movement: player and enemy actions advance the simulation. Movement is eight-directional, with diagonal corner rules determined by the terrain. Combat takes place in dungeons; the overworld is focused on travel and has no combat encounters.

Dungeon floors contain enemies, stairs, and possible treasure, items, traps, hazards, and destructible scenery. Story layouts are tied to their location and floor; repeatable dungeons can regenerate between runs. Biome profiles give floors distinct room and corridor shapes. Entry and exit floors are designed as quieter transition spaces. Defeat or abandonment returns the party to safety under the applicable town/dungeon run rules.

The combat system supports weapons, active and passive skills, SP costs, status effects, equipment, and AI-controlled allies. The class system defines combat identities such as Warrior, Guardian, Archer, Elementalist, Healer, Bard, Occultist, Rogue, Commander, and Scout. The protagonist chooses a class at new game; recruited heroes have authored fixed classes or class combinations. Classes affect stats, weapon eligibility, and learnable skills. Classes are separate from world-traversal capabilities: a combat skill does not satisfy an overworld gate.

## Party, classes, and growth

The party consists of the protagonist and up to three companions. Companions can be recruited, benched, and reassigned in towns. Some companions support exploration by providing personal traversal abilities; others may be selected for their combat roles. Paid recruits and campaign reward companions have distinct purposes. Party members retain learned skills and progression between dungeon runs, while the active party and equipment are saved.

Heroes have a fixed class, with optional primary/secondary combinations for authored recruits. The protagonist's class selection determines a combat build, not the campaign layout. Class skill trees use ranks; primary and secondary classes have different progression allowances. Trainers provide skill learning and towns provide equipment and services.

## Towns and economy

Towns are safe preparation and recovery hubs on a continuous walkable map. Their generated plans place roads, buildings, parks, trees, allies, and a dungeon entrance. Shop buildings can contain walk-in interiors with vendors. Town services support shopping, training, recruitment, donations, and party management.

Purchases and training are paid with gold. Shop catalogs define stock, prices, and quantities; stock persists across leaving and continuing, with restock rules tied to completed dungeon returns. Some legacy town content also uses donation thresholds to unlock dungeon tiers. The campaign mode uses generated story/repeatable dungeon destinations and its own progression rules.

Victory carries the surviving inventory and equipment back according to the town run rules. Defeat clears carried items and equipment while retaining earned gold; party membership, learned skills, and each hero's highest level reached persist. Starting supplies are granted for a new game rather than every dungeon entry.

## Narrative and tone

The project frames progression through a fantasy world of distinct biomes, towns, landmarks, ruins, and dungeons. The procedural design aims to derive place meaning and route fiction from the generated progression structure so obstacles feel like parts of the world rather than abstract checks. The specific plot, protagonist motivation, character arcs, dialogue, and ending details are not established in the reviewed design material and remain open content decisions.

## Modes and persistence

**Campaign** is the normal persistent game. Saves capture campaign identity and generation versions, position, permanent unlocks, keys, resolved routes, claimed rewards, completed locations, visited towns, party state, and any pending dungeon run. Continuing a campaign rebuilds its context and can recover an interrupted run without falsely awarding victory or defeat.

**Sandbox** uses the same campaign generation, movement, and completion systems with an isolated in-memory save. It supports testing and experimentation without replacing the player's normal save. Developer autoplay can run normal or debug-assisted sessions for observation; debug runs are not eligible for balance statistics.

## Visual and audio direction

The existing project uses stylized, approachable fantasy characters and props, alongside distinct biome and dungeon themes. The art direction should keep generated spaces readable at a glance: clear floors, walls, gates, landmarks, interactables, and party silhouettes. Each biome should be identifiable through palette, architecture, vegetation, and dungeon geometry. A definitive audio direction and music brief are not specified in the reviewed documents.

## Current build and scope notes

This document summarizes the repository's implemented systems and current design documents, rather than presenting every planned feature as shipped. Core campaign generation, structural validation, tile-world generation, town and dungeon plans, campaign saves and travel, class assignment, and several Unity gameplay integrations are documented in the project. The overall project remains in active development. Some detailed combat balance, class breadth, narrative content, and cross-system polish are still evolving. The procedural campaign core and Unity gameplay are separate layers, and a validated core campaign does not by itself verify every Unity presentation or combat interaction.

## Reference documents

- [Campaign and sandbox flow](CampaignFlow.md)
- [Campaign generation](CampaignGeneration.md)
- [Dungeon floors and town interiors](DungeonFloor.md)
- [Biome dungeons](BiomeDungeons.md)
- [Classes and skills](Classes.md)
- [Campaign presentation](CampaignPresentation.md)
- [Enemy behaviors](EnemyBehaviors.md)
- [Procedural RPG generation specification](../Procedural_RPG_Generation_Spec.md)
