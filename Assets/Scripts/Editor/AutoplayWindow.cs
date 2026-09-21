using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Manual entry point only; deliberately independent of HarnessTestRunner and CI.</summary>
[InitializeOnLoad]
public sealed class AutoplayWindow : EditorWindow
{
    private const string SettingsKey = "EternalEnigma.Autoplay.Options";
    private const string RestoreKey = "EternalEnigma.Autoplay.Restore";
    private AutoplayOptions options;

    static AutoplayWindow() { EditorApplication.playModeStateChanged += PlayModeChanged; }

    [MenuItem("Tools/Eternal Enigma/Playthrough/Open")]
    public static void Open() => GetWindow<AutoplayWindow>("Playthrough");

    private void OnEnable()
    {
        options = JsonUtility.FromJson<AutoplayOptions>(EditorPrefs.GetString(SettingsKey, "{}")) ?? new AutoplayOptions();
        EditorApplication.update += Repaint;
    }
    private void OnDisable() { EditorApplication.update -= Repaint; }

    private void OnGUI()
    {
        var run = AutoplayRunner.Active;
        if (run != null)
        {
            EditorGUILayout.LabelField(run.Options.DebugPlaythrough ? "Debug playthrough" : "Normal playthrough", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(run.Status, MessageType.Info);
            EditorGUILayout.LabelField("Actions / dungeon turns", run.Report.Actions + " / " + run.Report.DungeonTurns);
            if (run.Running)
            {
                if (GUILayout.Button(run.Paused ? "Resume" : "Pause")) run.SetPaused(!run.Paused);
                if (GUILayout.Button("Stop and report")) run.Stop();
            }
            if (GUILayout.Button("Open report folder")) EditorUtility.RevealInFinder(run.DirectoryPath);
            EditorGUILayout.HelpBox("The run uses an isolated save. Exit Play Mode when finished inspecting it. Continue from the saved snapshot follows normal interrupted-dungeon recovery; the action log and live scene preserve the failure details.", MessageType.None);
            return;
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            options.Seed = EditorGUILayout.IntField("Seed", options.Seed);
            options.DebugPlaythrough = EditorGUILayout.Toggle("Debug playthrough", options.DebugPlaythrough);
            if (options.DebugPlaythrough)
            {
                options.Godmode = EditorGUILayout.Toggle("Godmode + infinite strength", options.Godmode);
                options.InfiniteResources = EditorGUILayout.Toggle("Infinite resources", options.InfiniteResources);
                EditorGUILayout.HelpBox("Infinite resources replenishes gold, SP and hunger, and preserves used consumables. Keys, companions and abilities must still be earned. Debug runs are excluded from balance results.", MessageType.None);
            }
            options.ExploreAll = EditorGUILayout.Toggle("Visit all destinations", options.ExploreAll);
            options.Speed = EditorGUILayout.Slider("Playback speed", options.Speed, 1, 10);
            options.TimeLimitMinutes = Mathf.Max(1, EditorGUILayout.FloatField("Time limit (minutes)", options.TimeLimitMinutes));
            options.StallSeconds = Mathf.Max(10, EditorGUILayout.FloatField("Stall timeout (seconds)", options.StallSeconds));
            EditorGUILayout.HelpBox("Manual only. Normal runs can lose. The policy reads the current campaign and uses full dungeon map knowledge with the shared A*. Optional coverage visits each destination and completes each dungeon once.", MessageType.Info);
            if (GUILayout.Button("Start playthrough")) Launch(options);
        }
    }

    public static void Launch(AutoplayOptions settings)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Debug.LogWarning("Wait for Unity to finish importing before starting a playthrough."); return; }
        string json = JsonUtility.ToJson(settings);
        EditorPrefs.SetString(SettingsKey,json);
        SessionState.SetString(AutoplayRunner.PendingKey,json);
        SessionState.SetBool(RestoreKey,true);
        SessionState.SetString(RestoreKey+".scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(RestoreKey+".suppression", LoadingSceneIntegration.SuppressAutomaticSceneLoading);
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = true;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Common.unity");
        EditorApplication.isPlaying = true;
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && AutoplayRunner.Active != null) AutoplayRunner.Active.Stop();
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey,false)) return;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(RestoreKey+".scene",""));
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = SessionState.GetBool(RestoreKey+".suppression",false);
        SessionState.EraseBool(RestoreKey); SessionState.EraseString(AutoplayRunner.PendingKey);
    }

    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Normal")]
    public static void RunNormal() => Launch(new AutoplayOptions());
    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Debug")]
    public static void RunDebug() => Launch(new AutoplayOptions { DebugPlaythrough = true });
    [MenuItem("Tools/Eternal Enigma/Playthrough/Stop and Report")]
    public static void StopRun() => AutoplayRunner.Active?.Stop();
}
