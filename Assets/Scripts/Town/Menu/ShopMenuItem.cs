using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopMenuItem : MonoBehaviour, ISelectHandler
{
	public TextMeshProUGUI ItemText;
	public TextMeshProUGUI CostText;
	internal ShopItemData _data;

	public Button BuyButton;

	public Action SelectCallBack { get; internal set; }


	internal void Setup(ShopItemData data)
	{
		this._data = data;
		UpdateUI();
	}

	private void UpdateUI()
	{
		var inventoryText = $"{_data.ItemName}";
		ItemText.text = inventoryText;
		CostText.text = _data.Remaining > 0 ? $"{_data.Cost}g ({_data.Remaining} left)" : "Sold out";
	}

    public void OnSelect(BaseEventData eventData) => SelectCallBack?.Invoke();
}
