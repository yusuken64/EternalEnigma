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

    [MenuItem("Tools/Eternal Enigma/Tests/Run PlayMode")]
    public static void RunPlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode");

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
        "EternalEnigma.Tests.AutoplayTests.InputPromptCancelAndReturnPreserveOriginalSave",
        "EternalEnigma.Tests.AutoplayTests.NormalModeAllowsDefeatAndWritesTuningReport",
        "EternalEnigma.Tests.AutoplayTests.DebugProtectsOnlyPartyAndRestoresResourceRulesOnExit");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Demo")]
    public static void RunDemo() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutOverwritingExistingSave");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town")]
    public static void RunTown() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.TownGameplayTests",
        "EternalEnigma.Tests.TownTrainerRankTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.InventoryCanOpenAndCloseImmediatelyAndSettingsRestoreItsSelection",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.MenuSceneNavigationTests.ContinueKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DungeonReturnKeepsTownCoveredUntilHeroCameraIsReady",
        "HarnessSmokeTests.TownScenarioLoadsSuppliedGoldAndAlly");

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
