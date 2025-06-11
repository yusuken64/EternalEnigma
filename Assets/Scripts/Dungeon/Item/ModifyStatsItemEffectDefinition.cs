using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ModifyStatsItemEffectDefinition", menuName = "Game/ItemEffect/ModifyStatsItemEffectDefinition")]
public class ModifyStatsItemEffectDefinition : ItemEffectDefinition
{
	public StatModification StatModification;
	public VitalModification VitalModification;
	public bool DoDamageAnimation;

	public override List<GameAction> GetGameActions(Character attacker, Character target, Inventory inventory, InventoryItem item)
	{
		return new List<GameAction>()
		{
			new ModifyStatAction(attacker, target, (stats, vitals) =>
			{
				stats += StatModification;
				vitals += VitalModification;
			}, DoDamageAnimation)
		};
	}
}
