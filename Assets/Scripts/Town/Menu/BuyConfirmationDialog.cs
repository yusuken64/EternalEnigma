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
	private Func<string> unavailableReason;
	private string prompt;

	internal void Setup(ShopMenuItem view, ShopItemData data, Func<string> validate = null)
	{
        TownModalBackdrop.Ensure(this);
		this._view = view;
		this._data = data;
		unavailableReason = validate;
		prompt = data.IsSale ? $"Sell {data.Remaining} {GameplayText.Name(data.ItemName)} for {data.Cost} gold?" : $"Purchase {GameplayText.Name(data.ItemName)} for {data.Cost} gold?";
        if (!data.IsSale && data.Cost == 0 && data.ItemName == "Bread")
            prompt = "Take your free bread?\n\nFood restores Hunger. In the dungeon, open your bag, choose Bread, then Eat.";
        OkButton.GetComponentInChildren<TMP_Text>().text = !data.IsSale && data.Cost == 0 && data.ItemName == "Bread" ? "Take bread" : data.IsSale ? "Sell" : "Buy";
        GameUISkin.PadButtonLabel(OkButton);
        GameUISkin.PadButtonLabel(CancelButton);
		PromptText.enableAutoSizing = true;
		PromptText.fontSizeMin = 22;
		PromptText.fontSizeMax = 28;
		PromptText.textWrappingMode = TextWrappingModes.Normal;
		RefreshAvailability();
	}

	internal override void SetFirstSelect()
	{
		RefreshAvailability();
		OkButton.Select();
	}

	public void Ok_Clicked()
	{
        if (BuyCallBack == null || !gameObject.activeInHierarchy) return;
        if (!RefreshAvailability()) { GameMessages.Reject(unavailableReason()); return; }
        BuyCallBack(_view, _data);
	}

	public void Cancel_Clicked()
	{
		BuyCallBack = null;
		CloseDialog();
	}

    private void OnDisable() { BuyCallBack = null; unavailableReason = null; }

    private void Update()
    {
        if (BuyCallBack != null) RefreshAvailability();
    }

    private bool RefreshAvailability()
    {
        string reason = unavailableReason?.Invoke();
        bool available = string.IsNullOrEmpty(reason);
        PromptText.text = GameplayText.Format(prompt + (available ? "" : "\n" + reason));
        OkButton.interactable = true;
        SetNavigation();
        return available;
    }

    public void Reject(string reason)
    {
        PromptText.text = GameplayText.Format(prompt + "\n" + reason);
        GameMessages.Reject(reason);
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
