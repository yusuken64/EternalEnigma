using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ModifyStatsItemEffectDefinition", menuName = "Game/ItemEffect/ModifyStatsItemEffectDefinition")]
public class ModifyStatsItemEffectDefinition : ItemEffectDefinition
{
	public StatModification StatModification;
	public bool DoDamageAnimation;

	public override List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		return new List<GameAction>()
		{
			new ModifyStatAction(attacker, target, (x) =>
			{
				x.BaseStats += StatModification;
			}, DoDamageAnimation)
		};
	}
}