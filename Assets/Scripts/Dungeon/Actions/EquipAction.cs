using System.Collections;
using System.Collections.Generic;

internal class EquipAction : GameAction
{
	private Character character;
	private readonly EquipableInventoryItem equipableInventoryItem;

	public EquipAction() {}
	public EquipAction(Character character, EquipableInventoryItem equipableInventoryItem)
	{
		this.character = character;
		this.equipableInventoryItem = equipableInventoryItem;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		character.Equipment.Equip(equipableInventoryItem);
        QueueSound(SoundCue.Equip);
		return new();
	}

    internal override void RecordOutcome(Character character) { GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} equipped {equipableInventoryItem.DisplayName}."); }
	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) { yield break; }

		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return character.Equipment.CanEquip(equipableInventoryItem);
	}
}
