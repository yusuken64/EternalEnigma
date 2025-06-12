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
	public SkillDialog SkillDialog;
	public bool Opened;
	public Dialog CurrentDialog;
	public StairConfirm StairDialog;

	public Stack<Dialog> DialogStack = new();

	public GameObject TargetArrow;

	protected override void Initialize()
	{
		base.Initialize();
		InventoryMenu.gameObject.SetActive(false);
		ActionDialog.gameObject.SetActive(false);
		SkillDialog.gameObject.SetActive(false);
	}

	private void Update()
	{
		if (MenuInputHandler.Instance.MenuOpenClosedInput)
		{
			if (!Opened)
			{
				bool canOpenMenu = Game.Instance.PlayerController.CanOpenMenu();
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
		else if (MenuInputHandler.Instance.OpenSkillMenuInput)
		{
			if (!Opened)
			{
				bool canOpenMenu = Game.Instance.PlayerController.CanOpenMenu();
				if (canOpenMenu)
				{
					OpenSkillsMenu(Game.Instance.PlayerController.ControlledAlly);
				}
			}
			else
			{
				CloseMenu();
			}
		}

		if (MenuInputHandler.Instance.SubmitAction.WasPerformedThisFrame())
		{
			if (Game.Instance.PlayerController.CurrentControlMode == PlayerControlMode.FollowAlly)
			{
				if (DialogStack.Count > 0)
				{
					DialogStack.Peek().Submit();
				}
			}
			else
			{
				Game.Instance.PlayerController.ConfirmTarget();
				CloseMenu();
			}
		}

		if (MenuInputHandler.Instance.CancelMenuInput && DialogStack.Count > 0)
        {
			var top = DialogStack.Peek();
			Close(top);
        }
	}

	internal void CloseMenu()
	{
		CurrentDialog?.CloseAction?.Invoke();
		CurrentDialog = null;
		InventoryMenu.gameObject.SetActive(false);
		ActionDialog.gameObject.SetActive(false);
		SkillDialog.gameObject.SetActive(false);
		AllyActionDialog.gameObject.SetActive(false);
		StairDialog.gameObject.SetActive(false);
		AllyActionDialog.DynamicActionDialog.gameObject.SetActive(false);
		DialogStack.Clear();

		Opened = false;
		MenuInputHandler.Instance.CloseMenu();
	}

	private void OpenMenu()
	{
		MenuInputHandler.Instance.OpenMenu();
		MenuManager.Open(InventoryMenu);
		var equippedItems = Game.Instance.PlayerController.ControlledAlly.Equipment.GetEquippedItems();
		var items = Game.Instance.PlayerController.Inventory.InventoryItems;
		var allItems = equippedItems.Concat(items)
			.Where(x => x != null)
			.ToList();

		InventoryMenu.Setup(allItems, Game.Instance.PlayerController.ControlledAlly);
		InventoryMenu.SetNavigation();
		CurrentDialog = InventoryMenu;
		InventoryMenu.CloseAction = () =>
		{
			InventoryMenu.Close();
		};
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

		Opened = true;
	}

	public void OpenInventoryAs(Ally ally)
    {
        MenuManager.Open(InventoryMenu);
		var equippedItems = ally.Equipment.GetEquippedItems();
		var items = Game.Instance.PlayerController.Inventory.InventoryItems;
		var allItems = equippedItems.Concat(items)
			.Where(x => x != null)
			.ToList();

        InventoryMenu.Setup(allItems, ally);
        InventoryMenu.SetNavigation();
        CurrentDialog = InventoryMenu;
        InventoryMenu.CloseAction = () =>
        {
            InventoryMenu.Close();
        };
        AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

        Opened = true;
	}

	public void OpenAllyMenu(Ally ally)
	{
		this.gameObject.SetActive(true);
		MenuManager.Open(AllyActionDialog);
		AllyActionDialog.Setup(ally);
		CurrentDialog = InventoryMenu;
		AllyActionDialog.CloseAction = () =>
		{
			AllyActionDialog.Close();
		};
		AllyActionDialog.SetNavigation();
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

		Opened = true;
	}

	public void OpenSkillsMenu(Character character)
	{
		MenuInputHandler.Instance.OpenMenu();
		this.gameObject.SetActive(true);
		MenuManager.Open(SkillDialog);
		SkillDialog.Setup(character);
		CurrentDialog = InventoryMenu;
		SkillDialog.CloseAction = () =>
		{
			SkillDialog.Close();
		};
		SkillDialog.SetNavigation();
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

		Opened = true;
	}

	internal void ShowYesNoDialog(Action yesAction, Action noAction)
	{
		MenuInputHandler.Instance.OpenMenu();
		this.gameObject.SetActive(true);
		MenuManager.Open(StairDialog);
		StairDialog.Setup(yesAction, noAction);
		CurrentDialog = StairDialog;
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

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
		AudioManager.Instance.SoundEffects.Unpause.PlayAsSound();
		dialog.gameObject.SetActive(false);
		MenuManager.Instance.DialogStack.Pop();

		if (MenuManager.Instance.DialogStack.Count <= 0)
		{
			MenuManager.Instance.CloseMenu();
			return;
		}

		var top = MenuManager.Instance.DialogStack.Peek();
		MenuManager.Instance.LateAction = () =>
		{
			top.RestoreSelect();
		};
	}
}
