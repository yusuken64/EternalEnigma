using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuManager : SingletonMonoBehaviour<MenuManager>
{
	public EventSystem EventSystem;
	public InventoryMenu InventoryMenu;
	public ActionDialog ActionDialog;
	public AllyActionDialog AllyActionDialog;
	public bool Opened;

	public Stack<Dialog> DialogStack = new();

	protected override void Initialize()
	{
		base.Initialize();
		InventoryMenu.gameObject.SetActive(false);
		ActionDialog.gameObject.SetActive(false);
	}

	private void Update()
	{
		if (MenuInputHandler.Instance.MenuOpenClosedInput ||
			PlayerController.MenuOpenClosedInput)
		{
			if (!Opened)
			{
				bool canOpenMenu = Game.Instance.PlayerCharacter.CanOpenMenu();
				if (canOpenMenu)
				{
					OpenMenu();
				}
			}
			else
			{
				CloseMenu();
			}
		}

		if (MenuInputHandler.Instance.SubmitAction.WasPerformedThisFrame())
		{
			if (DialogStack.Count > 0)
			{
				DialogStack.Peek().Submit();
			}
		}
	}

	internal void CloseMenu()
	{
		InventoryMenu.gameObject.SetActive(false);
		ActionDialog.gameObject.SetActive(false);
		AllyActionDialog.gameObject.SetActive(false);
		AllyActionDialog.DynamicActionDialog.gameObject.SetActive(false);
		DialogStack.Clear();

		Opened = false;
	}

	private void OpenMenu()
	{
		MenuManager.Open(InventoryMenu);

		//TODO refactor to InventoryMenu.OpenMenu
		var items = Game.Instance.PlayerCharacter.Inventory.InventoryItems;
		InventoryMenu.Setup(items);
		InventoryMenu.SetNavigation();

		Opened = true;
	}

	public void OpenAllyMenu(Ally ally)
	{
		this.gameObject.SetActive(true);
		MenuManager.Open(AllyActionDialog);
		AllyActionDialog.Setup(ally);
		AllyActionDialog.SetNavigation();

		Opened = true;
	}

	public Action LateAction;

	private void LateUpdate()
	{
		LateAction?.Invoke();
		LateAction = null;
	}

	internal static void Open(Dialog dialog)
	{
		if (MenuManager.Instance.DialogStack.Count > 0)
		{
			MenuManager.Instance.DialogStack.Peek().SaveSelection();
		}

		dialog.gameObject.SetActive(true);
		MenuManager.Instance.DialogStack.Push(dialog);

		MenuManager.Instance.LateAction = () =>
		{
			dialog.SetFirstSelect();
		};
	}

	internal static void Close(Dialog dialog)
	{
		dialog.gameObject.SetActive(false);
		MenuManager.Instance.DialogStack.Pop();

		if (MenuManager.Instance.DialogStack.Count <= 0)
		{
			return;
		}

		var top = MenuManager.Instance.DialogStack.Peek();
		MenuManager.Instance.LateAction = () =>
		{
			top.RestoreSelect();
		};
	}
}
