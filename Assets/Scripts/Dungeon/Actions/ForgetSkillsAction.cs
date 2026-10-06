using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Respec: the hero forgets every skill except the free starting ones. Skill points are derived from the
// learned ranks, so clearing them refunds everything. The town save is rewritten by DungeonReturnService.
[Serializable]
public class ForgetSkillsAction : GameAction
{
	[NonSerialized] private Character target;

	public ForgetSkillsAction() { }
	public ForgetSkillsAction(Character target) { this.target = target; }

	internal override bool IsValid(Character character) => target is Ally ally && ally.Vitals.HP > 0;

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new List<GameAction>();
		var ally = (Ally)target;
		var keep = TownAlly.StartingSkillNames(ally.PrimaryClass, ally.SecondaryClass).ToHashSet();
		keep.UnionWith(DungeonClearAbilities.Unlocked.Select(skill => skill.SkillName));
		ally.Skills.RemoveAll(s => s == null || !keep.Contains(s.SkillName));
		foreach (var skill in ally.Skills) skill.Rank = 1;
		ally.SkillsForgotten = true;
		ally.InvalidateCachedStats();
		return new List<GameAction>();
	}

	internal override void RecordOutcome(Character character)
	{
		if (target != null) GameMessages.ForCharacter(target, $"{GameMessages.Name(target)} forgot their skills. Skill points return in town.");
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		yield return null;
	}
}
