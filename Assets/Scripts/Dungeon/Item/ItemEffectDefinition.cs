using System.Collections.Generic;
using UnityEngine;

public abstract class ItemEffectDefinition : ScriptableObject
{
	public abstract List<GameAction> GetGameActions(Character attacker, Character target, Inventory inventory, InventoryItem item);
}
