using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class ChaosTestLauncher
{
    private const string ModeKey = "EternalEnigma.Chaos.Tests.Mode";
    private const string JobKey = "EternalEnigma.Chaos.Tests.Job";
    private static readonly TestRunnerApi Api;
    static ChaosTestLauncher()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Api.RegisterCallbacks(new Results());
    }
    [MenuItem("Tools/Eternal Enigma/Tests/Run Chaos EditMode")]
    public static void EditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.ChaosTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Chaos PlayMode")]
    public static void PlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.ChaosInputTests", "EternalEnigma.Tests.ChaosMenuTests");
    private static void Run(TestMode mode, params string[] names)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || SessionState.GetInt(ModeKey, -1) >= 0)
            throw new System.InvalidOperationException("A Play Mode or chaos test run is already active.");
        SessionState.SetInt(ModeKey, (int)mode);
        try { SessionState.SetString(JobKey, Api.Execute(new ExecutionSettings(new Filter { testMode = mode, testNames = names }))); }
        catch { SessionState.EraseInt(ModeKey); throw; }
    }
    [MenuItem("Tools/Eternal Enigma/Tests/Cancel Chaos Tests")]
    public static void Cancel()
    {
        var job = SessionState.GetString(JobKey, "");
        if (!string.IsNullOrEmpty(job)) TestRunnerApi.CancelTestRun(job);
        int pending = SessionState.GetInt(ModeKey, -1);
        if (pending >= 0)
        {
            Directory.CreateDirectory("Temp/Chaos");
            File.WriteAllText("Temp/Chaos/tests-" + (TestMode)pending + ".txt", "Cancelled; no passing result.");
        }
        SessionState.EraseInt(ModeKey);
        SessionState.EraseString(JobKey);
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test)
        {
            int mode = SessionState.GetInt(ModeKey, -1); if (mode < 0) return;
            Directory.CreateDirectory("Temp/Chaos"); File.WriteAllText("Temp/Chaos/tests-" + (TestMode)mode + ".txt", "Running");
        }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            int pending = SessionState.GetInt(ModeKey, -1); if (pending < 0) return;
            var mode = (TestMode)pending;
            SessionState.EraseInt(ModeKey);
            SessionState.EraseString(JobKey);
            TestRunnerApi.SaveResultToFile(result, "Temp/Chaos/tests-" + mode + ".xml");
            File.WriteAllText("Temp/Chaos/tests-" + mode + ".txt", $"Completed\nPassed: {result.PassCount}\nFailed: {result.FailCount}\nSkipped: {result.SkipCount}\n{result.Message}");
        }
    }
}
