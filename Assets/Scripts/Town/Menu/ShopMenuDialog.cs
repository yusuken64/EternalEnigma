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
    public RectTransform DescriptionPanel;
    public TMPro.TMP_Text DescriptionText;
    public bool Selling { get; private set; }
    private TownInteractionContext context;
    private string selectedItemName;
    private int selectedIndex;
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
        BuyConfirmationDialog.gameObject.SetActive(false);
        ApplyPresentation();
        Setup();
    }

    public void ApplyPresentation()
    {
        var panel = StatusText.transform.parent;
        UseGameplayDock(panel);
        Fit(panel, .56f, .05f, .98f, .88f);
        foreach (var hint in GetComponentsInChildren<MenuControlHints>(true))
            hint.gameObject.SetActive(false);
        Fit(StatusText.transform, .075f, .865f, .925f, .96f);
        StatusText.fontSize = 30; StatusText.enableAutoSizing = true;
        StatusText.fontSizeMin = 22; StatusText.fontSizeMax = 30;
        StatusText.textWrappingMode = TMPro.TextWrappingModes.Normal;
        Fit(BuyModeButton.transform, .075f, .765f, .485f, .845f);
        Fit(SellModeButton.transform, .515f, .765f, .925f, .845f);
        Fit(BackButton.transform, .55f, .045f, .925f, .125f);
        if (scrollView != null) Fit(scrollView.transform, .075f, .15f, .925f, .735f);
        if (DescriptionPanel != null)
        {
            Fit(DescriptionPanel, .24f, .48f, .54f, .88f);
            Fit(DescriptionText.transform, .08f, .10f, .92f, .90f);
            DescriptionText.fontSize = DescriptionText.fontSizeMax = 24;
            DescriptionText.fontSizeMin = 18; DescriptionText.enableAutoSizing = true;
            DescriptionText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            DescriptionText.alignment = TMPro.TextAlignmentOptions.TopLeft;
            DescriptionText.raycastTarget = false;
        }
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
            Fit(StatusText.transform.parent, .18f, .28f, .82f, .80f);
            Fit(StatusText.transform, .075f, .32f, .925f, .94f);
            Fit(BuyModeButton.transform, .075f, .12f, .485f, .25f);
            Fit(BackButton.transform, .515f, .12f, .925f, .25f);
        }
        GameUITheme.StyleActiveTab(BuyModeButton, !Selling && !openingWelcome && !breadReceived);
        GameUITheme.StyleActiveTab(SellModeButton, Selling);
        float position=scrollView!=null && ShopItems?.Count>0?scrollView.verticalNormalizedPosition:1;
        var stock = context.Services.Shop(context.Building);
        ShopItemDatas = Selling ? context.Player.Inventory
            .Select(i => new ShopItemData(i.ItemName, TownServices.SellPrice(i)) {
                IsSale = true, BagItem = i, Definition = i.ItemDefinition, Remaining = i.StackStock ?? 1 }).ToList()
            : context.Services.Catalog(context.Building).Select(o => new ShopItemData(o.Item.ItemName, o.Price) {
            Definition = o.Item, Remaining = stock.Stock.FirstOrDefault(s => s.ItemName == o.Item.ItemName)?.Remaining ?? 0
        }).ToList();
        if (openingWelcome) ShopItemDatas = ShopItemDatas.Where(item => item.ItemName == "Bread").ToList();
        if (breadReceived) ShopItemDatas.Clear();
        StatusText.text = GameplayText.Format(context.Building.DisplayName +
            (Selling && ShopItemDatas.Count == 0 ? "\n<size=75%>No items to sell.</size>" : ""));
        if (openingWelcome) StatusText.text = "A loaf for your first journey\n<size=70%>Bread restores Hunger. Eat it from your bag.</size>";
        else if (breadReceived && !Selling) StatusText.text = "<b>Bread packed in your bag!</b>\n\n<size=80%>The baker hands you a fresh loaf.\nEat it in the dungeon to restore Hunger.\n\n" +
            (Common.Instance.CampaignContext.State.LearnedOpeningSkill ? "Next: return home and sleep until morning." : "Next: visit the trainer for your first skill.") + "</size>";
        Canvas.ForceUpdateCanvases();
		Action<ShopMenuItem, ShopItemData> action = (view, data) =>
		{
			view.Setup(data);
            if (openingWelcome && data.ItemName == "Bread") view.ItemText.text = "Take your free bread";
            if(view.BuyButton.GetComponent<PartyMenuRow>()==null)view.BuyButton.gameObject.AddComponent<PartyMenuRow>();
			Button button = view.BuyButton;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() =>
			{
				BuyConfirmationDialog.Setup(view, data, () => UnavailableReason(data));
				BuyConfirmationDialog.SetNavigation();
				BuyConfirmationDialog.gameObject.SetActive(true);
				BuyConfirmationDialog.BuyCallBack = Transact;

				FindFirstObjectByType<TownMenuManager>().Open(BuyConfirmationDialog);
			});

			view.SelectCallBack = eventData =>
			{
				selectedItemName=data.ItemName;
				selectedIndex=Mathf.Max(0,ShopItemDatas.IndexOf(data));
                ShowDescription(data);
                if (eventData is UnityEngine.EventSystems.PointerEventData) StopAutoScroll();
				else ScrollToSelected(view.gameObject);
			};
		};
		ShopItems = Container.RePopulateObjects(ShopItemPrefab, ShopItemDatas, action);
        int remembered=ShopItemDatas.FindIndex(i=>i.ItemName==selectedItemName);
        selectedIndex=remembered>=0?remembered:Mathf.Clamp(selectedIndex,0,Mathf.Max(0,ShopItems.Count-1));
        Canvas.ForceUpdateCanvases();
        if(scrollView!=null)scrollView.verticalNormalizedPosition=position;
        SetNavigation();
        ShowDescription(ShopItemDatas.Count > 0 ? ShopItemDatas[selectedIndex] : null);

	}

    private void ShowDescription(ShopItemData item)
    {
        if (DescriptionPanel == null) return;
        DescriptionPanel.gameObject.SetActive(item != null && !breadReceived);
        DescriptionText.text = item == null ? "" : GameplayText.Format(string.Join("\n\n",
            new[] { $"<b>{GameplayText.Name(item.ItemName)}</b>", item.Definition?.Description }
                .Where(text => !string.IsNullOrWhiteSpace(text))));
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
