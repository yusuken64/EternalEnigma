using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryMenuItem : Button
{
	public TextMeshProUGUI ItemText;
	private InventoryItem _data;

	public Action SelectCallBack { get; internal set; }

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

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		SelectCallBack?.Invoke();
	}
}
