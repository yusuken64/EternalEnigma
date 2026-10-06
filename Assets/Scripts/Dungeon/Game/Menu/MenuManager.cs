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
    public PartyMenu PartyMenu { get; private set; }
	public ActionDialog ActionDialog;
	public AllyActionDialog AllyActionDialog;
	public SkillDialog SkillDialog;
	public TargetDialog TargetDialog;
	public bool Opened => dialogs.Opened;
	public Dialog CurrentDialog => dialogs.Current;
	public StairConfirm StairDialog;

	private readonly DialogController dialogs = new(() => AudioManager.Instance.SoundEffects.Unpause.PlayAsUI());
    public Stack<Dialog> DialogStack => dialogs.Stack;

	public GameObject TargetArrow;
    private void Start()=>PartyMenuLauncher.Create(transform,()=>Game.Instance!=null&&Game.Instance.PlayerController.CanOpenMenu(),tab=>OpenPartyMenu(tab));

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
		if (AutoplayRunner.BlocksPlayerInput) return;
		if (MenuUIInputModule.Active?.InputConsumed == true || Common.Instance.GlobalSettings.IsOpen) return;
		if (Opened && Common.Instance.MenuInputHandler.OptionInput && !Common.Instance.MenuInputHandler.CancelMenuInput)
		{
			Common.Instance.GlobalSettings.ShowDialog();
			return;
		}
        if (Common.Instance.MenuInputHandler.MenuOpenClosedInput || Common.Instance.MenuInputHandler.OpenSkillMenuInput)
        {
            var tab = Common.Instance.MenuInputHandler.MenuOpenClosedInput ? PartyMenuTab.Inventory : PartyMenuTab.Skills;
            if (PartyMenu != null && CurrentDialog == PartyMenu) PartyMenu.Shortcut(tab);
            else if (!Opened && Game.Instance.PlayerController.CanOpenMenu()) OpenPartyMenu(tab);
            return;
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

    internal void CloseAllMenus() => dialogs.CloseAll();

    private void OpenMenu() => OpenPartyMenu(PartyMenuTab.Inventory);
    public void OpenPartyMenu(PartyMenuTab tab, Ally hero = null)
    {
        if (PartyMenu == null) PartyMenu = global::PartyMenu.Create(transform);
        PartyMenu.Setup(new DungeonPartyMenuContext(Game.Instance), tab,
            Game.Instance.PlayerController.ControlledAlly?.TownAllyId);
        Open(PartyMenu);
    }
    public void OpenInventoryAs(Ally ally) => OpenPartyMenu(PartyMenuTab.Inventory, ally);

	public void OpenAllyMenu(Ally ally)
	{
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		this.gameObject.SetActive(true);
		MenuManager.Open(AllyActionDialog);
		AllyActionDialog.Setup(ally);

		AllyActionDialog.CloseAction = () =>
		{
			AllyActionDialog.Close();
		};
		AllyActionDialog.SetNavigation();
		AudioManager.Instance.SoundEffects.Pause.PlayAsUI();

		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

    public void OpenSkillsMenu(Character character) => OpenPartyMenu(PartyMenuTab.Skills, character as Ally);

	public void OpenInventoryTargetingMenu(Character character, Skill skill)
	{
		if (skill.Targeting != SkillTargeting.InventoryItem || !character.CanCast(skill, out _)) return;
		OpenInventoryPicker(character, skill.GetInventoryTargets(character),
			item => SkillAction.ForInventoryItem(character, skill, item),
			$"Choose an item for {skill.SkillName} ({skill.SPCost} SP)");
	}

	private void OpenInventoryPicker(Character character, List<InventoryItem> targets,
		Func<InventoryItem, GameAction> createAction, string prompt)
	{
        if (PartyMenu != null && DialogStack.Contains(PartyMenu))
        {
            PartyMenu.Pick(prompt, targets.Select(item => (item.ItemName + (item.HasStacks ? $" x{item.StackStock}" : "") +
                (character.Equipment.IsEquipped(item) ? " [Equipped]" : ""), (Action)(() =>
            {
                var action = createAction(item);
                if (!action.IsValid(character)) { PartyMenu.Complete("That item can no longer be targeted."); return; }
                CloseAllMenus(); character.SetAction(action);
            }))).ToList());
            return;
        }
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		// Item use can open a second inventory picker over the original inventory.
		// Keep its rows/callbacks separate so Back restores the original item menu.
		bool temporary = DialogStack.Contains(InventoryMenu);
		var picker = temporary ? Instantiate(InventoryMenu, InventoryMenu.transform.parent) : InventoryMenu;
		var pickerCanvas = picker.GetComponent<Canvas>();
		int originalOrder = pickerCanvas.sortingOrder;
		pickerCanvas.sortingOrder = Mathf.Max(originalOrder, SkillDialog.GetComponent<Canvas>().sortingOrder + 1);
		Open(picker);
		picker.Setup(targets, character, item =>
		{
			var action = createAction(item);
			if (!action.IsValid(character))
			{
				Game.Instance.DoFloatingText("That item can no longer be targeted", Color.yellow, character);
				return;
			}
			CloseAllMenus();
			character.SetAction(action);
		}, prompt, followPortrait: !temporary);
		picker.SetNavigation();
		picker.CloseAction = () =>
		{
			picker.Close();
			pickerCanvas.sortingOrder = originalOrder;
			if (temporary) Destroy(picker.gameObject);
		};

		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

	public void OpenTargetingMenu(Character character, Skill skill)
	{
		if (!character.CanCast(skill, out var reason)) { GameMessages.Post(reason, true); return; }
		OpenWorldTargeting(character, skill.GetTargetCharacters(character),
			(target, direction) => skill.Targeting == SkillTargeting.Missile ?
				SkillAction.ForMissile(character, skill, direction) : new SkillAction(character, skill, target),
			skill.Targeting == SkillTargeting.Missile ? skill.MissileRange : 0, skill);
	}

	private void OpenWorldTargeting(Character character, List<Character> targets,
		Func<Character, Vector3Int, GameAction> createAction, int missileRange = 0, Skill skill = null)
	{
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		this.gameObject.SetActive(true);
		MenuManager.Open(TargetDialog);
		if(skill != null) TargetDialog.Setup(character,skill); else TargetDialog.Setup(character, targets, createAction, missileRange);

		TargetDialog.CloseAction = () =>
		{
			TargetDialog.Close();
			//CurrentDialog = null;
			//CloseAllMenus();
		};
		TargetDialog.SetNavigation();
		AudioManager.Instance.SoundEffects.Pause.PlayAsUI();

		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

	internal void UseInventoryItem(Character character, InventoryItem item)
	{
		var inventory = Game.Instance.PlayerController.Inventory;
		UseInventoryItemAction Create() => new(inventory, character, item);
		if (!Create().CanBegin(character))
		{
			var reason = item is EquipableInventoryItem equipment && character is Ally hero
				? HeroClass.EquipmentRestriction(hero.PrimaryClass, hero.SecondaryClass, equipment) : null;
			Game.Instance.DoFloatingText(reason ?? "That item cannot be used now", Color.yellow, character);
			Common.Instance.MenuInputHandler.ClearInputThisFrame();
			return;
		}
		if (item.ItemDefinition is UsableItemDefinition definition)
		{
			if (definition.Targeting == SkillTargeting.InventoryItem)
			{
				OpenInventoryPicker(character, definition.GetInventoryTargets(character), selected => Create().WithItem(selected),
					$"Choose an item for {item.ItemName}");
				return;
			}
			if (definition.Targeting == SkillTargeting.Missile || definition.TargetingRules.RequiresSelection)
			{
				OpenWorldTargeting(character, definition.TargetingRules.GetCharacters(character),
					(target, direction) => Create().WithTarget(target).WithDirection(direction),
					definition.Targeting == SkillTargeting.Missile ? definition.MissileRange : 0);
				return;
			}
		}
		var action = Create();
		if (!action.IsValid(character)) return;
		CloseAllMenus();
		character.SetAction(action);
	}

	internal void ShowYesNoDialog(string prompt, Action yesAction, Action noAction)
	{
		Common.Instance.MenuInputHandler.SwitchToUIInput();
		this.gameObject.SetActive(true);
		MenuManager.Open(StairDialog);
		StairDialog.Setup(prompt, yesAction, noAction);

		AudioManager.Instance.SoundEffects.Pause.PlayAsUI();

		Common.Instance.MenuInputHandler.SubmitMenuInput = false;
		Common.Instance.MenuInputHandler.ClearInputThisFrame();
	}

    public Action LateAction { get => dialogs.LateAction; set => dialogs.LateAction = value; }
    private void LateUpdate()
    {
        dialogs.Tick();
    }

    public static void Open(Dialog dialog) => Instance.dialogs.Open(dialog);

    public static void Close(Dialog dialog) => Instance.dialogs.Close(dialog);
}
