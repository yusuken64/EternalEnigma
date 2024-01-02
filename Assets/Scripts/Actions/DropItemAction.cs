using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class DropItemAction : GameAction
{
	private Player player;
	private InventoryItem item;
	private Vector3Int dropPosition;

	public DropItemAction(Player player, InventoryItem item)
	{
		this.player = player;
		this.item = item;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game game = Game.Instance;
		player.Inventory.InventoryItems.Remove(item);
		game.CurrentDungeon.SetDroppedItem(dropPosition, item.ItemDefinition, game.DungeonGenerator.DroppedItemTile, item.StackStock);

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();
	}

	internal override bool IsValid(Character character)
	{
		Game game = Game.Instance;

		if (!player.Inventory.InventoryItems.Contains(item))
		{
			return false;
		}

		var itemDropPosition = game.CurrentDungeon.GetDropPosition(character.TilemapPosition);
		if (itemDropPosition == null)
		{
			return false;
		}

		this.dropPosition = itemDropPosition.Value;

		return true;
	}
}