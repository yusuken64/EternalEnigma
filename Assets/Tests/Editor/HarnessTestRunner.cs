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
    public static void RunEditMode() => Run(TestMode.EditMode, "SaveStoreTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run PlayMode")]
    public static void RunPlayMode() => Run(TestMode.PlayMode, "HarnessSmokeTests");

    private static void Run(TestMode mode, string filter)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!string.IsNullOrEmpty(SessionState.GetString(SessionKey, "")))
            throw new InvalidOperationException("A harness run is already active.");
        var run = new RunSummary { runId = Guid.NewGuid().ToString("N"), mode = mode.ToString(), state = "Queued", filter = filter };
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
                testMode = (TestMode)Enum.Parse(typeof(TestMode), run.mode), testNames = new[] { run.filter }
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
        }
    }
}
