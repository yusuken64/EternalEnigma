using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ApplyStatusEffectAction : GameAction
{
	private readonly Character target;
	private readonly StatusEffect statusEffectPrefab;
	private StatusEffect statusInstance;

	public ApplyStatusEffectAction(Character target, StatusEffect statusEffectPrefab)
	{
		this.target = target;
		this.statusEffectPrefab = statusEffectPrefab;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		statusInstance = target.ApplyStatusEffect(statusEffectPrefab);
		statusInstance?.gameObject.SetActive(false);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		statusInstance?.gameObject.SetActive(true);
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
