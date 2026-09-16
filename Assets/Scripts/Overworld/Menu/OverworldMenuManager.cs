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


	private void Update()
	{
		if (MenuUIInputModule.Active?.InputConsumed == true || Common.Instance.GlobalSettings.IsOpen) return;
		if (Opened && Common.Instance.MenuInputHandler.OptionInput && !Common.Instance.MenuInputHandler.CancelMenuInput)
		{
			Common.Instance.GlobalSettings.ShowDialog();
			return;
		}
		if (Common.Instance.MenuInputHandler.MenuOpenClosedInput)
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
				CloseAllMenus();
				return;
			}
		}
		else if (Common.Instance.MenuInputHandler.OpenSkillMenuInput)
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

	}

	private void OpenMenu()
	{
		var overworldMenu = FindFirstObjectByType<OverworldMenu>();
		var overworldPlayer = FindFirstObjectByType<OverworldPlayer>();

		Open(overworldMenu.InventoryMenu);
		List<InventoryItem> inventoryItems = overworldPlayer.Inventory;
		overworldMenu.InventoryMenu.SetupOverworld(inventoryItems, overworldPlayer.ControllingOverworldAlly);
		overworldMenu.InventoryMenu.SetNavigation();
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
		overworldMenu.SkillDialog.SetNavigation();
		overworldMenu.SkillDialog.CloseAction = () =>
		{
			// This is an informational skill list; it owns no inventory face camera.
		};
	}

	internal void CloseMenu()
	{
		CloseAllMenus();
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

		if (DialogStack.Contains(dialog)) return;
		dialog.gameObject.SetActive(true);
		DialogStack.Push(dialog);
		MenuUIInputModule.Active?.PushDialog(dialog, dialog.transform, back: () => Close(dialog));
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
		if (DialogStack.Count == 0 || DialogStack.Peek() != dialog) return;
		dialog.gameObject.SetActive(false);
		MenuUIInputModule.Active?.PopDialog(dialog);
		DialogStack.Pop();
		dialog.CloseAction?.Invoke();

		if (DialogStack.Count <= 0)
		{
			Common.Instance.MenuInputHandler.SwitchToPlayerInput();
			Opened = false;
			CurrentDialog = null;
			LateAction = null;
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
		while (DialogStack.Count > 0)
        {
            var dialog = DialogStack.Pop();
            dialog.gameObject.SetActive(false);
            MenuUIInputModule.Active?.PopDialog(dialog);
            dialog.CloseAction?.Invoke();
        }
        CurrentDialog = null;
        LateAction = null;

		Opened = false;
		Common.Instance.MenuInputHandler.SwitchToPlayerInput();
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

}
