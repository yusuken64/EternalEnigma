# Make unused weapon models equippable

> Audit 2026-10-06: the original unobtainable-weapon/shop counts below are stale.
> Existing non-ammunition weapon definitions are now represented in tiered shop
> catalogs. Keep the stat, description, floor-drop and optional model-variant
> work; rebase shop proposals on the current economy. See
> [the source audit](../Docs/TodoAudit-2026-10-06.txt).

## Historical rationale (pre-economy baseline)
Every Tiny Hero weapon model (`Assets/Art/RPGTinyHeroWavePolyart/Mesh/Weapons`) is already a child object under the weapon bones on every hero prefab (checked on `Prefabs/Town/Allies/Ally_MC01.prefab`). `HeroAnimator.SetWeapon` (`Scripts/Town/HeroAnimator.cs:100-145`) shows the model whose name matches `EquipmentItemDefinition.WeaponModelName`.

Most models already have an item definition in `Prefabs/Dungeon/Items/Weapons/`. The player still can't get most of them, because those definitions are not in any source that hands out items.

Item sources (`ItemManager.GetAsInventoryItemByName`, `Scripts/Dungeon/System/ItemManager.cs:16-29`):
- **Drops:** `ItemManager.ItemDefinitions` in `Scenes/Common.unity`.
  - 39 entries: 28 weapons and 11 consumables.
  - A uniform pick over the list (`roll % Count`), with no floor tiering.
- **Starting items:** `ItemManager.StartingItems`, 3 entries.
- **Shops:** `Resources/Towns/Buildings/*.asset` `ShopCatalog`.
  - The only weapons sold are Wooden Arrows, Bow and Wooden Buckler, in `Items.asset`.
- **Demo:** the `DemoDungeonLoadout` asset, used by the demo and the test lab only.

## Audit: weapon models with no way to get them (45 item definitions, plus bow variants)
Item definitions already exist for all of these and are wired to a hero model. None of them are in drops, shops or starting items.

| Weapon type | Unobtainable definitions (model → name, stat) | Obtainable today |
|---|---|---|
| Main-hand sword/axe/hammer (`RightHand_OHS*`) | OHS03 Short Sword S3, OHS04 Iron Sword S4, OHS05 Steel Sword S5, OHS06 Knight's Sword S6, OHS07 Silver Sword S7, OHS08 Gilded Sword S8, OHS09 Royal Sword S9 | OHS01, 02, 10–16 (drops) |
| Off-hand sword/axe/hammer (`LeftHand_OHS*`) | OHS03–OHS14 (12 items: Short Sword … War Hammer (Off-hand), S3–S14) | OHS01, 02, 15, 16 (drops) |
| Shield (`LeftHand_Shield*`) | Shield02–15 (14 items: Wooden Shield D2 … Mythril Shield D15) | Shield01 (Items shop), Shield16–20 (drops) |
| Spear (`RightHand_Spear*`) | Spear01 Wooden S2, Spear02 Iron S4, Spear03 Steel S6 | Spear04, 05 (drops) |
| Two-handed sword (`RightHand_THS*`) | THS01–04 (Greatsword, Iron, Steel, Knight's), **all S12** | THS05–07 (drops) |
| Wand (`RightHand_Wand*`) | Wand01 Wooden S1, Wand02 Apprentice S2, Wand03 Oak S3, Wand04 Crystal S4 | Wand05–07 (drops) |
| Bow / arrows | **No item definitions** for the model variants Bow02–05 and Arrow02–05 | `LeftHand_Bows` "Bow" (shows Bow01 only), `RightHand_Arrows`, Wooden Arrows |

**Pattern:** the drop list holds the lowest tier (Stick, Needle) and the top tiers. The whole middle of each line is missing.

**Data problems found while auditing:**
- **Greatswords:** THS01–07 are all Strength 12, so the line has no progression.
- **`LeftHand_OHS07_Sword` (Silver Sword (Off-hand)):** S5/D7, while every other off-hand sword is S=n/D0. It looks like a copy-paste mistake.
- **Bow variants:** `Bows` and `Arrows` are single parent objects. Bow01 and Arrow01 are active, and 02–05 are inactive children. `SetWeapon` only switches the direct children of `weapon_r` and `weapon_l`, so the variants need code support before they can become items.
- **`Resources/DemoDungeon/Loadout.asset`:** "Demo Scroll: Inspect Item" has `WeaponType: BowAndArrow` and `WeaponModelName: Arrows`. This is test data, so leave it unless it causes problems.

## Phase 1: Reconcile existing shops
Tiered equipment offers already cover the existing non-ammunition weapon
catalog. Do not repeat the original starter-offer additions or overwrite the
adopted economy pricing. Remaining work is automated reachability coverage and
buy/equip/model checks; use [the active checklist](04-weapon-availability.md).

## Phase 2: Floor-tiered drops for the middle tiers (small code change)
The pick is uniform, so appending 34 more weapons would push weapons from 72% to about 87% of drops and crowd out consumables. Add tiering instead:
1. **Floor range on items:** add `MinFloor` / `MaxFloor` to `ItemDefinition`, defaulting to 1 and int.MaxValue so current items are unchanged.
2. **Floor-aware drop pick:**
   - Add `ItemManager.GetRandomDrop(int roll, int floor)`, which filters `ItemDefinitions` by floor and then picks `roll % filtered.Count`.
   - Thread the current floor through the three call sites: `Game.cs:296` floor items, `DungeonProp.cs:53`, and `MovementAction.cs:526` enemy drops.
   - Keep the old overloads, delegating with floor 1, for tests.
3. **Fill the tiers:** append the mid-tier definitions to `Common.unity` `ItemDefinitions`, and set the floor bands from the dungeon's floor count. Bands, as a starting point:

   | Strength / Defense | Floors | Example |
   |---|---|---|
   | 3–6 | 1–10 | Short Sword, Oak Wand, Iron Shield |
   | 7–10 | 6–20 | Gilded Sword, Knight's Shield |
   | 11+ | 15+ | Mythril, Dragon, Legendary |

   Move the existing top-tier drops into their bands as well, so the Legendary Shield stops dropping on floor 1.
4. **Weapon share:** keep consumables in every band, so their share stays about where it is today.
5. **Determinism:** the same roll and floor still give the same item. This does change which item a stored roll produces, so floors generated before the change and reloaded from a save may roll different items. That is acceptable before release; otherwise version the drop table.

## Phase 3: Fix the data problems
1. **Greatsword stats:** spread THS01–07 across about S8–S20, so two-handed stays above one-handed at each tier.
2. **`LeftHand_OHS07_Sword`:** change it to S7/D0.
3. **Item descriptions:** fill in the empty `Description` fields for all weapons, which are blank today.

## Phase 4 (optional): Bow variants
1. **Child selection:** let `EquipmentItemDefinition` name a child variant, for example a `WeaponModelVariant` of `Bow03`. `SetWeapon` switches on the parent (`Bows`) and then turns on only that child under it, for both `Bows` and `Arrows`.
2. **New items:** add four bow definitions (Bow02–05, S6–S12, ranged) with matching arrow variants, banded like Phase 2.
3. **Animation check:** check that `BowAndArrowStance` animates every variant, since all bows share the same skinned rig in `BowsSkinnedMesh.fbx`.

## Tests
- **New EditMode test:** every `EquipmentItemDefinition` under `Prefabs/Dungeon/Items/Weapons` is reachable from drops, a shop, or starting items. Allow an explicit list of exclusions, so this audit can't quietly regress.
- **New test:** `GetRandomDrop(roll, floor)` never returns an item outside its floor band, and always returns something for floors 1 through max.
- **Existing tests:**
  - `HeroPrefabTests:65` already checks the drop-list weapons against the hero prefabs, and it now covers more items.
  - `AuditRegressionTests`, `MenuSceneNavigationTests` and `ClassSkillSmokeTests` use `.First(...)` over `ItemDefinitions`. Appending keeps them stable.
- **Runs:** EditMode and PlayMode via `Tools/harness-*.json`, plus `dotnet test` for `Core` if the floor needs a Core type.

## Order
Phase 1 → 3 → 2 → 4, one commit each. Phase 1 alone makes every weapon stance buyable at low tier.
