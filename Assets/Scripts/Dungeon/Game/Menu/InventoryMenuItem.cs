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
    private Character _character;

	public Func<InventoryItem, bool> IsEquipped { get; set; }
	public Action SelectCallBack { get; internal set; }

	internal void Setup(InventoryItem data, Func<InventoryItem, bool> isEquipped)
	{
		this._data = data;
		this.IsEquipped = isEquipped;
		UpdateUI();
	}

	private void UpdateUI()
	{
		//var equipped = _character.Equipment.IsEquipped(_data);
		var equipped = IsEquipped(_data);

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
