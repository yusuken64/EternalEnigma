using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "RemoveStatusEffectItemEffectDefinition", menuName = "Game/ItemEffect/RemoveStatusEffectItemEffectDefinition")]
public class RemoveStatusEffectItemEffectDefinition : ItemEffectDefinition
{
	public List<StatusEffect> StatusEffectPrefabs;

	public override List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		return StatusEffectPrefabs
			.Select(x => new RemoveStatusEffectAction(target, x))
			.Cast<GameAction>()
			.ToList();
	}
}
