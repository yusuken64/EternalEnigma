using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class DropItemAction : GameAction
{
	private Inventory inventory;
	private InventoryItem item;
	private Vector3Int dropPosition;

	public DropItemAction(Inventory inventory, InventoryItem item, Vector3Int dropPosition)
	{
		this.inventory = inventory;
		this.item = item;
		this.dropPosition = dropPosition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game game = Game.Instance;
		inventory.Remove(item);
		var finalDropPosition = game.CurrentDungeon.GetDropPosition(dropPosition);
		game.CurrentDungeon.SetDroppedItem(finalDropPosition, item.ItemDefinition, item.StackStock);

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) { yield break; }
		AudioManager.Instance.SoundEffects.Unequip.PlayAsSound();
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();
	}

	internal override bool IsValid(Character character)
	{
		if (!inventory.InventoryItems.Contains(item))
		{
			return false;
		}

		return true;
	}
}