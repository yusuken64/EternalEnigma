using UnityEngine;

public class DroppedItem : Interactable
{
	public InventoryItem InventoryItem;

	internal override void DoInteraction()
	{
		if (!Opened)
		{
			Game game = Game.Instance;
			var canAdd = game.PlayerCharacter.Inventory.CanAdd();
			if (canAdd)
			{
				this.Opened = true;
				game.PlayerCharacter.Inventory.Add(InventoryItem);
			}
			else
			{
				game.DoFloatingText("Inventory is full", Color.red, game.PlayerCharacter.transform.position);
			}
		}
	}

	internal override string GetInteractionText()
	{
		return !Opened ? $"Pick up {InventoryItem.ItemDefinition.ItemName}" : "";
	}
}