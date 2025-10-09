using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class OverworldMenuManager : SingletonMonoBehaviour<OverworldMenuManager>
{
	public EventSystem EventSystem;
	public bool Opened;

	public Stack<Dialog> DialogStack = new();
	public Dialog CurrentDialog;

	private float confirmCooldown;
	private float confirmCooldownStart = 0.2f;

	protected override void Initialize()
	{
		base.Initialize();
	}

	private void Update()
	{
		confirmCooldown -= Time.deltaTime;
		if (confirmCooldown > 0) { return; }
		if (//MenuInputHandler.Instance.MenuOpenClosedInput ||
			PlayerInputHandler.Instance.menuPressed)
		{
			if (!Opened)
			{
				bool canOpenMenu = Instance.DialogStack.Count == 0;
				if (canOpenMenu)
				{
					OpenMenu();
					return;
				}
			}
			else
			{
				Close(CurrentDialog);
				CurrentDialog.CloseAction?.Invoke();
				return;
			}
		}
		//else if (MenuInputHandler.Instance.OpenSkillMenuInput)
		//{
		//	if (!Opened)
		//	{
		//		bool canOpenMenu = Game.Instance.PlayerController.CanOpenMenu();
		//		if (canOpenMenu)
		//		{
		//			OpenSkillsMenu(Game.Instance.PlayerController.ControlledAlly);
		//			return;
		//		}
		//	}
		//	else
		//	{
		//		CloseAllMenus();
		//		return;
		//	}
		//}

		//if (MenuInputHandler.Instance.SubmitMenuInput)
		//{
		//	if (Game.Instance.PlayerController.CurrentControlMode == PlayerControlMode.FollowAlly)
		//	{
		//		if (DialogStack.Count > 0)
		//		{
		//			DialogStack.Peek().Submit();
		//			return;
		//		}
		//	}
		//	else
		//	{
		//		TargetDialog.ConfirmTarget();
		//		CloseAllMenus();
		//		return;
		//	}
		//}

		if (MenuInputHandler.Instance.CancelMenuInput && DialogStack.Count > 0)
		{
			var top = DialogStack.Peek();
			Close(top);
			return;
		}
	}

	private void OpenMenu()
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		var overworldPlayer = FindFirstObjectByType<OverworldPlayer>();

		OverworldMenuManager.Open(overworldMenu.InventoryMenu);
		List<InventoryItem> inventoryItems = overworldPlayer.Inventory;
		overworldMenu.InventoryMenu.SetupOverworld(inventoryItems, overworldPlayer.ControllingOverworldAlly);
		overworldMenu.InventoryMenu.CloseAction = () =>
		{
			overworldMenu.InventoryMenu.Close();
		};
	}

	internal void CloseMenu()
	{
		DialogStack.Clear();
		Opened = false;
	}

	public Action LateAction;

	private void LateUpdate()
	{
		LateAction?.Invoke();
		LateAction = null;
	}

	internal static void Open(Dialog dialog)
	{
		if (OverworldMenuManager.Instance.DialogStack.Count > 0)
		{
			OverworldMenuManager.Instance.DialogStack.Peek().SaveSelection();
		}

		dialog.gameObject.SetActive(true);
		OverworldMenuManager.Instance.DialogStack.Push(dialog);
		Instance.CurrentDialog = dialog;
		OverworldMenuManager.Instance.Opened = true;

		OverworldMenuManager.Instance.LateAction = () =>
		{
			dialog.SetFirstSelect();
		};
	}

	internal static void Close(Dialog dialog)
	{
		dialog.gameObject.SetActive(false);
		OverworldMenuManager.Instance.DialogStack.Pop();

		if (OverworldMenuManager.Instance.DialogStack.Count <= 0)
		{
			OverworldMenuManager.Instance.Opened = false;
			return;
		}

		var top = OverworldMenuManager.Instance.DialogStack.Peek();
		Instance.CurrentDialog = top;
		OverworldMenuManager.Instance.LateAction = () =>
		{
			top.RestoreSelect();
		};
	}

	internal void CloseAllMenus()
	{
		CurrentDialog?.CloseAction?.Invoke();
		CurrentDialog = null;
		FindFirstObjectByType<OverworldMenu>().InventoryMenu.gameObject.SetActive(false);
		DialogStack.Clear();

		Opened = false;
		MenuInputHandler.Instance.CloseMenu();
		confirmCooldown = confirmCooldownStart;
	}

}
