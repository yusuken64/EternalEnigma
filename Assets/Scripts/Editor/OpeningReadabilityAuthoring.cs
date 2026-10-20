using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Idempotent repair for the authored map group and ordinary hit effects.</summary>
public static class OpeningReadabilityAuthoring
{
    [MenuItem("Tools/Eternal Enigma/UI/Apply Opening Readability")]
    public static void Apply()
    {
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/DungeonScene.unity");
            var hud = Object.FindFirstObjectByType<DungeonHud>();
            var header = (TMP_Text)new SerializedObject(hud).FindProperty("header").objectReferenceValue;
            Object.FindFirstObjectByType<Minimap>().AuthorLocationLabel(header);
            var oldBacking = hud.PartyRoot.parent.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(t => t.name == "Floor label parchment");
            if (oldBacking != null) oldBacking.gameObject.SetActive(false);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }

        foreach (var guid in AssetDatabase.FindAssets("t:CombatEffectProfile", new[] { "Assets/Resources/CombatEffects/Profiles" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (!profile.name.StartsWith("Melee") || profile.Impact.Prefab == null || profile.Impact.Prefab.name != "LightSlashHit") continue;
            profile.Impact.Scale = .65f;
            profile.Impact.Lifetime = .3f;
            profile.Impact.FitToTarget = true;
            profile.Impact.ScaleWithTarget = false;
            profile.ImpactSeconds = .2f;
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Opening readability: floor grouped with map; ordinary melee impacts reduced.");
    }
}
