using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class SettingsAuthoring
{
    private static void Fit(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
    [MenuItem("Tools/Eternal Enigma/UI/Author Settings")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Common.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Common has unsaved changes.");
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Common.unity", OpenSceneMode.Additive);
        try
        {
            var settings = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GlobalSettings>(true)).Single();
            if (settings.GetComponentInChildren<DisplayOptions>(true) != null)
            {
                Finish(settings);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }
            var audio = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioManager>(true)).Single();
            audio.UIAudioMixerGroup = AssetDatabase.LoadAllAssetsAtPath("Assets/Prefabs/Menu/NewAudioMixer.mixer").OfType<AudioMixerGroup>().Single(g => g.name == "UI");
            for (int i = 0; i < 4; i++)
            {
                var source = new GameObject("UI audio " + (i + 1)).AddComponent<AudioSource>();
                source.transform.SetParent(audio.transform, false);
                source.outputAudioMixerGroup = audio.UIAudioMixerGroup;
                source.playOnAwake = false; source.spatialBlend = 0; source.ignoreListenerPause = true;
                audio.UIAudioSources.Add(source);
            }
            var tabs = settings.TabGroup.TabContents;
            var audioTab = tabs.Single(t => t.Content.GetComponentsInChildren<VolumeSlider>(true).Length > 0);
            var volumes = audioTab.Content.GetComponentsInChildren<VolumeSlider>(true).OrderBy(v => v.VolumeParameterName == "MusicVolume" ? 0 : 1).ToList();
            var ui = Object.Instantiate(volumes[1], volumes[1].transform.parent);
            ui.name = "UI_VolumeSlider"; ui.VolumeParameterName = "UIVolume"; ui.AudioMixerGroup = audio.UIAudioMixerGroup;
            volumes.Add(ui);
            for (int i = 0; i < volumes.Count; i++)
            {
                var volume = volumes[i];
                Fit((RectTransform)volume.transform, new Vector2(.09f, .68f - i * .24f), new Vector2(.91f, .87f - i * .24f));
                // The old value text doubled as the title. Give each role its own label.
                volume.VolumeValueText.text = "100%";
                volume.VolumeValueText.alignment = TextAlignmentOptions.Right;
                Fit(volume.VolumeValueText.rectTransform, new Vector2(.72f,.5f), Vector2.one);
                GameUISkin.Label(volume.transform, new[] { "Music", "Effects", "UI" }[i], new Vector2(0,.5f), new Vector2(.7f,1), 28);
                Fit((RectTransform)volume.Slider.transform, new Vector2(0,0), new Vector2(1,.42f));
                volume.Slider.minValue = 0; volume.Slider.maxValue = 1;
                EditorUtility.SetDirty(volume);
                PrefabUtility.RecordPrefabInstancePropertyModifications(volume);
                PrefabUtility.RecordPrefabInstancePropertyModifications(volume.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(volume.Slider.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(volume.VolumeValueText);
                PrefabUtility.RecordPrefabInstancePropertyModifications(volume.VolumeValueText.transform);
            }
            var tabButton = Object.Instantiate(audioTab.TabButton, audioTab.TabButton.transform.parent);
            tabButton.name = "Display tab"; tabButton.GetComponentInChildren<TMP_Text>().text = "Display";
            tabButton.onClick = new Button.ButtonClickedEvent();
            tabButton.transform.SetSiblingIndex(audioTab.TabButton.transform.GetSiblingIndex() + 1);
            var original = (RectTransform)audioTab.Content.transform;
            var panel = GameUISkin.Panel(original.parent, original.anchorMin, original.anchorMax);
            panel.name = "Display options"; panel.rectTransform.offsetMin = original.offsetMin; panel.rectTransform.offsetMax = original.offsetMax;
            var display = panel.gameObject.AddComponent<DisplayOptions>();
            GameUISkin.Label(panel.transform, "DISPLAY", new Vector2(.09f,.85f), new Vector2(.91f,.95f), 32);
            GameUISkin.Label(panel.transform, "Window Mode", new Vector2(.09f,.71f), new Vector2(.91f,.80f), 28);
            display.WindowMode = Dropdown(panel.transform, new Vector2(.09f,.58f), new Vector2(.91f,.70f), tabButton);
            display.WindowMode.name = "Window Mode";
            display.WindowMode.ClearOptions(); display.WindowMode.AddOptions(new System.Collections.Generic.List<string> { "Fullscreen", "Borderless Fullscreen", "Windowed" });
            GameUISkin.Label(panel.transform, "Resolution", new Vector2(.09f,.43f), new Vector2(.91f,.52f), 28);
            display.Resolution = Dropdown(panel.transform, new Vector2(.09f,.30f), new Vector2(.91f,.42f), tabButton);
            display.Resolution.name = "Resolution";
            display.Resolution.ClearOptions(); display.Resolution.AddOptions(new System.Collections.Generic.List<string> { "1920 x 1080" });
            GameUISkin.Label(panel.transform, "In Borderless Fullscreen, resolution changes rendering size; the window fills your desktop.", new Vector2(.09f,.06f), new Vector2(.91f,.25f), 24);
            panel.gameObject.SetActive(false);
            tabs.Insert(tabs.IndexOf(audioTab) + 1, new TabContent { TabButton = tabButton, Content = panel.gameObject });
            settings.TabGroup.PreviewOnFocus = true;
            settings.NavigationHandler.FocusRoot = settings.SettingsCanvas.transform;
            settings.NavigationHandler.FocusOwner = settings;
            settings.NavigationHandler.arrowAnchor = ArrowAnchor.Right;
            settings.NavigationHandler.arrowOffset = Vector3.zero;
            settings.NavigationHandler.edgeSpacing = 14;
            foreach (var graphic in settings.NavigationHandler.selectionArrow.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            Finish(settings);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static void Finish(GlobalSettings settings)
    {
        var volumes = settings.GetComponentsInChildren<VolumeSlider>(true);
        if (!volumes.Any(v => v.VolumeParameterName == "MasterVolume"))
        {
            var music = volumes.Single(v => v.VolumeParameterName == "MusicVolume");
            var master = Object.Instantiate(music, music.transform.parent);
            master.name = "Master_VolumeSlider";
            master.VolumeParameterName = "MasterVolume";
            master.transform.SetSiblingIndex(music.transform.GetSiblingIndex());
            master.GetComponentsInChildren<TMP_Text>(true).First(t => t != master.VolumeValueText).text = "Master";
            volumes = settings.GetComponentsInChildren<VolumeSlider>(true);
        }
        foreach (var volume in volumes)
        {
            int row = volume.VolumeParameterName switch { "MasterVolume" => 0, "MusicVolume" => 1, "EffectVolume" => 2, _ => 3 };
            Fit((RectTransform)volume.transform, new Vector2(.09f, .76f - row * .20f), new Vector2(.91f, .92f - row * .20f));
        }
        settings.NavigationHandler.arrowAnchor = ArrowAnchor.Right;
        settings.NavigationHandler.selectionArrow.localRotation = Quaternion.Euler(0, 0, 180);
        foreach (var tab in settings.TabGroup.TabContents)
        {
            var details = tab.Content.GetComponent<CancelFocusScope>() ?? tab.Content.AddComponent<CancelFocusScope>();
            details.ReturnTarget = tab.TabButton.gameObject;
            var category = tab.TabButton.GetComponent<CancelFocusScope>() ?? tab.TabButton.gameObject.AddComponent<CancelFocusScope>();
            category.ReturnTarget = settings.ResumeButton.gameObject;
        }
        foreach (var volume in settings.GetComponentsInChildren<VolumeSlider>(true))
        {
            volume.VolumeValueText.fontSize = 28;
            PrefabUtility.RecordPrefabInstancePropertyModifications(volume.VolumeValueText);
        }
        foreach (var dropdown in settings.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            var background = dropdown.template.GetComponent<Image>();
            background.sprite = GameUITheme.Current.Button;
            background.type = Image.Type.Sliced;
            var item = dropdown.itemText.GetComponentInParent<Toggle>(true);
            var itemImage = (Image)item.targetGraphic;
            itemImage.sprite = GameUITheme.Current.Button;
            itemImage.type = Image.Type.Sliced;
            item.colors = dropdown.colors;
            dropdown.itemText.rectTransform.offsetMin = new Vector2(28, 1);
            if (item.graphic != null)
            {
                item.graphic.color = GameUISkin.Ink;
                item.graphic.rectTransform.sizeDelta = new Vector2(10, 10);
            }
        }
    }
    private static TMP_Dropdown Dropdown(Transform parent, Vector2 min, Vector2 max, Button style)
    {
        var obj = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        obj.transform.SetParent(parent, false);
        Fit((RectTransform)obj.transform, min, max);
        var dropdown = obj.GetComponent<TMP_Dropdown>();
        dropdown.colors = style.colors;
        var background = dropdown.GetComponent<Image>();
        background.sprite = ((Image)style.targetGraphic).sprite; background.type = Image.Type.Sliced;
        foreach (var label in obj.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = style.GetComponentInChildren<TMP_Text>().font;
            label.color = GameUISkin.Ink; label.fontSize = 26; label.raycastTarget = false;
        }
        dropdown.template.sizeDelta = new Vector2(0, 240);
        var item = dropdown.itemText.GetComponentInParent<Toggle>(true);
        ((RectTransform)item.transform).sizeDelta = new Vector2(0, 46);
        // Text glyphs keep the dropdown indicator readable without a dependency on default UI sprites.
        var arrow = obj.transform.Find("Arrow");
        if (arrow != null) { arrow.GetComponent<Image>().enabled = false; GameUISkin.Label(arrow, "v", Vector2.zero, Vector2.one, 22); }
        dropdown.captionText.rectTransform.offsetMin = new Vector2(18, 4);
        dropdown.captionText.rectTransform.offsetMax = new Vector2(-40, -4);
        return dropdown;
    }
}

