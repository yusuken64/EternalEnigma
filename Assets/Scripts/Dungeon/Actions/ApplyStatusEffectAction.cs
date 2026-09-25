using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ApplyStatusEffectAction : GameAction
{
	private readonly Character target;
	private readonly StatusEffect statusEffectPrefab;
	private readonly Character caster;
	private StatusEffect statusInstance;
	// Extra turns granted by the casting skill's rank (0 when unranked).
	private int durationDelta;

	public StatusEffect StatusEffect;

	public ApplyStatusEffectAction() { }
	public ApplyStatusEffectAction(Character target, StatusEffect statusEffectPrefab, Character caster)
	{
		this.target = target;
		this.statusEffectPrefab = statusEffectPrefab;
		this.caster = caster;
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return new ApplyStatusEffectAction(target, StatusEffect, caster);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var action = new ApplyStatusEffectAction(target, StatusEffect, caster);
		if (StatusEffect != null)
		{
			var scaling = rank.Scaling ?? new SkillRankScaling();
			action.durationDelta = scaling.ScaleDuration(StatusEffect.TurnsLeft, rank.Rank) - StatusEffect.TurnsLeft;
		}
		return action;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		TrackAnimationTarget(target);
		statusInstance = target.ApplyStatusEffect(statusEffectPrefab);
		if (durationDelta != 0)
		{
			var applied = statusInstance ?? target.StatusEffects.FirstOrDefault(x => x.GetType() == statusEffectPrefab.GetType());
			if (applied != null) applied.TurnsLeft += durationDelta;
		}
		var applied = statusInstance != null ? statusInstance :
			target.StatusEffects.FirstOrDefault(x => x != null && statusEffectPrefab != null && x.StackKey == statusEffectPrefab.StackKey);
		applied?.OnApplied(target, caster);
		statusInstance?.gameObject.SetActive(false);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		statusInstance?.gameObject.SetActive(true);
		if (skipAnimation) { yield break; }

		//TODO get sound from status
		AudioManager.Instance.SoundEffects.Sleep.PlayAsSound();
		Game.Instance.DoFloatingText($"{statusEffectPrefab.GetEffectName()}!", Color.yellow, caster.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
