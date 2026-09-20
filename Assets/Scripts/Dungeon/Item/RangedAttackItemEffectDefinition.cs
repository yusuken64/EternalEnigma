using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RangedAttackItemEffectDefinition", menuName = "Game/ItemEffect/RangedAttackItemEffectDefinition")]
public class RangedAttackItemEffectDefinition : ItemEffectDefinition
{
	public int Damage;
	public GameObject ProjectilePrefab;

	public override List<GameAction> GetGameActions(Character attacker, Character target, Inventory inventory, InventoryItem item)
	{
		// The missile use action has already traced and animated this shot.
		if (item.ItemDefinition is UsableItemDefinition definition && definition.Targeting == SkillTargeting.Missile)
			return new() { new TakeDamageAction(attacker, target, Damage, true, false) };
		return new List<GameAction>()
		{
			new RangedAttackAction(attacker, target, Damage, ProjectilePrefab)
		};
	}
}
