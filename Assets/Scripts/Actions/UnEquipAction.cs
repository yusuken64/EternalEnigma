using System.Collections;
using System.Collections.Generic;

internal class UnEquipAction : GameAction
{
	private Player player;
	private readonly EquipableInventoryItem equipableInventoryItem;

	public UnEquipAction(Player player, EquipableInventoryItem equipableInventoryItem)
	{
		this.player = player;
		this.equipableInventoryItem = equipableInventoryItem;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		player.Inventory.UnEquip(equipableInventoryItem);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return player.Inventory.IsEquipped(equipableInventoryItem);
	}
}