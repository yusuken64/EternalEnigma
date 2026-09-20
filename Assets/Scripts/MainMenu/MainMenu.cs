using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public TownConfiguration TownConfiguration;
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
        TownSceneLoader.Load(TownSceneLoader.ResolveSaved(TownConfiguration));
	}


	public void StartGame_Clicked()
	{
		Common.Instance.GameSaveData = NewSaveData();
		SaveSystem.SaveData(Common.Instance.GameSaveData);
        TownSceneLoader.Load(TownConfiguration ?? TownSceneLoader.Default);
	}

	private GameSaveData NewSaveData()
	{
		var gameSaveData = new GameSaveData();
        var configuration = TownConfiguration ?? TownSceneLoader.Default;
        configuration.Validate();
        gameSaveData.TownSaveData.ConfigurationId = configuration.Id;
        gameSaveData.TownSaveData.RecruitedAlliesData = configuration.StartingParty.Select(a =>
            new TownAllyData { AllyId = a.Id, AllyName = a.Name, Skills = a.Skills != null ? new(a.Skills) : new() }).ToList();
		gameSaveData.TownSaveData.TownSeed = UnityEngine.Random.Range(1, int.MaxValue);

        var supplies = Common.Instance.ItemManager.StartingItems.Select(i => i.AsInventoryItem(null)).ToList();
        gameSaveData.TownSaveData.Inventory = supplies.Select(i => i.ItemName).ToList();
        gameSaveData.TownSaveData.InventoryItems = ItemSaveData.Capture(supplies);
        gameSaveData.TownSaveData.InventoryFormatVersion = 1;
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

	public List<TownAllyData> DebugAllies;
	public List<TownAlly> DebugAllyPrefabs;
	public void TestDungeon_Clicked()
	{
		// DungeonScene consumes live town allies, normally prepared in town.
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
		save.TownSaveData.RecruitedAlliesData = DebugAllies.Select(data => new TownAllyData {
			AllyName = data.AllyName,
			Skills = data.Skills != null ? new List<string>(data.Skills) : new List<string>()
		}).ToList();
		// A debug launch must also work without a save and must not overwrite one.
		common.GameSaveData = save;
		common.InstantiatedTownAllies.Clear();
		foreach (Transform child in common.TownAllyParent.Cast<Transform>().ToArray())
		{
			child.SetParent(null);
			Destroy(child.gameObject);
		}
		foreach (var data in save.TownSaveData.RecruitedAlliesData)
		{
			var prefab = DebugAllyPrefabs.First(ally => ally != null && ally.Name == data.AllyName);
			var ally = Instantiate(prefab, common.TownAllyParent);
			ally.Skills = new List<string>(data.Skills);
			common.InstantiatedTownAllies.Add(ally);
		}

		common.PendingDemoLoadout = DemoDungeonLoadout.Load();
		SceneManager.LoadScene("DungeonScene");
	}
}
