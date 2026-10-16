using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public TownConfiguration TownConfiguration;
	public GameObject StartButton;
	public GameObject ContinueButton;
    public Button TestDungeonButton;
	public CampaignStartMenu CampaignStartMenu;

    public NavigationHandler NavigationHandler;

	private ProtagonistHeroPicker heroPicker;
    private readonly List<GameObject> suspendedEventSystems = new();
    public bool IsReady { get; private set; }

    private void Awake()
    {
        foreach (var button in StartButton.transform.parent.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;
            if (label.text == "Option") label.text = "Options";
            label.rectTransform.anchorMin = new Vector2(.08f, .06f);
            label.rectTransform.anchorMax = new Vector2(.92f, .94f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            FitMenuLabel(label, 28);
        }
        var title = StartButton.GetComponentInParent<Canvas>().rootCanvas.transform.Find("EternalEnigma")?.GetComponent<TMP_Text>();
        if (title != null) FitMenuLabel(title, 48);
    }

    private static void FitMenuLabel(TMP_Text label, float minimum)
    {
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = Mathf.Min(minimum, label.fontSizeMax);
        label.enableAutoSizing = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
    }

	private IEnumerator Start()
	{
        IsReady = false;
        var buttons = GetComponentsInChildren<Button>(true);
        var interactable = buttons.Select(button => button.interactable).ToArray();
        foreach (var button in buttons) button.interactable = false;
        yield return LoadingSceneIntegration.EnsureCommon();
        UseMenuEventSystem();
        for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null) buttons[i].interactable = interactable[i];
        GetComponent<MainMenuDeveloperControls>()?.Initialize();
        Common.Instance.EndSandbox();
        Common.Instance.Travel.SceneReady();
        ContinueButton.gameObject.SetActive(true);
        NavigationHandler.FocusRoot = StartButton.transform.parent;
        NavigationHandler.arrowAnchor = ArrowAnchor.Left;
        NavigationHandler.arrowOffset = Vector3.zero;
        NavigationHandler.edgeSpacing = 12;
        IsReady = true;
        StartButton.GetComponent<Button>().Select();
        CampaignStartMenu.gameObject.SetActive(false);
        if (Common.Instance.ScreenTransition.BlockScreen.activeSelf) Common.Instance.ScreenTransition.DoOpen();
    }

    private void Update()
    {
        var selected = EventSystem.current?.currentSelectedGameObject;
        NavigationHandler.FocusRoot = selected != null && selected.transform.IsChildOf(transform)
            ? transform : StartButton.transform.parent;
        var input = MenuUIInputModule.Active;
        if (!IsReady || input == null || input.HasDialog || input.InputConsumed ||
            Common.Instance.Travel.IsTransitioning) return;
        if (input.UI.Cancel.WasPressedThisFrame() &&
            GetComponent<MainMenuDeveloperControls>()?.Close() == true)
        {
            AudioManager.Instance?.PlayCue(SoundCue.Cancel);
            input.ConsumeInput();
            return;
        }
        if (input.UI.Options.WasPressedThisFrame()) Options_Clicked();
    }

	public void Continue_Clicked()
	{
        if (!IsReady) return;
        //CampaignSlots.Show(this, false);
        CampaignStartMenu.gameObject.SetActive(true);
        CampaignStartMenu.Setup(false);
    }

	public void StartGame_Clicked()
	{
        if (!IsReady) return;
		CampaignStartMenu.gameObject.SetActive(true);
		CampaignStartMenu.Setup(true);
    }

    public void ChooseHero()
    {
		if (Common.Instance.Travel.IsTransitioning || heroPicker != null) return;
		var configuration = TownConfiguration ?? TownSceneLoader.Default;
		configuration.Validate();
		// The authored hero picker shares the campaign-start container with the slots.
		CampaignStartMenu.gameObject.SetActive(true);
		CampaignStartMenu.SlotViewer.SetActive(false);
		NavigationHandler.gameObject.SetActive(false);
		heroPicker = ProtagonistHeroPicker.Show(configuration.AllyCatalog,
			hero => { heroPicker = null; CampaignStartMenu.gameObject.SetActive(false); NavigationHandler.gameObject.SetActive(true); StartGame(hero); },
			() => { heroPicker = null; CampaignStartMenu.gameObject.SetActive(false); NavigationHandler.gameObject.SetActive(true); StartButton.GetComponent<Button>().Select(); });
	}

	public void StartGame(TownAlly hero = null)
	{
        if (!IsReady) return;
		if (Common.Instance.Travel.IsTransitioning) return;
		Common.Instance.GameSaveData = CreateNewSave(NewCampaignSeed(), hero);
		Common.Instance.Travel.NewCampaign(Common.Instance.GameSaveData.TownSaveData.TownSeed);
	}

    private void UseMenuEventSystem()
    {
        var own = gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
        if (own == null) return;
        foreach (var other in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (other == own || other.gameObject.scene == gameObject.scene) continue;
            suspendedEventSystems.Add(other.gameObject);
            other.gameObject.SetActive(false);
        }
        EventSystem.current = own;
        // Refresh the static input-module owner after the other module is disabled.
        var module = own.GetComponent<MenuUIInputModule>();
        if (module != null) { module.enabled = false; module.enabled = true; }
    }

    private void OnDestroy()
    {
        foreach (var system in suspendedEventSystems)
            if (system != null) system.SetActive(true);
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

        // The opening bakery transaction supplies the campaign's first bread.
        var supplies = Common.Instance.ItemManager.StartingItems.Where(i => i.ItemName != "Bread")
            .Select(i => i.AsInventoryItem(null)).ToList();
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
		var returnSelection = EventSystem.current?.currentSelectedGameObject;
		NavigationHandler.gameObject.SetActive(false);
		Common.Instance.GlobalSettings.ShowDialog();
		Common.Instance.GlobalSettings.CloseAction = () =>
		{
			NavigationHandler.gameObject.SetActive(true);
			EventSystem.current?.SetSelectedGameObject(MenuUIInputModule.IsUsable(returnSelection) ? returnSelection : StartButton);
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
        if (IsReady && !Common.Instance.Travel.IsTransitioning) AutoplaySetup.Show(this);
	}

    public void EndgameTown_Clicked() => StartEndgameDebug(EndgameDebugDestination.Town);
    public void EndgameDungeon_Clicked() => StartEndgameDebug(EndgameDebugDestination.Dungeon);
    public void EndgameOverworld_Clicked() => StartEndgameDebug(EndgameDebugDestination.Overworld);

    private void StartEndgameDebug(EndgameDebugDestination destination)
    {
        if (!IsReady || Common.Instance.Travel.IsTransitioning) return;
        var common = Common.Instance;
        var configuration = TownConfiguration ?? TownSceneLoader.Default;
        var save = EndgameDebugStart.Create(configuration, common.ItemManager, destination);
        common.EndSandbox();
        common.CampaignContext = null;
        common.PendingDemoLoadout = null;
        CampaignParty.ClearLiveParty(common);
        common.GameSaveData = save;
        TownSceneLoader.Configure(configuration);
        common.Travel.Continue();
    }
}
