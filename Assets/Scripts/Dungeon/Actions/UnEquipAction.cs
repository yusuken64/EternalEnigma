using System.Collections;
using System.Collections.Generic;

internal class UnEquipAction : GameAction
{
	private Character character;
	private readonly EquipableInventoryItem equipableInventoryItem;

	public UnEquipAction(Character character, EquipableInventoryItem equipableInventoryItem)
	{
		this.character = character;
		this.equipableInventoryItem = equipableInventoryItem;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		this.character.Equipment.UnEquip(equipableInventoryItem);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return this.character.Equipment.IsEquipped(equipableInventoryItem);
	}
}