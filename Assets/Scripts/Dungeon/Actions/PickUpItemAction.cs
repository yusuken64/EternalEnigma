﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class PickUpItemAction : GameAction
{
	private DroppedItem droppedItem;
	private bool canAdd;

	public PickUpItemAction(DroppedItem droppedItem)
	{
		this.droppedItem = droppedItem;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game game = Game.Instance;
		canAdd = game.PlayerController.Inventory.CanAdd();
		if (canAdd)
		{
			droppedItem.Opened = true;
			game.PlayerController.Inventory.Add(droppedItem.InventoryItem);
			game.CurrentDungeon.RemoveInteractable(droppedItem); //this should be removeimmediate, and remove routine
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		if (!canAdd)
		{
			Game.Instance.DoFloatingText("Inventory is full", Color.red, Game.Instance.PlayerController.transform.position);
		}
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}