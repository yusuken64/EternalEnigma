#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Explicit command-line entry point for the production debug autorunner.</summary>
public sealed class AutoplayBatchPlayer : MonoBehaviour
{
    private AutoplayOptions options;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Application.isEditor) return;
        var arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, "--debug-autoplay-options");
        if (index < 0) return;
        try
        {
            if (index + 1 >= arguments.Length) throw new ArgumentException("Missing debug autoplay options path.");
            var settings = JsonUtility.FromJson<AutoplayOptions>(File.ReadAllText(arguments[index + 1]));
            if (settings == null || !settings.Validate(out _) || settings.Mode != AutoplayMode.Campaign ||
                !settings.DebugPlaythrough || string.IsNullOrWhiteSpace(settings.ReportRoot))
                throw new ArgumentException("Expected valid debug campaign options with an explicit ReportRoot.");
            var host = new GameObject("Debug campaign batch player");
            DontDestroyOnLoad(host);
            host.AddComponent<AutoplayBatchPlayer>().options = settings;
        }
        catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        double deadline = Time.realtimeSinceStartupAsDouble + 180;
        while (FindFirstObjectByType<MainMenu>()?.IsReady != true)
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) { Debug.LogError("Debug batch timed out waiting for main menu."); Application.Quit(1); yield break; }
            yield return null;
        }
        Screen.SetResolution(640, 360, FullScreenMode.Windowed);
        AutoplayRunner.StartBatch(options);
        var run = AutoplayRunner.Active;
        while (run.SessionRunning) yield return null;
        while (run.Finalizing || run.Advancing) yield return null;
        bool passed = run.Batch.State == "Completed" && run.Batch.Runs.Count == options.RunCount &&
            run.Batch.Runs.All(row => row.Outcome == "Victory" && row.ArtifactsComplete && row.Issues == 0);
        Debug.Log($"Debug campaign batch {(passed ? "passed" : "failed")}: {run.BatchDirectoryPath}");
        run.ExitDemo();
        deadline = Time.realtimeSinceStartupAsDouble + 180;
        while (AutoplayRunner.Active != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Application.Quit(passed && AutoplayRunner.Active == null ? 0 : 1);
    }
}
#endif
