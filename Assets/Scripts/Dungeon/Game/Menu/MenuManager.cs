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

	private float confirmCooldown;
	private float confirmCooldownStart = 0.2f;

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
		confirmCooldown -= Time.deltaTime;
		if (confirmCooldown > 0) { return; }
		if (MenuInputHandler.Instance.MenuOpenClosedInput)
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
		else if (MenuInputHandler.Instance.OpenSkillMenuInput)
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

		if (MenuInputHandler.Instance.SubmitMenuInput)
		{
			if (Game.Instance.PlayerController.CurrentControlMode == PlayerControlMode.FollowAlly)
			{
				if (DialogStack.Count > 0)
				{
					DialogStack.Peek().Submit();
					return;
				}
			}
			else
			{
				TargetDialog.ConfirmTarget();
				CloseAllMenus();
				return;
			}
		}

		if (MenuInputHandler.Instance.CancelMenuInput && DialogStack.Count > 0)
        {
			var top = DialogStack.Peek();
			Close(top);
			return;
        }
	}

	internal void CloseAllMenus()
	{
		CurrentDialog?.CloseAction?.Invoke();
		CurrentDialog = null;
		InventoryMenu.gameObject.SetActive(false);
		ActionDialog.gameObject.SetActive(false);
		SkillDialog.gameObject.SetActive(false);
		TargetDialog.gameObject.SetActive(false);
		AllyActionDialog.gameObject.SetActive(false);
		StairDialog.gameObject.SetActive(false);
		AllyActionDialog.DynamicActionDialog.gameObject.SetActive(false);
		DialogStack.Clear();

		Opened = false;
		MenuInputHandler.Instance.CloseMenu();
		confirmCooldown = confirmCooldownStart;
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
		MenuInputHandler.Instance.SubmitMenuInput = false;
		confirmCooldown = confirmCooldownStart;
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
		MenuInputHandler.Instance.SubmitMenuInput = false;
		confirmCooldown = confirmCooldownStart;
	}

	public void OpenAllyMenu(Ally ally)
	{
		MenuInputHandler.Instance.OpenMenu();
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
		MenuInputHandler.Instance.SubmitMenuInput = false;
		confirmCooldown = confirmCooldownStart;
	}

	public void OpenSkillsMenu(Character character)
	{
		MenuInputHandler.Instance.OpenMenu();
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
		MenuInputHandler.Instance.SubmitMenuInput = false;
		confirmCooldown = confirmCooldownStart;
	}

	public void OpenTargetingMenu(Character character, Skill skill)
	{
		MenuInputHandler.Instance.OpenMenu();
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
		MenuInputHandler.Instance.SubmitMenuInput = false;
		confirmCooldown = confirmCooldownStart;
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
		MenuInputHandler.Instance.SubmitMenuInput = false;
		confirmCooldown = confirmCooldownStart;
	}

	public Action LateAction;

	private void LateUpdate()
	{
		LateAction?.Invoke();
		LateAction = null;
	}

	internal static void Open(Dialog dialog)
	{
		MenuInputHandler.Instance.SubmitMenuInput = false;
		MenuManager.Instance.confirmCooldown = MenuManager.Instance.confirmCooldownStart;
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
		dialog.CloseAction?.Invoke();
		MenuManager.Instance.DialogStack.Pop();

		if (MenuManager.Instance.DialogStack.Count <= 0)
		{
			MenuManager.Instance.CloseAllMenus();
			return;
		}

		var top = MenuManager.Instance.DialogStack.Peek();
		MenuManager.Instance.LateAction = () =>
		{
			top.RestoreSelect();
		};
	}
}
