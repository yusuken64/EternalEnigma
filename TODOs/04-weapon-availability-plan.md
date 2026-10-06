# Weapon availability implementation

The historical audit in `Docs/TodoAudit-2026-10-06.txt` predates the tiered
economy. The general shop now offers every active non-ammunition weapon without
changing its established prices. Four bow definitions extend the existing Bow01
line, and the five ammunition offers map to Arrow01–Arrow05. The legacy
`RightHand_Arrows` definition remains an explicit catalog exclusion.

## Progression

| Shop tier | Dungeon floors | Weapon examples |
| --- | --- | --- |
| 0–1 | 1–10 | starter swords, shields, wands, Bow01–02 |
| 2 | 6–20 | silver swords, Bow03–04, first greatsword |
| 3 | 15–30 | mythril swords, Bow05, middle greatswords |
| 4 | 25–40 | legendary shields, royal greatsword |

Greatsword Strength runs from 8 to 20 in steps of 2. Bow Strength is 3, 5, 7,
9, 11. The off-hand Silver Sword has Strength 7 and Defense 0. All active weapon
definitions have descriptions.

`ItemManager.GetRandomDrop(roll, floor)` filters the loot table by inclusive
floor bands, preserves 28% consumable selection, and picks deterministically
within the selected category. The three dungeon drop sites supply the current
floor; older overloads use floor 1. Consumables remain eligible on every floor.
Stored rolls in older saves may resolve to different items after this change.

`HeroAnimator` activates the matching bow or arrow child. Empty variants select
Bow01 or Arrow01. The bow stance serves every bow and ammunition variant.

The EditMode catalog tests audit reachability, unique names, shop tiers,
descriptions, all five ammunition offers, floor boundaries, repeatability, and
the consumable share. PlayMode hero and economy tests cover models, stances,
purchases, equipment, and persistence.
