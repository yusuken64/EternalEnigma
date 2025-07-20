using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
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
		Common.Instance.GameSaveData.OverworldSaveData.RecruitedAlliesData = DebugAllies.Take(2).ToList();
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			SceneManager.LoadScene("OverworldScene");
		});
	}

	private GameSaveData NewSaveData()
	{
		return new GameSaveData();
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
