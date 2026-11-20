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
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        var sprite = GameUITheme.Current.Button;
        int count = 0;
        void Check(Button button, string path)
        {
            if (button.targetGraphic != null && button.targetGraphic.TryGetComponent<DungeonUIRole>(out var role))
            {
                if (!role.IsValid())
                    throw new InvalidOperationException($"Incorrect dungeon role: {path} / {button.name}");
                count++; return;
            }
            if (!(button.targetGraphic is Image image) || image.sprite != sprite ||
                image.type != Image.Type.Sliced || image.pixelsPerUnitMultiplier != 1 ||
                button.transition != Selectable.Transition.ColorTint)
                throw new InvalidOperationException($"Missing serialized style: {path} / {button.name}");
            GameUIButtonBackgroundAuthoring.ValidateBackground(image, path);
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
        Directory.CreateDirectory("Temp/UIValidation");
        File.WriteAllText("Temp/UIValidation/saved-button-validation.txt", $"PASS: {count} saved scene/prefab buttons inherit sliced backgrounds with multiplier 1 outside Play Mode.");
    }


    [MenuItem("Tools/Eternal Enigma/UI/Validate Saved Menu Fonts")]
    public static void ValidateMenuFonts()
    {
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        var normal = TMP_Settings.defaultFontAsset;
        if (normal == null) throw new InvalidOperationException("The standard menu font is missing.");
        int labels = 0, implicitDefaults = 0, assets = 0;
        var problems = new System.Collections.Generic.List<string>();
        var dialogTypes = new System.Collections.Generic.HashSet<string>();
        void Check(GameObject root, string path)
        {
            foreach (var dialog in root.GetComponentsInChildren<JuicyChickenGames.Menu.Dialog>(true))
                dialogTypes.Add(dialog.GetType().Name);
            foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                labels++;
                if (label.font == null) implicitDefaults++; // TMP resolves these to its configured default at startup.
                else if (label.font != GameTypography.Expected(label))
                    problems.Add(path + " / " + AnimationUtility.CalculateTransformPath(label.transform, root.transform) + ": " + label.font.name);
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
            assets++;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try { foreach (var root in scene.GetRootGameObjects()) Check(root, path); assets++; }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Directory.CreateDirectory("Temp/DialogFontVerification");
        File.WriteAllLines("Temp/DialogFontVerification/authored-fonts.txt",
            new[] { $"Checked {labels} menu labels in {assets} scene/prefab assets; {implicitDefaults} inherit the TMP default.",
                $"Dialog types ({dialogTypes.Count}): " + string.Join(", ", dialogTypes.OrderBy(name => name)),
                $"Font mismatches: {problems.Count}" }.Concat(problems));
        if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
        Debug.Log($"Menu font audit passed: {labels} authored labels; {dialogTypes.Count} dialog types.");
    }

    [MenuItem("Tools/Eternal Enigma/UI/Bake Button Styles")]
    public static void Bake()
    {
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        CreateTheme();
        CreateButtonPrefab();
        CreateLegacySkin();
        int prefabButtons = 0, sceneButtons = 0;
        var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct().OrderBy(p => p).ToArray();
        foreach (string path in prefabs)
        {
            // These assets define the artwork; the generic hierarchy pass must not restyle them.
            if (path == GameUIButtonBackgroundAuthoring.BackgroundPath ||
                path == GameUIPanelBackgroundAuthoring.BackgroundPath) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var buttons = root.GetComponentsInChildren<Button>(true);
                StyleHierarchy(root);
                if (root.GetComponentsInChildren<Graphic>(true).Length > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
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
                foreach (var root in scene.GetRootGameObjects()) StyleHierarchy(root);
                foreach (var button in buttons)
                {
                    foreach (var component in button.GetComponentsInChildren<Component>(true))
                        if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                EditorSceneManager.SaveScene(scene);
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
        GameUIButtonBackgroundAuthoring.EnsureBackground(button);
        GameUITheme.Current.StyleButton(button);
    }

    private static void StyleHierarchy(GameObject root)
    {
        var theme = GameUITheme.Current;
        var images = root.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (!image.enabled || image.GetComponent<DungeonUIRole>() != null || GameUIPanelBackgroundAuthoring.IsShared(image) ||
                GameUIButtonBackgroundAuthoring.IsShared(image)) continue;
            string name = image.name.ToLowerInvariant();
            if (image.GetComponentInParent<Button>() != null || image.GetComponentInParent<Slider>() != null ||
                image.GetComponentInParent<Toggle>() != null || image.GetComponentInParent<Scrollbar>() != null ||
                image.GetComponent<Mask>() != null || image.color.a < .1f ||
                name.Contains("portrait") || name.Contains("icon") || name.Contains("cursor") || name.Contains("shutter") ||
                name.Contains("fade") || name.Contains("transition") || name.Contains("mask") || name.Contains("overlay") ||
                name.Contains("input shield") || name.Contains("modal backdrop") || name.Contains("confirmation backdrop") ||
                name.Contains("dimmer") || image.GetComponent<TownModalBackdrop>() != null) continue;
            bool plain = image.sprite == null || AssetDatabase.GetAssetPath(image.sprite).StartsWith("Resources/unity_builtin");
            if (plain || name.Contains("background") || name.Contains("panel") || name.Contains("frame"))
                theme.Surface(image, theme.Panel, 24);
        }
        GameUIPanelBackgroundAuthoring.MigrateHierarchy(root);
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            // World labels and floating combat text keep their contrast against the world.
            if (label is not TextMeshProUGUI) continue;
            label.color = GameUITheme.Ink;
            GameTypography.Apply(label, label.TryGetComponent<GameTextStyle>(out var style)
                ? style.Role : GameTextRole.Interface);
        }
        foreach (var label in root.GetComponentsInChildren<Text>(true)) label.color = GameUITheme.Ink;
        foreach (var button in root.GetComponentsInChildren<Button>(true)) Style(button);
        foreach (var slider in root.GetComponentsInChildren<Slider>(true)) theme.StyleSlider(slider);
        foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
        {
            theme.Surface(toggle.targetGraphic as Image, theme.Toggle);
            theme.Surface(toggle.graphic as Image, theme.Check);
        }
        foreach (var scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
        {
            theme.Surface(scrollbar.GetComponent<Image>(), theme.Track);
            theme.Surface(scrollbar.targetGraphic as Image, theme.Button);
        }
        foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            theme.Surface(input.targetGraphic as Image, theme.Field);
            input.selectionColor = GameUITheme.Selected;
        }
        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            theme.Surface(dropdown.targetGraphic as Image, theme.Field);
            var arrow = dropdown.transform.Find("Arrow")?.GetComponent<Image>();
            theme.Surface(arrow, theme.Arrow);
        }
        foreach (var tabs in root.GetComponentsInChildren<TabGroup>(true))
        {
            tabs.NormalColor = Color.white; tabs.SelectedColor = GameUITheme.Selected;
            tabs.NormalTextColor = tabs.SelectedTextColor = GameUITheme.Ink;
        }
        StyleWorldLabels(root);
        foreach (var role in root.GetComponentsInChildren<DungeonUIRole>(true)) role.Apply();
        foreach (var text in root.GetComponentsInChildren<DungeonTextStyle>(true)) text.Apply();
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    private static void StyleWorldLabels(GameObject root)
    {
        void Light(TMP_Text text)
        {
            if (text == null) return;
            text.color = GameUITheme.LightInk;
            var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.12f,.065f,.025f,.95f); shadow.effectDistance = new Vector2(2,-2);
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text.text == "Eternal Enigma") Light(text);
        foreach (var player in root.GetComponentsInChildren<TownPlayer>(true)) Light(player.UIText);
    }

    [MenuItem("Tools/Eternal Enigma/UI/Refresh World Label Contrast")]
    private static void RefreshWorldLabelContrast()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Town.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects()) StyleWorldLabels(root);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    private const string Pack = "Assets/Bamao/BamaoUIPack/";
    private static void CreateTheme()
    {
        const string path = "Assets/Resources/UI/BamaoTheme.asset";
        var theme = AssetDatabase.LoadAssetAtPath<GameUITheme>(path);
        if (theme == null) { theme = ScriptableObject.CreateInstance<GameUITheme>(); AssetDatabase.CreateAsset(theme, path); }
        Sprite Load(string name, Vector4 border)
        {
            string asset = Pack + "Sprites/Button/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
            if (border != Vector4.zero && importer.spriteBorder != border)
            { importer.spriteBorder = border; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(asset);
        }
        theme.Button = Load("button short", new Vector4(32,24,32,24));
        theme.Panel = Load("Popup_paper_bg", Vector4.zero);
        theme.Field = Load("typing button", new Vector4(24,24,24,24));
        theme.Track = Load("number background dark", new Vector4(16,16,16,16));
        theme.Knob = Load("radio_circle", Vector4.zero);
        theme.Toggle = Load("radio_square", Vector4.zero);
        theme.Check = Load("radio_square_check", Vector4.zero);
        theme.Arrow = Load("dropdown triangle", Vector4.zero);
        theme.HeadingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Pack + "Fonts/Magical Neverland SDF.asset");
        EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
    }

    private static void CreateButtonPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath) != null) return;
        var rect = GameUISkin.Rect("GameButton", null, Vector2.zero, Vector2.one);
        try
        {
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<SelectToActivateButton>(); button.targetGraphic = image;
            button.ActivateOnFirstPointerPress = true;
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
        var texture = GameUITheme.Current.Button.texture;
        skin.button = new GUIStyle {
            name = "button", alignment = TextAnchor.MiddleCenter, fontSize = 13,
            border = new RectOffset(24, 24, 24, 24), padding = new RectOffset(12, 12, 6, 6),
            margin = new RectOffset(4, 4, 2, 2)
        };
        skin.button.normal.background = skin.button.hover.background = skin.button.active.background = skin.button.focused.background = texture;
        skin.button.normal.textColor = GameUISkin.Ink;
        skin.button.hover.textColor = skin.button.focused.textColor = Color.yellow;
        skin.button.active.textColor = Color.white;
        skin.label = new GUIStyle { fontSize = 13, padding = new RectOffset(4,4,3,3), wordWrap = true };
        skin.label.normal.textColor = GameUITheme.Ink;
        skin.box = new GUIStyle { border = new RectOffset(48,48,48,48), padding = new RectOffset(16,16,12,12) };
        skin.box.normal.background = GameUITheme.Current.Panel.texture;
        skin.box.normal.textColor = GameUITheme.Ink;
        EditorUtility.SetDirty(skin);
    }
}
