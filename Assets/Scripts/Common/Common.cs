using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Common : PersistedSingletonMonoBehaviour<Common>
{
	public GameSaveData GameSaveData;
    public EternalEnigma.Core.Progression.CampaignContext CampaignContext { get; internal set; }
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
		if (sceneIndex == 0)
		{
			otherScene = 1;
		};

		otherScene = sceneIndex;
		//make sure your _preload scene is the first in scene build list
		AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(0);
		asyncOperation.completed += AsyncOperation_completed;
	}

	private static void AsyncOperation_completed(AsyncOperation obj)
	{
		SceneManager.LoadScene(otherScene);
	}
#endif
}
