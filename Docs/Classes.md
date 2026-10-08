# Classes, skills and learning

Ten combat classes and their skill assets are implemented. The normal New Journey flow
chooses one of 24 authored heroes with a fixed primary/secondary combination; it does not
offer free class assignment. Classes never satisfy overworld capability locks.

## Class identity

| Class | Role | Tree |
|---|---|---|
| Warrior | Physical attacks and follow-ups | [Warrior](SkillTrees/warrior.skilltree) |
| Guardian | Defense, barriers and protection | [Guardian](SkillTrees/guardian.skilltree) |
| Archer | Bow attacks and arrow skills | [Archer](SkillTrees/archer.skilltree) |
| Elementalist | Elemental spell damage | [Elementalist](SkillTrees/elementalist.skilltree) |
| Healer | Healing, recovery and revival | [Healer](SkillTrees/healer.skilltree) |
| Bard | Songs and party support | [Bard](SkillTrees/bard.skilltree) |
| Occultist | Debuffs, ailments and control | [Occultist](SkillTrees/occultist.skilltree) |
| Rogue | Mobility, stealth and disruption | [Rogue](SkillTrees/rogue.skilltree) |
| Commander | Commands and party coordination | [Commander](SkillTrees/commander.skilltree) |
| Scout | Exploration and utility | [Scout](SkillTrees/scout.skilltree) |

`Assets/Resources/Classes/Definitions` and its catalog are the runtime authority. The linked
text trees record skill names, tiers, kinds and rank caps; [skill-tree editing](SkillTrees/README.md)
explains import/export. Effects and numbers are authored in the referenced Skill assets.
Primary class determines growth and mastery progression. Secondary classes add weapon access
and non-mastery skills through tier 2, capped at rank 3. Duplicate skills use the primary cap.

## Attributes and level growth

Each level grants one attribute point. Unspent points equal `max(0, level - 1 - spent points)`;
older heroes therefore receive their full allotment when loaded. Spending is free in the Stats
tab in town, the overworld, or a dungeon. A dungeon level-up offers a one-time choice; Later
defers it until the next level-up. Autoplay chooses the primary class's preferred attribute.

| Attribute | Each point grants |
|---|---|
| STR | +1 Attack, +3 maximum HP, and +1 Defense every four STR points |
| INT | +2 maximum SP, +1 Magic Power, and 15 less SP regeneration threshold (up to 400 less) |
| AGI | +1% Hit, +1% Evasion, +1% Crit, and +2 maximum Food; its Evasion and Crit contributions cap at 25% |

Magic Power multiplies magic damage and scaled healing by `max(0, 1 + 0.05 × Magic Power)`.
Archer bow damage and Rogue weapon damage gain 1% per AGI point. Class starting stat bonuses
remain in effect; no class starts with spent attribute points.

| Class | HP / level | SP / level | Attack / level | Recommended |
|---|---:|---:|---:|---|
| Warrior | 2 | 0 | 2 | STR |
| Guardian | 4 | 0 | 0 | STR |
| Archer | 4 | 0 | 3 | AGI |
| Rogue | 4 | 1 | 2 | AGI |
| Scout | 5 | 0 | 2 | AGI |
| Elementalist, Occultist | 3 | 0 | 1 | INT |
| Healer | 4 | 0 | 1 | INT |
| Bard | 5 | 0 | 2 | INT |
| Commander | 5 | 0 | 1 | INT |

## Stats and equipment views

The shared party window has Inventory, Equipment, Skills and Stats tabs; the overworld
also exposes Capabilities. Stats shows level/EXP, attributes, vitals, combat values and
resistances. Equipment shows all three slots, including empty or blocked slots and each
item's bonuses. Its change picker supplies full-stat previews with class/passive conditions,
two-handed displacement and proficiency restrictions before confirmation.

Source breakdowns and aggregate equipment bonuses are computed in entry descriptions,
but the compact root dock currently hides that description pane. Their visibility is an
open presentation decision in the attribute follow-ups below.

`HeroStatRules` rebuilds base stats; `StatPreview` and `StatBreakdown` share contribution
calculation with live stats. Attribute points transfer through saves, party travel and
summons; summons cannot earn or spend points. Town/overworld changes capture live state and
persist at the next explicit save. Dungeon equipment changes spend the controlled hero's
action; attribute spending is free. Equipment and Stats currently have clickable launcher
tabs without dedicated keyboard/gamepad shortcuts.

Current class identity uses starting stat bonuses, not starting attribute allocations.
Equipment-granted attributes, attribute respec and enemy attribute authoring remain optional
[follow-ups](../TODOs/06-level-up-attributes-plan.md).

## Weapon proficiency

Sticks and needles: no proficiency required, including their offhand versions.
Primary and secondary class proficiencies combine. Axes are available to Warriors and
Scouts; hammers to Warriors, Guardians and Healers. Each of these proficiencies permits
both main-hand and offhand versions, subject to the item's existing equipment slot.
Healers retain wands and need a secondary class for sword proficiency. Sword proficiency
alone does not grant axes or hammers. Other class proficiencies are unchanged.

Existing saved items resolve to their updated asset definitions. Equipment is not
removed or replaced on load; subsequent equip attempts use these rules.

## Training

Core `SkillLearningRules` is shared by Unity trainer offers and the console editor.

- Highest dungeon level earns `2 * max(1, level)` skill points, including two at level 1.
- Rank n costs n points; five ranks total 15. Starting Novice Training is free.
- Tier unlock levels are 1, 10 and 20; each additional rank adds three required levels.
- Higher mastery requires the previous mastery and three learned non-mastery skills from
  the preceding tier. Other higher-tier skills require that tier's primary mastery.
- Available points are derived from highest level and learned ranks, not a separately saved balance.
- The trainer lists the visiting hero's kit. A nonempty `LearnableSkills` configuration filters it.
  Classless configured heroes use gold-priced, single-rank offers.
- Successful learning/ranking captures live progression. Disk persistence waits for an explicit
  [save checkpoint](CampaignSaves.md). Rejected offers explain the missing requirement.

`TownAllyData` stores class IDs, learned names/ranks and highest level. Each dungeon ally gets
independent runtime skill instances. Rank scaling affects damage/healing/status/stat actions;
skills can override the scaling and cap. Current-schema saves only are supported.

## Implemented mechanics

Unity has elemental damage/resistances, hit/critical/evasion rules, binds and ailments,
threat/taunt, stealth, barriers, multi-hit/follow-up actions, songs, commands, summons,
movement skills, gathering, exploration reveals and ranked passives. Arrow skills spend arrows.
Dominate excludes bosses. Downed allies leave the active list and can be revived; the party
continues fighting while any ally remains alive. Carried HP/SP persists until rest/recovery.

Ally skill AI classifies effects rather than hardcoding classes. Priorities include revival,
emergency healing, cleansing, support upkeep, control and worthwhile damage. It reserves SP
for healing/revival and ammunition for bow skills, and respects hold-position for movement skills.
Autoplay uses the same policy. It is a baseline policy, not evidence of balanced classes.

The old implementation phases and run-once content-generation checklist are removed. Continue
editing the committed class/skill/prefab assets. Combat effect and portrait tools remain reusable.

## Verification

Core class tests cover gates, caps, points, costs and tree format. Unity fixtures include
`ClassContentTests`, `TownTrainerRankTests`, `AllySkillIntentTests`, `AllySkillPolicyTests`,
`AllyAiClassPartyTests`, combat-foundation, movement, song/command and inventory-targeting tests.
`HeroAttributeTests` covers conversion caps/breakpoints, pending points, save/return transfer
and simulated loadouts. `WeaponCatalogTests` covers [weapon availability](Weapons.md).
Run `node Tools/unity-mcp.mjs harness Classes`, `Town`, `AllyAI` and `EditMode` as appropriate.
Balance testing remains separate from content and rule verification.
