using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LockTestLabAuthoring
{
    public const string ScenePath = "Assets/Scenes/LockTestLab.unity";
    private const string RestoreKey = "EternalEnigma.LockTestLab.PreviousStartScene";
    static LockTestLabAuthoring()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey + ".active", false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(RestoreKey, ""));
            SessionState.EraseBool(RestoreKey + ".active");
        };
    }

    [MenuItem("Tools/Eternal Enigma/Locks/Create Or Refresh Lock Test Scene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var root = new GameObject("Lock Test Lab - press Play");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<LockTestLab>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(original); }
        Debug.Log("Created " + ScenePath + ". Every authored overworld lock and named gate key is available.");
    }

    [MenuItem("Tools/Eternal Enigma/Locks/Play Lock Test Scene")]
    public static void Play()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
        SessionState.SetString(RestoreKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(RestoreKey + ".active", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.isPlaying = true;
    }
}
