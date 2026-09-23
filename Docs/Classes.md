# Classes and skills (design)

Status: **proposal**. Nothing in this document is implemented yet. It defines ten
companion classes of 20 ranked skills each, written for the existing
Mystery Dungeon style grid combat, and gives a phased
[implementation plan](#implementation-plan) for the systems they need.

Decisions so far:

- Each hero has a **fixed** class, or a fixed combination of two classes.
- Heroes can't change class.
- Skills have **ranks**.
- The protagonist can be **any** class.
- Archer skills that fire arrows **use arrows**.
- **No save migration.** The game is still in development, so older saves
  aren't supported.
- **Dominate can't target bosses.**
- **The party fights on until everyone is downed.** A downed protagonist doesn't
  end the run.

## Where this fits today

- Heroes (`TownAlly` / `Ally`) have no class. Every hero levels identically
  (+2 Strength, +5 HPMax per level) and any hero can learn any skill at the
  trainer for gold (`TownServices.Learn`).
- Skills are `Skill` ScriptableObjects: `ActivationType` Active/Passive,
  `SPCost`, `TargetSelector` (team + area), `SkillTargeting`
  (SelectedTarget / Self / AllTargets / InventoryItem / Missile),
  `AreaRadius`, `MissileRange`, and a list of `GameAction` effects. There are no
  cooldowns and no level requirements. SP is the only cost.
- Status effects that already exist: Dot, Hot, Frailty, Silence, Sleep,
  Strength, Stuck. There are no elements, resistances, critical hits, evasion,
  threat, stealth, or gathering.
- Party is 4 (protagonist + 3). Spec §9.1 reserves room for "three preferred
  fighters plus one specialist". Classes give that phrase a concrete meaning.

Classes are a **combat identity** layer. They are separate from the spec's
*capabilities* (Climb, Grapple, Remedy, ...), which open world locks. A class
skill must never satisfy a capability lock. For example, the Scout's Grapple Line
moves the Scout within a room but is not the `Grapple` capability.

## The ten classes

| Class | Role | Weapons | Growth lean |
|---|---|---|---|
| Warrior | Front-line physical attacker | 1H/2H swords, axes, hammers | High Str, mid HP |
| Guardian | Tank / defender | 1H weapon + shield, spears | High HP/Def, low Str |
| Archer | Ranged damage dealer | Bows + arrows | High Str, low Def |
| Elementalist | Elemental mage | Wands | High SP, low HP/Def |
| Healer | Healing and support | Wands, sticks | High SP, mid Def |
| Bard | Buff and support fighter | 1H swords, needles | Balanced, mid SP |
| Occultist | Debuff and status specialist | Wands, needles | High SP, low Str |
| Rogue | Mobility and disruption | 1H swords, needles | Mid Str, high evasion |
| Commander | Party-wide buffs and leadership | 1H weapon + shield | Balanced, high SP |
| Scout | Utility and exploration | Bows, 1H swords | Balanced, low hunger drain |

Weapon lists reuse the existing weapon types. "Growth lean" replaces the flat
per-level gain with per-class growth, keeping the same total budget per level.

## Class assignment

- **Recruitable heroes** have a fixed class, set on the hero's `TownAlly` prefab.
  Some heroes instead have a fixed **combination**: a primary class plus a
  secondary class.
  - Across the 24 hero prefabs, aim for every class appearing as a primary at
    least twice, with about 4 heroes being combinations.
  - The recruit pool rotates between seeds, so spread each class across the
    catalog. That way any seed's local recruits cover a spread of roles.
- **The protagonist** chooses a class during new-game setup. Any of the ten
  classes can be picked as primary, and optionally any other class as secondary.
  The choice is stored in the save and can't be changed later.
- **No class changes.** Nothing in town or the dungeon changes a hero's class,
  and learned skills and ranks are permanent.

### Combination rules

- The **primary** class sets growth, Novice/Adept/Master Training, and full
  access to all three tiers and all ranks.
- The **secondary** class adds its tier 1 and tier 2 skills. Its mastery passives
  aren't learnable; secondary tiers unlock on the primary's mastery.
  - Secondary skills are capped at rank 3.
  - Tier 3 of the secondary is never available.
- **Weapons:** the hero can use the union of both classes' weapon types.
- **Gathering:** the hero can learn both classes' gathering passives.
- **Duplicates:** a skill that exists in both classes (for example HP Up) is
  learned once, using the primary's cap.

A combination hero is broader but less deep than a single-class hero. That
trade-off stops combinations from simply being better.

## Common skill structure

Every class has:

- **Three mastery passives:** Novice Training → Adept Training → Master Training.
  Each one unlocks the next skill tier and gives a small bonus with the class
  weapons.
  - **Novice Training** is known from level 1 and unlocks tier 1.
  - **Adept Training** needs level 10 and 3 tier 1 skills. It unlocks tier 2.
  - **Master Training** needs level 20 and 3 tier 2 skills. It unlocks tier 3.
- **One gathering passive:** Mining, Harvesting or Foraging. See
  [Gathering](#gathering).
- **Stat passives:** the existing HP Up / Defense Up / Strength Up / Sp Up
  style, reused across classes where they fit.
- **Class actives:** everything else.

Skills stay gold-bought at the trainer. The trainer only offers a hero skills from
their class(es), and only when that hero meets the tier requirement.

### Skill ranks

- **Ranks 1–5.** Mastery, gathering and unlock-style passives (such as Harmony or
  Twin Shadows) have a single rank. Secondary-class skills stop at rank 3 (see
  [Combination rules](#combination-rules)).
- **Learning:** buying a skill gives rank 1. Each further purchase at the trainer
  raises it by one rank and costs `LearnCost × new rank` gold.
- **Level gate:** rank *n* needs hero level ≥ tier unlock level + 3 × (*n* − 1).
  - Tier unlock levels are 1, 10 and 20.
  - With the current cap of about 37, a tier 3 skill reaches rank 5 at level 32.
- **The tables show rank 1.** Default scaling for higher ranks:

  | Skill kind | Per extra rank |
  |---|---|
  | Damage, healing | +15% |
  | Status or ailment chance | +5 percentage points |
  | Buff or debuff size, stat passives | +1 step (e.g. +2 Strength → +3, +4, ...) |
  | Duration | +1 turn at ranks 3 and 5 |
  | SP cost | Unchanged |

  A skill can override any of these with its own per-rank values. Tier
  prerequisites count skills at any rank.
- **Save format:** learned skills are saved as name + rank.

### Table conventions

- **Tier** is 1, 2 or 3. **M** marks a mastery passive and **G** a gathering passive.
- **SP** is a relative cost (Low / Med / High), mapped to numbers during
  balancing. Current SP pools are small (the Sp Up passive gives +2).
- **Target** uses existing targeting terms:
  - *Self*.
  - *Melee*: an adjacent tile.
  - *Missile*: a line in one of 8 directions that stops at the first character.
  - *Radius N*: Chebyshev distance around the target or the caster.
  - *Visible*: everything in view.
  - *Party* means allies within the given area.

---

## Warrior

Hits hard up close. Its signature mechanic is the **elemental follow-up**. A
follow-up strike marks the target for 3 turns, and every hit any party member
lands on a marked target adds a bonus elemental hit from the Warrior.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. +5% damage with swords/axes/hammers. |
| 2 | Mining | 1 G | Passive | Self | Can harvest ore points. |
| 3 | Double Strike | 1 | Active / Low | Melee | Two hits at 60% each. |
| 4 | Vanguard | 1 | Passive | Self | +3 Strength, −1 Defense. |
| 5 | Power Boost | 1 | Passive | Self | +2 Strength (existing Strength Up). |
| 6 | Iron Skin | 1 | Passive | Self | +2 Defense. |
| 7 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. +5% weapon damage. |
| 8 | Cleave | 2 | Active / Med | Radius 1 (self) | Strikes every adjacent enemy at 80%. |
| 9 | Piercing Thrust | 2 | Active / Med | Missile, range 2 | Hits the first two enemies in a line. |
| 10 | Initiative | 2 | Passive | Self | +1 action on the first turn of each floor. |
| 11 | Sunder | 2 | Active / Low | Melee | Damage + Weaken (−Strength) for 5 turns. |
| 12 | Rattle | 2 | Active / Low | Melee | Damage + Silence for 3 turns. |
| 13 | Flame Follow-up | 2 | Active / Med | Melee | Hit + Fire mark. |
| 14 | Frost Follow-up | 2 | Active / Med | Melee | Hit + Ice mark. |
| 15 | Shock Follow-up | 2 | Active / Med | Melee | Hit + Lightning mark. |
| 16 | Master Training | 3 M | Passive | Self | Unlocks tier 3. +5% weapon damage. |
| 17 | Whirlwind | 3 | Active / High | Radius 2 (self) | 4–6 hits on random enemies in range. |
| 18 | Lunge | 3 | Active / Med | Missile, range 3 | Dash to the first enemy in a line and strike at 150%. |
| 19 | Improved Follow-ups | 3 | Passive | Self | Marks last 5 turns. Follow-up hits deal +25%. |
| 20 | Follow-up Mastery | 3 | Passive | Self | A follow-up hit can trigger a second follow-up (at most 1 extra per hit). |

## Guardian

Keeps the party alive by drawing attacks, redirecting damage and raising
barriers. Its signature mechanics are **Taunt** and **elemental barriers**. A
barrier lasts until the Guardian's next turn and cuts party damage of its
element by 75%.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. +1 Defense with a shield equipped. |
| 2 | Mining | 1 G | Passive | Self | Can harvest ore points. |
| 3 | HP Up | 1 | Passive | Self | +10 HPMax (existing asset). |
| 4 | Provoke | 1 | Active / Low | Radius 2 (self) | Taunts enemies for 3 turns. They must target the Guardian if able. |
| 5 | Shield Smite | 1 | Active / Low | Melee | Damage scaled by the shield's Defense. Needs a shield. |
| 6 | Defense Boost | 1 | Passive | Self | +2 Defense (existing Defense Up). |
| 7 | Rally | 1 | Active / Med | Radius 1 (self), Party | Removes ailments and binds from the Guardian and adjacent allies. |
| 8 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. +1 Defense with a shield. |
| 9 | HP Regen | 2 | Passive | Self | Faster HP regeneration (lower regen threshold). |
| 10 | Last Stand | 2 | Passive | Self | +4 Defense while below 25% HP. |
| 11 | Cover | 2 | Passive | Self | 30% chance to take an attack aimed at an adjacent ally. |
| 12 | Shield Bash | 2 | Active / Low | Melee | Damage + Silence (existing asset). |
| 13 | Flame Barrier | 2 | Active / Med | Visible, Party | Fire barrier. |
| 14 | Frost Barrier | 2 | Active / Med | Visible, Party | Ice barrier. |
| 15 | Storm Barrier | 2 | Active / Med | Visible, Party | Lightning barrier. |
| 16 | Master Training | 3 M | Passive | Self | Unlocks tier 3. +1 Defense with a shield. |
| 17 | Parry Stance | 3 | Active / Med | Self | Taunts within radius 2 and blocks 50% of melee hits for 2 turns. |
| 18 | Bulwark | 3 | Active / High | Visible, Party | All damage to the party −50% until the Guardian's next turn. |
| 19 | Sanctuary Wall | 3 | Active / High | Visible, Party | Absorbs the next 30 total damage to the party within 3 turns. |
| 20 | Aegis | 3 | Passive | Self | Barriers last 1 extra turn and block their element's ailment. |

## Archer

Fights from range with bows. Its signature mechanic is **pinning arrows**, the
three binds (see [Binds](#binds)).

**Arrow use.** Every Archer skill that fires an arrow uses arrows from the
equipped arrow stack, the same as a normal ranged attack:

- **One arrow:** Aimed Shot, Pinning / Disarming / Stunning Shot, Volley, Venom
  Arrow, Sleep Arrow, Retreat Shot and Piercing Arrow.
- **Multishot:** 3 arrows. It fires fewer shots if fewer arrows are left.
- **Rain of Arrows:** one arrow per enemy hit. If the stack runs out, the rest of
  the enemies are skipped.
- **Double Shot:** its second shot uses a second arrow, and doesn't fire without one.
- **Arrow Recovery** rolls separately for each arrow.
- Arrow skills can't be cast unless a bow and at least one arrow are equipped.
  The skill menu shows the arrow cost next to the SP cost.
- Passives, and non-arrow skills in combination kits, never use arrows.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. +5% bow damage. |
| 2 | Foraging | 1 G | Passive | Self | Can harvest forage points. |
| 3 | Aimed Shot | 1 | Active / Low | Missile | 150% damage that always hits. |
| 4 | Pinning Shot | 1 | Active / Low | Missile | Damage + Leg bind (existing Stuck) for 3 turns. |
| 5 | Disarming Shot | 1 | Active / Low | Missile | Damage + Arm bind for 3 turns. |
| 6 | Stunning Shot | 1 | Active / Low | Missile | Damage + Head bind (existing Silence) for 3 turns. |
| 7 | Keen Eye | 1 | Passive | Self | +10% hit chance (base is 80%). |
| 8 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. +5% bow damage. |
| 9 | Longshot | 2 | Passive | Self | +3 missile range. |
| 10 | Volley | 2 | Active / Med | Missile, then Radius 1 | Hits the target and every enemy adjacent to it. |
| 11 | Multishot | 2 | Active / Med | Visible | 3 arrows at random visible enemies. |
| 12 | Venom Arrow | 2 | Active / Low | Missile | Damage + Poison (existing Dot). |
| 13 | Sleep Arrow | 2 | Active / Med | Missile | Damage + Sleep. |
| 14 | Retreat Shot | 2 | Active / Low | Missile | Shoot, then step one tile away from the target. |
| 15 | Deadeye | 2 | Passive | Self | +15% critical chance with bows. |
| 16 | Master Training | 3 M | Passive | Self | Unlocks tier 3. +5% bow damage. |
| 17 | Rain of Arrows | 3 | Active / High | Visible | Hits every visible enemy at 70%. |
| 18 | Piercing Arrow | 3 | Active / Med | Missile | Passes through every enemy in the line and stops only at walls. |
| 19 | Double Shot | 3 | Passive | Self | 25% chance for a normal ranged attack to fire twice. |
| 20 | Arrow Recovery | 3 | Passive | Self | 30% chance a shot does not use an arrow. |

## Elementalist

The party's elemental damage source. Each element has three versions: a
single-target bolt, a radius-1 burst, and a whole-screen spell. **Expose**
lowers a target's resistances, which pairs with the Warrior's follow-ups and the
Commander's Arms commands.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. +5% elemental damage. |
| 2 | Harvesting | 1 G | Passive | Self | Can harvest plant points. |
| 3 | Fire Bolt | 1 | Active / Low | Missile | Fire damage. |
| 4 | Ice Bolt | 1 | Active / Low | Missile | Ice damage. |
| 5 | Lightning Bolt | 1 | Active / Low | Missile | Lightning damage. |
| 6 | SP Up | 1 | Passive | Self | +2 SPMax (existing asset). |
| 7 | Focus | 1 | Passive | Self | +10% elemental damage. |
| 8 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. +5% elemental damage. |
| 9 | Fireball | 2 | Active / Med | Missile, then Radius 1 | Fire damage around the impact point. |
| 10 | Blizzard | 2 | Active / Med | Missile, then Radius 1 | Ice damage around the impact point. |
| 11 | Thunderclap | 2 | Active / Med | Missile, then Radius 1 | Lightning damage around the impact point. |
| 12 | Expose | 2 | Active / Low | Missile | Target's elemental resistances −1 step for 5 turns. |
| 13 | Amplify | 2 | Active / Low | Self | The next elemental spell deals +50%. |
| 14 | Mana Flow | 2 | Passive | Self | Faster SP regeneration. |
| 15 | Master Training | 3 M | Passive | Self | Unlocks tier 3. +5% elemental damage. |
| 16 | Inferno | 3 | Active / High | Visible | Fire damage to every visible enemy. |
| 17 | Glacier | 3 | Active / High | Visible | Ice damage to every visible enemy. |
| 18 | Tempest | 3 | Active / High | Visible | Lightning damage to every visible enemy. |
| 19 | Elemental Mastery | 3 | Passive | Self | Elemental hits can inflict Burn (fire), Stuck (ice) or Paralysis (lightning). |
| 20 | Spell Echo | 3 | Passive | Self | 20% chance for a bolt to cast twice. |

## Healer

Heals, cures and revives. It has the only revive in the game, so it needs a
downed-ally state (see [Phase 4](#phase-4-advanced-mechanics)).

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. +10% healing. |
| 2 | Harvesting | 1 G | Passive | Self | Can harvest plant points. |
| 3 | Heal | 1 | Active / Low | Selected ally | Restores HP (existing Healing). |
| 4 | Healing Touch | 1 | Passive | Self | +15% healing. |
| 5 | Cure | 1 | Active / Low | Selected ally | Removes ailments. |
| 6 | Unbind | 1 | Active / Low | Selected ally | Removes binds. |
| 7 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. +10% healing. |
| 8 | Group Heal | 2 | Active / Med | Radius 2 (self), Party | Restores HP to nearby allies. |
| 9 | Revive | 2 | Active / High | Adjacent downed ally | Revives the ally at 25% HP. |
| 10 | Renew | 2 | Active / Low | Selected ally | Regeneration over time (existing Hot). |
| 11 | Triage | 2 | Passive | Self | +50% healing on allies below 30% HP. |
| 12 | Field Medicine | 2 | Passive | Self | The party recovers 10% HP when taking stairs. |
| 13 | Heavy Strike | 2 | Active / Low | Melee | Weapon hit with a chance to Stun (lose next action). |
| 14 | Master Training | 3 M | Passive | Self | Unlocks tier 3. +10% healing. |
| 15 | Full Heal | 3 | Active / High | Visible, Party | Large heal to every visible ally. |
| 16 | Mass Cure | 3 | Active / Med | Visible, Party | Removes ailments and binds from every visible ally. |
| 17 | Second Wind | 3 | Passive | Self | Once per floor, automatically heals an ally who drops below 10% HP. |
| 18 | Mass Revive | 3 | Active / High | Visible, downed allies | Revives every visible downed ally at 25% HP. |
| 19 | Meditation | 3 | Passive | Self | Faster SP regeneration. |
| 20 | Vitality | 3 | Passive | Self | +10 HPMax. |

## Bard

A support fighter whose buffs are **songs**. A song affects allies within radius
3 of the Bard for 5 turns, and a Bard can keep up to 2 songs at once (3 with
Harmony). Starting a new song replaces the oldest one.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. Songs +1 turn. |
| 2 | Foraging | 1 G | Passive | Self | Can harvest forage points. |
| 3 | Battle Hymn | 1 | Active / Low | Song | +Strength. |
| 4 | Ballad of Stone | 1 | Active / Low | Song | +Defense. |
| 5 | Soothing Melody | 1 | Active / Low | Song | Small HP regeneration each turn. |
| 6 | Nimble | 1 | Passive | Self | +10% evasion. |
| 7 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. Songs +1 turn. |
| 8 | Evasive Rhythm | 2 | Active / Med | Song | Enemy hit chance against allies −20%. |
| 9 | Quickstep | 2 | Active / High | Song, 3 turns | +1 action per turn. |
| 10 | Encore | 2 | Active / Low | Self | Extends all active songs by 3 turns. |
| 11 | Blade Dance | 2 | Active / Med | Melee | One hit per active song, at 70% each. |
| 12 | Discord | 2 | Active / Med | Radius 2 (self) | Removes buffs (e.g. Strength) from enemies. |
| 13 | War Drums | 2 | Active / Med | Radius 3 (self) | Enemies −Defense for 5 turns. |
| 14 | Master Training | 3 M | Passive | Self | Unlocks tier 3. Songs +1 turn. |
| 15 | Grand Finale | 3 | Active / High | Visible | Ends all songs. Heals the party and damages enemies, scaled by songs ended. |
| 16 | Crescendo | 3 | Passive | Self | Song effects +50%. |
| 17 | Harmony | 3 | Passive | Self | Can keep 3 songs at once. |
| 18 | Rousing Chorus | 3 | Active / High | Radius 3 (self), Party | Restores SP to allies (not the Bard). |
| 19 | Stage Presence | 3 | Passive | Self | +2 Strength, +2 Defense while a song is active. |
| 20 | Vigor | 3 | Passive | Self | +10 HPMax. |

## Occultist

Disables and weakens enemies with **hexes** (ailments and debuffs), then cashes
them in with damage that scales with the number of ailments.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. +10% ailment chance. |
| 2 | Harvesting | 1 G | Passive | Self | Can harvest plant points. |
| 3 | Venom Hex | 1 | Active / Low | Missile | Poison (existing Dot). |
| 4 | Paralysis Hex | 1 | Active / Low | Missile | Paralysis for 4 turns. |
| 5 | Slumber Hex | 1 | Active / Low | Missile | Sleep. |
| 6 | Enfeeble | 1 | Active / Low | Missile | Weaken (−Strength) for 5 turns. |
| 7 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. +10% ailment chance. |
| 8 | Curse | 2 | Active / Med | Missile | For 4 turns, the target takes 50% of the damage it deals. |
| 9 | Terror | 2 | Active / Med | Missile | Fear: the target flees and cannot attack for 3 turns. |
| 10 | Silence Hex | 2 | Active / Low | Missile | Silence. |
| 11 | Malice | 2 | Passive | Self | +15% ailment chance. |
| 12 | Lingering Hex | 2 | Passive | Self | Ailments last +2 turns. |
| 13 | Blinding Mist | 2 | Active / Med | Missile, then Radius 1 | Blind: hit chance −30% for 4 turns. |
| 14 | Exploit | 2 | Active / Med | Missile | Damage ×1.5 for each ailment on the target. |
| 15 | Master Training | 3 M | Passive | Self | Unlocks tier 3. +10% ailment chance. |
| 16 | Plague | 3 | Active / High | Radius 2 (target) | Copies the target's ailments to nearby enemies. |
| 17 | Dominate | 3 | Active / High | Missile, Feared non-boss target | The target fights for the party for 3 turns. |
| 18 | Siphon | 3 | Active / Med | Missile | Damage. Heals the caster per ailment on the target. |
| 19 | SP Up | 3 | Passive | Self | +2 SPMax. |
| 20 | Grim Harvest | 3 | Passive | Self | Restores SP when an enemy with an ailment dies. |

## Rogue

A mobile skirmisher who moves around the board (teleport strikes, stealth,
clones and player-placed traps) instead of trading hits.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. +5% evasion. |
| 2 | Foraging | 1 G | Passive | Self | Can harvest forage points. |
| 3 | Shadow Step | 1 | Active / Low | Enemy within 4 | Teleports behind the target and strikes. |
| 4 | Smoke Bomb | 1 | Active / Low | Radius 1 (self) | Blinds adjacent enemies for 3 turns. |
| 5 | Evasion | 1 | Passive | Self | +10% evasion. |
| 6 | Hamstring | 1 | Active / Low | Melee | Damage + Leg bind (Stuck). |
| 7 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. +5% evasion. |
| 8 | Swiftness | 2 | Passive | Self | 15% chance of an extra action each turn. |
| 9 | Clone | 2 | Active / High | Adjacent tile | Summons a clone with 50% stats for 10 turns. Enemies may target it. |
| 10 | Vanish | 2 | Active / Med | Self | Stealth for 5 turns or until the Rogue attacks. Enemies ignore the Rogue. |
| 11 | Ambush | 2 | Passive | Self | Attacks from stealth deal double damage. |
| 12 | Caltrops | 2 | Active / Low | Adjacent tile | Places a visible trap that Sticks the enemy that steps on it. |
| 13 | Ember Scroll | 2 | Active / Med | Radius 1 (self) | Fire damage to adjacent enemies. |
| 14 | Throat Strike | 2 | Active / Low | Melee | Damage + Head bind (Silence). |
| 15 | Master Training | 3 M | Passive | Self | Unlocks tier 3. +5% evasion. |
| 16 | Shadow Dance | 3 | Active / High | Radius 2 (self) | Teleports to and strikes each enemy in range once. |
| 17 | Twin Shadows | 3 | Passive | Self | Up to 2 clones at once. |
| 18 | Assassinate | 3 | Active / Med | Melee | Kills a non-boss enemy below 20% HP. Otherwise a normal hit. |
| 19 | Escape Artist | 3 | Passive | Self | Immune to Leg bind. Attacks from stealth do not end it on a kill. |
| 20 | Blinding Flash | 3 | Active / Med | Radius 2 (self) | Blinds enemies in range. |

## Commander

The party's leader. **Commands** are party-wide buffs that affect every visible
ally, and **Reinforce** turns expiring commands into healing.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. Commands +1 turn. |
| 2 | Mining | 1 G | Passive | Self | Can harvest ore points. |
| 3 | Command: Attack | 1 | Active / Med | Visible, Party | +Strength for 5 turns. |
| 4 | Command: Guard | 1 | Active / Med | Visible, Party | +Defense for 5 turns. |
| 5 | Blazing Arms | 1 | Active / Med | Visible, Party | Allies' attacks deal bonus fire damage for 5 turns. |
| 6 | Frost Arms | 1 | Active / Med | Visible, Party | Allies' attacks deal bonus ice damage for 5 turns. |
| 7 | Storm Arms | 1 | Active / Med | Visible, Party | Allies' attacks deal bonus lightning damage for 5 turns. |
| 8 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. Commands +1 turn. |
| 9 | Reinforce | 2 | Passive | Self | When a command ends, affected allies recover 10% HP. |
| 10 | Rally Cry | 2 | Active / Med | Visible, Party | Removes ailments from allies. |
| 11 | Inspire | 2 | Active / Low | Selected ally | Restores SP to one ally. |
| 12 | Command: Endure | 2 | Active / High | Visible, Party | For 3 turns, the first lethal hit on each ally leaves them at 1 HP. |
| 13 | Aura of Command | 2 | Passive | Self | Allies within radius 2 regenerate SP faster. |
| 14 | Command: Advance | 2 | Active / High | Visible, Party | Allies get +1 action next turn. |
| 15 | Master Training | 3 M | Passive | Self | Unlocks tier 3. Commands +1 turn. |
| 16 | Decisive Order | 3 | Active / High | Visible, Party | Doubles active command effects for 2 turns, then ends them (which triggers Reinforce). |
| 17 | Leadership | 3 | Passive | Self | Commands last +3 turns. |
| 18 | Command: Hold | 3 | Active / Med | Visible, Party | Allies regenerate HP each turn for 5 turns. |
| 19 | Royal Bearing | 3 | Passive | Self | +2 Strength, +2 Defense. |
| 20 | Vigor | 3 | Passive | Self | +10 HPMax. |

## Scout

The exploration specialist. It deals with traps, hunger, stealth, items and
floor knowledge. Its combat skills are modest by design, and it is the natural
"one specialist" of spec §9.1.

| # | Skill | Tier | Type / SP | Target | Effect |
|---|---|---|---|---|---|
| 1 | Novice Training | 1 M | Passive | Self | Unlocks tier 1. Hunger drains 10% slower. |
| 2 | Survey | 1 G | Passive | Self | Can harvest any gathering point type. |
| 3 | Trap Sense | 1 | Passive | Self | Reveals traps within radius 2. |
| 4 | Disarm | 1 | Active / Low | Adjacent trap | Removes a trap. 50% chance to recover it as an item. |
| 5 | Pathfinder | 1 | Passive | Self | +10 HungerMax (existing Hunger Up). |
| 6 | Floor Sense | 1 | Active / Med | Floor | Reveals the floor layout and stairs. |
| 7 | Adept Training | 2 M | Passive | Self | Unlocks tier 2. Hunger drains 10% slower. |
| 8 | Soft Step | 2 | Passive | Self | Sleeping enemies do not wake when the party passes. |
| 9 | Resourceful | 2 | Passive | Self | Consumables the Scout uses are 50% stronger. |
| 10 | Throwing Arm | 2 | Passive | Self | Thrown items +50% damage and +2 range. |
| 11 | Retreat | 2 | Active / High | Self | Returns the party to town under victory rules. Not usable on boss floors. |
| 12 | Take Point | 2 | Passive | Self | The party acts first when entering a room with awake enemies. |
| 13 | Safe Passage | 2 | Active / Med | Visible, Party | Party stealth for 5 turns or until anyone attacks. |
| 14 | Master Training | 3 M | Passive | Self | Unlocks tier 3. Hunger drains 10% slower. |
| 15 | Grapple Line | 3 | Active / Med | Tile within 5 | Pulls the Scout to a tile in line of sight. Does not satisfy the `Grapple` capability. |
| 16 | Treasure Hunter | 3 | Passive | Self | +DropRate. Reveals treasure tiles on the floor. |
| 17 | Scavenger | 3 | Passive | Self | 20% chance for gathering to yield double. |
| 18 | Field Kitchen | 3 | Active / Med | Visible, Party | Uses one food item to restore hunger to the whole party. |
| 19 | Farsight | 3 | Active / Med | Floor | Reveals every enemy on the floor for 10 turns. |
| 20 | Vigor | 3 | Passive | Self | +10 HPMax. |

---

## Shared rules

### Elements

Fire, Ice and Lightning are new damage types alongside plain physical damage.
Each character gets a resistance per element, on a scale of Weak (×1.5) /
Normal / Resist (×0.5) / Immune. Element sources:

- Warrior follow-ups
- Elementalist spells
- Commander Arms
- Rogue Ember Scroll
- Guardian barriers (defence only)

Expose lowers resistance one step. Enemy definitions need resistance fields so
that generated floors can favour or counter particular elements.

### Binds

- **Leg bind:** can't move. This is the existing `Stuck`.
- **Arm bind:** can't make normal attacks or use weapon skills. This is new.
- **Head bind:** can't use SP skills. This is the existing `Silence`.

Binds are removed by Unbind, Rally and Mass Cure, not by Cure.

### Ailments

The existing ones are Poison (= `Dot`) and Sleep.

New ones:
- **Paralysis:** 50% chance to lose each action.
- **Curse:** the target takes back part of the damage it deals.
- **Fear:** the target flees and cannot attack.
- **Blind:** −30% hit chance.
- **Burn:** damage over time; the fire variant of `Dot`.
- **Stun:** lose the next action.

Every new ailment can be built as a `StatusEffect` using `GetActionOverride`,
`GetStatModification` or `GetTickEffects`.

### Buff stacking

Songs and Commands are separate buff families, and the same stat can receive one
buff from each. Two buffs from the same family don't stack; the stronger one
wins. The existing `ReApply` flag covers the refresh behaviour.

## Gathering

No gathering system exists yet. The proposal:

- The dungeon floor generator places **gathering points** of three types:
  ore (Mining), plant (Harvesting) and forage (Foraging).
- A party member with the matching passive (or the Scout's Survey) can interact
  with a point to get materials.
- Materials are sold at shops, or used by later crafting.
- Points are optional loot. They must never be required for progression and
  must never stand in for a capability lock.
- Each class has one gathering passive, so any balanced party of 4 covers at
  least two types.

## Implementation plan

Phases are ordered by dependency. Each phase ends in a working build with its
tests passing, so the game stays playable throughout. Paths are the expected
homes for new code, next to the existing systems they extend.

| Phase | Delivers | Depends on |
|---|---|---|
| 0 | Class and rank rules as pure, tested Core logic | — |
| 1 | Class data, fixed hero classes, protagonist class choice, class growth, weapon rules | 0 |
| 2 | Skill ranks, the class-aware trainer, ranked skill saves | 0, 1 |
| 3 | Combat foundations: elements, crits/evasion, new status effects, hit-response hooks, arrow use, Taunt/Stealth targeting | 2 |
| 4 | Advanced mechanics: downed/revive, summons, movement skills, floor reveal, songs/commands | 3 |
| 5 | Exploration: gathering points, trap sense/disarm/placement, Retreat | 3 |
| 6 | Ally AI that uses skills | 2 (and more useful after 3–4) |
| 7 | Content and balance: all 200 skill assets, hero class assignment, tuning | Every system its skills use |

**First playable slice:** phases 0–2, plus the skills that only use existing
effects. For example Double Strike, HP Up, Shield Bash, Heal, Renew, Pinning
Shot, Venom Arrow, Stunning Shot, Command: Attack and the stat passives. That
gives every class a real kit before phase 3 begins.

### Phase 0: rules in Core

This phase keeps every rule in one evaluator so the trainer, the save loader and
validation agree. That follows the Progression README's rule that runtime and
validation share one evaluator.

- Add `EternalEnigma.Core/Classes/` with plain C# types:
  - `ClassId`
  - `ClassSkillEntry` (skill ID, tier, max rank, kind: mastery / gathering /
    single-rank / normal)
  - `ClassKit` (primary, optional secondary)
  - `LearnedSkill` (skill ID, rank)
- `SkillLearningRules` covers:
  - tier unlock (mastery owned and level)
  - the rank level gate
  - rank cost (`LearnCost × rank`)
  - secondary-class caps (tiers 1–2, rank ≤ 3, no secondary masteries)
  - duplicate skills (learned once, at the primary's cap)
  - a readable reason for every refusal
- `ClassKitValidator` checks that primary ≠ secondary, that both classes exist,
  and that every entry's skill resolves.
- **Tests** (`EternalEnigma.Core.Tests/Classes/`): each gate, each cap, the cost
  curve, duplicates, refusal reasons, and every class kit shape.

### Phase 1: class data and assignment

- **`ClassDefinition` ScriptableObject** (`Assets/Scripts/Classes/`):
  - Id, display name, role text
  - allowed weapon types
  - per-level growth as a `StatModification`, starting-stat adjustments
  - skill entries (`Skill` reference + tier + max rank + kind)
  - It converts itself to the Core `ClassKit` form for the rules.
- **`ClassCatalog`** asset in `Resources/Classes/`. It resolves class IDs and
  fails validation on duplicate IDs.
- **Heroes:**
  - `TownAlly` gets `PrimaryClass` and an optional `SecondaryClass`.
  - These are fixed prefab data; nothing at runtime writes to them.
  - Extend the hero prefab audit (`Docs/HeroPrefabAudit.md`) to require a
    primary class and a valid kit.
- **Save:** `TownAllyData` gets `PrimaryClassId` / `SecondaryClassId`.
  - For recruits, the prefab is authoritative. The saved IDs are only checked,
    and a mismatch logs a warning and uses the prefab's values.
  - For the protagonist, the saved IDs are authoritative.
  - No save migration: the game is still in development, so older saves aren't
    supported.
- **Protagonist class choice:** add a class selection step to new-game setup
  (`MainMenu`). It offers any primary and an optional different secondary, and
  shows each class's role, weapons and tier 1 skills. `CreateNewSave` takes the
  chosen kit and writes it to the protagonist's `TownAllyData`. The protagonist
  is `RecruitedAlliesData.First()` / `ProtagonistId`.
- **Growth:** `LevelUpAction` applies the primary class's growth instead of the
  hard-coded +2 Strength / +5 HPMax. A missing class falls back to today's values.
- **Weapon rules:** the equip actions (both the town and dungeon paths) reject
  weapons outside the kit's weapon types with a reason. Starting equipment must
  match the hero's kit (checked by the prefab audit).
- **Town UI:** the recruit, party and trainer views show each hero's class (for
  example "Warrior / Scout").
- **Tests:**
  - EditMode: catalog validation, growth per class, weapon gating.
  - EditMode: new save with each class choice, class kit save round-trip.
  - PlayMode: the recruit dialog shows classes.

### Phase 2: ranks and trainer

- **`Skill` additions:**
  - `MaxRank` (default 5; 1 for single-rank kinds)
  - `RankScaling`, holding the default table values plus per-skill overrides:
    power %, chance points, buff steps, and the turn bonus at ranks 3 and 5
  - Tier and kind come from the class entry, not the skill, so a skill shared by
    two classes can sit at different tiers.
- **Rank-aware effects:**
  - `Skill.GetEffects` passes the caster's rank to each `GameAction` through
    `AsTargetedSkill`.
  - `TakeDamageAction`, `TakeHealAction`, `ApplyStatusEffectAction` and
    `ModifyStatAction` scale by rank.
  - `PassiveStatModification` scales in `Character.UpdateCachedStats`.
- **Learned skills:**
  - Replace `TownAllyData.Skills` (`List<string>`) and `TownAlly.Skills` with a
    list of `LearnedSkill { Name, Rank }`.
  - Carry the new list into `Ally` / `Character` so `CanCast`, the skill menu and
    the passives read ranks.
- **Trainer (`TownServices.Learn`, `BallistaDialog`, `SkillGridItem`):**
  - It lists the hero's class skills grouped by tier, each showing its current
    rank, next-rank cost and effect, or its lock reason (from phase 0 rules).
  - Buying learns rank 1 or raises the rank by one. It commits and saves
    immediately, as it does today.
  - `TownConfiguration.LearnableSkills` becomes an optional per-town allowlist
    or filter on top of the class kits. An empty list means every class skill is
    offered.
- **Tests:**
  - EditMode: rank scaling of each effect type, trainer gating and costs,
    combination caps.
  - PlayMode: learn → rank up → close dialog → save round-trip.

### Phase 3: combat foundations

Each item below can be built and merged independently once phase 2 lands.

1. **Elements and resistances.**
   - Add a `DamageElement` (Physical / Fire / Ice / Lightning) to
     `TakeDamageAction`.
   - Add per-element `Resistance` (Weak / Normal / Resist / Immune) to `Stats`,
     `StartingStats` and `StatModification`, and to the enemy definitions.
   - Resistance shifts ("Expose") are status effects that move one step.
   - While editing, add the fields `Stats + StatModification` currently skips
     (`SPRegenAcccumlateThreshold`, `DropRate`). The class passives rely on them.
2. **Critical hits and evasion.**
   - New `CritChance` and `Evasion` stats.
   - `AttackAction.GetAttackDamage` rolls crits (×1.5).
   - The hit check becomes `80% + hit bonuses − target evasion`, clamped to
     5–100%.
   - Missiles and skills use the same check unless marked "always hits".
3. **New status effects.** New `StatusEffect` prefabs:
   - Arm bind, Paralysis, Curse, Fear, Blind, Burn, Stun, Weaken, Taunt and
     Stealth.
   - Barriers, songs and commands as timed buffs.
   - Add a `BuffFamily` tag (None / Song / Command / Barrier), so that within one
     family the stronger buff replaces the weaker. The existing `ReApply` covers
     refreshing.
4. **Hit-response hooks.**
   - Add damage-dealt and damage-taken response points to the `TurnManager`
     action/response pipeline, plus a status-expired event.
   - These drive Warrior follow-ups, Guardian Cover and Parry, Occultist Curse,
     Commander Reinforce and Command: Endure, and Healer Second Wind.
   - A single hit's response chain is depth-limited, so follow-ups can't loop.
5. **Arrow use for skills.**
   - `Skill` gets `ArrowCost` and an `ArrowCostMode` (Fixed / PerTarget).
   - `CanCast` requires a bow and enough arrows (at least 1 for PerTarget).
   - Consumption reuses the ranged attack's arrow-stack code, with the Arrow
     Recovery roll per arrow.
   - The skill menu shows the arrow cost.
6. **Taunt and Stealth targeting.** The enemy policies (`AttackPolicy`,
   `PursuitPolicy`, `Cast*Policy`) choose targets through one shared filter:
   - A Taunted enemy must pick the taunter if reachable.
   - Stealthed characters can't be picked.
   - A Feared enemy flees.

- **Tests:** EditMode for each element × resistance, crit/evasion bounds, every
  status effect's tick, override and expiry, follow-up chaining limits, arrow
  counts (including partial Multishot/Rain), and taunt/stealth target selection.

### Phase 4: advanced mechanics

- **Downed allies and revive.**
  - An ally at 0 HP becomes *downed* instead of being removed. A downed ally
    can't act or be targeted, stays on its tile, and blocks nothing.
  - Revive and Mass Revive restore a downed ally. Leaving the floor or returning
    to town also restores downed allies at 1 HP.
  - The protagonist is downed like anyone else. **Defeat happens only when every
    party member is downed** (summons don't count).
  - If the player-controlled hero is downed, control passes to a standing party
    member, the same way it does when the controlled ally is dismissed.
  - Downed allies travel down stairs with the party.
  - Needs changes to death handling in `Character`/`Ally`, turn order, the
    game-over check, player control switching, and the party UI.
- **Summons.**
  - A temporary party-side `Character`, used for Rogue clones and Occultist
    Dominate.
  - Summons get a lifetime in turns, scaled stats, and join the ally turn order.
    They get no EXP, no inventory and aren't saved, and despawn on floor exit.
  - A Dominated enemy returns to its original team when the effect ends.
    Dominate can't target bosses (`CanCast` rejects them).
- **Movement skill actions.** Teleport-behind (Shadow Step/Shadow Dance), dash to
  first target (Lunge), step-back (Retreat Shot) and pull-to-tile (Grapple Line).
  They build on the existing teleport and movement actions, respect walls and
  3x3 footprints, and never cross capability-locked terrain.
- **Floor reveal.** Floor Sense, Farsight and Treasure Hunter write to
  `DungeonSight`/`FogOverlay` with a duration.
- **Songs and commands.**
  - An aura-style buff centred on the Bard (radius 3, re-evaluated each turn),
    with a per-caster slot limit (2, or 3 with Harmony).
  - Commands cover every visible ally when cast.
  - Grand Finale and Decisive Order consume active buffs.
- **Tests:** EditMode for downed/revive/floor-exit flow, summon lifetime and team
  switching, each movement action against walls and big units, song slot
  eviction. PlayMode for a full floor with a downed ally revived.

### Phase 5: exploration

- **Gathering points.**
  - `DungeonFloorGenerator` (Core) places optional ore, plant and forage points
    as a floor feature, seeded and never on the required path.
  - A validation rule ensures they never replace capability locks.
  - Unity renders them and adds an interactable.
  - Interacting needs the matching gathering passive (or Survey) and yields
    material items, with rank raising yield.
  - Materials are sellable at shops; crafting is out of scope.
- **Traps.**
  - Trap Sense reveals traps within a radius each turn.
  - Disarm removes an adjacent trap, with a chance to add a trap item.
  - Caltrops places a party-owned visible trap that only triggers on enemies.
  - These extend `Trap`/`BumpTrap`/`DamageTrap`; pathfinding already avoids
    revealed traps.
- **Retreat.** Ends the run under victory return rules through the existing
  return path. It's blocked on boss floors.
- **Wake and stealth rules.** Soft Step and Safe Passage hook the enemy wake check.
- **Tests:** Core tests for gathering-point placement and determinism. EditMode
  for gathering eligibility, trap reveal/disarm/placement, and Retreat's
  inventory results.

### Phase 6: ally AI skill use

- Add an `AllySkillPolicy` ahead of the existing attack/pursuit/ranged policies
  in `Ally`. It scores castable skills each turn, in priority order:
  1. revive
  2. emergency heal (below 30%)
  3. cure binds and ailments
  4. keep class buffs (songs, commands, barriers) running while enemies are visible
  5. crowd control on the most dangerous enemy
  6. damage skills when they beat a normal attack
- It keeps an SP reserve for heals and revives, and never uses arrow skills when
  arrows are nearly gone (it keeps a few for normal shots).
- `AllyStrategy` affects it: HoldPosition skips movement skills, and Aggressive
  lowers the SP reserve.
- **Tests:** EditMode scenario tests for each priority and the SP/arrow reserves.
  Extend the Autoplay harness (`Docs/Autoplay.md`) to run parties of every class.

### Phase 7: content and balance

- **Assets:** one `ClassDefinition` for each of the 10 classes and all 200
  `Skill` assets, using the phase 3–5 effects. The existing assets (HP Up, Sp Up,
  Defense Up, Strength Up, Healing, Hot, Dot, ShieldBash) are reused where the
  tables name them.
- **Heroes:** assign classes and combinations to the 24 hero prefabs, following
  [Class assignment](#class-assignment). Update the prefab audit.
- **Numbers:** map the Low/Med/High SP costs to numbers against the SP pools,
  and set growth, `LearnCost` and resistance values.
  - Enemy resistances by floor are authored so each element has good and bad
    matchups.
  - Tune using Autoplay runs across class compositions and campaign seeds.
- **Docs:** update `Docs/Town.md` (the trainer, recruit class display) and this
  document's status.

### Verification

Run for every phase:

```text
dotnet test Core/EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release
node Tools/unity-mcp.mjs harness EditMode
node Tools/unity-mcp.mjs harness PlayMode
node Tools/unity-mcp.mjs harness Town
node Tools/unity-mcp.mjs harness Autoplay
```
