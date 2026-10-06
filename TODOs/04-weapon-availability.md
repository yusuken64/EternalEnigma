# TODO: Make unused weapon models equippable

> Audit 2026-10-06: **partial; the baseline below is stale.** Tiered shops now
> reference the existing non-ammunition weapon definitions. Reconcile Phase 1
> with the adopted economy catalog/prices instead of adding duplicate offers.
> Stat fixes, descriptions, floor-banded drops and bow-model variants remain
> open. See [the source audit](../Docs/TodoAudit-2026-10-06.txt) for evidence and limits.

## Goal
Every Tiny Hero weapon model the heroes can display can also be obtained:
- the starter tiers of every weapon type are bought in town
- the middle and top tiers drop in floor-appropriate bands
- weapon stats rise steadily within each line

Full audit and rationale: `TODOs/04-weapon-availability-plan.md`.

## Current state (audited 2026-10-06)
Existing non-ammunition weapon definitions are represented in tiered shop
catalogs. `TownShopCatalog.Resolve` filters offers by `MinimumTier`; the adopted
economy pricing replaces the original starter-shop proposal. The old claim that
45 definitions are unobtainable is obsolete.

Drops still use an unbanded `ItemManager.DungeonLoot` pool. Weapon data corrections
and Bow02?05 / Arrow02?05 model selection remain open.

## Phase 1: Verify existing catalog coverage
- [ ] Add a reachability assertion for every weapon definition across shops,
  drops and starting items, with an explicit legacy/exclusion list.
- [ ] Buy/equip one of each weapon type and verify model/stance and proficiency
  messages. Preserve current tier/pricing rules unless deliberately rebalanced.

## Phase 2: Fix the weapon data
- [ ] Spread the Strength of `RightHand_THS01_Sword` … `THS07_Sword` across about S8–S20. They are all S12 today.
- [ ] Change `LeftHand_OHS07_Sword` (Silver Sword (Off-hand)) from S5/D7 to S7/D0.
- [ ] Write a `Description` for every weapon definition; all are blank today.

## Phase 3: Floor-tiered drops (small code change)
- [ ] Add `MinFloor` (default 1) and `MaxFloor` (default `int.MaxValue`) to `ItemDefinition`. Existing items keep their current behaviour.
- [ ] Add `ItemManager.GetRandomDrop(int roll, int floor)`:
  - filter `ItemDefinitions` by floor band, then pick `roll % filtered.Count`
  - throw if no item fits the floor
- [ ] Keep the existing overloads, delegating with floor 1.
- [ ] Pass the current floor at all three call sites:
  - [ ] `Assets/Scripts/Dungeon/Game/Game.cs:296` (floor items)
  - [ ] `Assets/Scripts/Dungeon/Game/Interactables/DungeonProp.cs:53`
  - [ ] `Assets/Scripts/Dungeon/Actions/MovementAction.cs:526` (enemy drops)
- [ ] Append the remaining mid-tier definitions to `Common.unity` `ItemDefinitions` (don't reorder existing entries).
- [ ] Set floor bands on every weapon (starting point; tune to the dungeon's floor count):

  | Strength / Defense | Floors |
  |---|---|
  | 3–6 | 1–10 |
  | 7–10 | 6–20 |
  | 11+ | 15+ |

- [ ] Move the existing top-tier drops (Legendary Shield, Dragon Spear, …) into their bands.
- [ ] Keep consumables in every band, so their share of drops stays about where it is today (~28%).
- [ ] Note in the PR that floors generated before this change and reloaded from a save may roll different items.

## Phase 4 (optional): Bow variants
- [ ] Add an optional `WeaponModelVariant` to `EquipmentItemDefinition`.
- [ ] In `SetWeapon`, after switching on the parent (`Bows` or `Arrows`), turn on only the named child (`Bow03`, `Arrow03`) and switch the others off. With no variant set, keep today's Bow01/Arrow01 default.
- [ ] Add Bow02–05 definitions (ranged, about S6–S12) with matching arrow variants, banded like Phase 3.
- [ ] Check that `BowAndArrowStance` animates every variant (they share the rig in `BowsSkinnedMesh.fbx`).

## Tests
- [ ] **New EditMode test:** every `EquipmentItemDefinition` under `Prefabs/Dungeon/Items/Weapons` is reachable from drops, a shop, or `StartingItems`, apart from an explicit exclusion list.
- [ ] **New test:** for every floor from 1 to max, `GetRandomDrop(roll, floor)` returns an item and never returns one outside its band.
- [ ] **New test:** the same roll and floor always give the same item.
- [ ] **Existing tests:** check they still pass:
  - `HeroPrefabTests` (now covers more weapons)
  - `AuditRegressionTests`
  - `MenuSceneNavigationTests`
  - `ClassSkillSmokeTests`
  - `TownGameplayTests`
- [ ] **Runs:** EditMode and PlayMode via `Tools/harness-editmode.json` / `harness-playmode.json` through `Tools/unity-mcp.mjs`, plus `dotnet test` for `Core` if a Core type changes.

## Order
Phase 1 → 2 → 3 → 4, one commit each with tests green.
