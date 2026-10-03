using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Regenerates only the dedicated trainer prefab, never the town scene.
public static class TrainerLayoutAuthoring
{
    public const string Path = "Assets/Resources/UI/TrainerLayout.prefab";
    [MenuItem("Tools/Eternal Enigma/UI/Build Trainer Layout")]
    public static void Build()
    {
        var root = GameUISkin.Rect("TrainerLayout", null, Vector2.zero, Vector2.one);
        try
        {
            var view = root.gameObject.AddComponent<TrainerLayout>();
            Background(root, false);
            view.Balance = Label(root, "Skill training", new Vector2(.04f,.89f), new Vector2(.96f,.97f), 17);
            view.PrimaryTab = Button(root, "Primary", new Vector2(.04f,.79f), new Vector2(.49f,.88f));
            view.SecondaryTab = Button(root, "Secondary", new Vector2(.51f,.79f), new Vector2(.96f,.88f));
            view.ListScroll = Scroll(root, "Skills", new Vector2(.04f,.14f), new Vector2(.55f,.77f));
            var list = view.ListScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 8; list.childControlHeight = true; list.childControlWidth = true;
            list.childForceExpandHeight = false;
            view.ListScroll.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var panel = GameUISkin.Rect("DescriptionPanel", root, new Vector2(.57f,.14f), new Vector2(.96f,.77f));
            Background(panel, false);
            view.PreviewScroll = Scroll(panel, "Description", new Vector2(.08f,.19f), new Vector2(.92f,.91f));
            view.Preview = view.PreviewScroll.content.gameObject.AddComponent<TextMeshProUGUI>();
            view.Preview.fontSize = 15; view.Preview.color = GameUITheme.Ink;
            view.Preview.raycastTarget = false;
            view.PreviewScroll.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.PreviewControl = (TrainerPreviewScroll)Button(panel, "Details: Up/Down scroll\nLeft: skills  Right: Close",
                new Vector2(.08f,.025f), new Vector2(.92f,.16f), true);
            view.PreviewControl.Scroll = view.PreviewScroll;
            view.Close = Button(root, "Close", new Vector2(.72f,.025f), new Vector2(.96f,.115f));
            Label(root, "Select to preview. Submit / click again to train.", new Vector2(.04f,.03f), new Vector2(.69f,.105f), 14);

            var templates = GameUISkin.Rect("Templates", root, Vector2.zero, Vector2.one);
            view.HeadingTemplate = Label(templates, "Tier", Vector2.zero, Vector2.one, 16);
            view.HeadingTemplate.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            var skillButton = Button(templates, "Skill", Vector2.zero, Vector2.one);
            var row = skillButton.gameObject.AddComponent<SkillGridItem>();
            row.GridButton = skillButton;
            row.SkillText = skillButton.GetComponentInChildren<TextMeshProUGUI>();
            row.SkillText.rectTransform.anchorMax = new Vector2(.78f, 1);
            row.SkillText.rectTransform.offsetMin = new Vector2(30, 4);
            row.SkillText.alignment = TextAlignmentOptions.MidlineLeft;
            row.SkillText.fontSize = 16;
            row.SkillText.fontSizeMin = 12;
            row.SkillText.enableAutoSizing = true;
            row.ActiveImage = GameUISkin.Rect("Rank indicator", skillButton.transform, new Vector2(.02f,.2f), new Vector2(.03f,.8f)).gameObject.AddComponent<Image>();
            row.ActiveImage.raycastTarget = false;
            row.CostText = Label(skillButton.transform, "1 pt", new Vector2(.79f,.1f), new Vector2(.95f,.9f), 14);
            row.CostText.alignment = TextAlignmentOptions.Center;
            row.CostObject = row.CostText.gameObject;
            skillButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
            view.SkillTemplate = row;
            templates.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Path);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
        Debug.Log("Trainer layout prefab authored with inherited sliced backgrounds.");
    }

    private static Image Background(Transform parent, bool button)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(button ? GameUIButtonBackgroundAuthoring.BackgroundPath : GameUIPanelBackgroundAuthoring.BackgroundPath);
        return ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).GetComponent<Image>();
    }
    private static TextMeshProUGUI Label(Transform parent, string text, Vector2 min, Vector2 max, int size)
    {
        var label = GameUISkin.Label(parent, text, min, max, size);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
    private static Button Button(Transform parent, string text, Vector2 min, Vector2 max, bool preview = false)
    {
        var rect = GameUISkin.Rect(text, parent, min, max);
        var button = preview ? (Button)rect.gameObject.AddComponent<TrainerPreviewScroll>() : rect.gameObject.AddComponent<SelectToActivateButton>();
        button.targetGraphic = Background(rect, true);
        var label = Label(rect, text, Vector2.zero, Vector2.one, 16);
        label.rectTransform.offsetMin = new Vector2(12, 4); label.rectTransform.offsetMax = new Vector2(-12,-4);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 11; label.fontSizeMax = 18;
        GameUITheme.Current.StyleButton(button);
        return button;
    }
    private static ScrollRect Scroll(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = GameUISkin.Rect(name, parent, min, max);
        var scroll = rect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false; scroll.scrollSensitivity = 40;
        var viewport = GameUISkin.Rect("Viewport", rect, Vector2.zero, Vector2.one);
        // A transparent raycast surface receives wheel and drag events in empty areas.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;
        var content = GameUISkin.Rect("Content", viewport, new Vector2(0,1), Vector2.one);
        content.pivot = new Vector2(.5f,1);
        scroll.content = content;
        return scroll;
    }
}
