# Level-Up Attribute Choice + Stats Screen — Plan

Goal: on every level-up the player puts one point into an **attribute** (Strength,
Intelligence or Agility). Each attribute raises a bundle of the existing stats.
The player can inspect each hero's full stats at any time (dungeon, town, overworld).

## Current behaviour (summary of the audit)

- `AddXPAction` (`Dungeon/Actions/MovementAction.cs`) adds EXP, checks
  `LevelSystem.GetLevelUps`, and queues one `LevelUpAction` per level.
- `LevelUpAction` applies `HeroClass.Growth(primary)` (class `GrowthPerLevel`, or a flat
  +2 Str / +5 HPMax) to `BaseStats`, and raises current HP/SP by the same amount.
- Only `Level` / `Experience` / `Hp` / `Sp` are saved. Stats are rebuilt as
  `template + class StartingStatBonus + growth × (level − 1)` in three places:
  `Game.cs:154-158`, `TownUtilityService.StatsFor`, `CampaignSaving.cs:69-72`.
- What drives combat today:
  - Physical damage: `Strength × (15/16)^Defense` (melee, `DamageScaling.Strength` skills).
  - **Magic damage: `(BaseDamage + PerLevel × Level)`, with no stat input**
    (`ScaledDamageAction.RollDamage`, `DamageScaling.Magic`).
  - Hit: `0.8 + HitBonus − target Evasion`, clamped to 5–100% (`CombatMath`).
  - Crit: `CritChance`, ×1.5 damage.
  - SP regen: one SP every `SPRegenAcccumlateThreshold` (500) accumulation
    (`PlayerController.cs:371`).
- The only in-game stat view is one line in `PartyMenuContext.HeroDetails`
  (HP, SP, Str, Def) plus the HUD party cards (Lv, HP, SP, Food).

## Design

### Attributes

Three attributes, **STR / INT / AGI**. They are a layer *above* the existing stats: an
attribute has no direct effect; it converts into a `StatModification`. Combat code keeps
reading `FinalStats` as it does now, so no combat code changes, apart from the new magic
stat below.

The existing `Stats.Strength` (physical attack power) keeps its name in code. In the UI
the attribute is **STR** and the stat is **Attack**, so the two don't get confused.

| Attribute | Per point                                               | Role                    |
|-----------|---------------------------------------------------------|-------------------------|
| **STR**   | +1 Attack, +3 HPMax, +1 Defense every 4 points          | Melee, frontline        |
| **INT**   | +2 max SP (MP), +1 Magic Power, −15 SP regen threshold (faster SP regen) | Casters, healers |
| **AGI**   | +1% Hit, +1% Evasion, +1% Crit, +2 max Food (HungerMax) | Archers, rogues, scouts |

- INT is the main source of max SP (MP). Class growth keeps a small SPMax amount so
  non-INT builds can still cast a few skills.
- AGI's max Food stacks with the Scout's existing `+1 HungerMax` growth and `+20` starting
  bonus, so Scouts stay the best explorers. Base HungerMax is 100, so 20 AGI points give
  +40%.

- STR = max HP + defense + physical damage (Attack).
- STR gives Defense in steps (`floor(points / 4)`) rather than per point, because Defense
  stacks multiplicatively: each point cuts incoming damage by ~6% (`(15/16)^Defense`).
  At +1 per point, 20 STR would block ~72% of every hit. With steps, 20 STR is +5 Def,
  about 28%.
- **Caps**: evasion and crit from AGI are capped (e.g. 25% / 25%) inside the conversion,
  so stacking AGI can't make a hero untouchable (base hit is only 80%).
- HP is no longer a direct pick; it comes from class growth plus STR. That keeps
  Elementalist/Occultist fragile unless the player deliberately builds STR.
- All numbers live in one table in `HeroAttributes` for tuning.

### New stat: Magic Power

INT needs something to feed into, because magic damage currently ignores stats.

- Add `MagicPower` (int) to `StartingStats` / `Stats` / `StatModification`, plus all their
  copy/sync/hash/`DescribeEffect` methods, and to the `ClassPassive` scaling (~line 295)
  and `TimedBuffStatusEffect` lists.
- Magic damage: multiply the existing formula by `1 + 0.05 × MagicPower`. Scaling it as a
  multiplier keeps every Magic skill's authored `BaseDamage`/`PerLevel` balance relative,
  rather than adding a flat number that would swamp weak skills.
  - Also apply it in `RawSceneryDamage`.
  - Also apply it in the ally AI estimates (`SkillEstimates.cs:70/94/119`), so AI choices
    still match real damage.
- Healing (`ScaledHealAction`) gets the same multiplier, so INT matters for Healer/Bard.
- Equipment and buffs can later grant Magic Power directly (staves, etc.).

### Where points come from

- Each level gained gives **class growth (trimmed, see below) + 1 attribute point**.
- **Class growth is trimmed** so total power growth stays near today's. For example,
  Warrior growth goes from `+5 HP / +3 Str` to `+3 HP / +2 Str`, and a point in STR makes
  up the rest. Do the per-class pass in step 7.
- Optional **class starting attributes** (`ClassDefinition.StartingAttributes`), e.g.
  Elementalist starts INT 3, Rogue AGI 3. This gives class identity at level 1 and replaces
  some of today's `StartingStatBonus` values (Archer's +5% hit, Rogue's +5% evasion fit
  naturally under AGI).
- `ClassDefinition.PreferredAttribute` is used by autoplay and to mark a "recommended"
  option in the dialog.

### Data

- New serializable `AttributePoints { int Str, Int, Agi; int Total; }`, kept next to
  `Level`/`Experience` on:
  - `TownAllyData` (`MainMenu/GameSaveData.cs`): the save format.
  - `TownAlly` (`Town/TownAlly.cs`): the town/overworld actor.
  - `Ally` (dungeon actor): copied in from `TownAlly` in `Game.InitializeGame`.
- **Unspent points are derived, not stored**:
  `pending = (Level − 1) − AttributePoints.Total`. That way the count can't drift out of
  sync, and old saves migrate for free: existing heroes arrive with `Level − 1` points to
  spend.
- Copy sites (each already copies `Level`/`Experience`): `Town.cs:91` and `:171`,
  `CampaignParty.cs:65`, `OverworldPartyMenuContext.RestoreHero`,
  `DungeonReturnService.cs:28`, `TownAllyManager.cs:26` (new recruits start at zero).
- Summon clones (`SummonRules`) copy the summoner's attributes but never earn points.

### Stat pipeline

```
template StartingStats
 + class StartingStatBonus
 + class growth × (level − 1)
 + HeroAttributes.ToModification(class starting attributes + spent points)   ← new
 = BaseStats
 + equipment + passives + status + song + command + class conditionals
 = FinalStats  (unchanged)
```

Add `HeroStatRules.BaseStats(template, primary, level, attributes)` in `Classes/` and use
it at all three current rebuild sites (`Game.cs`, `TownUtilityService.StatsFor`,
`CampaignSaving`). This also fixes the copied-rebuild drift from the audit.

**Attributes are computed from totals, not added up one point at a time.** The
conversion has caps and every-4-points breakpoints, so spending a point must be
"remove the old attribute modification, add the new one" (or simply rebuild
`BaseStats`). Simply adding "one point's worth" would get the breakpoints and caps wrong.

**Future option (not in v1):** let equipment and buffs grant attributes (+2 AGI boots).
That needs attributes to be collected into `FinalStats` like the other sources. The v1
design keeps attributes inside `BaseStats`, but `HeroAttributes` is written so it can
move there later.

### Dungeon flow (when the choice is shown)

The turn system runs `ExecuteImmediate` chains synchronously, so it can't stop mid-chain
for UI. So:

1. `LevelUpAction` keeps doing class growth, level text and sound. The new point simply
   becomes pending.
2. **Prompt at a safe point.** When the player is next asked for input
   (`PlayerController.StartTurn`, or whenever `CanOpenMenu()` is true and no dialog is
   open), check every party member for `pending > 0`. If one has points, open the
   **LevelUpChoiceDialog** for that hero first. With several heroes or several levels,
   show the dialogs one after another.
3. Spending a point happens outside turn resolution, so it changes real and displayed
   state directly:
   - increment the attribute;
   - swap the attribute modification on `BaseStats`;
   - raise current HP/SP by however much HPMax/SPMax went up;
   - call `SyncDisplayedStats()`.

   It costs no turn.
4. The dialog has a **Later** button. Deferred points can be spent from the Stats tab.
   The auto-prompt comes back on the next level-up, not every turn.
5. **Autoplay / tests**: `AutoplayRunner` spends points on `PreferredAttribute`
   automatically. Classless heroes default to STR.

Town and overworld: deferred or migrated points can be spent from the Stats tab. Spending
one writes `TownAllyData` and saves, the same way the overworld equip action does
(`OverworldPartyMenuContext.Actions`).

## LevelUpChoiceDialog (UI)

A `Dialog`, authored like `PartyMenuPicker` (`GameUISkin` + `AuthoredUI.Require`).

```
┌──────────────────────────────────────────────────┐
│  Aria reached Lv 6!          2 points to spend   │
│                                                  │
│  > STR  4 → 5   Attack 12→13  HP 30→33  Def 1→1  │
│    INT  2 → 3   SP 9→11  Magic 2→3  SP regen ↑   │
│    AGI  6 → 7   Hit/Eva/Crit +1%  Food 112→114   │
│                                     (recommended)│
│                     [Later]                      │
└──────────────────────────────────────────────────┘
```

- Each row previews its real stat changes, computed by running the conversion on
  "points + 1" and comparing with the current result. Breakpoints and caps therefore show
  correctly (e.g. "Def 1→2" only on the 4th STR point; "Eva 25% (max)").
- Explicit gamepad/keyboard `Navigation`, following the `PartyMenu` pattern. It opens
  through `MenuManager.Open`, so `HasDialog` blocks movement input.

## Stats screen

Add a third party-menu tab: **Stats**. It works in the dungeon, town and overworld,
because `PartyMenu` is shared by all three contexts.

- `PartyMenuTab.Stats` enum value, plus a `StatsTab` button next to Inventory/Skills
  (wire it in `Refresh`/`WireNavigation`, re-author the prefab).
- `Entries(hero, Stats)` rows, grouped into sections:
  - **Level**: Lv, EXP, EXP to next level (`LevelSystem.Progress`), unspent points.
  - **Attributes**: STR, INT, AGI.
  - **Vitals**: HP x/max, SP x/max, Food x/max.
  - **Combat**: Attack, Magic Power, Defense, Hit %, Evasion %, Crit %, Attacks/turn,
    Actions/turn.
  - **Resistances**: Fire, Ice, Lightning.
- The details panel for the selected row shows where the value comes from:
  ```
  Attack 14
    Base 5 · Class +1 · Level growth +4 · STR +5
    Equipment +3 · Passives 0 · Status −4 (Weaken)
  ```
  Selecting an attribute instead lists what it grants
  ("AGI 7 → Hit +7%, Evasion +7%, Crit +7%, Food +14").
  To support this, split `Character.UpdateCachedStats` into named parts
  (`StatBreakdown`), so the breakdown uses exactly the same math as `FinalStats`.
- If the hero has unspent points, a **Spend attribute point** row appears at the top of
  the tab and opens the LevelUpChoiceDialog. In the dungeon it is available whenever the
  menu is (it costs no turn, so it isn't limited to the controlled hero).
- Shortcut: a "Stats [T]" button in `PartyMenuLauncher` plus a gamepad binding (pick it
  together with `Docs/InputPromptsTODO.md`).
- HUD: a "▲" badge on the `DungeonPartyCard` of a hero with unspent points.

## Equipment tab

A dedicated list of what each hero is wearing, separate from the bag. From it the player
can see each item's effect on their stats, and equip or unequip.

### Today

- The Inventory tab mixes an "Equipped" section and the "Shared bag" section in one list
  (`PartyMenuContext.Entries`). The details panel shows only the item's own modifiers
  (`DescribeEffect`), not the effect on the hero.
- The old `InventoryMenu.UpdateItemPreview` compares Strength/Defense only, and it skips
  passives, status effects and class conditionals.
- Slots (`Equipment.cs`): **Weapon** (`MainHand` or `TwoHand`, both stored in
  `EquippedWeapon`), **Off-hand** (`EquippedShield`), **Accessory**. Equipping a two-hander
  removes the off-hand; equipping an off-hand removes a two-hander.
- Class passives depend on gear: `SupportPassives` `StatCondition.ShieldEquipped` /
  `BowEquipped`. So an item's real effect is not just its own `StatModification`.

### Layout

Tabs become **Inventory · Equipment · Skills · Stats**. Inventory now lists only the
shared bag.

```
┌ Equipment ───────────────────────┬────────────────────────────────────┐
│  Weapon     Iron Sword           │ Iron Sword  (Main hand)            │
│             Attack +4            │ A plain, reliable blade.           │
│> Off-hand   Buckler              │                                    │
│             Def +2, Eva +3%      │ Without it:  Attack 15 → 11        │
│  Accessory  — empty —            │ Classes: Warrior, Guardian, Rogue  │
│                                  │                                    │
│  Totals from gear:               │ [A] Change   [Y] Unequip           │
│  Attack +4  Def +2  Eva +3%      │                                    │
└──────────────────────────────────┴────────────────────────────────────┘
```

- **One row per slot, always shown**, empty slots included, so it's clear what can still
  be filled. A two-hander shows in Weapon, with the Off-hand row reading
  "— blocked by Greatsword —".
- Each row shows the item's own modifiers inline (`DescribeEffect`, joined).
- The details panel shows the hero's **real** stat change if the item were removed.
  This uses the simulation below, so passive bonuses (e.g. Guardian's shield bonus) show up.
- A footer row sums all gear modifiers. It matches the "Equipment" line in the Stats tab
  breakdown.

### Changing a slot

Choosing **Change** opens a picker with:

1. **Unequip** (if the slot is filled).
2. Every bag item that fits the slot. Weapon lists `MainHand` + `TwoHand` items;
   Off-hand lists `OffHand`; Accessory lists `Accessory`.
3. Items the hero's classes can't use are listed last, greyed out, with the reason
   (`HeroClass.AllowsItem`). This shows the item exists and explains why it can't be used.

Each option previews the full stat change, showing only the stats that change:

```
  Unequip                 Def 5→3  Eva 3%→0%
> Tower Shield            Def 5→8  Eva 3%→0%  Attack 15→15
  Greatsword (2H)         Attack 15→21  Def 5→3  ⚠ removes Buckler
  Spiked Shield (Guardian only)
```

- `PartyMenuPicker` gets an optional description per option. The diff is shown in a side
  text area when the option is focused, the same way `PartyMenu` uses `Details`.
- Warnings come from comparing slots before and after
  (`⚠ removes Buckler` / `⚠ removes Greatsword`).

### Same stat preview everywhere

Add `StatPreview` (in `Dungeon/Game/`), built on the `StatBreakdown` refactor from the
Stats tab:

```csharp
// The gear a hero would have after a change, without touching the real Equipment.
public readonly struct Loadout { public EquipableInventoryItem Weapon, OffHand, Accessory; }
Loadout Equipment.Current();
Loadout Equipment.With(EquipableInventoryItem item);          // reuses ApplyEquipChange
Loadout Equipment.Without(EquipmentSlot slot);

// FinalStats if this loadout were worn: base + loadout + passives + status + song + command
// + ClassPassives.ConditionalStats(owner, loadout)
Stats StatBreakdown.Final(Character c, Loadout loadout);
List<StatDelta> StatPreview.Diff(Stats before, Stats after);  // only changed stats, formatted
```

- `ClassPassives.ConditionalStats` / `ArrowSupply.HasBow` get an overload that takes a
  `Loadout`, so shield/bow conditions are judged on the simulated gear.
- `Equipment.GetStatsIfEquipped` becomes a thin wrapper over `With(item)`.
- Town/overworld heroes use `TownUtilityService.StatsFor` with a loadout parameter.
- The diff is used by the Equipment tab, the picker, the Inventory tab details for bag
  equipment, and the level-up dialog's attribute preview. The old Str/Def-only preview in
  `InventoryMenu` is replaced.

### Rules per context (unchanged behaviour)

| Context   | Equip / unequip cost | Who can change gear                                     |
|-----------|----------------------|---------------------------------------------------------|
| Dungeon   | Uses the hero's action (existing `EquipAction`/`UnEquipAction` via `EquipEffectDefinition`) | Only the controlled hero awaiting input (`DungeonPartyMenuContext.Eligibility`); others can be viewed |
| Town      | Free, saves immediately (`TownServices.ToggleEquipment`) | Anyone |
| Overworld | Free, saves immediately (`EquipmentTransferService.Toggle`) | Anyone |

In the dungeon the picker states the cost ("Uses Aria's turn"). Choosing an item shuts the
menu and queues the action, the same as the Inventory tab's Equip does today.

### Code touchpoints

- `PartyMenuTab.Equipment`, an `EquipmentTab` button, navigation wiring in `PartyMenu`;
  prefab re-author.
- `PartyMenuEntry.Slot` (nullable `EquipmentSlot`) for slot rows; `Entries(hero, Equipment)`
  in `PartyMenuContext`. `Entries(hero, Inventory)` drops the Equipped section.
- `Actions(hero, slotEntry)` in each of the three contexts returns "Change" and "Unequip".
  These delegate to the existing equip paths, so the class filter and saving behave the
  same.
- Launcher: an "Equipment [E]" button plus a gamepad binding (alongside
  `Docs/InputPromptsTODO.md`).

## Prerequisites (bugs from the audit)

1. **Fix `AddXPAction`**: check level/EXP on `this.character`, not the acting character,
   and give `LevelUpAction` an explicit target. Otherwise the point (and growth) can go to
   the wrong hero.
2. Optional: credit DoT/burn/curse kills to the status source, so leveling pace is even
   across classes. This matters more now, because INT casters lean on DoTs.

## Implementation order

1. Audit fix for `AddXPAction`/`LevelUpAction` targeting (+ EditMode test).
2. `MagicPower` stat end-to-end (the stat classes, magic damage, heals, AI estimates).
   Tests: zero Magic Power gives identical damage to today.
3. `AttributePoints`, `HeroAttributes` conversion, `HeroStatRules`; replace the three
   rebuild sites; save fields and copy sites.
   Tests: zero points matches the current formula; breakpoints/caps; pending derivation;
   an old save gives `Level − 1` pending.
4. Spend-point service (dungeon `Ally`, town `TownAlly`/save) + tests.
5. LevelUpChoiceDialog + safe-point prompt in `PlayerController`; autoplay auto-spend.
6. Stats tab + `StatBreakdown` + spend row; launcher shortcut; HUD badge.
7. Equipment tab: `Loadout` + `StatPreview` (+ tests: the simulated loadout equals the
   real `FinalStats` after equipping; a shield-conditional passive shows in the preview;
   two-hander/off-hand displacement warnings). Then the slot rows, the change picker with
   per-option diff, Inventory tab trimmed to the bag, and the old `InventoryMenu` preview
   replaced.
8. Balance pass: trim class growth, set class starting attributes and preferred attribute
   per class, update `Docs/Classes.md`.

## Open questions

- Per-point values and caps are a first guess. Should AGI also give something durable
  (e.g. +1 Attack every 3 points for bow users), so Archers/Rogues don't fall behind?
- Should equipment/buffs grant attributes in v1, or later (see the future option above)?
- Respec: a paid town service that zeroes `AttributePoints` is trivial with derived
  pending points. Do you want one?
- Should enemies get attributes (as an authoring shortcut for monster stats), or keep
  their raw stats from `MonsterData`?
