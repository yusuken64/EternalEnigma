using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public sealed class ChaosWindow : EditorWindow
{
    private const string RestoreKey = "EternalEnigma.Chaos.Restore";
    private ChaosOptions options = new();
    private Vector2 scroll;
    static ChaosWindow() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Eternal Enigma/Chaos/Open")]
    public static void Open() => GetWindow<ChaosWindow>("Input chaos");
    [MenuItem("Tools/Eternal Enigma/Chaos/Run From File")]
    public static void FromFile() => Launch(ChaosJson.Read<ChaosOptions>("Temp/Chaos/options.json"));
    [MenuItem("Tools/Eternal Enigma/Chaos/Stop and Report")]
    public static void Stop() => ChaosRunner.Active?.Stop();
    public static void Launch(ChaosOptions options)
    {
        options.Validate();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Wait for imports and exit Play Mode before launching chaos.");
        if (!string.IsNullOrEmpty(SessionState.GetString(AutoplayRunner.PendingKey, ""))) throw new InvalidOperationException("Autoplay is queued.");
        SessionState.SetString(ChaosRunner.PendingKey, JsonUtility.ToJson(options));
        SessionState.SetBool(RestoreKey, true);
        SessionState.SetString(RestoreKey + ".scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(RestoreKey + ".suppression", LoadingSceneIntegration.SuppressAutomaticSceneLoading);
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = true;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) ChaosRunner.Active?.Stop();
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey, false)) return;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(RestoreKey + ".scene", ""));
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = SessionState.GetBool(RestoreKey + ".suppression", false);
        SessionState.EraseBool(RestoreKey); SessionState.EraseString(ChaosRunner.PendingKey);
    }
    private void OnGUI()
    {
        if (ChaosRunner.Active != null)
        {
            EditorGUILayout.LabelField(ChaosRunner.Active.Status, EditorStyles.wordWrappedLabel);
            EditorGUILayout.SelectableLabel(ChaosRunner.Active.DirectoryPath);
            if (GUILayout.Button("Stop and report")) Stop();
            Repaint(); return;
        }
        using var view = new EditorGUILayout.ScrollViewScope(scroll); scroll = view.scrollPosition;
        options.Mode = (ChaosMode)EditorGUILayout.EnumPopup("Mode", options.Mode);
        options.Seed = EditorGUILayout.IntField("Seed", options.Seed);
        options.RunCount = EditorGUILayout.IntField("Runs", options.RunCount);
        options.DurationSeconds = EditorGUILayout.FloatField("Smoke duration (seconds)", options.DurationSeconds);
        options.Device = (ChaosDevice)EditorGUILayout.EnumPopup("Devices", options.Device);
        options.ScenarioFilter = EditorGUILayout.TextField("Scenario filter", options.ScenarioFilter);
        options.AbilityFilter = EditorGUILayout.TextField("Ability filter", options.AbilityFilter);
        options.ActionTimeoutSeconds = EditorGUILayout.FloatField("Action deadline", options.ActionTimeoutSeconds);
        options.LoadTimeoutSeconds = EditorGUILayout.FloatField("Loading deadline", options.LoadTimeoutSeconds);
        options.RecoverySeconds = EditorGUILayout.FloatField("Input recovery seconds", options.RecoverySeconds);
        options.ReportRoot = EditorGUILayout.TextField("Report directory", options.ReportRoot);
        options.ScenarioDirectory = EditorGUILayout.TextField("Full scenario directory", options.ScenarioDirectory);
        options.ReplayDirectory = EditorGUILayout.TextField("Replay report", options.ReplayDirectory);
        options.ResumeDirectory = EditorGUILayout.TextField("Resume report", options.ResumeDirectory);
        EditorGUILayout.HelpBox("Isolated saves and preferences. Random party/items are fixed before each scenario. Missing cases remain untested; smoke is not full acceptance.", MessageType.Info);
        if (GUILayout.Button("Start input chaos")) Launch(options);
    }
    [MenuItem("Tools/Eternal Enigma/Chaos/Build Windows Development Player")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
        Directory.CreateDirectory("Temp/Chaos"); File.WriteAllText("Temp/Chaos/build.txt", "Running");
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64, locationPathName = "Builds/Chaos/EternalEnigma-Chaos.exe", options = BuildOptions.Development });
            File.WriteAllText("Temp/Chaos/build.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Chaos player build failed.");
        }
        catch (Exception error) { File.WriteAllText("Temp/Chaos/build.txt", "Failed\n" + error); throw; }
    }
}
