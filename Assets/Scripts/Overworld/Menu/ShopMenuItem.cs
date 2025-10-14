using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopMenuItem : MonoBehaviour
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
		CostText.text = $"{_data.Cost}";
	}

	//public override void OnSelect(BaseEventData eventData)
	//{
	//	base.OnSelect(eventData);
	//	SelectCallBack?.Invoke();
	//}
}
