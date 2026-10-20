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

		ApplyPresentation();
		UpdateUI();
        if (transform.parent is RectTransform parent && parent.rect.width > 160)
        {
            float nameWidth = Mathf.Max(80, parent.rect.width * .74f - 48);
            float priceWidth = Mathf.Max(60, parent.rect.width * .26f - 40);
            GetComponent<LayoutElement>().preferredHeight = Mathf.Max(96,
                Mathf.Max(ItemText.GetPreferredValues(ItemText.text, nameWidth, 0).y,
                    CostText.GetPreferredValues(CostText.text, priceWidth, 0).y) + 24);
        }
	}

    public void ApplyPresentation()
    {
        // Separate columns protect the price from wrapping names and keep both clear of the frame.
        ItemText.rectTransform.anchorMin = new Vector2(0, 0);
        ItemText.rectTransform.anchorMax = new Vector2(.74f, 1);
        ItemText.rectTransform.offsetMin = new Vector2(32, 12);
        ItemText.rectTransform.offsetMax = new Vector2(-16, -12);
        CostText.rectTransform.anchorMin = new Vector2(.74f, 0);
        CostText.rectTransform.anchorMax = Vector2.one;
        CostText.rectTransform.offsetMin = new Vector2(8, 12);
        CostText.rectTransform.offsetMax = new Vector2(-32, -12);
        ItemText.margin = CostText.margin = Vector4.zero;
        ItemText.alignment = TextAlignmentOptions.MidlineLeft;
        CostText.alignment = TextAlignmentOptions.MidlineRight;
        ItemText.fontSize = ItemText.fontSizeMax = 26;
        ItemText.fontSizeMin = 24; ItemText.enableAutoSizing = true;
        ItemText.textWrappingMode = TextWrappingModes.Normal;
        CostText.fontSize = CostText.fontSizeMax = 26;
        CostText.fontSizeMin = 22; CostText.enableAutoSizing = true;
        CostText.textWrappingMode = TextWrappingModes.Normal;
        ItemText.raycastTarget = CostText.raycastTarget = false;
        var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 96;
    }

	private void UpdateUI()
	{
		if (_data.IsSale)
		{
			ItemText.text = GameplayText.Format($"{GameplayText.Name(_data.ItemName)}\n<size=65%>Quantity {_data.Remaining}</size>");
			CostText.text = GameplayText.Format($"{_data.Cost}g");
			return;
		}
		ItemText.text = GameplayText.Format(_data.Remaining > 0
			? $"{GameplayText.Name(_data.ItemName)}\n<size=65%>{_data.Remaining} left</size>"
			: GameplayText.Name(_data.ItemName));
		CostText.text = GameplayText.Format(_data.Remaining > 0 ? _data.Cost == 0 ? "Free" : $"{_data.Cost}g" : "Sold out");
	}

    public void OnSelect(BaseEventData eventData) => SelectCallBack?.Invoke();
}
