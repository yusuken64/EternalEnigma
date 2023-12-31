using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
	public TextMeshProUGUI MessageText;

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

		Game.Instance.ResetGame();
	}
}
