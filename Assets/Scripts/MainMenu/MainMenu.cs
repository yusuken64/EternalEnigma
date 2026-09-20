using System.Collections.Generic;
using System.Linq;
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
	public List<OverworldAlly> DebugAllyPrefabs;
	public void TestDungeon_Clicked()
	{
		// DungeonScene consumes live overworld allies, normally prepared in town.
		if (DebugAllies == null || DebugAllies.Count == 0 || DebugAllies.Any(data =>
			data == null || DebugAllyPrefabs == null ||
			!DebugAllyPrefabs.Any(prefab => prefab != null && prefab.Name == data.AllyName)))
		{
			Debug.LogError("Test dungeon requires a prefab for every debug ally.", this);
			return;
		}

		var common = Common.Instance;
		var save = NewSaveData();
		save.DungeonSaveData = new DungeonSaveData { StartFloor = 1, EndFloor = 5 };
		save.OverworldSaveData.RecruitedAlliesData = DebugAllies.Select(data => new OverworldAllyData {
			AllyName = data.AllyName,
			Skills = data.Skills != null ? new List<string>(data.Skills) : new List<string>()
		}).ToList();
		// A debug launch must also work without a save and must not overwrite one.
		common.GameSaveData = save;
		common.InstantiatedOverworldAllies.Clear();
		foreach (Transform child in common.OverworldAllyParent.Cast<Transform>().ToArray())
		{
			child.SetParent(null);
			Destroy(child.gameObject);
		}
		foreach (var data in save.OverworldSaveData.RecruitedAlliesData)
		{
			var prefab = DebugAllyPrefabs.First(ally => ally != null && ally.Name == data.AllyName);
			var ally = Instantiate(prefab, common.OverworldAllyParent);
			ally.Skills = new List<string>(data.Skills);
			common.InstantiatedOverworldAllies.Add(ally);
		}

		common.PendingDemoLoadout = DemoDungeonLoadout.Load();
		SceneManager.LoadScene("DungeonScene");
	}
}
