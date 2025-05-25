using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class UseInventoryItemAction : GameAction
{
	private Player player;
	private InventoryItem item;

	public UseInventoryItemAction(Player player, Character character, InventoryItem item)
	{
		this.player = player;
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
			player.Inventory.Remove(item);
		}
		return item.GetGameActions(character, character, item);
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