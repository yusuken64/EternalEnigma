﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class UseInventoryItemAction : GameAction
{
	private Player player;
	private InventoryItem item;

	public UseInventoryItemAction(Player player, InventoryItem item)
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
		return item.GetGameActions(player, player, item);
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		Game.Instance.DoFloatingText(item.ItemName, Color.white, player.transform.position);
		yield return new WaitForSecondsRealtime(1f);
	}

	internal override bool IsValid(Character character)
	{
		return item != null;
	}
}