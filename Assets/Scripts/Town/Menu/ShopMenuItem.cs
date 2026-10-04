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
		ItemText.text = _data.Remaining > 0
			? $"{_data.ItemName}\n<size=65%>{_data.Remaining} left</size>"
			: _data.ItemName;
		CostText.text = _data.Remaining > 0 ? $"{_data.Cost}g" : "Sold out";
	}

    public void OnSelect(BaseEventData eventData) => SelectCallBack?.Invoke();
}
