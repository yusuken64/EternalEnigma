#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authors ordinary scene objects; the menu never constructs its scenery at runtime.</summary>
public static class MainMenuSceneAuthoring
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Eternal Enigma/Main Menu/Author Endgame Debug Starts")]
    public static void AuthorEndgameDebugStarts()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the MainMenu scene first.");
        var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenu>(true)).Single();
        var developer = menu.GetComponent<MainMenuDeveloperControls>();
        var parent = developer.Toggle.transform.parent;
        var existing = parent.Find("Debug starts");
        var panel = existing != null ? existing.GetComponent<Image>() :
            GameUISkin.Panel(parent, new Vector2(.41f, .105f), new Vector2(.75f, .655f));
        panel.name = "Debug starts";
        var heading = panel.transform.Find("Debug heading")?.GetComponent<TMP_Text>();
        if (heading == null)
        {
            heading = GameUISkin.Label(panel.transform, "", new Vector2(.055f, .81f), new Vector2(.945f, .96f), 30);
            heading.name = "Debug heading";
        }
        heading.alignment = TextAlignmentOptions.Center;
        heading.text = $"DEVELOPER\n<size=23>Endgame: 4 heroes / level {LevelSystem.MaxLevel}</size>";
        Button Create(string label, UnityEngine.Events.UnityAction action)
        {
            var button = panel.transform.Find(label)?.GetComponent<Button>() ??
                GameUISkin.Button(panel.transform, label, Vector2.zero, Vector2.one, null);
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }
        var autoplay = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Button>(true)).Single(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i =>
                button.onClick.GetPersistentMethodName(i) == nameof(MainMenu.DebugAutoplay_Clicked)));
        var buttons = new[] {
            Create("Endgame Town", menu.EndgameTown_Clicked),
            Create("Endgame Dungeon", menu.EndgameDungeon_Clicked),
            Create("Endgame Overworld", menu.EndgameOverworld_Clicked),
            menu.TestDungeonButton, autoplay
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            button.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)button.transform;
            float top = .795f - i * .15f;
            rect.anchorMin = new Vector2(.06f, top - .125f); rect.anchorMax = new Vector2(.94f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.fontSize = 28; label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 28;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = i == 0 ? developer.Toggle : buttons[i - 1];
            navigation.selectOnDown = i == buttons.Length - 1 ? developer.Toggle : buttons[i + 1];
            navigation.selectOnRight = menu.StartButton.GetComponent<Button>();
            button.navigation = navigation;
            button.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }
        developer.FirstControl = buttons[0];
        developer.Controls = new[] { panel.gameObject }.Concat(buttons.Select(button => button.gameObject)).ToArray();
        panel.gameObject.SetActive(false);
        EditorUtility.SetDirty(developer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Authored endgame town, dungeon, and overworld developer starts.");
    }

    [MenuItem("Tools/Eternal Enigma/Main Menu/Apply Gameplay Lighting")]
    public static void ApplyGameplayLighting()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var menu=SceneManager.GetSceneByPath(ScenePath);
        if(!menu.IsValid() || !menu.isLoaded)throw new InvalidOperationException("Open the MainMenu scene first.");
        var stage=menu.GetRootGameObjects().Single(r=>r.name=="Menu Stage").transform;
        var key=stage.GetComponentsInChildren<Light>(true).Single(l=>l.type==LightType.Directional && l.name!="Cool fill");
        var fill=stage.Find("Cool fill").GetComponent<Light>();
        var sourceMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Diorama/Characters/PolyartDefault_Lit.mat");
        var menuMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/MainMenu/BackdropEnemy_PolyartDefault.mat");
        if(sourceMaterial==null || menuMaterial==null)throw new InvalidOperationException("Gameplay or menu monster material is missing.");

        var previous=SceneManager.GetActiveScene();
        var town=SceneManager.GetSceneByPath("Assets/Scenes/Town.unity");
        bool opened=!town.IsValid() || !town.isLoaded;
        try
        {
            if(opened)town=EditorSceneManager.OpenScene("Assets/Scenes/Town.unity",OpenSceneMode.Additive);
            SceneManager.SetActiveScene(town);
            var source=town.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>())
                .First(l=>l.type==LightType.Directional);
            var ambient=ScenePresentation.FillAmbient(RenderSettings.ambientLight);
            var color=source.color;
            float intensity=source.intensity;
            SceneManager.SetActiveScene(menu);
            Undo.RecordObjects(new Object[]{key,key.transform,key.gameObject,fill,menuMaterial},"Apply gameplay lighting to main menu");
            key.name="Gameplay key";
            key.color=color;key.intensity=intensity;key.shadows=LightShadows.None;
            // Gameplay uses XY ground with -Z up; this stage uses XZ ground with +Y up.
            var direction=Quaternion.Euler(90,180,0)*ScenePresentation.SunDirection(false,0);
            key.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            fill.enabled=false;
            RenderSettings.sun=key;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=ambient;
            menuMaterial.shader=sourceMaterial.shader;
            menuMaterial.CopyPropertiesFromMaterial(sourceMaterial);
            EditorUtility.SetDirty(menuMaterial);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            if(opened && town.IsValid())EditorSceneManager.CloseScene(town,true);
        }
        Debug.Log("Main menu now uses gameplay ambient fill, directional lighting, and lit monster materials.");
    }

    [MenuItem("Tools/Eternal Enigma/Main Menu/Capture Scene")]
    public static void Capture()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).First();
        Directory.CreateDirectory("Temp/MainMenuValidation");
        foreach (var particles in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ParticleSystem>()))
            particles.Simulate(2, true, true);
        foreach (var size in new[] { new Vector2Int(598,336), new Vector2Int(1280,720), new Vector2Int(1280,800) })
        {
            var target = new RenderTexture(size.x, size.y, 24);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)size.x * camera.rect.width / (size.y * camera.rect.height);
                camera.GetComponent<MenuCameraFraming>().Apply(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                File.WriteAllBytes($"Temp/MainMenuValidation/scene-{size.x}x{size.y}.png", image.EncodeToPNG());
            }
            finally { camera.targetTexture = previousTarget; camera.ResetAspect(); RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
        camera.GetComponent<MenuCameraFraming>().Apply();
        Debug.Log("Main menu scene captures saved to Temp/MainMenuValidation.");
    }
}
#endif
