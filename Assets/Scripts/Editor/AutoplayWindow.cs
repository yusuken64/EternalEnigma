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
    private Vector2 scroll;

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
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        try
        {
            EditorGUIUtility.labelWidth = Mathf.Min(210, position.width * .55f);
            DrawWindow();
        }
        finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
    }

    private void DrawWindow()
    {
        using var scrollScope = new EditorGUILayout.ScrollViewScope(scroll);
        scroll = scrollScope.scrollPosition;
        var run = AutoplayRunner.Active;
        if (run != null)
        {
            EditorGUILayout.LabelField(run.IsLootSweep ? "Floor loot sweep" : run.IsFinaleDebug ? "Direct-start finale debug" : run.Options.DebugPlaythrough ? "Debug playthrough" : "Normal playthrough", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(run.Status, MessageType.Info);
            EditorGUILayout.LabelField(run.ProgressLabel,EditorStyles.boldLabel);
            if (run.IsLootSweep) EditorGUILayout.LabelField("Loot collected / expected", $"{run.Report?.CollectedLoot ?? 0} / {run.Report?.ExpectedLoot ?? 0}");
            EditorGUILayout.LabelField("Actions / dungeon turns", (run.Report?.Actions ?? 0) + " / " + (run.Report?.DungeonTurns ?? 0));
            if (run.SessionRunning)
            {
                float speed = EditorGUILayout.Slider("Playback speed",run.Options.Speed,.5f,32);
                if (!Mathf.Approximately(speed,run.Options.Speed)) run.SetSpeed(speed);
                if (GUILayout.Button(run.Paused ? "Resume" : "Pause")) run.SetPaused(!run.Paused);
                if (GUILayout.Button("Stop and report")) run.Stop();
            }
            if (GUILayout.Button("Open report folder")) EditorUtility.RevealInFinder(run.DirectoryPath);
            if (GUILayout.Button("Open batch folder")) EditorUtility.RevealInFinder(run.BatchDirectoryPath);
            EditorGUILayout.SelectableLabel(run.DirectoryPath,EditorStyles.wordWrappedLabel,GUILayout.MinHeight(48));
            EditorGUILayout.HelpBox("The run uses an isolated save. Exit Play Mode when finished inspecting it. Continue from the saved snapshot follows normal interrupted-dungeon recovery; the action log and live scene preserve the failure details.", MessageType.None);
            return;
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var mode = (AutoplayMode)EditorGUILayout.EnumPopup("Mode", options.Mode);
            if (mode != options.Mode) options = mode == AutoplayMode.FloorLootSweep ? AutoplayOptions.LootSweep() : mode == AutoplayMode.Finale ? AutoplayOptions.FinaleDebug() : new AutoplayOptions();
            bool sweep = options.Mode == AutoplayMode.FloorLootSweep;
            options.Seed = EditorGUILayout.IntField("Starting seed", options.Seed);
            options.RunCount = EditorGUILayout.IntField(sweep ? "Floor count" : "Run count", options.RunCount);
            if (sweep)
            {
                options.ResumeManifestPath = EditorGUILayout.TextField("Resume manifest", options.ResumeManifestPath);
                EditorGUILayout.HelpBox("Validation only. Collects all loot through gameplay with debug survivability, 32× playback and expanded inventory. Resume keeps prior attempts and skips only finalized cases on the same build/content version.", MessageType.Info);
            }
            else
            {
            options.DebugPlaythrough = EditorGUILayout.Toggle("Debug playthrough", options.DebugPlaythrough);
            if (options.DebugPlaythrough)
            {
                options.Godmode = EditorGUILayout.Toggle("Godmode", options.Godmode);
                options.InfiniteStrength = EditorGUILayout.Toggle("Infinite strength", options.InfiniteStrength);
                options.InfiniteResources = EditorGUILayout.Toggle("Infinite resources", options.InfiniteResources);
                options.DebugCoverage = EditorGUILayout.Toggle("Item and ability coverage", options.DebugCoverage);
                EditorGUILayout.HelpBox("Infinite resources replenishes gold, SP and hunger, and preserves used consumables. Keys, companions and abilities must still be earned. Debug runs are excluded from balance results.", MessageType.None);
            }
            if (options.Mode != AutoplayMode.Finale) options.ExploreAll = EditorGUILayout.Toggle("Visit all destinations", options.ExploreAll);
            else EditorGUILayout.HelpBox("Direct start at floor 1 with an endgame party. Both floors and waking at home are required. Excluded from full-campaign acceptance.", MessageType.Info);
            options.NoAnimations = EditorGUILayout.Toggle("Disable animations for run", options.NoAnimations);
            }
            options.Speed = EditorGUILayout.Slider("Playback speed", options.Speed, .5f, 32);
            options.TimeLimitMinutes = Mathf.Max(1, EditorGUILayout.FloatField("Time limit (minutes)", options.TimeLimitMinutes));
            options.StallSeconds = Mathf.Max(10, EditorGUILayout.FloatField("Stall timeout (seconds)", options.StallSeconds));
            options.ActionLimit = EditorGUILayout.IntField("Action limit", options.ActionLimit);
            EditorGUILayout.HelpBox("Manual only. Normal runs can lose. The policy reads the current campaign and uses full dungeon map knowledge with the shared A*. Optional coverage visits each destination and completes each dungeon once.", MessageType.Info);
            bool valid = options.Validate(out var reason);
            if (!valid) EditorGUILayout.HelpBox(reason,MessageType.Error);
            using (new EditorGUI.DisabledScope(!valid))
                if (GUILayout.Button("Start playthrough")) Launch(options);
        }
    }

    public static void Launch(AutoplayOptions settings)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (settings == null || !settings.Validate(out _)) { Debug.LogWarning("Invalid autoplay options. Check run count and seed range."); return; }
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

    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Debug Finale")]
    public static void RunDebugFinale() => Launch(AutoplayOptions.FinaleDebug());
    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Debug Finale From File")]
    public static void RunDebugFinaleFromFile()
    {
        var settings = AutoplayOptions.FinaleDebug();
        JsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText("Temp/FinaleDebug/options.json"), settings);
        if (settings.Mode != AutoplayMode.Finale) throw new System.ArgumentException("Expected direct finale options.");
        Launch(settings);
    }
    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Normal")]
    public static void RunNormal() => Launch(new AutoplayOptions());
    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Debug")]
    public static void RunDebug() => Launch(new AutoplayOptions { DebugPlaythrough = true, DebugCoverage = true });
    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Debug From File")]
    public static void RunDebugFromFile()
    {
        var settings = JsonUtility.FromJson<AutoplayOptions>(System.IO.File.ReadAllText("Temp/DebugAutoplay/options.json"));
        if (settings.Mode != AutoplayMode.Campaign || !settings.DebugPlaythrough)
            throw new System.ArgumentException("Expected debug campaign options.");
        Launch(settings);
    }
    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Floor Loot Sweep")]
    public static void RunFloorLootSweep() => Launch(AutoplayOptions.LootSweep());
    [MenuItem("Tools/Eternal Enigma/Playthrough/Run Floor Loot Sweep From File")]
    public static void RunFloorLootSweepFromFile()
    {
        var settings = JsonUtility.FromJson<AutoplayOptions>(System.IO.File.ReadAllText("Temp/FloorLootSweep/options.json"));
        if (settings.Mode != AutoplayMode.FloorLootSweep) throw new System.ArgumentException("Expected floor sweep options.");
        Launch(settings);
    }
    [MenuItem("Tools/Eternal Enigma/Playthrough/Stop and Report")]
    public static void StopRun() => AutoplayRunner.Active?.Stop();
}
