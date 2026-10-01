using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ForgetSkillsItemEffectDefinition", menuName = "Game/ItemEffect/ForgetSkillsItemEffectDefinition")]
public class ForgetSkillsItemEffectDefinition : ItemEffectDefinition
{
	public override List<GameAction> GetGameActions(Character attacker, Character target, Inventory inventory, InventoryItem item) =>
		new List<GameAction> { new ForgetSkillsAction(target) };
}
