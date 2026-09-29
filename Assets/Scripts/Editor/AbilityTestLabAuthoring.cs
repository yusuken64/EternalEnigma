using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AbilityTestLabAuthoring
{
    public const string ScenePath = "Assets/Scenes/AbilityTestLab.unity";
    const string RestoreKey = "EternalEnigma.AbilityTestLab.PreviousStartScene";

    static AbilityTestLabAuthoring()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey + ".active", false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(RestoreKey, ""));
            SessionState.EraseBool(RestoreKey + ".active");
        };
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Create Or Refresh Ability Test Scene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var root = new GameObject("Ability Test Lab — press Play");
        SceneManager.MoveGameObjectToScene(root, scene);
        var lab = root.AddComponent<AbilityTestLab>();
        lab.Abilities = AssetDatabase.FindAssets("t:Skill").Select(g => AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null).OrderBy(s => s.SkillName).ToArray();
        var party = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Town/Allies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<TownAlly>(AssetDatabase.GUIDToAssetPath(g))).Where(a => a != null).ToArray();
        lab.PartyPrefabs = new[] { party.First(a => a.Name == "Rowan"), party.First(a => a.Name == "Reese") };
        lab.TargetPrefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab");
        int count = lab.Abilities.Length;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        SceneManager.SetActiveScene(original);
        Debug.Log($"Created {ScenePath}: {count} abilities.");
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Play Ability Test Scene")]
    public static void Play()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
        SessionState.SetString(RestoreKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(RestoreKey + ".active", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Check Running Ability Test Scene")]
    public static void Check()
    {
        var lab = AbilityTestLab.Active;
        if (lab == null || !lab.Ready || lab.Busy || lab.CastingAll) { Debug.LogWarning("Wait for the ability test scene to be ready."); return; }
        lab.StartCoroutine(lab.SmokeCheck());
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Capture Melee Impact In Test Scene")]
    public static void CaptureMelee()
    {
        var lab = AbilityTestLab.Active;
        if (lab != null && lab.Ready && !lab.Busy && !lab.CastingAll) lab.StartCoroutine(lab.CaptureMeleeImpact());
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Cast All In Test Scene")]
    public static void CastAll()
    {
        var lab = AbilityTestLab.Active;
        if (lab != null && lab.Ready && !lab.Busy && !lab.CastingAll) lab.StartCoroutine(lab.CastAllAbilities());
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Stop Casting All In Test Scene")]
    public static void StopAll() => AbilityTestLab.Active?.StopCastingAll();
}
