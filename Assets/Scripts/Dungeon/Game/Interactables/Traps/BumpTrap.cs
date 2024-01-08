using System.Collections.Generic;
using System.Linq;

public class BumpTrap : Trap
{
	internal override string GetInteractionText()
	{
		return "Bump Trap";
	}

	internal override List<GameAction> GetTrapSideEffects(Character character)
	{
		VisualObject.gameObject.SetActive(true);

		if (UnityEngine.Random.value > 0.5f)
		{
			if (character is Player player)
			{
				var inventoryItems = player.Inventory.InventoryItems.Take(3);
				return inventoryItems.Select(x => new DropItemAction(player, x, player.TilemapPosition))
					.Cast<GameAction>()
					.ToList();
			}
		}
		return new();
	}
}
