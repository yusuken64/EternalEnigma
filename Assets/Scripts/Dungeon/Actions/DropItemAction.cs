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
		if (!dropped) { QueueSound(SoundCue.Denied); return new(); }
        QueueSound(SoundCue.Drop);

		// Equipped items are listed in the inventory too; dropping one must not leave it equipped.
		if (item is EquipableInventoryItem equipable && character.Equipment.IsEquipped(equipable))
			character.Equipment.UnEquip(equipable);
		inventory.Remove(item);

		return new();
	}

    internal override void RecordOutcome(Character character) { GameMessages.ForCharacter(character, dropped ? $"{GameMessages.Name(character)} dropped {item.DisplayName} nearby; it can be recovered." : $"Kept {item.DisplayName} because there is no room to drop it."); }
	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) { yield break; }

		yield return character.AnimateInteraction();
	}

	internal override bool IsValid(Character character) => ValidateCommand(character, out _);

	internal override bool ValidateCommand(Character character, out string reason)
	{
		reason = null;
		if (character == null || character.Vitals.HP <= 0) { reason = "A downed hero cannot drop items."; return false; }
		if (inventory == null || item == null || !inventory.InventoryItems.Contains(item))
		{ reason = "This item is no longer in your inventory."; return false; }
		var status = character.StatusEffects.FirstOrDefault(s => !s.IsExpired() && (s.PreventsMenu() || s.Interupts(this)));
		if (status != null) { reason = $"Cannot drop items while affected by {GameplayText.Name(status.GetEffectName())}."; return false; }
		return DungeonPlacement.CanDrop(Game.Instance?.CurrentDungeon, dropPosition, item, out reason);
	}
}
