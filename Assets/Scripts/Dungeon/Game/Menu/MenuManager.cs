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
	public TargetDialog TargetDialog;
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
		AllyActionDialog.gameObject.SetActive(false);
		SkillDialog.gameObject.SetActive(false);
		TargetDialog.gameObject.SetActive(false);
	}

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
				bool canOpenMenu = Game.Instance.PlayerController.CanOpenMenu();
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
				bool canOpenMenu = Game.Instance.PlayerController.CanOpenMenu();
				if (canOpenMenu)
				{
					OpenSkillsMenu(Game.Instance.PlayerController.ControlledAlly);
					return;
				}
			}
			else
			{
				CloseAllMenus();
				return;
			}
		}

		// UI buttons receive Submit once through EventSystem. Only world-target
		// selection needs a manual confirm path.
		if (Common.Instance.MenuInputHandler.SubmitMenuInput &&
			Game.Instance.PlayerController.CurrentControlMode == PlayerControlMode.TargetSelecting)
		{
			TargetDialog.ConfirmTarget();
			return;
		}

		if (Common.Instance.MenuInputHandler.CancelMenuInput && DialogStack.Count > 0 &&
            Game.Instance.PlayerController.CurrentControlMode == PlayerControlMode.TargetSelecting)
        {
			var top = DialogStack.Peek();
			Close(top);
			return;
        }
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

	private void OpenMenu()
	{
		Common.Instance.MenuInputHandler.SwitchToUIInput();
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
		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
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
		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

	public void OpenAllyMenu(Ally ally)
	{
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		this.gameObject.SetActive(true);
		MenuManager.Open(AllyActionDialog);
		AllyActionDialog.Setup(ally);
		CurrentDialog = AllyActionDialog;
		AllyActionDialog.CloseAction = () =>
		{
			AllyActionDialog.Close();
		};
		AllyActionDialog.SetNavigation();
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

		Opened = true;
		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

	public void OpenSkillsMenu(Character character)
	{
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		this.gameObject.SetActive(true);
		MenuManager.Open(SkillDialog);
		SkillDialog.Setup(character);
		CurrentDialog = SkillDialog;
		SkillDialog.CloseAction = () =>
		{
			SkillDialog.Close();
		};
		SkillDialog.SetNavigation();
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

		Opened = true;
		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

	public void OpenTargetingMenu(Character character, Skill skill)
	{
		if (!character.CanCast(skill, out _)) return;
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		this.gameObject.SetActive(true);
		MenuManager.Open(TargetDialog);
		TargetDialog.Setup(character, skill);
		CurrentDialog = TargetDialog;
		TargetDialog.CloseAction = () =>
		{
			TargetDialog.Close();
			//CurrentDialog = null;
			//CloseAllMenus();
		};
		TargetDialog.SetNavigation();
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

		Opened = true;
		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

	internal void ShowYesNoDialog(string prompt, Action yesAction, Action noAction)
	{
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		this.gameObject.SetActive(true);
		MenuManager.Open(StairDialog);
		StairDialog.Setup(prompt, yesAction, noAction);
		CurrentDialog = StairDialog;
		AudioManager.Instance.SoundEffects.Pause.PlayAsSound();

		Opened = true;
		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

	public Action LateAction;

	private void LateUpdate()
	{
		LateAction?.Invoke();
		LateAction = null;
	}

	public static void Open(Dialog dialog)
	{
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
		Common.Instance.MenuInputHandler.SubmitMenuInput = false;

		if (MenuManager.Instance.DialogStack.Count > 0)
		{
			MenuManager.Instance.DialogStack.Peek().SaveSelection();
		}

		if (MenuManager.Instance.DialogStack.Contains(dialog)) return;
		dialog.gameObject.SetActive(true);
		MenuManager.Instance.DialogStack.Push(dialog);
		MenuUIInputModule.Active?.PushDialog(dialog, dialog.transform, back: () => Close(dialog));

		MenuManager.Instance.LateAction = () =>
		{
			dialog.SetFirstSelect();
		};
	}

	public static void Close(Dialog dialog)
	{
		if (MenuManager.Instance.DialogStack.Count == 0 || MenuManager.Instance.DialogStack.Peek() != dialog) return;
		AudioManager.Instance.SoundEffects.Unpause.PlayAsSound();
		dialog.gameObject.SetActive(false);
		MenuUIInputModule.Active?.PopDialog(dialog);
		MenuManager.Instance.DialogStack.Pop();
		dialog.CloseAction?.Invoke();

		if (MenuManager.Instance.DialogStack.Count <= 0)
		{
			MenuManager.Instance.CloseAllMenus();
			return;
		}

		var top = MenuManager.Instance.DialogStack.Peek();
		MenuManager.Instance.CurrentDialog = top;
		MenuManager.Instance.LateAction = () =>
		{
			top.RestoreSelect();
		};
	}
}
