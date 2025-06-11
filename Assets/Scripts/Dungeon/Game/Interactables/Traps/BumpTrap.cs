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
			var playerController = FindFirstObjectByType<PlayerController>();
			if (character == playerController.ControlledAlly)
			{
				var inventoryItems = playerController.Inventory.InventoryItems.Take(3);
				return inventoryItems.Select(x => new DropItemAction(playerController.Inventory, x, character.TilemapPosition))
					.Cast<GameAction>()
					.ToList();
			}
		}
		return new();
	}
}
