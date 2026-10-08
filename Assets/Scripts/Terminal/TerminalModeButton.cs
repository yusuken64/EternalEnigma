using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TerminalModeButton : MonoBehaviour
{
    private Button button;
    private TextMeshProUGUI label;
    public static TerminalModeButton Ensure(Transform parent, TMP_Dropdown style = null)
    {
        var existing = parent.GetComponentInChildren<TerminalModeButton>(true);
        if (existing != null) return existing;
        var root = new GameObject("Terminal mode", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(TerminalModeButton));
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        if (style != null)
        {
            rect.anchorMin = new Vector2(.09f, .16f);
            rect.anchorMax = new Vector2(.91f, .28f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        else rect.sizeDelta = new Vector2(250, 42);
        var layout = root.GetComponent<LayoutElement>();
        if (style == null) { layout.preferredWidth = 250; layout.preferredHeight = 42; }
        var image = root.GetComponent<Image>();
        if (style != null && style.targetGraphic is Image background)
        {
            image.sprite = background.sprite;
            image.type = background.type;
            image.color = background.color;
        }
        else image.color = new Color(.16f, .16f, .20f, .95f);
        var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        if (style != null) { textRect.offsetMin = new Vector2(18, 4); textRect.offsetMax = new Vector2(-18, -4); }
        var component = root.GetComponent<TerminalModeButton>();
        component.button = root.GetComponent<Button>();
        component.button.targetGraphic = image;
        component.label = textObject.GetComponent<TextMeshProUGUI>();
        component.label.font = style != null ? style.captionText.font : TMP_Settings.defaultFontAsset;
        component.label.fontSize = style != null ? style.captionText.fontSize : 22;
        component.label.color = style != null ? style.captionText.color : Color.white;
        component.label.alignment = style != null ? style.captionText.alignment : TextAlignmentOptions.Center;
        component.label.raycastTarget = false;
        if (style != null) component.button.colors = style.colors;
        component.button.onClick.AddListener(() => TerminalMode.SetRequested(!TerminalMode.Requested));
        component.Refresh();
        return component;
    }
    private void OnEnable() { TerminalMode.Changed += Refresh; Refresh(); }
    private void OnDisable() { TerminalMode.Changed -= Refresh; }
    private void Refresh() { if (label != null) label.text = "Terminal mode (F11): " + (TerminalMode.Requested ? "On" : "Off"); }
}
