using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShopMenuDialog : Dialog
{
	//this generates the shop items
	//this should persist until the player enters the dungeon
	//where is the cost defined?
	//in the item defintion?
	public void GenerateShop()
	{
		ShopItemDatas = new List<ShopItemData>()
		{
			new ShopItemData("Potion", 100),
			new ShopItemData("Bread", 200),
			new ShopItemData("Wooden Club", 50),
			new ShopItemData("Gift", 3100),
			new ShopItemData("Bits", 200),
			new ShopItemData("Offering", 300),
		};
	}

	private void Awake()
	{
		GenerateShop();
	}

	internal void Show()
	{
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

				OverworldMenuManager.Open(BuyConfirmationDialog);
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
		var player = FindAnyObjectByType<OverworldPlayer>();

		if (player.Gold >= item.Cost &&
			ShopItemDatas.Contains(item))
		{
			ShopItemDatas.Remove(item);
			player.Gold -= item.Cost;
			player.Inventory.Add(item.ItemName);

			Setup();
		}
	}

	internal override void SetFirstSelect()
	{
		ShopItems[0].Select();
		ScrollToSelected(ShopItems[0].gameObject);
	}

	public void Cancel_Clicked()
	{
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}
}