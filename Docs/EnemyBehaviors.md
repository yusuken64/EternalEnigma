# Dungeon enemy behaviors

The listed behaviors are runtime components on committed enemy prefabs. Tune their behavior data directly.

The roster reference follows `Assets/Data/MonsterData.txt`; the behavior table describes
current prefab assignments, including later changes to the original mapping.

| Reference enemy | Existing prefab | Added behavior |
|---|---|---|
| Magician / まどうし | Enemy_EvilMage | Waits until attacked, then uses its existing combat policies |
| Ice Sloth / イエティ | Enemy_Orc | Waits until attacked |
| Lilypa / リリパット | Enemy_RatAssassin | Six-tile, eight-direction ranged attacks |
| Green Dragon / ドラゴン | Enemy_Dragon | Six-tile fire breath; 50% chance after shot, attack budget, silence and cooldown checks |
| Trick Bag / わらいぶくろ | Enemy_MonsterPlant | Steals 20% of carried gold, rounded up, then flees |
| Demonite / ベビーサタン | Enemy_Salamander | Waits until attacked; steals an unequipped inventory entry, then flees |
| Giant Eyeball / おおめだま | Enemy_Beholder | Five-tile confusion gaze; three affected turns |
| Mimic / ミミック | Enemy_ChestMonster | Treasure-chest disguise until attacked |
| Silver Devil / シルバーデビル | Enemy_LizardWarrior | Two actions and up to two attacks per activation |
| Metal Babble / はぐれメタル | Enemy_MetalSlime | Silver recolored slime; 3 HP, 49 defense, 500 XP, two movement actions; flees, has spawn weight 1 versus the normal 20, and a 25% warp chance after surviving a hit |
| Nessie (rooting behavior moved from Skeleton) | Enemy_WormMonster | Stationary; roots adjacent targets until this enemy dies, allowing attacks and item use |
| Bomb Crag / ばくだんいわ | Enemy_TurtleShell | Explodes on death; silence suppresses both damage and the explosion effect |
| Hork / くさったしたい | Enemy_Werewolf | Packs of three; each member has two movement actions and one attack per activation |

Early combat floors (2–3 in the starter run) can contain a breakable Black Knight-shaped Stone Hulk statue where its full footprint fits. It is static scenery with 40 HP and no loot. Its baked south-facing idle pose retains the live enemy's size. Placement preserves connected movement and keeps the entire model clear of walls, stairs, props and characters. **Art > Build Enemy Statue** rebuilds it from the enemy prefab; **Art > Build Dungeon Door** rebuilds the textured, normalized door adapter.

Dormancy is an enemy state, separate from the Sleep ailment. Nearby movement does not wake these attack-provoked archetypes. A missed attack still provokes them. Ordinary randomly dormant enemies retain their proximity wake behavior. Every spawned enemy receives a random initial facing, including dormant enemies and later summons, using visual randomness that does not advance gameplay RNG.

Ranged attacks and confusion require a clear straight or diagonal line. Walls, blocked diagonal corners, scenery and intervening characters stop the shot. Rat Assassin shots use normal physical hit/critical resolution; dragon breath deals Fire damage. Both consume an attack. Only dragons roll the 50% fireball chance; an invalid shot consumes no probability roll. Confusion uses the status system and can be cleansed as an ailment. Cooldowns are two turns for ranged attacks and four for confusion.

Confused characters spend their turns attempting a random direction. They may walk, bump into an obstruction, or attack an adjacent character regardless of team. Expired confusion does not override input, and commands entered while confused are not queued for execution after recovery.

Thieves carry one stolen inventory entry or gold amount. They seek greater distance from the party and fight when cornered. Defeating one drops its stolen property once, preserving the original inventory object, stack and upgrades. Equipped items cannot be stolen. Drops use normal pickup/full-inventory handling. Stolen property is floor-local, following the existing dungeon save model.

Disguised mimics remain damageable enemies for deliberate attacks and area damage. They appear as treasure on the minimap, use a chest label in messages, and are excluded from autonomous targeting. Their chest disguise matches the enemy's size and faces south independently of the hidden enemy's random facing. On provocation, the disguise disappears and the original animated model returns.

The inspector component `EnemyBehavior` controls these abilities; edit the committed
prefabs directly. The one-time behavior authoring command was removed.
`Tools > Eternal Enigma > Tests > Run Enemy Behaviors` runs behavior integration
and prefab combat/animation regression checks.

## Ability feedback coverage (2026-10-11)

| Ability family | Visual/audio path | History entry |
|---|---|---|
| Melee, physical ranged | Character attack clips, melee/arrow profiles, existing hit sounds | Resolved attack/damage |
| Dragon fireball | Fire Flight profile and fire cast sound | Breath plus resolved damage |
| Theft, root, confusion | Steal/Root/Confusion profiles, profile cast sound and status auras | Stolen resource, grip or confusion cast/status |
| Spider duplication | Summon profile and cast sound; spawned model retains its authored pose | Split into two, only after a successful spawn |
| Large slime death split | Same spawn feedback for each successful child | Breaks apart and names the child |
| Metal slime warp | Portal cast/arrival cue and sound; immediate teleport | Warped away, only when a destination exists |
| Death explosion and explosion spell | Asset-pack Fire Direct Area profile and fire cast sound | Explosion plus resulting damage |
| Ordinary chest and finale town/dungeon reveals | Shared Mimic Reveal profile with shadow cast sound | Existing chest/town/dungeon reveal message |
| Every large boss family | Existing warning tiles/fields; preparation cue, family-specific release profile and cast sound | Named preparation and release, followed by damage/status results |
| Dormancy, packs, extra actions, flee/stationary policies | Existing idle/walk/attack animations; these are behavior rules, not separate casts | Wake message when provoked; no repeated event for each AI decision |

Reveal transitions are captured during action resolution and played once with the action replay. Hidden/offscreen and No Animations policies still apply. "Overworld dungeon reveal" refers to the finale's disguised dungeon locations; ordinary overworld locations remain visible under the existing exploration rules.

**Tools > Eternal Enigma > Combat Effects > Complete Enemy Feedback** rebuilds these default bindings and the dragon/werewolf prefab settings. It is an explicit authoring operation, not a runtime migration.

## Original roster reference

This source-data mapping records the initial substitutions. It does not override
current prefab behavior: Skeleton no longer roots targets; WormMonster owns that ability.

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
