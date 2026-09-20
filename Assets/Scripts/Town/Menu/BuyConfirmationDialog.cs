using JuicyChickenGames.Menu;
using System;
using TMPro;
using UnityEngine.UI;

public class BuyConfirmationDialog : Dialog
{
	public TextMeshProUGUI PromptText;
	public Button OkButton;
	public Button CancelButton;

	public Action<ShopMenuItem, ShopItemData> BuyCallBack;
	private ShopMenuItem _view;
	private ShopItemData _data;

	internal void Setup(ShopMenuItem view, ShopItemData data)
	{
		this._view = view;
		this._data = data;
		PromptText.text = $"Purchase {data.ItemName} ({data.Cost})?";
	}

	internal override void SetFirstSelect()
	{
		OkButton.Select();
	}

	public void Ok_Clicked()
	{
        var callback = BuyCallBack;
        BuyCallBack = null;
        CloseDialog();
        callback?.Invoke(_view, _data);
	}

	public void Cancel_Clicked()
	{
		CloseDialog();
	}

	internal void SetNavigation()
	{
		Navigation okNavigation = new Navigation();
		okNavigation.mode = Navigation.Mode.Explicit;
		okNavigation.selectOnRight = CancelButton;
		okNavigation.selectOnLeft = CancelButton;
		OkButton.navigation = okNavigation;

		Navigation cancelNavigation = new Navigation();
		cancelNavigation.mode = Navigation.Mode.Explicit;
		cancelNavigation.selectOnRight = OkButton;
		cancelNavigation.selectOnLeft = OkButton;
		CancelButton.navigation = cancelNavigation;
	}
}
