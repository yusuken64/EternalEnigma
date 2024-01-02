using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class FallToGroundAction : GameAction
{
	private Vector3Int dropPosition;
	private InventoryItem item;

	public FallToGroundAction(Vector3Int dropPosition, InventoryItem item)
	{
		this.dropPosition = dropPosition;
		this.item = item;
	}


	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		Game.Instance.CurrentDungeon.SetDroppedItem(dropPosition, item.ItemDefinition, Game.Instance.DungeonGenerator.DroppedItemTile, item.StackStock);
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}