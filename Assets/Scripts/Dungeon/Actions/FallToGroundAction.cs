using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class FallToGroundAction : GameAction
{
	private Vector3Int dropPosition;
	private InventoryItem item;
	private Vector3Int finalDropPositon;
	private DroppedItem droppedItemInstance;

	public FallToGroundAction(Vector3Int dropPosition, InventoryItem item)
	{
		this.dropPosition = dropPosition;
		this.item = item;
	}


	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		TileWorldDungeon currentDungeon = Game.Instance.CurrentDungeon;
		finalDropPositon = currentDungeon.GetPositionWith(dropPosition,
			node =>
			{
				var first = currentDungeon.Interactables.FirstOrDefault(x => x.Position == new Vector3Int(node.X, node.Y));
				return first == null;
			});
		droppedItemInstance = currentDungeon.SetDroppedItem(finalDropPositon, item.ItemDefinition, item.StackStock);
		droppedItemInstance.gameObject.SetActive(false);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		droppedItemInstance.transform.position = Game.Instance.CurrentDungeon.CellToWorld(dropPosition);
		droppedItemInstance.gameObject.SetActive(true);
		if (finalDropPositon != dropPosition)
		{
			var finalDungeonPosition = Game.Instance.CurrentDungeon.CellToWorld(finalDropPositon);
			var jumpTween = droppedItemInstance.transform.DOJump(finalDungeonPosition, 1, 1, 0.2f);
			yield return jumpTween.WaitForCompletion();
		}
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}