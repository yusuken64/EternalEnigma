using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class OverworldMenuManager : MonoBehaviour
{
	public EventSystem EventSystem;
	public bool Opened;

	public Stack<Dialog> DialogStack = new();
	public Dialog CurrentDialog;

	private float confirmCooldown;
	private float confirmCooldownStart = 0.2f;

	private void Update()
	{
		confirmCooldown -= Time.deltaTime;
		if (confirmCooldown > 0) { return; }
		if (Common.Instance.MenuInputHandler.MenuOpenClosedInput ||
			PlayerInputHandler.Instance.menuPressed)
		{
			if (!Opened)
			{
				bool canOpenMenu = DialogStack.Count == 0;
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
		else if (PlayerInputHandler.Instance.skillsPressed)
		{
			if (!Opened)
			{
				bool canOpenMenu = DialogStack.Count == 0;
				if (canOpenMenu)
				{
					OpenSkills();
					return;
				}
			}
			else
			{
				CloseAllMenus();
				return;
			}
		}

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

		if (Common.Instance.MenuInputHandler.CancelMenuInput && DialogStack.Count > 0)
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

		Open(overworldMenu.InventoryMenu);
		List<InventoryItem> inventoryItems = overworldPlayer.Inventory;
		overworldMenu.InventoryMenu.SetupOverworld(inventoryItems, overworldPlayer.ControllingOverworldAlly);
		overworldMenu.InventoryMenu.CloseAction = () =>
		{
			overworldMenu.InventoryMenu.Close();
		};
	}

	private void OpenSkills ()
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		var overworldPlayer = FindFirstObjectByType<OverworldPlayer>();

		Open(overworldMenu.SkillDialog);
		List<InventoryItem> inventoryItems = overworldPlayer.Inventory;
		overworldMenu.SkillDialog.gameObject.SetActive(true);
		overworldMenu.SkillDialog.SetupOverworld(overworldPlayer.ControllingOverworldAlly);
		overworldMenu.SkillDialog.CloseAction = () =>
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

	internal void Open(Dialog dialog)
	{
		if (DialogStack.Count > 0)
		{
			DialogStack.Peek().SaveSelection();
		}

		dialog.gameObject.SetActive(true);
		DialogStack.Push(dialog);
		CurrentDialog = dialog;
		Opened = true;

		LateAction = () =>
		{
			dialog.SetFirstSelect();
		};

		Common.Instance.MenuInputHandler.SwitchToUIInput();
	}

	internal void Close(Dialog dialog)
	{
		dialog.gameObject.SetActive(false);
		dialog.CloseAction?.Invoke();
		DialogStack.Pop();

		if (DialogStack.Count <= 0)
		{
			Common.Instance.MenuInputHandler.SwitchToPlayerInput();
			Opened = false;
			return;
		}

		var top = DialogStack.Peek();
		CurrentDialog = top;
		LateAction = () =>
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
		Common.Instance.MenuInputHandler.SwitchToPlayerInput();
		confirmCooldown = confirmCooldownStart;
	}

}
