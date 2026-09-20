using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShopMenuDialog : Dialog
{
    private TownInteractionContext context;
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
        var stock = context.Services.Shop(context.Building);
        ShopItemDatas = context.Building.ShopCatalog.Select(o => new ShopItemData(o.Item.ItemName, o.Price) {
            Remaining = stock.Stock.FirstOrDefault(s => s.ItemName == o.Item.ItemName)?.Remaining ?? 0
        }).ToList();
		Action<ShopMenuItem, ShopItemData> action = (view, data) =>
		{
			view.Setup(data);
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
				ScrollToSelected(view.gameObject);
			};
		};
		ShopItems = Container.RePopulateObjects(ShopItemPrefab, ShopItemDatas, action);
	}

	private void BuyItem(ShopMenuItem view, ShopItemData item)
	{
        if (!context.Services.Buy(context.Building, item.ItemName, out var reason)) TownMenu.ShowMessage(reason);
        Setup();
    }

	internal override void SetFirstSelect()
	{
		if (ShopItems.IsNullOrEmpty()) { return; }
		ShopItems[0].BuyButton.Select();
		ScrollToSelected(ShopItems[0].BuyButton.gameObject);
	}

	public void Cancel_Clicked()
	{
		CloseDialog();
	}
}