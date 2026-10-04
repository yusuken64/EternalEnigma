using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

	private ProtagonistHeroPicker heroPicker;
    public bool IsReady { get; private set; }

	private IEnumerator Start()
	{
        IsReady = false;
        var buttons = GetComponentsInChildren<Button>(true);
        var interactable = buttons.Select(button => button.interactable).ToArray();
        foreach (var button in buttons) button.interactable = false;
        yield return LoadingSceneIntegration.EnsureCommon();
        for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null) buttons[i].interactable = interactable[i];
        IsReady = true;
        GetComponent<MainMenuDeveloperControls>()?.Initialize();
        Common.Instance.EndSandbox();
        Common.Instance.Travel.SceneReady();
        ContinueButton.gameObject.SetActive(true);
        StartButton.GetComponent<Button>().Select();
	}

	public void Continue_Clicked()
	{
        if (!IsReady) return;
        CampaignSlots.Show(this, false);
	}


	public void StartGame_Clicked()
	{
        if (!IsReady) return;
        CampaignSlots.Show(this, true);
    }
    public void ChooseHero()
    {
		if (Common.Instance.Travel.IsTransitioning || heroPicker != null) return;
		var configuration = TownConfiguration ?? TownSceneLoader.Default;
		configuration.Validate();
		NavigationHandler.gameObject.SetActive(false);
		heroPicker = ProtagonistHeroPicker.Show(configuration.AllyCatalog,
			hero => { heroPicker = null; NavigationHandler.gameObject.SetActive(true); StartGame(hero); },
			() => { heroPicker = null; NavigationHandler.gameObject.SetActive(true); StartButton.GetComponent<Button>().Select(); });
	}

	public void StartGame(TownAlly hero = null)
	{
        if (!IsReady) return;
		if (Common.Instance.Travel.IsTransitioning) return;
		Common.Instance.GameSaveData = CreateNewSave(NewCampaignSeed(), hero);
		Common.Instance.Travel.NewCampaign(Common.Instance.GameSaveData.TownSaveData.TownSeed);
	}

	private GameSaveData NewSaveData()
		=> CreateNewSave(NewCampaignSeed());

	private static int NewCampaignSeed()
	{
		using var random = RandomNumberGenerator.Create();
		var bytes = new byte[sizeof(int)];
		int seed;
		do
		{
			random.GetBytes(bytes);
			seed = System.BitConverter.ToInt32(bytes, 0) & int.MaxValue;
		} while (seed == 0);
		return seed;
	}

	public GameSaveData CreateNewSave(int seed, TownAlly hero = null)
	{
		var gameSaveData = new GameSaveData();
        var configuration = TownConfiguration ?? TownSceneLoader.Default;
        configuration.Validate();
		var protagonist = hero ?? configuration.StartingParty[0];
		if (!configuration.AllyCatalog.Contains(protagonist))
			throw new System.ArgumentException("The chosen hero must belong to the town roster.", nameof(hero));
        gameSaveData.TownSaveData.ConfigurationId = configuration.Id;
		gameSaveData.ProtagonistId = protagonist.Id;
		gameSaveData.TownSaveData.RecruitedAlliesData = new[] { protagonist }.Select(a => HeroClassBinding.FromPrefab(a,
            new TownAllyData { AllyId = a.Id, AllyName = a.Name, Skills = a.Skills != null ? new(a.Skills) : new() })).ToList();
		gameSaveData.TownSaveData.TownSeed = seed;

        var supplies = Common.Instance.ItemManager.StartingItems.Select(i => i.AsInventoryItem(null)).ToList();
        gameSaveData.TownSaveData.InventoryItems = ItemSaveData.Capture(supplies);

        var catalog = ClassCatalog.Load();
        if (catalog != null)
            foreach (var data in gameSaveData.TownSaveData.RecruitedAlliesData)
            {
                data.Skills ??= new List<string>();
                foreach (var skillName in TownAlly.StartingSkillNames(catalog.Get(data.PrimaryClassId), catalog.Get(data.SecondaryClassId)))
                    if (!data.Skills.Contains(skillName)) data.Skills.Add(skillName);
            }

		return gameSaveData;
	}

	public void Options_Clicked()
	{
        if (!IsReady) return;
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
        if (!IsReady) return;
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
        save.IsSandbox = true;
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
