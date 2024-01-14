using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
	private void Start()
	{
		if (Common.Instance.GameSaveData != null)
		{
			//show continue;
		}
	}

	public void StartGame_Clicked()
	{
		Common.Instance.GameSaveData = NewSaveData();
		SceneManager.LoadScene("OverworldScene");
	}

	private GameSaveData NewSaveData()
	{
		return new GameSaveData();
	}
}
