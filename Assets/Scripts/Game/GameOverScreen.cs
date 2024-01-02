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

	internal void Setup(Player playerCharacter)
	{
		MessageText.text = $@"Player Perished
On floor {playerCharacter.RealStats.Floor}
with {playerCharacter.RealStats.Gold} Treasure";
	}

	public void TryAgain_Clicked()
	{
		int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
		SceneManager.LoadScene(currentSceneIndex);

		MenuManager.Close(this);
		Game.Instance.ResetGame();
	}

	internal override void SetFirstSelect()
	{
		OkButton.Select();
	}
}
