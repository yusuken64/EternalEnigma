using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WarpItemEffectDefinition", menuName = "Game/ItemEffect/WarpItemEffectDefinition")]
public class WarpItemEffectDefinition : ItemEffectDefinition
{
	public override List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		return new List<GameAction>()
		{
			new WarpAction(attacker)
		};
	}
}
