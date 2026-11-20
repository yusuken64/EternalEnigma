using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class CompactPartyHudAuthoring
{
    [MenuItem("Tools/Eternal Enigma/UI/Apply Compact Party HUD")]
    internal static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring the party HUD.");
        GameUIButtonBackgroundAuthoring.RequireSavedScenes();
        AuthorShape("PartyPortraitMask", false);
        AuthorShape("PartyPortraitFrame", true);
        foreach (var path in new[] {
            "Assets/Prefabs/Dungeon/StatDisplay/CharacterStatsDisplay.prefab",
            "Assets/Prefabs/Dungeon/UI/CharacterStatsDisplay-3e44211b.prefab",
            "Assets/Resources/UI/Authored/TravelPartyHud.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var card in root.GetComponentsInChildren<DungeonPartyCard>(true)) card.AuthorCompactLayout();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        const string scenePath = "Assets/Scenes/DungeonScene.unity";
        var scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var card in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DungeonPartyCard>(true)))
                card.AuthorCompactLayout();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        AssetDatabase.SaveAssets();
        Debug.Log("Saved compact party cards for dungeon, town and overworld; preserved text settings and gameplay bindings.");
    }

    // Small code-authored UI shapes, with a 1.5-reference-pixel gold frame.
    private static void AuthorShape(string name, bool outline)
    {
        const int scale = 4, size = 64 * scale;
        const string folder = "Assets/Art/UI";
        Directory.CreateDirectory(folder);
        string path = folder + "/" + name + ".png";
        if (File.Exists(path)) return;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        try
        {
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var d = new Vector2(Mathf.Abs((x + .5f) / scale - 32) - 24, Mathf.Abs((y + .5f) / scale - 32) - 24);
                    float distance = new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - 8;
                    float alpha = Mathf.Clamp01(.5f - distance * scale);
                    if (outline) alpha *= Mathf.Clamp01(.5f + (distance + 1.5f) * scale);
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(colors); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100 * scale;
        importer.spriteBorder = Vector4.one * 10 * scale;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
