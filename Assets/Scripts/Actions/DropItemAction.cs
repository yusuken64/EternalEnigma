using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class DropItemAction : GameAction
{
	private Vector3Int dropPosition;

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game game = Game.Instance;
		var player = character as Player;

		var item = player.Inventory.InventoryItems.Last();
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
		if (character is Player player)
		{
			if (player.Inventory.Count() == 0)
			{
				return false;
			}
		}

		Game game = Game.Instance;
		var itemDropPosition = game.CurrentDungeon.GetDropPosition(character.TilemapPosition);
		if (itemDropPosition == null)
		{
			return false;
		}

		this.dropPosition = itemDropPosition.Value;

		return true;
	}
}