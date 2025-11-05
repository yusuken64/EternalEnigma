using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
	public GameObject StartButton;
	public GameObject ContinueButton;

	public NavigationHandler NavigationHandler;

	private void Start()
	{
		if (Common.Instance.GameSaveData != null)
		{
			ContinueButton.gameObject.SetActive(true);
			ContinueButton.GetComponent<Button>().Select();
		}
		else
		{
			ContinueButton.gameObject.SetActive(false);
			StartButton.GetComponent<Button>().Select();
		}
	}

	public void Continue_Clicked()
	{
		Common.Instance.ScreenTransition.DoTransition(
			() =>
			{
				SceneManager.LoadScene("OverworldScene");
			},
			false);
	}


	public void StartGame_Clicked()
	{
		Common.Instance.GameSaveData = NewSaveData();
		SaveSystem.SaveData(Common.Instance.GameSaveData);
		Common.Instance.ScreenTransition.DoTransition(
			() =>
			{
				SceneManager.LoadScene("OverworldScene");
			},
			false);
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
		NavigationHandler.gameObject.SetActive(false);
		Common.Instance.GlobalSettings.ShowDialog();
		Common.Instance.GlobalSettings.CloseAction = () =>
		{
			NavigationHandler.gameObject.SetActive(true);
		};
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
