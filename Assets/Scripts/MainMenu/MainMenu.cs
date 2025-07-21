using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
	public GameObject ContinueButton;

	private void Start()
	{
		if (Common.Instance.GameSaveData != null)
		{
			ContinueButton.gameObject.SetActive(true);
		}
		else
		{
			ContinueButton.gameObject.SetActive(false);
		}
	}

	public void Continue_Clicked()
	{
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SceneManager.LoadScene("OverworldScene");
		});
	}


	public void StartGame_Clicked()
	{
		Common.Instance.GameSaveData = NewSaveData();
		SaveSystem.SaveData(Common.Instance.GameSaveData);
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SceneManager.LoadScene("OverworldScene");
		});
	}

	private GameSaveData NewSaveData()
	{
		var gameSaveData = new GameSaveData();
		gameSaveData.OverworldSaveData.RecruitedAlliesData = new()
		{
			new OverworldAllyData()
			{
				AllyName = "Rowan"
			}
		};
		gameSaveData.OverworldSaveData.OverworldSeed = UnityEngine.Random.Range(1, int.MaxValue);

		return gameSaveData;
	}

	public void Options_Clicked()
	{
        GlobalSettings globalSettings = FindFirstObjectByType<GlobalSettings>();
		globalSettings.ShowDialog();
	}

	public void Exit_Clicked()
	{
		Application.Quit();
	}

	public List<OverworldAllyData> DebugAllies;
	public void TestDungeon_Clicked()
	{
		Common.Instance.GameSaveData.OverworldSaveData.RecruitedAlliesData = DebugAllies;
		
		SceneManager.LoadScene("DungeonScene");
	}
}
