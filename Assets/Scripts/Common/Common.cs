using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Common : PersistedSingletonMonoBehaviour<Common>
{
	public GameSaveData GameSaveData;
    private EternalEnigma.Core.Progression.CampaignContext campaignContext;
    private OverworldTerrainCache overworldTerrain;
    public OverworldTerrainCache OverworldTerrain => overworldTerrain ??= new OverworldTerrainCache(transform);
    public EternalEnigma.Core.Progression.CampaignContext CampaignContext
    {
        get => campaignContext;
        internal set
        {
            if (!ReferenceEquals(campaignContext, value)) overworldTerrain?.Clear();
            campaignContext = value;
        }
    }
    private void OnDestroy() { overworldTerrain?.Clear(); }
    public CampaignTravelService Travel { get; private set; }
    private GameSaveData playerSave;
    public void BeginSandbox(int seed)
    {
        EndSandbox();
        playerSave = GameSaveData;
        GameSaveData = new GameSaveData { IsSandbox = true };
        CampaignContext = new(new(EternalEnigma.Core.Progression.OverworldLaunchMode.Sandbox, seed));
    }
    public void EndSandbox()
    {
        if (CampaignContext?.IsSandbox != true) return;
        GameSaveData = playerSave; playerSave = null; CampaignContext = null;
    }
	internal DemoDungeonLoadout PendingDemoLoadout;
	public TownConfiguration CurrentTownConfiguration { get; internal set; }

	public AudioManager AudioManager;
	public ItemManager ItemManager;
	public SkillManager SkillManager;
	public GameObject SceneTransferObjects;
	public ScreenTransition ScreenTransition;
	public MessageDialog MessageDialog;

	public List<TownAlly> InstantiatedTownAllies = new();
	public Transform TownAllyParent;

	public MenuInputHandler MenuInputHandler;
	public GlobalSettings GlobalSettings;

	protected override void Initialize()
	{
		LoadData();
        Travel = new CampaignTravelService(this);
#if !UNITY_EDITOR
		SceneManager.LoadScene(1);
#endif
	}

	private void LoadData()
	{
		GameSaveData = SaveSystem.LoadData();
        Travel = new CampaignTravelService(this);
	}
}

public class LoadingSceneIntegration
{
    private static AsyncOperation commonLoad;
    private static bool bootstrapping;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStartupState()
    {
        commonLoad = null;
        bootstrapping = false;
    }

    public static IEnumerator EnsureCommon()
    {
        // The editor's normal bootstrap replaces the original scene; do not race that load.
        while (bootstrapping) yield return null;
        if (UnityEngine.Object.FindFirstObjectByType<Common>() != null) yield break;
        if (commonLoad == null || commonLoad.isDone)
            commonLoad = SceneManager.LoadSceneAsync("Common", LoadSceneMode.Additive);
        // Unity allows an AsyncOperation to be yielded by only one coroutine.
        // Menu and music can both wait here, so poll the shared load instead.
        while (!commonLoad.isDone) yield return null;
    }

#if UNITY_EDITOR
	public static int otherScene = -2;

	// SessionState survives Test Runner domain reloads; normal editor play defaults to automatic loading.
	public static bool SuppressAutomaticSceneLoading
	{
		get => UnityEditor.SessionState.GetBool("EternalEnigma.SuppressAutomaticSceneLoading", false);
		set => UnityEditor.SessionState.SetBool("EternalEnigma.SuppressAutomaticSceneLoading", value);
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void InitLoadingScene()
	{
		if (SuppressAutomaticSceneLoading) return;
		int sceneIndex = SceneManager.GetActiveScene().buildIndex;
		// Test Runner and isolated editor scenes own their initialization.
		if (sceneIndex < 0) return;
		otherScene = sceneIndex == 0 ? 1 : sceneIndex;
		bootstrapping = true;
		//make sure your _preload scene is the first in scene build list
		AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(0);
		asyncOperation.completed += AsyncOperation_completed;
	}

	private static void AsyncOperation_completed(AsyncOperation obj)
	{
		bootstrapping = false;
		SceneManager.LoadScene(otherScene);
	}
#endif
}
