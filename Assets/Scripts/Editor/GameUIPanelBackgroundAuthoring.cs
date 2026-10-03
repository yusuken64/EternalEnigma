using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared parchment backgrounds, without replacing panel containers or their contents.</summary>
public static class GameUIPanelBackgroundAuthoring
{
    public const string BackgroundPath = "Assets/Resources/UI/GamePanelBackground.prefab";
    private static readonly string[] VisualProperties =
        { "m_Sprite", "m_Type", "m_PixelsPerUnitMultiplier" };

    private static GameObject BackgroundPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackgroundPath);
        if (prefab != null) return prefab;
        var rect = GameUISkin.Rect("GamePanelBackground", null, Vector2.zero, Vector2.one);
        try
        {
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            GameUITheme.Current.Surface(rect.gameObject.AddComponent<Image>(), GameUITheme.Current.Panel, 1);
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return PrefabUtility.SaveAsPrefabAsset(rect.gameObject, BackgroundPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(rect.gameObject); }
    }

    public static bool IsShared(Image image) => image != null &&
        (AssetDatabase.GetAssetPath(image) == BackgroundPath ||
         AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(image)) == BackgroundPath);

    private static bool IsPanel(Image image) => image != null && !IsShared(image) &&
        image.sprite == GameUITheme.Current.Panel && image.GetComponentInParent<Button>(true) == null &&
        image.GetComponent<Mask>() == null && image.GetComponentInParent<ScreenTransition>(true) == null &&
        image.GetComponentInParent<NewFloorMessage>(true) == null;

    private static Image ChildBackground(Image image) =>
        image.transform.Cast<Transform>().Select(child => child.GetComponent<Image>()).FirstOrDefault(IsShared);

    private static void Record(UnityEngine.Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }

    private static bool EnsureBackground(Image oldImage, out Image background)
    {
        background = ChildBackground(oldImage);
        bool changed = false;
        if (background == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(BackgroundPrefab(), oldImage.transform);
            instance.transform.SetAsFirstSibling();
            background = instance.GetComponent<Image>();
            // Local tints, visibility and input blocking are behavior, not shared artwork.
            background.color = oldImage.color;
            background.enabled = oldImage.enabled;
            background.raycastTarget = oldImage.raycastTarget;
            background.raycastPadding = oldImage.raycastPadding;
            background.maskable = oldImage.maskable;
            background.material = oldImage.material;
            Record(background);
            changed = true;
        }
        if (oldImage.enabled)
        {
            oldImage.enabled = false;
            Record(oldImage);
            changed = true;
        }
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

    public static bool MigrateHierarchy(GameObject root)
    {
        bool changed = false;
        var replacements = new Dictionary<Image, Image>();
        foreach (var image in root.GetComponentsInChildren<Image>(true).Where(IsPanel))
        {
            changed |= EnsureBackground(image, out var background);
            replacements.Add(image, background);
        }
        if (replacements.Count == 0) return changed;
        // Redirect Image fields (including Selectable.targetGraphic) while preserving container
        // GameObject/RectTransform references. Include other roots for cross-root scene references.
        foreach (var sceneRoot in root.scene.GetRootGameObjects())
        foreach (var component in sceneRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            bool replaced = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue is not Image oldImage ||
                    !replacements.TryGetValue(oldImage, out var background)) continue;
                property.objectReferenceValue = background;
                replaced = true;
            }
            if (!replaced) continue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Record(component);
            changed = true;
        }
        return changed;
    }

    private static void Check(Image image, string path)
    {
        if (image.sprite != GameUITheme.Current.Panel || image.type != Image.Type.Sliced || image.pixelsPerUnitMultiplier != 1)
            throw new InvalidOperationException($"Invalid panel background: {path} / {image.name}");
        for (var current = image; current != null && AssetDatabase.GetAssetPath(current) != BackgroundPath;
             current = PrefabUtility.GetCorrespondingObjectFromSource(current))
        {
            var serialized = new SerializedObject(current);
            foreach (string name in VisualProperties)
                if (serialized.FindProperty(name).prefabOverride)
                    throw new InvalidOperationException($"Panel background overrides {name}: {path} / {image.name}");
        }
    }

    private static int ValidateHierarchy(GameObject root, string path)
    {
        int count = 0;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (IsPanel(image) && (image.enabled || ChildBackground(image) == null))
                throw new InvalidOperationException($"Panel has not been migrated: {path} / {image.name}");
            if (!IsShared(image)) continue;
            Check(image, path);
            count++;
        }
        return count;
    }

    [MenuItem("Tools/Eternal Enigma/UI/Migrate Panel Backgrounds to Shared Prefab")]
    public static void Migrate()
    {
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        BackgroundPrefab();
        string changes = GameUIButtonBackgroundAuthoring.MigrateAssets(MigrateHierarchy,
            root => root.GetComponentsInChildren<Image>(true).Any(IsPanel));
        File.WriteAllText("Temp/UIValidation/panel-background-migration.txt", changes);
        Validate();
        Debug.Log($"Panel backgrounds: {changes}");
    }

    [MenuItem("Tools/Eternal Enigma/UI/Validate Saved Panel Styles")]
    public static void Validate()
    {
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            count += ValidateHierarchy(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects()) count += ValidateHierarchy(root, path);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Directory.CreateDirectory("Temp/UIValidation");
        File.WriteAllText("Temp/UIValidation/saved-panel-validation.txt",
            $"PASS: {count} panel backgrounds inherit sliced parchment with multiplier 1.");
    }
}
