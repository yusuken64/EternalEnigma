using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopMenuItem : Button
{
	public TextMeshProUGUI ItemText;
	internal ShopItemData _data;

	public Action SelectCallBack { get; internal set; }


	internal void Setup(ShopItemData data)
	{
		this._data = data;
		UpdateUI();
	}

	private void UpdateUI()
	{
		if (ItemText == null) ItemText = GetComponentInChildren<TextMeshProUGUI>();

		var inventoryText = $"{_data.ItemName} ({_data.Cost})g";
		ItemText.text = inventoryText;
	}

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		SelectCallBack?.Invoke();
	}
}
