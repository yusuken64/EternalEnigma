using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ControlsRebindingAuthoring
{
    [MenuItem("Tools/Eternal Enigma/UI/Author Controls")]
    public static void Run() => EditScene(Author);

    [MenuItem("Tools/Eternal Enigma/UI/Author Optional Binding Clear")]
    public static void UpdateClearButton() => EditScene(settings =>
        AuthorClearButton(settings.GetComponentInChildren<ControlsRebindingScreen>(true)));

    private static void EditScene(Action<GlobalSettings> author)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Common.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Common has unsaved changes.");
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Common.unity", OpenSceneMode.Additive);
        try
        {
            var settings = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GlobalSettings>(true)).Single();
            author(settings);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void Author(GlobalSettings settings)
    {
        var panel = settings.TabGroup.TabContents.Single(tab => tab.Content.name == "Controls").Content;
        var screen = panel.GetComponent<ControlsRebindingScreen>();
        foreach (Transform child in panel.transform.Cast<Transform>().ToArray())
            if (child.name != "Section heading" && child.GetComponent<HeaderRibbon>() == null && child.name != "GamePanelBackground")
                Object.DestroyImmediate(child.gameObject);
        if (screen == null) screen = panel.AddComponent<ControlsRebindingScreen>();
        screen.DeviceButton = Button(panel.transform, "Keyboard / mouse", .04f, .765f, .48f, .845f);
        screen.ContextButton = Button(panel.transform, "Gameplay", .52f, .765f, .96f, .845f);
        Text(panel.transform, "ACTION", .05f, .71f, .53f, .755f, 20);
        Text(panel.transform, "CURRENT BINDING", .55f, .71f, .95f, .755f, 20);

        var list = GameUISkin.Rect("Bindings scroll", panel.transform, new Vector2(.04f, .17f), new Vector2(.96f, .70f));
        screen.BindingScroll = list.gameObject.AddComponent<ScrollRect>();
        var viewport = GameUISkin.Rect("Viewport", list, Vector2.zero, new Vector2(.97f, 1));
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        var content = GameUISkin.Rect("Bindings", viewport, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var row = GameUISkin.Rect("Binding row 1", content, Vector2.zero, Vector2.one);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
        var action = Text(row, "Action", .01f, .05f, .52f, .95f, 24);
        action.name = "Action";
        var binding = Button(row, "Binding", .57f, .05f, .93f, .95f);
        screen.BindingButtons = new[] { binding };
        screen.ActionLabels = new[] { action };
        screen.BindingScroll.viewport = viewport;
        screen.BindingScroll.content = content;
        screen.BindingScroll.horizontal = false;
        screen.BindingScroll.movementType = ScrollRect.MovementType.Clamped;
        screen.BindingScroll.inertia = false;
        screen.BindingScroll.scrollSensitivity = 25;

        var track = GameUISkin.Rect("Scrollbar", list, new Vector2(.985f, 0), Vector2.one);
        track.gameObject.AddComponent<Image>().color = new Color(.25f, .17f, .09f, .15f);
        var handle = GameUISkin.Rect("Handle", track, Vector2.zero, Vector2.one);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(.25f, .17f, .09f, .65f);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        screen.BindingScroll.verticalScrollbar = scrollbar;
        screen.BindingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        screen.ResetButton = Button(panel.transform, "Restore all defaults", .19f, .072f, .81f, .144f);
        screen.Status = Text(panel.transform, "Select a binding. Changes save automatically.", .04f, .005f, .96f, .066f, 20);
        screen.Status.fontSizeMin = 14;
        screen.Status.alignment = TextAlignmentOptions.Center;

        var capture = GameUISkin.Panel(panel.transform, new Vector2(.01f, .01f), new Vector2(.99f, .85f));
        capture.name = "Capture binding";
        capture.raycastTarget = true;
        screen.CapturePanel = capture.gameObject;
        var title = Text(capture.transform, "CHANGE BINDING", .08f, .72f, .92f, .88f, 30);
        title.alignment = TextAlignmentOptions.Center;
        screen.CaptureLabel = Text(capture.transform, "Press a new key or button", .08f, .29f, .92f, .69f, 26);
        screen.CaptureLabel.alignment = TextAlignmentOptions.Center;
        screen.CancelButton = Button(capture.transform, "Cancel", .25f, .10f, .75f, .24f);
        SelectToActivateButton.MarkCancel(screen.CancelButton);
        AuthorClearButton(screen);
        capture.gameObject.SetActive(false);
        EditorUtility.SetDirty(screen);
    }

    private static void AuthorClearButton(ControlsRebindingScreen screen)
    {
        if (screen.ClearButton == null)
            screen.ClearButton = Button(screen.CapturePanel.transform, "Clear binding", .08f, .10f, .48f, .24f);
        var cancel = (RectTransform)screen.CancelButton.transform;
        cancel.anchorMin = new Vector2(.52f, .10f);
        cancel.anchorMax = new Vector2(.92f, .24f);
        cancel.offsetMin = cancel.offsetMax = Vector2.zero;
        screen.ClearButton.gameObject.SetActive(false);
        EditorUtility.SetDirty(screen);
    }

    private static TMP_Text Text(Transform parent, string value, float x0, float y0, float x1, float y1, float size)
    {
        var label = GameUISkin.Label(parent, value, new Vector2(x0, y0), new Vector2(x1, y1), size);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = true;
        label.fontSizeMin = size - 2;
        label.fontSizeMax = size;
        return label;
    }

    private static Button Button(Transform parent, string value, float x0, float y0, float x1, float y1)
    {
        var button = GameUISkin.Button(parent, value, new Vector2(x0, y0), new Vector2(x1, y1), null);
        if (button is SelectToActivateButton select) select.ActivateOnFirstPointerPress = true;
        var label = button.GetComponentInChildren<TMP_Text>();
        label.enableAutoSizing = true;
        label.fontSizeMin = 20;
        label.fontSizeMax = 26;
        return button;
    }
}
