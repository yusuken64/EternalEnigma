using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class PickUpItemAction : GameAction
{
	private DroppedItem droppedItem;
	private bool canAdd;

	public PickUpItemAction()
	{

	}
	public PickUpItemAction(DroppedItem droppedItem)
	{
		this.droppedItem = droppedItem;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game game = Game.Instance;
        if (droppedItem == null || droppedItem.Opened || !game.CurrentDungeon.Interactables.Contains(droppedItem)) return new();
        using var receipt = AutoplayRunner.LootPickup(droppedItem, character, droppedItem.InventoryItem.StackStock ?? 1);
		canAdd = game.PlayerController.Inventory.CanAdd();
		if (canAdd)
		{
            GameMessages.Post($"Picked up {droppedItem.InventoryItem.DisplayName}.");
			droppedItem.Opened = true;
			game.PlayerController.Inventory.Add(droppedItem.InventoryItem);
			game.CurrentDungeon.RemoveInteractable(droppedItem); //this should be removeimmediate, and remove routine
		}

		return new();
	}

    internal override void RecordOutcome(Character character) { if (!canAdd) GameMessages.ForCharacter(character, "Inventory is full."); }

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
        if (skipAnimation) yield break;
        if (canAdd) AudioManager.Instance.SoundEffects.Unequip.PlayAsSound();
		if (!canAdd)
		{

		}
		if (!skipAnimation) yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
