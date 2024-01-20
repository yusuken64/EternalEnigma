using System.Collections.Generic;
using UnityEngine;

public class DroppedItem : Interactable
{
	public DroppedItemVisual DroppedItemVisual;
	public InventoryItem InventoryItem;

	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
		if (!Opened)
		{
			return new()
			{
				new PickUpItemAction(this)
			};
		}

		return new();
	}

	internal override string GetInteractionText()
	{
		return !Opened ? $"Pick up {InventoryItem.ItemDefinition.ItemName}" : "";
	}
}