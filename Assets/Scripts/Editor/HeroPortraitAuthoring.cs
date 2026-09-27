#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class HeroPortraitAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Portraits/Bind Dungeon HUD")]
    public static void BindDungeonHud()
    {
        const string path = "Assets/Prefabs/Dungeon/StatDisplay/CharacterStatsDisplay.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var frame = root.transform.Find("Avatar/Image (1)");
            var mask = frame.Find("Portrait Mask");
            if (mask == null)
            {
                mask = new GameObject("Portrait Mask", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask)).transform;
                mask.SetParent(frame, false);
            }
            var rect = (RectTransform)mask;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * 5; rect.offsetMax = Vector2.one * -5;
            mask.GetComponent<UnityEngine.UI.Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Bamao/BamaoUIPack/Sprites/MAP/avatar_mask.png");
            mask.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            mask.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var portrait = mask.Find("Portrait")?.GetComponent<UnityEngine.UI.Image>();
            if (portrait == null)
            {
                portrait = new GameObject("Portrait", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                portrait.transform.SetParent(mask, false);
            }
            portrait.rectTransform.anchorMin = new Vector2(0, -.125f);
            portrait.rectTransform.anchorMax = new Vector2(1, 1.125f);
            portrait.rectTransform.offsetMin = portrait.rectTransform.offsetMax = Vector2.zero;
            portrait.raycastTarget = false;
            root.GetComponent<CharacterStatsDisplay>().PortraitImage = portrait;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    const string Folder = "Assets/Art/HeroPortraits";
    const string ScenePath = "Assets/Scenes/HeroPortraitStudio.unity";

    [MenuItem("Tools/Eternal Enigma/Portraits/Build Scene and Capture All")]
    public static void Build()
    {
        var open = SceneManager.GetSceneByPath(ScenePath);
        if (open.IsValid() && open.isLoaded)
        {
            CaptureAll(open.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HeroPortraitStudio>()).Single());
            return;
        }
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .55f, .55f);
            var studio = new GameObject("Hero Portrait Studio").AddComponent<HeroPortraitStudio>();
            studio.Heroes = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Town/Allies" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<TownAlly>).Where(h => h != null && h.AnimatedModel != null).ToArray();
            var output = AssetDatabase.LoadAssetAtPath<RenderTexture>(Folder + "/PortraitCapture.renderTexture");
            if (output == null)
            {
                output = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32) { name = "Portrait Capture", antiAliasing = 4 };
                AssetDatabase.CreateAsset(output, Folder + "/PortraitCapture.renderTexture");
            }
            studio.Output = output;
            var camera = new GameObject("Portrait Camera").AddComponent<Camera>();
            camera.transform.SetParent(studio.transform);
            camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 50;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f, .13f, .18f, 1);
            camera.cullingMask = 1 << 31; camera.targetTexture = output;
            studio.PortraitCamera = camera;
            Light(studio.transform, "Key Light", new Vector3(25, 155, 0), 1.2f);
            Light(studio.transform, "Fill Light", new Vector3(10, 215, 0), .7f);
            CaptureAll(studio);
            studio.Show(0);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
    }

    static void Light(Transform parent, string name, Vector3 rotation, float intensity)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent); light.transform.rotation = Quaternion.Euler(rotation);
        light.type = LightType.Directional; light.intensity = intensity; light.cullingMask = 1 << 31;
    }

    public static void CaptureAll(HeroPortraitStudio studio)
    {
        int previous = studio.Index;
        try { for (int i = 0; i < studio.Heroes.Length; i++) { studio.Show(i); Capture(studio); } }
        finally { studio.Show(previous); AssetDatabase.SaveAssets(); }
        Debug.Log($"Saved and assigned {studio.Heroes.Length} hero portraits to {Folder}.");
    }

    public static void Capture(HeroPortraitStudio studio)
    {
        var hero = studio.Heroes[studio.Index];
        string path = Folder + "/" + hero.name + ".png";
        var previous = RenderTexture.active;
        var image = new Texture2D(studio.Output.width, studio.Output.height, TextureFormat.RGBA32, false);
        try
        {
            studio.PortraitCamera.Render(); RenderTexture.active = studio.Output;
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var data = new SerializedObject(hero);
        data.FindProperty("Portrait").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hero); AssetDatabase.SaveAssetIfDirty(hero);
    }
}

[CustomEditor(typeof(HeroPortraitStudio))]
public sealed class HeroPortraitStudioInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var studio = (HeroPortraitStudio)target;
        if (GUILayout.Button("Previous Hero")) studio.Show(studio.Index - 1);
        if (GUILayout.Button("Next Hero")) studio.Show(studio.Index + 1);
        if (GUILayout.Button("Save Portrait and Assign")) HeroPortraitAuthoring.Capture(studio);
        if (GUILayout.Button("Capture All Heroes")) HeroPortraitAuthoring.CaptureAll(studio);
    }
}
#endif
