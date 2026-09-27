using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit authoring command. Never runs in a player or when entering Play Mode.</summary>
public static class GameUIButtonAuthoring
{
    private const string ButtonPath = "Assets/Resources/UI/GameButton.prefab";
    private const string SkinPath = "Assets/Resources/UI/GameSkin.guiskin";

    [MenuItem("Tools/Eternal Enigma/UI/Validate Saved Button Styles")]
    public static void Validate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Validate outside Play Mode.");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Button.png");
        int count = 0;
        void Check(Button button, string path)
        {
            if (!(button.targetGraphic is Image image) || image.sprite != sprite ||
                image.type != Image.Type.Sliced || button.transition != Selectable.Transition.ColorTint)
                throw new InvalidOperationException($"Missing serialized style: {path} / {button.name}");
            count++;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var button in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Button>(true)) Check(button, path);
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var button in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true))) Check(button, path);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        File.WriteAllText("Temp/UIValidation/saved-button-validation.txt", $"PASS: {count} saved scene/prefab buttons have serialized styles outside Play Mode.");
    }

    [MenuItem("Tools/Eternal Enigma/UI/Bake Button Styles")]
    public static void Bake()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before baking UI assets.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the open scene edits before baking button styles.");
        CreateButtonPrefab();
        CreateLegacySkin();
        int prefabButtons = 0, sceneButtons = 0;
        var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Concat(AssetDatabase.GetDependencies(scenes, true).Where(p => p.EndsWith(".prefab"))).Distinct().OrderBy(p => p).ToArray();
        foreach (string path in prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var buttons = root.GetComponentsInChildren<Button>(true);
                foreach (var button in buttons) Style(button);
                if (buttons.Length > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabButtons += buttons.Length;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var buttons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)).ToArray();
                foreach (var button in buttons)
                {
                    Style(button);
                    foreach (var component in button.GetComponentsInChildren<Component>(true))
                        if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                if (buttons.Length > 0) EditorSceneManager.SaveScene(scene);
                sceneButtons += buttons.Length;
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Temp/UIValidation");
        File.WriteAllText("Temp/UIValidation/baked-buttons.txt", $"Serialized {sceneButtons} scene buttons and {prefabButtons} prefab buttons.\n");
        Debug.Log($"Baked styles into {sceneButtons} scene buttons and {prefabButtons} prefab buttons.");
    }

    private static void Style(Button button)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Button.png");
        var image = button.targetGraphic as Image;
        bool hasLabel = button.GetComponentInChildren<TMP_Text>(true) != null || button.GetComponentInChildren<Text>(true) != null;
        if (image == null || (!hasLabel && image.sprite != sprite))
        {
            var existing = button.transform.Find("Button frame");
            image = existing != null ? existing.GetComponent<Image>() :
                GameUISkin.Rect("Button frame", button.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            image.transform.SetAsFirstSibling(); image.raycastTarget = false;
        }
        image.sprite = sprite; image.overrideSprite = null; image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2; image.color = Color.white;
        button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock {
            normalColor = new Color(.82f, .86f, .9f), highlightedColor = new Color(1, .94f, .72f),
            selectedColor = new Color(1, .86f, .48f), pressedColor = new Color(.6f, .67f, .73f),
            disabledColor = new Color(.4f, .4f, .4f, .65f), colorMultiplier = 1, fadeDuration = .1f
        };
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.color = GameUISkin.Ink;
        foreach (var text in button.GetComponentsInChildren<Text>(true)) text.color = GameUISkin.Ink;
        EditorUtility.SetDirty(button); EditorUtility.SetDirty(image);
    }

    private static void CreateButtonPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath) != null) return;
        var rect = GameUISkin.Rect("GameButton", null, Vector2.zero, Vector2.one);
        try
        {
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = GameUISkin.Label(rect, "Button", new Vector2(.08f, .12f), new Vector2(.92f, .88f));
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 26;
            Style(button);
            PrefabUtility.SaveAsPrefabAsset(rect.gameObject, ButtonPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(rect.gameObject); }
    }

    private static void CreateLegacySkin()
    {
        var skin = AssetDatabase.LoadAssetAtPath<GUISkin>(SkinPath);
        if (skin == null) { skin = ScriptableObject.CreateInstance<GUISkin>(); AssetDatabase.CreateAsset(skin, SkinPath); }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/Button.png");
        skin.button = new GUIStyle {
            name = "button", alignment = TextAnchor.MiddleCenter, fontSize = 13,
            border = new RectOffset(40, 40, 40, 40), padding = new RectOffset(12, 12, 6, 6),
            margin = new RectOffset(4, 4, 2, 2)
        };
        skin.button.normal.background = skin.button.hover.background = skin.button.active.background = skin.button.focused.background = texture;
        skin.button.normal.textColor = GameUISkin.Ink;
        skin.button.hover.textColor = skin.button.focused.textColor = Color.yellow;
        skin.button.active.textColor = Color.white;
        EditorUtility.SetDirty(skin);
    }
}
