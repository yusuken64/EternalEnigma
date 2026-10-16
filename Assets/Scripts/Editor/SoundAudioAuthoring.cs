using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SoundAudioAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Audio/Author All")]
    public static void AuthorAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit play mode before authoring audio.");
        AssetDatabase.Refresh();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds/ActionFeedback" }))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Common.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Common.unity", OpenSceneMode.Additive);
        try
        {
            var sound = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SoundEffects>(true)).Single();
            sound.DoorOpen = Clip("ActionFeedback/DoorOpen.wav");
            sound.GateUnlock = Clip("ActionFeedback/GateUnlock.wav");
            sound.LevelUp = Clip("ActionFeedback/LevelUp.wav");
            sound.Purchase = Clip("ActionFeedback/Purchase.wav");
            sound.Sell = Clip("ActionFeedback/Sell.wav");
            sound.Wait = Clip("ActionFeedback/Wait.wav");
            sound.Rest = Clip("ActionFeedback/Rest.wav");
            sound.AbilityCast = Clip("ActionFeedback/CastStart.wav");
            sound.Status = Clip("ActionFeedback/Status.wav");
            sound.Gather = sound.Equip;
            sound.ContainerOpen = sound.DoorOpen;
            sound.Throw = sound.Slash;
            sound.Trap = sound.Claw;
            sound.Revive = Clip("RPG_Essentials_Free/8_Buffs_Heals_SFX/30_Revive_03.wav");
            sound.StepGrass = sound.StepRock = sound.StepWood = sound.StepWater = null;
            var audio = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioManager>(true)).Single();
            foreach (var source in audio.EffectAudioSources) Configure(source, audio.EffectAudioMixerGroup);
            foreach (var source in audio.UIAudioSources) Configure(source, audio.UIAudioMixerGroup);
            audio.MusicAudioSource.outputAudioMixerGroup = audio.MusicAudioMixerGroup;
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
                if (sound.For(cue) == null) throw new InvalidOperationException("Unassigned sound cue: " + cue);
            EditorUtility.SetDirty(sound);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        AbilitySoundAuthoring.Author();
        MenuAudioAuthoring.Author();
        AssetDatabase.SaveAssets();
        Debug.Log("Audio authoring complete: all cues assigned, footsteps disabled, UI/Effects/Music routed.");
    }

    private static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/" + path)
        ?? throw new InvalidOperationException("Missing audio asset: " + path);
    private static void Configure(AudioSource source, UnityEngine.Audio.AudioMixerGroup group)
    {
        if (source == null || group == null) throw new InvalidOperationException("Missing audio pool source or mixer group.");
        source.outputAudioMixerGroup = group;
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.pitch = 1;
        source.volume = 1;
    }
}
