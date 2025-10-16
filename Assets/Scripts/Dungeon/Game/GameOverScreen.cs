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
	}

	public void TryAgain_Clicked()
	{
		GoBackToOverworld(false, _playerController);
	}

	public static void GoBackToOverworld(bool isWin, PlayerController playerController)
	{
		Common.Instance.GameSaveData.OverworldSaveData.Gold += playerController.Gold;
		SaveSystem.SaveData(Common.Instance.GameSaveData);
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SceneManager.LoadScene("OverworldScene");
		});
	}

	public void Quit_Clicked()
	{
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SceneManager.LoadScene("MainMenu");
		});
	}

	internal override void SetFirstSelect()
	{
		OkButton.Select();
	}
}
