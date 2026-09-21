using JuicyChickenGames.Menu;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TownMenuManager : MonoBehaviour
{
	public EventSystem EventSystem;
    private readonly DialogController dialogs = new();
    public bool Opened => dialogs.Opened;
    public Stack<Dialog> DialogStack => dialogs.Stack;
    public Dialog CurrentDialog => dialogs.Current;

	private void Update()
	{
		if (AutoplayRunner.Active != null) return;
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
				bool canOpenMenu = DialogStack.Count == 0 && FindFirstObjectByType<Town>().IsReady;
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
				bool canOpenMenu = DialogStack.Count == 0 && FindFirstObjectByType<Town>().IsReady;
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
		var townMenu = FindFirstObjectByType<TownMenu>();
		var townPlayer = FindFirstObjectByType<TownPlayer>();

		Open(townMenu.InventoryMenu);
		List<InventoryItem> inventoryItems = townPlayer.ControllingTownAlly.Equipment.GetEquippedItems().Cast<InventoryItem>().Concat(townPlayer.Inventory).ToList();
		townMenu.InventoryMenu.SetupTown(inventoryItems, townPlayer.ControllingTownAlly);
		townMenu.InventoryMenu.SetNavigation();
		townMenu.InventoryMenu.CloseAction = () =>
		{
			townMenu.InventoryMenu.Close();
		};
	}

	private void OpenSkills ()
	{
		var townMenu = FindFirstObjectByType<TownMenu>();
		var townPlayer = FindFirstObjectByType<TownPlayer>();

		Open(townMenu.SkillDialog);
		townMenu.SkillDialog.gameObject.SetActive(true);
		townMenu.SkillDialog.SetupTown(townPlayer.ControllingTownAlly);
		townMenu.SkillDialog.SetNavigation();
	}

	internal void CloseMenu()
	{
		CloseAllMenus();
	}

    private void LateUpdate() => dialogs.Tick();
    internal void Open(Dialog dialog) => dialogs.Open(dialog);
    internal void Close(Dialog dialog) => dialogs.Close(dialog);
    internal void CloseAllMenus() => dialogs.CloseAll();
}
