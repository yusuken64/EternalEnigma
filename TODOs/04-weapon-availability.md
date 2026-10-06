# Weapon availability

Implemented 2026-10-06. The 76 active weapon definitions are sold by the tiered
general shop and included in dungeon loot. `RightHand_Arrows` is retained as a
legacy asset and is the sole explicit exclusion from the weapon catalog audit.

- [x] Keep the adopted shop offers, prices, and stock quantities.
- [x] Set greatswords THS01–07 to Strength 8, 10, 12, 14, 16, 18, 20; set the
  off-hand Silver Sword to Strength 7 and Defense 0; describe every weapon.
- [x] Add Bow02–Bow05 at Strength 5, 7, 9, 11, with unique item names. The five
  ammunition offers use Arrow01–Arrow05 and can be restored by item name.
- [x] Select the equipped bow and arrow child models, defaulting to Bow01 and
  Arrow01 when no variant is set. All variants use the bow stance.
- [x] Pass the current floor at floor-item, prop, and enemy drop sites. The old
  `GetRandomDrop` overloads enter at floor 1.
- [x] Band all weapons by minimum shop tier: tiers 0–1 on floors 1–10, tier 2
  on 6–20, tier 3 on 15–30, and tier 4 on 25–40. Consumables remain eligible
  throughout, with a 28% selection share.
- [x] Add catalog, floor-boundary, deterministic-drop, hero-model, and town
  purchase/equip coverage.

Stored dungeon rolls from saves generated before this loot table change can
resolve to different items when loaded.

Verification results: see the implementation commit or task report.
