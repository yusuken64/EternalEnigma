using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShopMenuDialog : Dialog
{
    private TownInteractionContext context;
    private string selectedItemName;
    private int selectedIndex;
    public override void PrepareTown(TownInteractionContext value)
    {
        context = value;
        BuyConfirmationDialog.gameObject.SetActive(false);
        Setup();
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
        ShopItemDatas = context.Building.ShopCatalog.Select(o => new ShopItemData(o.Item.ItemName, o.Price) {
            Remaining = stock.Stock.FirstOrDefault(s => s.ItemName == o.Item.ItemName)?.Remaining ?? 0
        }).ToList();
		Action<ShopMenuItem, ShopItemData> action = (view, data) =>
		{
			view.Setup(data);
            if(view.BuyButton.GetComponent<PartyMenuRow>()==null)view.BuyButton.gameObject.AddComponent<PartyMenuRow>();
			Button button = view.GetComponent<Button>();
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() =>
			{
				BuyConfirmationDialog.Setup(view, data);
				BuyConfirmationDialog.SetNavigation();
				BuyConfirmationDialog.gameObject.SetActive(true);
				BuyConfirmationDialog.BuyCallBack = BuyItem;

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

	}

	private void BuyItem(ShopMenuItem view, ShopItemData item)
	{
        if (!context.Services.Buy(context.Building, item.ItemName, out var reason)) TownMenu.ShowMessage(reason);
        Setup();
    }

	internal override void SetFirstSelect()
	{
		if (ShopItems.IsNullOrEmpty()) { return; }
		var row=ShopItems[Mathf.Clamp(selectedIndex,0,ShopItems.Count-1)];
		row.BuyButton.Select();
		ScrollToSelected(row.BuyButton.gameObject);
	}

	public void Cancel_Clicked()
	{
		CloseDialog();
	}
}
