using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class GameTypographyAuthoring
{
    private const string Destination = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
    private const string Sources = "Assets/TextMesh Pro/Fonts/";

    [MenuItem("Tools/Eternal Enigma/UI/Apply Font Rules")]
    public static void Apply()
    {
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        try
        {
            CreateFont("Marcellus", "Marcellus/Marcellus-Regular.ttf");
            CreateFont("Uncial Antiqua", "Uncial_Antiqua/UncialAntiqua-Regular.ttf");
            CreateFont("Germania One", "Germania_One/GermaniaOne-Regular.ttf");
            CreateFont("Roboto Condensed", "Roboto_Condensed/static/RobotoCondensed-Regular.ttf");
            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = GameTypography.Font(GameTextRole.Interface);
            settings.ApplyModifiedPropertiesWithoutUndo();
            GameUITheme.Current.HeadingFont = GameTypography.Font(GameTextRole.Heading);
            EditorUtility.SetDirty(GameUITheme.Current);
            AssetDatabase.SaveAssets();
            int labels = 0;
            var report = new List<string>();
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => AssetDatabase.GetDependencies(p, false).Length))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponentInChildren<TMP_Text>(true) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try { labels += ApplyRoot(root, path, report); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects()) labels += ApplyRoot(root, path, report);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Temp/GameplayPresentation");
            File.WriteAllLines("Temp/GameplayPresentation/fonts.txt", report);
            Debug.Log($"Applied FontRules.txt to {labels} labels. Created four source-backed TMP SDF fonts.");
        }
        catch (Exception exception) { Debug.LogException(exception); throw; }
    }

    private static void CreateFont(string name, string sourcePath)
    {
        string path = Destination + name + " SDF.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) return;
        var source = AssetDatabase.LoadAssetAtPath<Font>(Sources + sourcePath)
            ?? throw new InvalidOperationException("Missing font: " + sourcePath);
        var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (font == null) throw new InvalidOperationException("Could not create TMP font: " + name);
        font.name = name + " SDF";
        font.atlasTextures[0].name = name + " Atlas";
        font.material.name = name + " SDF Material";
        // Prepopulate normal game copy; source fonts remain bundled for additional glyphs.
        string characters = string.Concat(Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96)).Select(c => (char)c)) + "\u2013\u2014\u2018\u2019\u201c\u201d\u2022\u2026\u2190\u2191\u2192\u2193\u2212";
        font.TryAddCharacters(characters, out _);
        font.fallbackFontAssetTable = new List<TMP_FontAsset> {
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Destination + "LiberationSans SDF.asset")
        }.Where(f => f != null).ToList();
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
        EditorUtility.SetDirty(font);
    }

    private static int ApplyRoot(GameObject root, string path, List<string> report)
    {
        int count = 0;
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            var role = Classify(label, path);
            var style = label.GetComponent<GameTextStyle>() ?? label.gameObject.AddComponent<GameTextStyle>();
            style.Role = role;
            style.Apply();
            if (role == GameTextRole.Combat) label.fontSize = Mathf.Max(label.fontSize, label is TextMeshProUGUI ? 30 : 4);
            EditorUtility.SetDirty(style); EditorUtility.SetDirty(label);
            if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            if (PrefabUtility.IsPartOfPrefabInstance(style)) PrefabUtility.RecordPrefabInstancePropertyModifications(style);
            report.Add($"{role} | {path} | {AnimationUtility.CalculateTransformPath(label.transform, root.transform)} | {label.text?.Replace('\n', ' ')}");
            count++;
        }
        return count;
    }

    private static GameTextRole Classify(TMP_Text label, string path)
    {
        var parents = label.GetComponentsInParent<MonoBehaviour>(true).Where(c => c != null).ToArray();
        string[] types = parents.Select(c => c.GetType().Name).ToArray();
        if (types.Any(t => t is "DungeonFloatingText" or "FloatingText") || path.Contains("/StatusEffects/") ||
            path.EndsWith("/Callout.prefab") || path.EndsWith("/ProminentCallout.prefab")) return GameTextRole.Combat;
        if (types.Any(t => t is "GlobalSettings" or "ControlsSettings" or "AutoplayPanel" or "AutoplaySetup" or "GameMessages" or "EventHistoryDialog") ||
            path.EndsWith("/VolumeSlider.prefab")) return GameTextRole.Utility;
        var fields = new List<string>();
        foreach (var component in parents)
        {
            var property = new SerializedObject(component).GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == label)
                    fields.Add(property.name.ToLowerInvariant());
        }
        if (fields.Any(n => n.Contains("prompt") || n.Contains("hint") || n.Contains("objective") || n.Contains("notification")))
            return GameTextRole.Utility;
        if (label.GetComponentInParent<UnityEngine.UI.Selectable>(true) != null) return GameTextRole.Interface;
        if (fields.Any(n => n is "title" or "titletext" or "heading" or "headingtext" or "headingtemplate" or "header") ||
            label.name.Contains("Title", StringComparison.OrdinalIgnoreCase) || label.name.Contains("Heading", StringComparison.OrdinalIgnoreCase) ||
            label.name == "EternalEnigma" || path.EndsWith("/Section.prefab") || types.Contains("ScreenTransition") ||
            types.Contains("NewFloorMessage")) return GameTextRole.Heading;
        return GameTextRole.Interface;
    }
}
