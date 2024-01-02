using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryMenuItem : Button
{
	public TextMeshProUGUI ItemText;
	private InventoryItem _data;

	internal void Setup(InventoryItem data)
	{
		this._data = data;
		UpdateUI();
	}

	private void UpdateUI()
	{
		var equipped = Game.Instance.PlayerCharacter.Inventory.IsEquipped(_data);

		var inventoryText = $"{_data.ItemName} " +
			$"{ (_data.StackStock.HasValue ? $"x{_data.StackStock.Value}" : "") }" +
			$"{ (equipped ? "(Equipped)" : "")}";
		ItemText.text = inventoryText;
	}
}
