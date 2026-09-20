using System.Collections.Generic;
using UnityEngine;

public class SkillItemEffectDefinition : ItemEffectDefinition
{
    public Skill EffectSkill;
    public override List<GameAction> GetGameActions(Character attacker, Character target, Inventory inventory, InventoryItem item) =>
        EffectSkill.GetEffects(attacker, target);
}
