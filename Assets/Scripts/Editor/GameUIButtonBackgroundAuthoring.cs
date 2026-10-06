using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time migration to nested backgrounds; Button identities and events stay intact.</summary>
public static class GameUIButtonBackgroundAuthoring
{
    public const string BackgroundPath = "Assets/Resources/UI/GameButtonBackground.prefab";
    private static readonly string[] VisualProperties =
        { "m_Sprite", "m_Type", "m_PixelsPerUnitMultiplier" };

    public static void RequireSavedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before authoring button backgrounds.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Save and close Prefab Mode before authoring button backgrounds.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the open scene edits before authoring button backgrounds.");
    }

    private static GameObject BackgroundPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackgroundPath);
        if (prefab != null)
        {
            if (prefab.GetComponent<LayoutElement>() == null)
            {
                var contents = PrefabUtility.LoadPrefabContents(BackgroundPath);
                try
                {
                    contents.AddComponent<LayoutElement>().ignoreLayout = true;
                    PrefabUtility.SaveAsPrefabAsset(contents, BackgroundPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            return prefab;
        }
        var rect = GameUISkin.Rect("Button background", null, Vector2.zero, Vector2.one);
        try
        {
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            var image = rect.gameObject.AddComponent<Image>();
            GameUITheme.Current.Surface(image, GameUITheme.Current.Button, 1);
            image.raycastTarget = true;
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return PrefabUtility.SaveAsPrefabAsset(rect.gameObject, BackgroundPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(rect.gameObject); }
    }

    public static bool IsShared(Image image) => image != null &&
        (AssetDatabase.GetAssetPath(image) == BackgroundPath ||
         AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(image)) == BackgroundPath);

    public static bool EnsureBackground(Button button)
    {
        if (button.targetGraphic != null && button.targetGraphic.GetComponent<DungeonUIRole>() != null) return false;
        var oldImage = button.targetGraphic as Image;
        var background = IsShared(oldImage) ? oldImage : button.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(image => IsShared(image) && image.GetComponentInParent<Button>(true) == button);
        bool changed = false;
        if (background == null)
        {
            // Keep the original Image component for any serialized references. Nesting under
            // its RectTransform preserves custom background geometry as well as button layout.
            var parent = oldImage != null ? oldImage.transform : button.transform;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(BackgroundPrefab(), parent);
            instance.transform.SetAsFirstSibling();
            background = instance.GetComponent<Image>();
            changed = true;
        }
        if (oldImage != null && oldImage != background && oldImage.enabled)
        {
            if (oldImage.GetComponent<Mask>() != null)
                throw new InvalidOperationException($"Button background is also a mask: {button.name}");
            oldImage.enabled = false;
            Record(oldImage);
            changed = true;
        }
        if (button.targetGraphic != background)
        {
            button.targetGraphic = background;
            Record(button);
            changed = true;
        }
        // Clear only styling overrides. Geometry and interaction colors belong to the button.
        var serialized = new SerializedObject(background);
        foreach (string name in VisualProperties)
        {
            var property = serialized.FindProperty(name);
            if (!property.prefabOverride) continue;
            PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
            serialized.Update();
            changed = true;
        }
        return changed;
    }

    private static void Record(UnityEngine.Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value))
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }

    public static void ValidateBackground(Image image, string path)
    {
        if (!IsShared(image) || !image.enabled || !image.raycastTarget || image.overrideSprite != image.sprite)
            throw new InvalidOperationException($"Missing inherited button background: {path} / {image.name}");
        // Follow every prefab layer so a variant cannot hide a conflicting inherited override.
        for (var current = image; current != null && AssetDatabase.GetAssetPath(current) != BackgroundPath;
             current = PrefabUtility.GetCorrespondingObjectFromSource(current))
        {
            var serialized = new SerializedObject(current);
            foreach (string name in VisualProperties)
                if (serialized.FindProperty(name).prefabOverride)
                    throw new InvalidOperationException($"Button background overrides {name}: {path} / {image.name}");
        }
    }

    // Includes custom Button subclasses, navigation, persistent callbacks and interaction colors.
    private static string ButtonState(Button button) => Regex.Replace(EditorJsonUtility.ToJson(button),
        "\"m_TargetGraphic\":\\{[^}]*\\}", "\"m_TargetGraphic\":{}");

    private static bool MigrateHierarchy(GameObject root, ref int count)
    {
        bool changed = false;
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            string before = ButtonState(button);
            changed |= EnsureBackground(button);
            if (ButtonState(button) != before)
                throw new InvalidOperationException($"Migration changed button behavior: {button.name}");
            ValidateBackground((Image)button.targetGraphic, root.name);
            count++;
        }
        return changed;
    }

    [MenuItem("Tools/Eternal Enigma/UI/Migrate Button Backgrounds to Shared Prefab")]
    public static void Migrate()
    {
        RequireSavedScenes();
        BackgroundPrefab();
        int count = 0;
        string changes = MigrateAssets(root => MigrateHierarchy(root, ref count),
            root => root.GetComponentInChildren<Button>(true) != null);
        string result = $"PASS: {count} buttons checked; {changes} Button behavior preserved.";
        File.WriteAllText("Temp/UIValidation/button-background-migration.txt", result);
        Debug.Log(result);
    }

    internal static string MigrateAssets(Func<GameObject, bool> migrate, Func<GameObject, bool> relevant)
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath).ToHashSet();
        var ordered = new List<string>();
        var visited = new HashSet<string>();
        void Visit(string path)
        {
            if (!visited.Add(path)) return;
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                if (paths.Contains(dependency)) Visit(dependency);
            ordered.Add(path);
        }
        foreach (string path in paths.OrderBy(p => p)) Visit(path);
        int changedPrefabs = 0, changedScenes = 0;
        foreach (string path in ordered)
        {
            if (!relevant(AssetDatabase.LoadAssetAtPath<GameObject>(path))) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!migrate(root)) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changedPrefabs++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= migrate(root);
                if (!changed) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changedScenes++;
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Temp/UIValidation");
        return $"{changedPrefabs} prefabs and {changedScenes} scenes updated.";
    }
}
