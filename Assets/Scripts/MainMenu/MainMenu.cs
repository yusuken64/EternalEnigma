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
    public Button TestDungeonButton;

	public NavigationHandler NavigationHandler;

	private ProtagonistClassPicker classPicker;

	private void Start()
	{
        Common.Instance.EndSandbox();
        Common.Instance.Travel.SceneReady();
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
        Common.Instance.Travel.Continue();
	}


	public void StartGame_Clicked()
	{
        if (Common.Instance.Travel.IsTransitioning || classPicker != null) return;
        var catalog = ClassCatalog.Load();
        var classes = catalog != null ? catalog.Classes.Where(c => c != null).ToList() : new List<ClassDefinition>();
        if (classes.Count == 0) { StartGame(null, null); return; }
        NavigationHandler.gameObject.SetActive(false);
        classPicker = ProtagonistClassPicker.Show(classes,
            (primary, secondary) => { classPicker = null; NavigationHandler.gameObject.SetActive(true); StartGame(primary, secondary); },
            () => { classPicker = null; NavigationHandler.gameObject.SetActive(true); StartButton.GetComponent<Button>().Select(); });
	}

	// Starts a campaign with the protagonist's chosen classes (null keeps the prefab's class).
	public void StartGame(ClassDefinition primary, ClassDefinition secondary)
	{
        if (Common.Instance.Travel.IsTransitioning) return;
		Common.Instance.GameSaveData = CreateNewSave(UnityEngine.Random.Range(1, int.MaxValue), primary, secondary);
		Common.Instance.Travel.NewCampaign(Common.Instance.GameSaveData.TownSaveData.TownSeed);
	}

	private GameSaveData NewSaveData()
		=> CreateNewSave(UnityEngine.Random.Range(1, int.MaxValue));

	public GameSaveData CreateNewSave(int seed, ClassDefinition primary = null, ClassDefinition secondary = null)
	{
		var gameSaveData = new GameSaveData();
        var configuration = TownConfiguration ?? TownSceneLoader.Default;
        configuration.Validate();
        gameSaveData.TownSaveData.ConfigurationId = configuration.Id;
        gameSaveData.TownSaveData.RecruitedAlliesData = configuration.StartingParty.Select(a => HeroClassBinding.FromPrefab(a,
            new TownAllyData { AllyId = a.Id, AllyName = a.Name, Skills = a.Skills != null ? new(a.Skills) : new() })).ToList();
        // The first starting-party member is the protagonist; their chosen class overrides the prefab's.
        var protagonist = gameSaveData.TownSaveData.RecruitedAlliesData.FirstOrDefault();
        if (protagonist != null && primary != null)
        {
            protagonist.PrimaryClassId = primary.Id;
            protagonist.SecondaryClassId = secondary != null && secondary.Id != primary.Id ? secondary.Id : "";
        }
		gameSaveData.TownSaveData.TownSeed = seed;

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
		common.CampaignContext = null;
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

	public void DebugAutoplay_Clicked()
	{
        AutoplayRunner.WatchDemo(new AutoplayOptions {
            DebugPlaythrough = true, Godmode = true, InfiniteResources = true, Speed = 1 });
    }
}
