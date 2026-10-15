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
        RefreshControlsGuide(settings);
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
            Fit((RectTransform)volume.transform, new Vector2(.09f, .64f - row * .19f), new Vector2(.91f, .80f - row * .19f));
        }
        settings.NavigationHandler.arrowAnchor = ArrowAnchor.Right;
        settings.NavigationHandler.selectionArrow.localRotation = Quaternion.Euler(0, 0, 180);
        foreach (var tab in settings.TabGroup.TabContents)
        {
            string headingText = tab.TabButton.GetComponentInChildren<TMP_Text>().text.ToUpperInvariant();
            var heading = tab.Content.transform.Find("Section heading")?.GetComponent<TMP_Text>();
            if (heading == null)
                heading = tab.Content.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label =>
                    label.text == "DISPLAY" || label.text == "DUNGEON" || label.text == "GAMEPLAY");
            if (heading == null)
                heading = GameUISkin.Label(tab.Content.transform, headingText, Vector2.zero, Vector2.one, 32);
            heading.name = "Section heading";
            heading.text = headingText;
            Fit(heading.rectTransform, new Vector2(.14f, .86f), new Vector2(.86f, .99f));
            heading.fontSize = 32;
            heading.enableAutoSizing = false;
            GameUISkin.RibbonHeader(heading);
            // All categories share the outer settings sheet. A second parchment
            // border around the body makes Display/Gameplay look like nested dialogs.
            var surface = GameUISkin.PanelGraphic(tab.Content.transform);
            if (surface != null)
            {
                surface.enabled = false;
                surface.raycastTarget = false;
                EditorUtility.SetDirty(surface);
                PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            }
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
            var arrow = dropdown.transform.Find("Arrow")?.GetComponentInChildren<TMP_Text>(true);
            if (arrow != null)
            {
                Fit(arrow.rectTransform, Vector2.zero, Vector2.one);
                arrow.fontSize = 16;
                arrow.fontSizeMin = 12;
                arrow.fontSizeMax = 16;
                arrow.enableAutoSizing = true;
                arrow.alignment = TextAlignmentOptions.Center;
                arrow.textWrappingMode = TextWrappingModes.NoWrap;
                arrow.margin = Vector4.zero;
                arrow.raycastTarget = false;
                EditorUtility.SetDirty(arrow);
            }
        }
    }

    private static void RefreshControlsGuide(GlobalSettings settings)
    {
        var controls = settings.TabGroup.TabContents.Single(tab => tab.Content.name == "Controls").Content;
        var labels = controls.GetComponentsInChildren<TMP_Text>(true);
        var keyboard = labels.Single(label => label.text.StartsWith("<b>KEYBOARD"));
        var controller = labels.Single(label => label.text.StartsWith("<b>CONTROLLER"));
        keyboard.text = "<b>KEYBOARD / MOUSE</b>\n" +
            "Move / browse: WASD / arrows\nConfirm: Enter / Space\nBack / settings: Esc\n" +
            "Click to select; click again to use.\n3D / terminal view: F11\n\n" +
            "<b>WORLD</b>\nTown / dungeon attack / talk: Space\nOverworld interact / enter: Enter\n" +
            "Inventory: Q  |  Equipment: I\nSkills: R  |  Stats: T\nNext hero: Tab\n" +
            "Turn in place: Shift + direction\nDungeon orders: E  |  Map: M\n" +
            "Wait: .  |  Full Control: F\n\n" +
            "<b>PARTY MENUS</b>\nTabs: Left / Right  |  Browse: Up / Down\nHeroes: Tab / Shift+Tab\n" +
            "Full Control On orders each hero.\nTurn it Off to switch the leader.";
        controller.text = "<b>CONTROLLER</b>\n" +
            "Move / browse: Left stick / D-pad\nConfirm: A / Cross\nBack: B / Circle\n" +
            "Settings: Start / Menu\n3D / terminal: menu view button\n\n" +
            "<b>WORLD</b>\nAttack / talk / interact: A / Cross\nInventory: X / Square\n" +
            "Skills: LB / L1\nNext hero: RB / R1\nTurn: hold Y / Triangle + direction\n" +
            "Dungeon orders: B / Circle\nMap: View / Select\nWait: Left stick press\n" +
            "Full Control: Right stick press\n\n" +
            "<b>PARTY MENUS</b>\nTabs: Left / Right  |  Browse: Up / Down\nHeroes: RB / R1 or LT / L2\n" +
            "Equipment / Stats: next tab right";
        foreach (var label in new[] { keyboard, controller })
        {
            label.fontSize = 24;
            label.fontSizeMax = 24;
            label.fontSizeMin = 24;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            EditorUtility.SetDirty(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }
        var viewport = controls.transform.Find("Controls viewport") as RectTransform;
        if (viewport == null)
            viewport = GameUISkin.Rect("Controls viewport", controls.transform, new Vector2(.025f, .15f), new Vector2(.91f, .84f));
        Fit(viewport, new Vector2(.025f, .15f), new Vector2(.91f, .84f));
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(1, 1, 1, .04f);
        var content = viewport.Find("Guide content") as RectTransform;
        if (content == null) content = GameUISkin.Rect("Guide content", viewport, new Vector2(0, 1), Vector2.one);
        Fit(content, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.GetComponent<HorizontalLayoutGroup>() ?? content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 24;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var label in new[] { keyboard, controller })
        {
            label.transform.SetParent(content, false);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.textWrappingMode = TextWrappingModes.Normal;
            var width = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
            width.minWidth = width.preferredWidth = 0;
            width.flexibleWidth = 1;
        }
        var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        scroll.verticalNormalizedPosition = 1;
        var reader = viewport.GetComponent<TrainerPreviewScroll>() ?? viewport.gameObject.AddComponent<TrainerPreviewScroll>();
        reader.Scroll = scroll;
        reader.targetGraphic = hit;
        reader.transition = Selectable.Transition.ColorTint;
        GameUITheme.Current.StyleButton(reader);
        var hint = controls.transform.Find("Controls scroll hint")?.GetComponent<TMP_Text>();
        if (hint == null) hint = GameUISkin.Label(controls.transform, "", new Vector2(.025f, .015f), new Vector2(.91f, .13f), 20);
        hint.name = "Controls scroll hint";
        hint.text = "Confirm / Right: read  |  Up / Down / wheel: scroll\nBack / Left: Controls";
        hint.fontSize = 20;
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
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

