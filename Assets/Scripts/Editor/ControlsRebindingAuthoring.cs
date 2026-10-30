using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ControlsRebindingAuthoring
{
    public static void Author(GlobalSettings settings)
    {
        var panel = settings.TabGroup.TabContents.Single(tab => tab.Content.name == "Controls").Content;
        var screen = panel.GetComponent<ControlsRebindingScreen>();
        if (screen != null) return;
        foreach (Transform child in panel.transform.Cast<Transform>().ToArray())
            if (child.name != "Section heading" && child.GetComponent<HeaderRibbon>() == null && child.name != "GamePanelBackground")
                Object.DestroyImmediate(child.gameObject);
        screen = panel.AddComponent<ControlsRebindingScreen>();
        screen.DeviceButton = Button(panel.transform, "Keyboard / mouse", .04f, .765f, .58f, .845f);
        screen.ContextButton = Button(panel.transform, "Gameplay", .61f, .765f, .96f, .845f);
        Text(panel.transform, "ACTION", .05f, .71f, .53f, .755f, 20);
        Text(panel.transform, "CURRENT BINDING", .55f, .71f, .95f, .755f, 20);
        screen.BindingButtons = new Button[6];
        screen.ActionLabels = new TMP_Text[6];
        for (int i = 0; i < 6; i++)
        {
            float top = .70f - i * .076f;
            var label = Text(panel.transform, "Action", .05f, top - .066f, .53f, top, 24);
            label.name = "Action " + (i + 1);
            screen.ActionLabels[i] = label;
            screen.BindingButtons[i] = Button(panel.transform, "Binding", .55f, top - .066f, .95f, top);
            screen.BindingButtons[i].name = "Rebind " + (i + 1);
        }
        screen.PreviousPageButton = Button(panel.transform, "Previous", .04f, .16f, .36f, .232f);
        screen.PageLabel = Text(panel.transform, "1 / 1", .38f, .16f, .62f, .232f, 24);
        screen.PageLabel.alignment = TextAlignmentOptions.Center;
        screen.NextPageButton = Button(panel.transform, "Next", .64f, .16f, .96f, .232f);
        screen.ResetButton = Button(panel.transform, "Restore all defaults", .19f, .072f, .81f, .144f);
        screen.Status = Text(panel.transform, "Select a binding to change it. Changes save automatically.", .04f, .005f, .96f, .066f, 20);
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
        capture.gameObject.SetActive(false);
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
