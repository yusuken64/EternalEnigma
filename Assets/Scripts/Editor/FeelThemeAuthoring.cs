using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FeelThemeAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Apply Feel Assets")]
    public static void Apply()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DungeonThemeCatalog>("Assets/Resources/DungeonThemes/Catalog.asset");
        var fallback = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Music/MusicBox_07.mp3");
        var boss = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Music/影の支配者.mp3");
        catalog.FallbackMusic = fallback;

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Overworld.unity", OpenSceneMode.Additive);
        try
        {
            var overworld = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>(true)).FirstOrDefault();
            foreach (var theme in catalog.Themes)
            {
                theme.Music = overworld?.BiomeMusic?.FirstOrDefault(m => m.Biome == theme.Biome)?.Clip ?? fallback;
                theme.BossMusic = boss != null ? boss : fallback;
                theme.Decorations = theme.Environment == DungeonEnvironmentKind.Interior
                    ? theme.Biome.ToString() == "Mountain" ? new[] { "Barrel", "Crate", "Sack", "Lantern" }
                        : new[] { "Candles", "Sconce", "Banner", "Books", "ArcanePedestal", "Chest" }
                    : theme.Biome.ToString() == "Tundra"
                        ? new[] { "Rock", "SnowPine", "SnowRock", "Shrine" }
                        : theme.Biome.ToString() == "Mountain"
                            ? new[] { "Rock", "MountainPeak", "MountainSpires", "MountainRidge" }
                            : theme.Decorations.Concat(new[] { "Flowers", "Shrine", "DeadTree", "Willow" }).Distinct().ToArray();
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        var common = EditorSceneManager.OpenScene("Assets/Scenes/Common.unity", OpenSceneMode.Additive);
        try
        {
            var sounds = common.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SoundEffects>(true)).First();
            const string movement = "Assets/Sounds/RPG_Essentials_Free/12_Player_Movement_SFX/";
            sounds.StepGrass = AssetDatabase.LoadAssetAtPath<AudioClip>(movement + "03_Step_grass_03.wav");
            sounds.StepRock = AssetDatabase.LoadAssetAtPath<AudioClip>(movement + "08_Step_rock_02.wav");
            sounds.StepWood = AssetDatabase.LoadAssetAtPath<AudioClip>(movement + "12_Step_wood_03.wav");
            sounds.StepWater = AssetDatabase.LoadAssetAtPath<AudioClip>(movement + "14_Step_water_02.wav");
            sounds.Jump = AssetDatabase.LoadAssetAtPath<AudioClip>(movement + "30_Jump_03.wav");
            sounds.Landing = AssetDatabase.LoadAssetAtPath<AudioClip>(movement + "45_Landing_01.wav");
            sounds.Ambush = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Music/Ambush Transition.mp3");
            EditorUtility.SetDirty(sounds);
            EditorSceneManager.SaveScene(common);
        }
        finally { EditorSceneManager.CloseScene(common, true); }

        foreach (var path in Directory.GetFiles("Assets/Sounds/Music", "*.mp3"))
        {
            var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as AudioImporter;
            if (importer == null) continue;
            var settings = importer.GetOverrideSampleSettings("Standalone");
            settings.loadType = AudioClipLoadType.Streaming;
            settings.quality = .5f;
            importer.SetOverrideSampleSettings("Standalone", settings);
            importer.SaveAndReimport();
        }
    }
}
