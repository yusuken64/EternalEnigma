using System.Collections;
using System.Collections.Generic;

public class RemoveStatusEffectAction : GameAction
{
	private readonly Character target;
	private readonly StatusEffect statusEffectPrefab;
	private StatusEffect removedInstance;

	public RemoveStatusEffectAction(Character target, StatusEffect statusEffectPrefab)
	{
		this.target = target;
		this.statusEffectPrefab = statusEffectPrefab;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		removedInstance = target.RemoveStatusEffect(statusEffectPrefab);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		if (removedInstance != null)
		{
			UnityEngine.Object.Destroy(removedInstance.gameObject);
		}
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}