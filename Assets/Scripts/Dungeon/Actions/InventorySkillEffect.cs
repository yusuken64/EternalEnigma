using System;
using System.Collections;
using System.Collections.Generic;

// An item-effect template in Skill.ActionEffects. Bind creates a fresh action for
// the selected runtime item, so duplicate names never target the wrong copy.
[Serializable]
public abstract class InventorySkillEffect : GameAction
{
    internal virtual bool CanTarget(Character caster, InventoryItem item) => item?.ItemDefinition != null;
    internal abstract GameAction Bind(Character caster, InventoryItem item);

    internal sealed override bool IsValid(Character character) => false;
    internal sealed override List<GameAction> ExecuteImmediate(Character character) =>
        throw new InvalidOperationException("Bind the inventory skill effect to an item before executing it.");
    internal sealed override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false) { yield break; }
}
