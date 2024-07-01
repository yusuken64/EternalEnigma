using System.Collections;
using System.Collections.Generic;

internal class EquipAction : GameAction
{
	private Player player;
	private readonly EquipableInventoryItem equipableInventoryItem;

	public EquipAction(Player player, EquipableInventoryItem equipableInventoryItem)
	{
		this.player = player;
		this.equipableInventoryItem = equipableInventoryItem;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		player.Inventory.Equip(equipableInventoryItem);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		AudioManager.Instance.SoundEffects.Equip.PlayAsSound();
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		if (character is Player player)
		{
			return player.Inventory.CanEquip(equipableInventoryItem);
		}

		return false;
	}
}
