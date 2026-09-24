using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverScreen : Dialog
{
	public TextMeshProUGUI MessageText;
	public Button OkButton;
	private PlayerController _playerController;

	internal void Setup(PlayerController playerController)
	{
		_playerController = playerController;
		MessageText.text = $@"Player Perished
On floor {playerController.Floor}
with {playerController.Gold} Treasure";

		var nav = OkButton.navigation;
		nav.mode = Navigation.Mode.None;
		OkButton.navigation = nav;
		OkButton.Select();
	}

	public void TryAgain_Clicked()
	{
		GoBackToTown(false, _playerController);
	}

	public static void GoBackToTown(bool isWin, PlayerController playerController)
	{
        var common = Common.Instance;
        if (common.CampaignContext != null) { common.Travel.FinishDungeon(isWin, playerController); return; }
        var configuration = TownSceneLoader.ResolveSaved();
        DungeonReturnService.Commit(common.GameSaveData, configuration, isWin,
            playerController.Gold, playerController.Inventory.InventoryItems, PartyRules.PartyMembers(Game.Instance));
        SaveSystem.SaveData(common.GameSaveData);
        TownSceneLoader.Load(configuration);
	}

	public void Quit_Clicked()
	{
        if (Common.Instance.CampaignContext != null) { Common.Instance.Travel.ReturnToMenu(); return; }
		CommitAbandonedRun(_playerController);
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SceneManager.LoadScene("MainMenu");
		});
	}

    public static void CommitAbandonedRun(PlayerController player)
    {
        var common = Common.Instance;
        if (common.CampaignContext != null) { common.Travel.FinishDungeon(false, player, false); return; }
        DungeonReturnService.Commit(common.GameSaveData, TownSceneLoader.ResolveSaved(), false,
            player.Gold, player.Inventory.InventoryItems, PartyRules.PartyMembers(Game.Instance));
        SaveSystem.SaveData(common.GameSaveData);
    }

	internal override void SetFirstSelect()
	{
		OkButton.Select();
	}
}
