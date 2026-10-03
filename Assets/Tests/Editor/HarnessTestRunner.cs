using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Persist results independently of the MCP socket, which reconnects at Play Mode transitions.
[InitializeOnLoad]
public static class HarnessTestRunner
{
    private const string SessionKey = "EternalEnigma.Tests.ActiveRun";
    private static readonly TestRunnerApi Api;

    static HarnessTestRunner()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Api.RegisterCallbacks(new Results());
        EditorApplication.update += StartQueuedRun;
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Run EditMode")]
    public static void RunEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Main Menu Startup")]
    public static void RunMainMenuStartup() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.MainMenuStartupTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Controller Flows")]
    public static void RunControllerFlows() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.ControllerFlowTests", "EternalEnigma.Tests.MenuSelectionTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.MenuSceneNavigationTests.InventoryCanOpenAndCloseImmediatelyAndSettingsRestoreItsSelection");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Controller Combat")]
    public static void RunControllerCombat() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.ControllerFlowTests.CombatMovementAttackSkillTargetCancelAndConfirmUseControllerOnly");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Combat Effects")]
    public static void RunCombatEffects() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.CombatEffectPlaybackTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Combat Effects EditMode")]
    public static void RunCombatEffectsEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode",
        "CombatEffectAssignmentTests", "PresentationActionTests", "StatusEffectTests", "CombatMathTests", "ClassContentTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Combat Effects Regression")]
    public static void RunCombatEffectsRegression() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CombatEffectPlaybackTests", "EternalEnigma.Tests.CombatFoundationTests", "SightPlaybackTests", "EternalEnigma.Tests.SkillMovementTests", "EternalEnigma.Tests.SongCommandTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town And Prop Visuals")]
    public static void RunTownAndPropVisuals() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.TownAndPropVisualTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Fantasy Traps")]
    public static void RunFantasyTraps() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.FantasyTrapTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Fantasy Trap Playback")]
    public static void RunFantasyTrapPlayback() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.FantasyTrapPlaybackTests");

    // These synchronous geometry checks create isolated TWC output and can preserve a live run.
    [MenuItem("Tools/Eternal Enigma/Tests/Check Dungeon Ground Heights")]
    public static void CheckDungeonGroundHeights()
    {
        var assembly = System.Reflection.Assembly.Load("EternalEnigma.Tests.EditMode");
        var type = assembly.GetType("EternalEnigma.Tests.CoreIntegration.DungeonThemeTests", true);
        var fixture = Activator.CreateInstance(type);
        var method = type.GetMethod("ThemedGroundIgnoresLegacyRaisedMapRoot");
        method.Invoke(fixture, new object[] { false });
        method.Invoke(fixture, new object[] { true });
        Debug.Log("Dungeon ground-height regression checks passed: regular and throne floors.");
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Check Dungeon Reload Recovery")]
    public static void CheckDungeonReloadRecovery()
    {
        var type = System.Reflection.Assembly.Load("EternalEnigma.Tests.EditMode")
            .GetType("EternalEnigma.Tests.CoreIntegration.CoreLayerGeneratorTests", true);
        foreach (bool throne in new[] { false, true })
        {
            var fixture = Activator.CreateInstance(type);
            type.GetMethod("SetUp").Invoke(fixture, null);
            try { type.GetMethod("RuntimeFloorRecoversAfterScriptReload").Invoke(fixture, new object[] { throne }); }
            finally { type.GetMethod("TearDown").Invoke(fixture, null); }
        }
        Debug.Log("Dungeon reload recovery checks passed: regular and throne floors.");
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Run Scenery Gameplay")]
    public static void RunSceneryGameplay() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.BiomeSceneryTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Hero Portraits")]
    public static void RunHeroPortraits() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.HeroPortraitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon HUD Portraits")]
    public static void RunDungeonHudPortraits() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.DungeonHudPortraitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Controls")]
    public static void RunDungeonControls() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.DungeonControlTests", "EternalEnigma.Tests.DungeonHudPortraitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Movement Regression")]
    public static void RunMovementRegression() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "MovementRegressionTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Entry Visuals")]
    public static void RunDungeonEntryVisuals() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignTravelTests.TownToDungeonRemovesTransferredHeroPositionCircles");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Enemy Behaviors")]
    public static void RunEnemyBehaviors() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnemyBehaviorTests", "EternalEnigma.Tests.EnemyPrefabTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Enemy Animation States")]
    public static void RunEnemyAnimationStates() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EnemyAnimationStateTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Floor Message")]
    public static void RunFloorMessage() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "FloorMessageTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Roster Additions")]
    public static void RunRosterAdditions() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnemyBehaviorTests.SilverDevilActsTwiceAndGoopiRootsUntilKilled", "EternalEnigma.Tests.EnemyBehaviorTests.StatueAndMetalSlimeUseStaticAndRecoloredModels");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Messages")]
    public static void RunMessages() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.GameMessageTests", "EternalEnigma.Tests.OverworldSceneTests", "EternalEnigma.Tests.InventorySkillTargetingTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Message Display")]
    public static void RunMessageDisplay() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.GameMessageTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Regression")]
    public static void RunBiomeRegression() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.BiomeSceneryTests", "EternalEnigma.Tests.SkillRegressionTests", "EternalEnigma.Tests.SkillMovementTests", "EternalEnigma.Tests.InventorySkillTargetingTests", "EternalEnigma.Tests.DungeonThemeTransitionTests", "EternalEnigma.Tests.DungeonThemeExplorerTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Scenery")]
    public static void RunBiomeScenery() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.BiomeSceneryTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Layouts")]
    public static void RunBiomeLayouts() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.BiomeLayoutIntegrationTests", "EternalEnigma.Tests.CoreIntegration.BiomePreviewTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Themes")]
    public static void RunDungeonThemes() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.DungeonThemeTransitionTests", "EternalEnigma.Tests.DungeonThemeExplorerTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Environment")]
    public static void RunEnvironment() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnvironmentPlaygroundTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Environment Assets")]
    public static void RunEnvironmentAssets() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.EnvironmentKitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run PlayMode")]
    public static void RunPlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation")]
    public static void RunPresentation() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignPresentationTests", "EternalEnigma.Tests.MenuSelectionTests",
        "EternalEnigma.Tests.CampaignTravelTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Bamao Dialog Preview")]
    public static void RunBamaoDialogPreview() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.AutoplayTests.InputPromptCancelAndReturnPreserveOriginalSave");

    [MenuItem("Tools/Eternal Enigma/Tests/Run ButtonStyles")]
    public static void RunButtonStyles() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignPresentationTests", "EternalEnigma.Tests.MenuSelectionTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Skills")]
    public static void RunSkills() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.SkillRegressionTests", "EternalEnigma.Tests.InventorySkillTargetingTests", "EternalEnigma.Tests.SkillRankRegressionTests", "EternalEnigma.Tests.CombatFoundationTests", "EternalEnigma.Tests.AllySkillPolicyTests", "EternalEnigma.Tests.AllyAiClassPartyTests", "EternalEnigma.Tests.ClassSkillSmokeTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run AllyAI")]
    public static void RunAllyAI() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.AllySkillPolicyTests", "EternalEnigma.Tests.AllyAiClassPartyTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Overworld")]
    public static void RunOverworld() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.OverworldSceneTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Enemies")]
    public static void RunEnemies() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnemyPrefabTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Heroes")]
    public static void RunHeroes() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.HeroPrefabTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Classes")]
    public static void RunClasses() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.ClassAssignmentTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Campaign")]
    public static void RunCampaign() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.CampaignTravelTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Autoplay")]
    public static void RunAutoplay() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.AutoplayTests.HidePanelAndTakeControlPreserveSessionAndSave",
        "EternalEnigma.Tests.AutoplayTests.InputPromptCancelAndReturnPreserveOriginalSave",
        "EternalEnigma.Tests.AutoplayTests.NormalModeAllowsDefeatAndWritesTuningReport",
        "EternalEnigma.Tests.AutoplayTests.DebugProtectsOnlyPartyAndRestoresResourceRulesOnExit");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Demo")]
    public static void RunDemo() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutOverwritingExistingSave");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Main Menu")]
    public static void RunMainMenu() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MainMenuPresentationTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutOverwritingExistingSave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.ContinueKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DungeonReturnKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.DungeonControlTests.FullControlPromptsForEachActionAndRestoresLeader");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Main Menu Presentation")]
    public static void RunMainMenuPresentation() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MainMenuPresentationTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Hero Selection")]
    public static void RunHeroSelection() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignPresentationTests.HeroPickerUsesRealArtworkAndBackDoesNotStartCampaign",
        "EternalEnigma.Tests.CampaignPresentationTests.SelectedHeroConfirmsExactlyOnce",
        "EternalEnigma.Tests.ClassAssignmentTests.NewSaveUsesTheChosenHeroesFixedClasses",
        "EternalEnigma.Tests.MainMenuPresentationTests.VisualsLoopWithoutGameplayAndMenuRemainsUsable");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Startup")]
    public static void RunDungeonStartup() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DirectDungeonStartCreatesPartyWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DirectDungeonStartPreservesExistingSave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutOverwritingExistingSave");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town")]
    public static void RunTown() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignTownServiceTests",
        "EternalEnigma.Tests.TownGameplayTests",
        "EternalEnigma.Tests.TownTrainerRankTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.InventoryCanOpenAndCloseImmediatelyAndSettingsRestoreItsSelection",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.MenuSceneNavigationTests.ContinueKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DungeonReturnKeepsTownCoveredUntilHeroCameraIsReady",
        "HarnessSmokeTests.TownScenarioLoadsSuppliedGoldAndAlly");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town Generation")]
    public static void RunTownGeneration() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignTownServiceTests",
        "EternalEnigma.Tests.CampaignTravelTests",
        "EternalEnigma.Tests.EnvironmentPlaygroundTests");

    private static void Run(TestMode mode, string filter, params string[] testFilters)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!string.IsNullOrEmpty(SessionState.GetString(SessionKey, "")))
            throw new InvalidOperationException("A harness run is already active.");
        SessionState.SetBool("EternalEnigma.Autoplay.ManualChecks", Array.Exists(testFilters,
            name => name.StartsWith("EternalEnigma.Tests.AutoplayTests.", StringComparison.Ordinal)));
        var run = new RunSummary { runId = Guid.NewGuid().ToString("N"), mode = mode.ToString(), state = "Queued", filter = filter, testFilters = testFilters };
        Directory.CreateDirectory("Temp/HarnessResults");
        SessionState.SetString(SessionKey, JsonUtility.ToJson(run));
        Write(run);
    }

    private static void StartQueuedRun()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var json = SessionState.GetString(SessionKey, "");
        if (string.IsNullOrEmpty(json)) return;
        var run = JsonUtility.FromJson<RunSummary>(json);
        if (run.state != "Queued") return;
        run.state = "Running";
        SessionState.SetString(SessionKey, JsonUtility.ToJson(run));
        Write(run);
        try
        {
            var id = Api.Execute(new ExecutionSettings(new Filter {
                testMode = (TestMode)Enum.Parse(typeof(TestMode), run.mode), assemblyNames = new[] { run.filter },
                testNames = run.testFilters == null || run.testFilters.Length == 0 ? null : run.testFilters
            }));
            SessionState.SetString(SessionKey + ".Job", id);
        }
        catch
        {
            run.state = "Completed";
            run.failed = 1;
            Write(run);
            SessionState.EraseString(SessionKey);
            throw;
        }
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Cancel Run")]
    public static void CancelRun()
    {
        var job = SessionState.GetString(SessionKey + ".Job", "");
        if (!string.IsNullOrEmpty(job)) TestRunnerApi.CancelTestRun(job);
        var json = SessionState.GetString(SessionKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            var run = JsonUtility.FromJson<RunSummary>(json);
            run.state = "Completed";
            run.failed = 1;
            Write(run);
        }
        SessionState.EraseString(SessionKey);
        SessionState.EraseString(SessionKey + ".Job");
        SessionState.EraseBool("EternalEnigma.Autoplay.ManualChecks");
    }

    private static void Write(RunSummary run) =>
        File.WriteAllText($"Temp/HarnessResults/{run.mode}.json", JsonUtility.ToJson(run, true));

    [Serializable]
    private sealed class RunSummary
    {
        public string runId;
        public string mode;
        public string state;
        public string filter;
        public string[] testFilters;
        public int passed;
        public int failed;
        public int skipped;
        public string xml;
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var run = JsonUtility.FromJson<RunSummary>(json);
            run.passed = result.PassCount;
            run.failed = result.FailCount;
            run.skipped = result.SkipCount;
            run.state = "Completed";
            run.xml = $"Temp/HarnessResults/{run.mode}.xml";
            TestRunnerApi.SaveResultToFile(result, run.xml);
            Write(run);
            SessionState.EraseString(SessionKey);
            SessionState.EraseString(SessionKey + ".Job");
            SessionState.EraseBool("EternalEnigma.Autoplay.ManualChecks");
        }
    }
}
