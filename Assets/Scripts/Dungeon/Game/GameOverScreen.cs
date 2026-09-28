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
	private Image messageBackdrop;
	private PlayerController _playerController;
	private bool _victory;
	private string _defaultButtonText;

	private void OnEnable()
	{
		// The result is a full-screen dialog above the runtime HUD canvases.
		var canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 100;
		if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
	}

	private void PreparePresentation()
	{
		var textRect = MessageText.rectTransform;
		textRect.anchorMin = new Vector2(.17f, .34f);
		textRect.anchorMax = new Vector2(.83f, .75f);
		textRect.offsetMin = textRect.offsetMax = Vector2.zero;
		MessageText.alignment = TextAlignmentOptions.Center;
		MessageText.enableAutoSizing = true;
		MessageText.fontSizeMin = 28;
		MessageText.fontSizeMax = 42;
		MessageText.fontSize = 42;
		if (messageBackdrop == null)
		{
			var panel = GameUISkin.Rect("Result message backdrop", transform,
				new Vector2(.15f, .32f), new Vector2(.85f, .77f));
			panel.SetSiblingIndex(textRect.GetSiblingIndex());
			messageBackdrop = panel.gameObject.AddComponent<Image>();
			messageBackdrop.color = new Color(.08f, .10f, .09f, .82f);
			messageBackdrop.raycastTarget = false;
		}
		MessageText.color = GameUITheme.LightInk;
	}

	internal void Setup(PlayerController playerController, bool victory = false)
	{
		PreparePresentation();
		_playerController = playerController;
		_victory = victory;
		CloseAction = victory ? () => { Common.Instance.Travel.ReturnToMenu(); } : null;
		MessageText.text = victory ? $"Victory!\nFinal dungeon cleared\nTreasure: {playerController.Gold:N0}" :
			$"Player Perished\nOn floor {playerController.Floor}\nTreasure: {playerController.Gold:N0}";
		var label = OkButton.GetComponentInChildren<TMP_Text>();
		if (label != null)
		{
			_defaultButtonText ??= label.text;
			label.text = victory ? "Main Menu" : _defaultButtonText;
		}

		var nav = OkButton.navigation;
		nav.mode = Navigation.Mode.None;
		OkButton.navigation = nav;
		OkButton.Select();
	}

	public void TryAgain_Clicked()
	{
		if (_victory) { Common.Instance.Travel.ReturnToMenu(); return; }
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

	// Retreat skill: victory return rules (keep gold and items) without completing the dungeon.
	public static void Retreat(PlayerController playerController)
	{
		var common = Common.Instance;
		if (common.CampaignContext != null) { common.Travel.FinishDungeon(false, playerController, true, keepLoot: true); return; }
		var configuration = TownSceneLoader.ResolveSaved();
		DungeonReturnService.Commit(common.GameSaveData, configuration, false,
			playerController.Gold, playerController.Inventory.InventoryItems, Game.Instance.Allies, keepLoot: true);
		SaveSystem.SaveData(common.GameSaveData);
		TownSceneLoader.Load(configuration);
	}

	public void Quit_Clicked()
	{
        if (AutoplayRunner.Active != null && AutoplayRunner.Active.PlayerControlled) { AutoplayRunner.Active.ExitDemo(); return; }
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
