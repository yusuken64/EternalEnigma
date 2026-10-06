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
    public override void PrepareTown(TownInteractionContext value)
    {
        context = value;
        Selling = false;
        BuyModeButton.onClick.RemoveAllListeners(); SellModeButton.onClick.RemoveAllListeners();
        BackButton.onClick.RemoveAllListeners();
        BuyModeButton.onClick.AddListener(() => SetMode(false));
        SellModeButton.onClick.AddListener(() => SetMode(true));
        BackButton.onClick.AddListener(Cancel_Clicked);
        BuyConfirmationDialog.gameObject.SetActive(false);
        Setup();
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
        float position=scrollView!=null && ShopItems?.Count>0?scrollView.verticalNormalizedPosition:1;
        var stock = context.Services.Shop(context.Building);
        ShopItemDatas = Selling ? context.Player.Inventory.Where(context.Services.CanSell)
            .Select(i => new ShopItemData(i.ItemName, TownServices.SellPrice(i)) {
                IsSale = true, BagItem = i, Remaining = i.StackStock ?? 1 }).ToList()
            : context.Services.Catalog(context.Building).Select(o => new ShopItemData(o.Item.ItemName, o.Price) {
            Remaining = stock.Stock.FirstOrDefault(s => s.ItemName == o.Item.ItemName)?.Remaining ?? 0
        }).ToList();
        StatusText.text = $"{context.Building.DisplayName} — {context.Player.Gold:N0}G" +
            (Selling && ShopItemDatas.Count == 0 ? "\nNo items to sell." : Selling ? "\nSell a bag entry" : "\nBuy supplies");
		Action<ShopMenuItem, ShopItemData> action = (view, data) =>
		{
			view.Setup(data);
            if(view.BuyButton.GetComponent<PartyMenuRow>()==null)view.BuyButton.gameObject.AddComponent<PartyMenuRow>();
			Button button = view.BuyButton;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() =>
			{
				BuyConfirmationDialog.Setup(view, data);
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

	}

	private void Transact(ShopMenuItem view, ShopItemData item)
	{
        string reason;
        bool success = item.IsSale ? context.Services.Sell(item.BagItem, out reason) : context.Services.Buy(context.Building, item.ItemName, out reason);
        Setup();
        if (!success) TownMenu.ShowMessage(reason);
    }

    private void SetNavigation()
    {
        var activeTab = Selling ? SellModeButton : BuyModeButton;
        var first = ShopItems.Count > 0 ? ShopItems[0].BuyButton : BackButton;
        BuyModeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = SellModeButton, selectOnDown = first, selectOnUp = BackButton };
        SellModeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = BuyModeButton, selectOnDown = first, selectOnUp = BackButton };
        for (int i = 0; i < ShopItems.Count; i++)
            ShopItems[i].BuyButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? activeTab : ShopItems[i-1].BuyButton,
                selectOnDown = i+1 == ShopItems.Count ? BackButton : ShopItems[i+1].BuyButton,
                selectOnLeft = BuyModeButton, selectOnRight = SellModeButton };
        BackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = activeTab,
            selectOnUp = ShopItems.Count > 0 ? ShopItems[^1].BuyButton : activeTab };
    }

	internal override void SetFirstSelect()
	{
		if (ShopItems.IsNullOrEmpty()) { (Selling ? SellModeButton : BuyModeButton).Select(); return; }
		var row=ShopItems[Mathf.Clamp(selectedIndex,0,ShopItems.Count-1)];
		row.BuyButton.Select();
		ScrollToSelected(row.BuyButton.gameObject);
	}

	public void Cancel_Clicked()
	{
		CloseDialog();
	}
}
