using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ApplyStatusEffectAction : GameAction
{
	private readonly Character target;
	private readonly StatusEffect statusEffectPrefab;
	private readonly Character caster;
	private StatusEffect statusInstance;

	public ApplyStatusEffectAction(Character target, StatusEffect statusEffectPrefab, Character caster)
	{
		this.target = target;
		this.statusEffectPrefab = statusEffectPrefab;
		this.caster = caster;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		statusInstance = target.ApplyStatusEffect(statusEffectPrefab);
		statusInstance?.gameObject.SetActive(false);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) { yield break; }

		//TODO get sound from status
		AudioManager.Instance.SoundEffects.Sleep.PlayAsSound();
		Game.Instance.DoFloatingText($"{statusEffectPrefab.GetEffectName()}!", Color.yellow, caster.transform.position);
		statusInstance?.gameObject.SetActive(true);
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
