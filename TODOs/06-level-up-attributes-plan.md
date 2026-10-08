# Attribute and equipment follow-ups

Audited 2026-10-08. The implementation in `7bf32675` includes attribute points,
Magic Power in damage/healing/AI estimates, shared base-stat reconstruction,
save/party transfer, free spending, safe-point level-up choices, autoplay spending,
Stats and Equipment tabs, contribution breakdowns and simulated loadout previews.
The EXP-recipient prerequisite was fixed in `b2255ef1`.

Current values, growth and behavior are documented in [Classes](../Docs/Classes.md)
and [Dungeon controls](../Docs/DungeonControls.md). The six `HeroAttributeTests`
passed in the retained [2026-10-07 EditMode report](../Docs/Art/Previews/Diorama/Verification/PostCleanupEditMode.xml).
Do not reimplement the old plan's stat/save/UI pipeline.

## Remaining decisions and acceptance

- [ ] Playtest class progression and attribute choices for balance; rule and
  preview tests do not establish balance. Archer bow and Rogue weapon damage
  already gain 1% per AGI point.
- [ ] Reconcile the original visible-breakdown requirement with the compact dock.
  Stat sources and aggregate equipment bonuses exist in entry descriptions, but
  `PartyMenu.UseHudTabs` hides their pane. Decide how these should be accessible;
  the Equipment change picker still exposes its loadout previews.
- [ ] Decide whether optional class starting attributes should replace any
  existing starting-stat bonuses. Current heroes start with zero spent points.
- [ ] Decide whether equipment/buffs should grant attributes, whether a paid
  attribute respec is wanted, and whether enemies need attribute-based authoring.
  None of these optional extensions is implemented.
- [ ] Resolve damage-over-time kill attribution before changing leveling balance.
  `DotStatusEffect` and `BurnStatusEffect` construct damage actions with the victim
  as attacker, and curse reflection uses its owner. Original-caster EXP credit
  remains a separate follow-up.

Equipment/Stats shortcut decisions are tracked in [task 03](03-input-prompts.md).
Town and overworld spending captures live state; persistence follows explicit
[save checkpoints](../Docs/CampaignSaves.md), not an automatic disk save per point.
