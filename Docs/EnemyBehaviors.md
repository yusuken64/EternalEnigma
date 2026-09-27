# Dungeon enemy behaviors

Names and models follow the existing assignments in `Assets/Data/MonsterData.txt`.

| Reference enemy | Existing prefab | Added behavior |
|---|---|---|
| Magician / まどうし | Enemy_EvilMage | Waits until attacked, then uses its existing combat policies |
| Ice Sloth / イエティ | Enemy_Orc | Waits until attacked |
| Lilypa / リリパット | Enemy_RatAssassin | Six-tile, eight-direction ranged attacks |
| Trick Bag / わらいぶくろ | Enemy_MonsterPlant | Steals 20% of carried gold, rounded up, then flees |
| Demonite / ベビーサタン | Enemy_Salamander | Waits until attacked; steals an unequipped inventory entry, then flees |
| Giant Eyeball / おおめだま | Enemy_Beholder | Five-tile confusion gaze; three affected turns |
| Mimic / ミミック | Enemy_ChestMonster | Treasure-chest disguise until attacked |
| Silver Devil / シルバーデビル | Enemy_LizardWarrior | Two actions and up to two attacks per activation |
| Metal Babble / はぐれメタル | Enemy_MetalSlime | Silver recolored slime; 3 HP, 49 defense, 500 XP, two movement actions; flees |
| Goopi / マドハンド | Enemy_Skeleton | Stationary; roots adjacent targets until this enemy dies, allowing attacks and item use |

Early combat floors (2–3 in the starter run) contain a breakable Black Knight-shaped Stone Hulk statue. It is static scenery with 40 HP and no loot. Placement preserves connected movement and avoids stairs, spawns, items and characters. The live Black Knight enemy remains separate.

Dormancy is an enemy state, separate from the Sleep ailment. Nearby movement does not wake these attack-provoked archetypes. A missed attack still provokes them. Ordinary randomly dormant enemies retain their proximity wake behavior.

Ranged attacks and confusion require a clear straight or diagonal line. Walls, blocked diagonal corners, scenery and intervening characters stop the shot. Ranged attacks use normal hit/critical resolution and consume an attack; confusion uses the status system and can be cleansed as an ailment. Cooldowns are two turns for ranged attacks and four for confusion.

Confused characters spend their turns attempting a random direction. They may walk, bump into an obstruction, or attack an adjacent character regardless of team. Expired confusion does not override input, and commands entered while confused are not queued for execution after recovery.

Thieves carry one stolen inventory entry or gold amount. They seek greater distance from the party and fight when cornered. Defeating one drops its stolen property once, preserving the original inventory object, stack and upgrades. Equipped items cannot be stolen. Drops use normal pickup/full-inventory handling. Stolen property is floor-local, following the existing dungeon save model.

Disguised mimics remain damageable enemies for deliberate attacks and area damage. They appear as treasure on the minimap, use a chest label in messages, and are excluded from autonomous targeting. On provocation, the disguise disappears and the original animated model returns.

The inspector component `EnemyBehavior` controls these abilities. `Tools > Eternal Enigma > Enemies > Author Dungeon Behaviors` reapplies the assignments. `Tools > Eternal Enigma > Tests > Run Enemy Behaviors` runs behavior integration and prefab combat/animation regression checks.

## Roster reference

| Reference | Existing prefab |
|---|---|
| Bomb Crag | Enemy_TurtleShell |
| Demonite | Enemy_Salamander |
| Deranger | Enemy_NagaWizard |
| Drakee | Enemy_Bat |
| Ghost | Enemy_Fishman |
| Giant | Enemy_Cyclops |
| Giant Eyeball | Enemy_Beholder |
| Giant Slug | Enemy_Cactus |
| Golem | Enemy_Golem |
| Goopi | Enemy_Skeleton |
| Green Dragon | Enemy_Dragon |
| Hork | Enemy_Werewolf |
| Ice Sloth | Enemy_Orc |
| Imposter | Unassigned in the source data |
| Liclick | Enemy_Spider |
| Lilypa | Enemy_RatAssassin |
| Magician | Enemy_EvilMage |
| Metal Babble | Enemy_MetalSlime (replaces MushroomSmile in the spawn table) |
| Mimic | Enemy_ChestMonster |
| Monjar | Enemy_CrabMonster |
| Mud Doll | Enemy_WormMonster |
| Mummy | Enemy_Specter |
| Mystic Doll | Enemy_FylingDemon |
| Rogue Knight | Enemy_BishopKnight |
| Shadow | Enemy_BattleBee |
| Silver Devil | Enemy_LizardWarrior |
| Slime | Enemy_Slime |
| Stone Hulk | Enemy_BlackKnight |
| Toadstool | Enemy_MushroomAngry |
| Trick Bag | Enemy_MonsterPlant |
| Winged Demon | Enemy_DemonKing |
| Wyvern | Enemy_StingRay |

Movement restrictions use StatusEffect.BlocksMovement. Leg bind (Stuck) blocks movement without skipping attacks or menus. Goopi grip inherits this behavior, with a source-dependent lifetime and release on the shared character-death notification; movement code has no Goopi-specific checks.
