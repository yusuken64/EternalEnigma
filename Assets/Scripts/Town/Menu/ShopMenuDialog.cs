using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShopMenuDialog : Dialog
{
    public Button BuyModeButton, SellModeButton, BackButton;
    public TMPro.TMP_Text StatusText;
    public bool Selling { get; private set; }
    private TownInteractionContext context;
    private string selectedItemName;
    private int selectedIndex;
    private TMPro.TMP_Text[] controlHints;
    private bool? hintGamepad;
    private bool openingWelcome;
    private bool breadReceived;
    public override void PrepareTown(TownInteractionContext value)
    {
        context = value;
        Selling = false;
        openingWelcome = context.Services.OffersOpeningBread(context.Building);
        breadReceived = false;
        BuyModeButton.onClick.RemoveAllListeners(); SellModeButton.onClick.RemoveAllListeners();
        BackButton.onClick.RemoveAllListeners();
        BuyModeButton.onClick.AddListener(() => { openingWelcome = false; breadReceived = false; SetMode(false); });
        SellModeButton.onClick.AddListener(() => SetMode(true));
        BackButton.onClick.AddListener(Cancel_Clicked);
        controlHints = GetComponentsInChildren<MenuControlHints>(true).Select(hint => {
            hint.enabled = false;
            var label = hint.Label != null ? hint.Label : hint.GetComponent<TMPro.TMP_Text>();
            label.enableAutoSizing = false; label.fontSize = 20;
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
            label.raycastTarget = false;
            Fit(label.transform, .075f, .045f, .67f, .155f);
            return label;
        }).ToArray();
        hintGamepad = null;
        Update();
        BuyConfirmationDialog.gameObject.SetActive(false);
        ApplyPresentation();
        Setup();
    }

    public void ApplyPresentation()
    {
        Fit(StatusText.transform, .075f, .82f, .925f, .955f);
        StatusText.fontSize = 30; StatusText.enableAutoSizing = true;
        StatusText.fontSizeMin = 26; StatusText.fontSizeMax = 30;
        StatusText.textWrappingMode = TMPro.TextWrappingModes.Normal;
        Fit(BuyModeButton.transform, .075f, .705f, .485f, .79f);
        Fit(SellModeButton.transform, .515f, .705f, .925f, .79f);
        Fit(BackButton.transform, .72f, .055f, .925f, .135f);
        if (scrollView != null) Fit(scrollView.transform, .075f, .20f, .925f, .675f);
        GameUISkin.PadButtonLabel(BuyModeButton);
        GameUISkin.PadButtonLabel(SellModeButton);
        GameUISkin.PadButtonLabel(BackButton);
    }

    public void SetMode(bool selling)
    {
        Selling = selling; selectedIndex = 0; selectedItemName = null;
        Setup(); SetFirstSelect();
    }

	internal void Show()
	{
		BuyConfirmationDialog.gameObject.SetActive(false);
		this.gameObject.SetActive(true);
		Setup();
	}

	public List<ShopItemData> ShopItemDatas;

	public Transform Container;
	public ShopMenuItem ShopItemPrefab;
	public List<ShopMenuItem> ShopItems;

	public BuyConfirmationDialog BuyConfirmationDialog;

	public void Setup()
	{
        openingWelcome &= context.Services.OffersOpeningBread(context.Building);
        ApplyPresentation();
        BuyModeButton.GetComponentInChildren<TMPro.TMP_Text>().text = openingWelcome || breadReceived ? "Browse shop" : "Buy";
        BackButton.GetComponentInChildren<TMPro.TMP_Text>().text = breadReceived ? "Done" : "Back";
        SellModeButton.gameObject.SetActive(!openingWelcome && !breadReceived);
        scrollView.gameObject.SetActive(!breadReceived);
        if (breadReceived)
        {
            Fit(StatusText.transform, .075f, .42f, .925f, .955f);
            Fit(BuyModeButton.transform, .075f, .20f, .485f, .295f);
            Fit(BackButton.transform, .515f, .20f, .925f, .295f);
        }
        GameUITheme.StyleActiveTab(BuyModeButton, !Selling && !openingWelcome && !breadReceived);
        GameUITheme.StyleActiveTab(SellModeButton, Selling);
        float position=scrollView!=null && ShopItems?.Count>0?scrollView.verticalNormalizedPosition:1;
        var stock = context.Services.Shop(context.Building);
        ShopItemDatas = Selling ? context.Player.Inventory
            .Select(i => new ShopItemData(i.ItemName, TownServices.SellPrice(i)) {
                IsSale = true, BagItem = i, Remaining = i.StackStock ?? 1 }).ToList()
            : context.Services.Catalog(context.Building).Select(o => new ShopItemData(o.Item.ItemName, o.Price) {
            Remaining = stock.Stock.FirstOrDefault(s => s.ItemName == o.Item.ItemName)?.Remaining ?? 0
        }).ToList();
        if (openingWelcome) ShopItemDatas = ShopItemDatas.Where(item => item.ItemName == "Bread").ToList();
        if (breadReceived) ShopItemDatas.Clear();
        StatusText.text = GameplayText.Format($"{context.Building.DisplayName} — {context.Player.Gold:N0}G" +
            (Selling && ShopItemDatas.Count == 0 ? "\nNo items to sell." : Selling ? "\nSell a bag entry" :
                context.Services.OffersOpeningBread(context.Building) ? "\nYour first loaf of bread is free." : "\nBuy supplies"));
        if (openingWelcome) StatusText.text = "A loaf for your first journey\n<size=70%>Bread restores Hunger. Eat it from your bag.</size>";
        else if (breadReceived && !Selling) StatusText.text = "<b>Bread packed in your bag!</b>\n\n<size=80%>The baker hands you a fresh loaf.\nEat it in the dungeon to restore Hunger.\n\n" +
            (Common.Instance.CampaignContext.State.LearnedOpeningSkill ? "Next: return home and sleep until morning." : "Next: visit the trainer for your first skill.") + "</size>";
		Action<ShopMenuItem, ShopItemData> action = (view, data) =>
		{
			view.Setup(data);
            if (openingWelcome && data.ItemName == "Bread") view.ItemText.text = "Take your free bread";
            if(view.BuyButton.GetComponent<PartyMenuRow>()==null)view.BuyButton.gameObject.AddComponent<PartyMenuRow>();
			Button button = view.BuyButton;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() =>
			{
                if (openingWelcome && data.ItemName == "Bread" && data.Cost == 0)
                {
                    string unavailable = UnavailableReason(data);
                    if (unavailable != null) { GameMessages.Reject(unavailable); return; }
                    Transact(view, data);
                    return;
                }
				BuyConfirmationDialog.Setup(view, data, () => UnavailableReason(data));
				BuyConfirmationDialog.SetNavigation();
				BuyConfirmationDialog.gameObject.SetActive(true);
				BuyConfirmationDialog.BuyCallBack = Transact;

				FindFirstObjectByType<TownMenuManager>().Open(BuyConfirmationDialog);
			});

			view.SelectCallBack = () =>
			{
				selectedItemName=data.ItemName;
				selectedIndex=Mathf.Max(0,ShopItemDatas.IndexOf(data));
				ScrollToSelected(view.gameObject);
			};
		};
		ShopItems = Container.RePopulateObjects(ShopItemPrefab, ShopItemDatas, action);
        int remembered=ShopItemDatas.FindIndex(i=>i.ItemName==selectedItemName);
        selectedIndex=remembered>=0?remembered:Mathf.Clamp(selectedIndex,0,Mathf.Max(0,ShopItems.Count-1));
        Canvas.ForceUpdateCanvases();
        if(scrollView!=null)scrollView.verticalNormalizedPosition=position;
        SetNavigation();
        hintGamepad = null;
        Update();

	}

    private void Update()
    {
        bool pad = ControlDeviceState.Gamepad;
        if (hintGamepad == pad || controlHints == null) return;
        hintGamepad = pad;
        foreach (var label in controlHints)
        {
            Fit(label.transform, .075f, .045f, breadReceived ? .925f : .67f, .155f);
            if (breadReceived)
            {
                label.text = InputPrompts.Pick("Enter: choose  |  Esc: back", "A / Cross: choose  |  B / Circle: back");
                continue;
            }
            label.text = InputPrompts.Pick("Up/Down: browse  |  Left/Right: tabs\nEnter / click: choose  |  Esc: back",
                "Up/Down: browse  |  Left/Right: tabs\nA / Cross: choose  |  B / Circle: back");
        }
    }

    private string UnavailableReason(ShopItemData item)
    {
        if (item.IsSale)
        {
            var restriction = context.Services.SellRestriction(item.BagItem);
            if (restriction != null) return restriction;
            int price = TownServices.SellPrice(item.BagItem);
            if (context.Player.Gold > int.MaxValue - price) return "Your gold is full.";
            if (price != item.Cost || (item.BagItem.StackStock ?? 1) != item.Remaining)
                return "This offer changed. Choose the item again.";
            return null;
        }
        var offer = context.Services.Catalog(context.Building).FirstOrDefault(o => o.Item.ItemName == item.ItemName);
        var stock = context.Services.Shop(context.Building).Stock.FirstOrDefault(s => s.ItemName == item.ItemName);
        if (offer == null || stock == null || stock.Remaining <= 0) return "This item is sold out.";
        if (offer.Price != item.Cost) return "The price changed. Choose the item again.";
        return context.Player.Gold < offer.Price ? $"Costs {offer.Price} gold; you have {context.Player.Gold}." : null;
    }

	private void Transact(ShopMenuItem view, ShopItemData item)
	{
        string reason;
        bool success = item.IsSale ? context.Services.Sell(item.BagItem, out reason) : context.Services.Buy(context.Building, item.ItemName, out reason);
        if (!success)
        {
            if (BuyConfirmationDialog.gameObject.activeInHierarchy) BuyConfirmationDialog.Reject(reason);
            else GameMessages.Reject(reason);
            return;
        }
        if (openingWelcome && item.ItemName == "Bread") { openingWelcome = false; breadReceived = true; }
        if (BuyConfirmationDialog.gameObject.activeInHierarchy) BuyConfirmationDialog.CloseDialog();
        Setup();
        SetFirstSelect();
    }

    private void SetNavigation()
    {
        if (breadReceived)
        {
            BuyModeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = BackButton, selectOnDown = BackButton, selectOnLeft = BackButton, selectOnRight = BackButton };
            BackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = BuyModeButton, selectOnDown = BuyModeButton, selectOnLeft = BuyModeButton, selectOnRight = BuyModeButton };
            return;
        }
        var activeTab = Selling ? SellModeButton : BuyModeButton;
        var first = ShopItems.Count > 0 ? ShopItems[0].BuyButton : BackButton;
        var secondTab = SellModeButton.gameObject.activeSelf ? SellModeButton : BuyModeButton;
        BuyModeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = secondTab, selectOnDown = first, selectOnUp = BackButton };
        SellModeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = BuyModeButton, selectOnDown = first, selectOnUp = BackButton };
        for (int i = 0; i < ShopItems.Count; i++)
            ShopItems[i].BuyButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? activeTab : ShopItems[i-1].BuyButton,
                selectOnDown = i+1 == ShopItems.Count ? BackButton : ShopItems[i+1].BuyButton,
                selectOnLeft = BuyModeButton, selectOnRight = secondTab };
        BackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = activeTab,
            selectOnUp = ShopItems.Count > 0 ? ShopItems[^1].BuyButton : activeTab };
    }

	internal override void SetFirstSelect()
	{
		if (ShopItems.IsNullOrEmpty()) { (breadReceived ? BackButton : Selling ? SellModeButton : BuyModeButton).Select(); return; }
		var row=ShopItems[Mathf.Clamp(selectedIndex,0,ShopItems.Count-1)];
		row.BuyButton.Select();
		ScrollToSelected(row.BuyButton.gameObject);
	}

	public void Cancel_Clicked()
	{
		CloseDialog();
	}
}
