using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryMenu : Dialog
{
	public Transform MenuItemContainer;
	public InventoryMenuItem InventoryMenuItemPrefab;
	public ActionDialog ActionDialog;

	public List<InventoryMenuItem> InventoryMenuItems { get; private set; }

	public void Setup(List<InventoryItem> Items)
	{
		Action<InventoryMenuItem, InventoryItem> action = (view, data) =>
		{
			view.Setup(data);
			view.GetComponent<Button>().onClick.AddListener(() =>
			{
				ActionDialog.Setup(view, data);
				ActionDialog.SetNavigation();
				ActionDialog.gameObject.SetActive(true);

				MenuManager.Open(ActionDialog);
			});
		};
		InventoryMenuItems = MenuItemContainer.RePopulateObjects(InventoryMenuItemPrefab, Items, action);

		SetNavigation();
	}

	public void SetNavigation()
	{
		for (int i = 0; i < InventoryMenuItems.Count; i++)
		{
			InventoryMenuItem item = InventoryMenuItems[i];

			Navigation customNav = new Navigation();
			customNav.mode = Navigation.Mode.Explicit;
			customNav.selectOnDown = InventoryMenuItems[(i + 1) % InventoryMenuItems.Count];
			customNav.selectOnUp = InventoryMenuItems[(i - 1 + InventoryMenuItems.Count) % InventoryMenuItems.Count];
			item.navigation = customNav;
		}
	}

	internal override void SetFirstSelect()
	{
		if (InventoryMenuItems.Count > 0)
		{
			InventoryMenuItems[0].Select();
		}
	}
}
