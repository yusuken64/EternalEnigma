using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ApplyStatusEffectItemEffectDefinition", menuName = "Game/ItemEffect/ApplyStatusEffectItemEffectDefinition")]
public class ApplyStatusEffectItemEffectDefinition : ItemEffectDefinition
{
	public StatusEffect StatusEffectPrefab;

	public override List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		return new List<GameAction>()
		{
			new ApplyStatusEffectAction(target, StatusEffectPrefab, attacker)
		};
	}
}
