using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class UseInventoryItemAction : GameAction
{
	private Inventory inventory;
	private InventoryItem item;

	public UseInventoryItemAction(Inventory inventory, Character character, InventoryItem item)
	{
		this.inventory = inventory;
		this.item = item;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (item.HasStacks)
		{
			item.Decrement();
		}

		if (item.ShouldRemoveAfterUse())
		{
			inventory.Remove(item);
		}
		return item.GetGameActions(character, character, inventory, item);
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		//TODO get sound from item
		AudioManager.Instance.SoundEffects.UseItem.PlayAsSound();
		Game.Instance.DoFloatingText(item.ItemName, Color.white, character.transform.position);
		yield return new WaitForSecondsRealtime(1f);
	}

	internal override bool IsValid(Character character)
	{
		return item != null;
	}
}