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

	internal void Setup(PlayerController playerController)
	{
		MessageText.text = $@"Player Perished
On floor {playerController.Floor}
with {playerController.Gold} Treasure";

		//todo turn inventory items into gold;
		Common.Instance.GameSaveData.OverworldSaveData.Gold += playerController.Gold;
	}

	public void TryAgain_Clicked()
	{
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
