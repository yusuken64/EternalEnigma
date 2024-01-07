using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RangedAttackItemEffectDefinition", menuName = "Game/ItemEffect/RangedAttackItemEffectDefinition")]
public class RangedAttackItemEffectDefinition : ItemEffectDefinition
{
	public int Damage;
	public GameObject ProjectilePrefab;

	public override List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		return new List<GameAction>()
		{
			new RangedAttackAction(attacker, target, Damage, ProjectilePrefab)
		};
	}
}
