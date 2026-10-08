# Weapon availability and loot progression

The tiered general shop offers all 76 active weapon definitions, including five
bow and five ammunition variants. The legacy `RightHand_Arrows` asset is the
explicit exclusion from the active weapon catalog audit. Existing prices and
stock quantities remain authored in the shop catalog.

| Minimum shop tier | Eligible dungeon floors | Examples |
| --- | --- | --- |
| 0-1 | 1-10 | Starter swords, shields, wands, Bow01-02 |
| 2 | 6-20 | Silver swords, Bow03-04, first greatsword |
| 3 | 15-30 | Mythril swords, Bow05, middle greatswords |
| 4 | 25-40 | Legendary shields, royal greatsword |

Greatswords THS01-07 have Strength 8, 10, 12, 14, 16, 18 and 20.
Bow01-05 have Strength 3, 5, 7, 9 and 11. The off-hand Silver Sword has
Strength 7 and Defense 0. Active weapons have descriptions and distinct item
names; ammunition offers map to Arrow01-05.

`ItemManager.GetRandomDrop(roll, floor)` filters by inclusive floor bands,
preserves a 28% consumable share and selects deterministically within the
chosen category. Floor-item, prop and enemy drops pass the current floor;
legacy overloads default to floor 1. Consumables remain eligible on all floors.
Stored rolls from saves created before this table change may resolve differently.

`HeroAnimator` activates the equipped bow/arrow child, defaulting empty variants
to Bow01/Arrow01. All bow and ammunition variants use the bow stance.
Equipment proficiency and displacement rules are described in [Classes](Classes.md).

`WeaponCatalogTests` checks reachability, names, descriptions, shop tiers,
ammunition offers, floor boundaries, repeatability and consumable share.
Hero and town economy fixtures cover models, purchases, equipment and persistence.
The three catalog tests passed in the retained
[2026-10-07 EditMode report](Art/Previews/Diorama/Verification/PostCleanupEditMode.xml);
that report was inspected, not rerun, during the documentation audit.
