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
- Successful learning/ranking saves immediately. Rejected offers explain the missing requirement.

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
Run `node Tools/unity-mcp.mjs harness Classes`, `Town`, `AllyAI` and `EditMode` as appropriate.
Balance testing remains separate from content and rule verification.
