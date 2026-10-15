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
    public bool CoversGameplayHud => DialogStack.Any(dialog => dialog is not global::PartyMenu &&
        dialog is not PartyMenuPicker && dialog is not EventHistoryDialog && dialog is not ActionDialog);
    private CampaignHUD campaignHUD;
    public PartyMenu PartyMenu { get; private set; }

    private void Start()
    {
        var town = GetComponentInParent<Town>();
        PartyMenuLauncher.Create(transform,
            () => town.IsReady && !town.TownPlayer.CutsceneLocked && !Opened && MenuUIInputModule.Active?.HasDialog != true, OpenPartyMenu);
    }

	private void Update()
	{
		if (AutoplayRunner.BlocksPlayerInput || Common.Instance.Travel.IsTransitioning || FindFirstObjectByType<Town>()?.TownPlayer.CutsceneLocked == true) return;
		if (campaignHUD == null && Common.Instance.CampaignContext != null) campaignHUD = FindFirstObjectByType<CampaignHUD>();
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
            else if (!Opened && MenuUIInputModule.Active?.HasDialog != true && FindFirstObjectByType<Town>().IsReady) OpenPartyMenu(tab);
        }
    }
    public void OpenPartyMenu(PartyMenuTab tab)
    {
        var town = FindFirstObjectByType<Town>();
        if (town == null || !town.IsReady || town.TownPlayer.CutsceneLocked || Common.Instance.Travel.IsTransitioning ||
            Common.Instance.GlobalSettings.IsOpen || AutoplayRunner.BlocksPlayerInput ||
            Opened && CurrentDialog != PartyMenu || !Opened && MenuUIInputModule.Active?.HasDialog == true) return;
        if (PartyMenu == null) PartyMenu = global::PartyMenu.Create(transform);
        PartyMenu.Setup(new TownPartyMenuContext(town), tab, town.TownPlayer.ControllingTownAlly?.Id);
        Open(PartyMenu);
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
