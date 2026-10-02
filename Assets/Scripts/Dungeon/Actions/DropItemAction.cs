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
	private bool dropped;

	public DropItemAction()	{}

	public DropItemAction(Inventory inventory, InventoryItem item, Vector3Int dropPosition)
	{
		this.inventory = inventory;
		this.item = item;
		this.dropPosition = dropPosition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game game = Game.Instance;
		if (!inventory.InventoryItems.Contains(item)) return new();
		dropped = DungeonPlacement.TryDrop(game.CurrentDungeon, dropPosition, item, out _);
		if (!dropped) return new();

		// Equipped items are listed in the inventory too; dropping one must not leave it equipped.
		if (item is EquipableInventoryItem equipable && character.Equipment.IsEquipped(equipable))
			character.Equipment.UnEquip(equipable);
		inventory.Remove(item);

		return new();
	}

    internal override void RecordOutcome(Character character) { GameMessages.ForCharacter(character, dropped ? $"{GameMessages.Name(character)} dropped {item.ItemName} nearby; it can be recovered." : $"{item.ItemName} was kept: no free drop location."); }
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
