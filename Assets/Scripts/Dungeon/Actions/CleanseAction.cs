using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class CleanseAction : GameAction, ICureEffect
{
	public bool Ailments;
	public bool Binds;
	public bool Buffs;

	[NonSerialized] private Character target;

	public CleanseAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		new CleanseAction { Ailments = Ailments, Binds = Binds, Buffs = Buffs, target = target };

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new CleanseAction { Ailments = Ailments, Binds = Binds, Buffs = Buffs, target = target };

	internal override bool IsValid(Character character) =>
		target != null && target.Vitals.HP > 0;

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new();
		return target.StatusEffects.Where(s => s != null && !s.IsExpired() && ((Ailments && StatusCategories.IsAilment(s)) || (Binds && StatusCategories.IsBind(s)) || (Buffs && StatusCategories.IsBuff(s)))).ToList().Select(s => (GameAction)new RemoveStatusEffectAction(target, s)).ToList();
	}

	public bool Cures(StatusEffect status) =>
		status != null && ((Ailments && StatusCategories.IsAilment(status)) || (Binds && StatusCategories.IsBind(status)));
}
