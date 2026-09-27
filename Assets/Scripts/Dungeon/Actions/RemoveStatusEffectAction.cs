using System.Collections;
using System.Collections.Generic;

public class RemoveStatusEffectAction : GameAction
{
	private readonly Character target;
	private readonly StatusEffect statusEffectPrefab;
	private StatusEffect removedInstance;

	public RemoveStatusEffectAction()
	{

	}
	public RemoveStatusEffectAction(Character target, StatusEffect statusEffectPrefab)
	{
		this.target = target;
		this.statusEffectPrefab = statusEffectPrefab;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		removedInstance = target.RemoveStatusEffect(statusEffectPrefab);
        if (removedInstance != null) GameMessages.ForCharacter(target, $"{GameMessages.Name(target)}: {removedInstance.GetEffectName()} ended.");
		var effects = removedInstance != null ? removedInstance.GetExpiryEffects(target) : null;
		return effects ?? new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (removedInstance != null)
		{
			UnityEngine.Object.Destroy(removedInstance.gameObject);
		}
		if (!skipAnimation) yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}