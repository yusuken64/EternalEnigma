# Eternal Enigma: current game

Eternal Enigma is a Unity fantasy RPG with a generated overworld, safe towns and
Mystery Dungeon-style turn-based dungeon combat. The engine-independent Core creates
the campaign and grid layouts; Unity runs the playable game.

## Player flow

New Journey opens the authored hero roster. Choosing a hero chooses their model and fixed
primary/secondary class combination; the normal menu does not offer arbitrary class reassignment.
A new campaign begins with that protagonist in town-0 and grants starting supplies once.
Recruit companions, equip the party, learn skills and enter the town's story dungeon.
Clearing it opens the town exit; clearing the outdoor opening dungeon supplies the area-gate key.
Explore the six-region campaign, earn capabilities, use shortcuts and return to earlier regions,
then finish the final dungeon. Finished campaigns no longer offer Continue.

## Party and combat

The protagonist can travel with three companions. Paid recruits and campaign capability
companions have different progression roles. Combat classes do not satisfy overworld locks.
Ten classes provide weapon restrictions, starting stats, growth, ranked skills and passives.
Training for class heroes spends earned skill points. Party members act through the shared
turn system; downed allies can be revived and defeat occurs when the party is down.

Dungeon play includes melee, ranged and missile attacks, healing, statuses, elements,
movement skills, items, equipment, traps, gathering, destructible props and shared fog of war.
Ally AI uses skill intent and priorities. The Full Control preference changes party control;
autoplay owns input while it is running.

## World and town state

Core generates six ordered regions and five progression tiers. Personal traversal abilities
depend on active companions; acquired utility and vehicle capabilities persist. Fictional gate
text and named keys are generated from the logical requirements. The overworld has no combat.

Towns use configured buildings, walk-in service interiors, recruitment, shops, a trainer,
donation/entrance flows and an inn. The inn rests the party for free and can record a checkpoint.
Damage carries between successful runs until rested. Defeat's recovery button restores an inn
checkpoint when present; otherwise town return rules apply. Quit/abandon uses ordinary defeat
return rules. Retreat preserves loot without awarding dungeon completion.

Core's detailed service towns and Unity's configured towns are separate integration paths.
The full Core service-slot layout is not yet wired into Unity. See [towns](Town.md).

## Saves and development modes

Only the current save schema is supported; there are no save-format versions or migrations.
Campaign state, roster, current item stock, equipment, learned ranks, carried HP/SP and inn
checkpoint data persist. Layouts regenerate from seeds; exact combat turns do not resume.
Sandbox, Test Dungeon, Ability Test Lab and autoplay isolate their state from the player's save.

The game uses stylized fantasy models, Bamao UI, biome terrain and dungeon themes. Editor
playgrounds support environments, portraits and combat effects. Committed Blender files and
Unity assets are the editable art sources; completed construction scripts have been removed.

## Current limitations

Balance, full narrative content and target-hardware performance remain development work.
Logical completion checks do not prove combat victory. The unfinished town-service and
TWC-styling integration plans are retained with current scope. See [generation contract](../Procedural_RPG_Generation_Spec.md),
[campaign flow](CampaignFlow.md), [classes](Classes.md) and [test harness](../Assets/Tests/README.md).
