using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class CureStatusEffectsAction : GameAction, ICureEffect
{
	public bool CureAilments = true;
	public bool CureBinds;
	public List<string> ExtraEffectNames = new();

	[NonSerialized] private Character caster;
	[NonSerialized] private Character target;
	[NonSerialized] private int removed;

	public CureStatusEffectsAction() { }

	public bool Cures(StatusEffect status)
	{
		if (status == null) return false;
		var name = status.GetEffectName();
		return (CureAilments && SkillIntents.AilmentStatuses.Contains(name)) ||
			(CureBinds && SkillIntents.BindStatuses.Contains(name)) ||
			(ExtraEffectNames != null && ExtraEffectNames.Contains(name));
	}

	private CureStatusEffectsAction Bind(Character caster, Character target) => new()
	{
		CureAilments = CureAilments, CureBinds = CureBinds,
		ExtraEffectNames = ExtraEffectNames != null ? new List<string>(ExtraEffectNames) : new List<string>(),
		caster = caster, target = target
	};

	internal override GameAction AsTargetedSkill(Character caster, Character target) => Bind(caster, target);
	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) => Bind(caster, target);

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (target == null) return new();
		TrackAnimationTarget(target);
		var cured = target.StatusEffects.Where(s => s != null && !s.IsExpired() && Cures(s)).ToList();
		removed = cured.Count;
		return cured.Select(s => (GameAction)new RemoveStatusEffectAction(target, s)).ToList();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || target == null) yield break;
		Game.Instance.DoFloatingText(removed > 0 ? "Cured" : "No effect", Color.green, target.VisualParent.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}
