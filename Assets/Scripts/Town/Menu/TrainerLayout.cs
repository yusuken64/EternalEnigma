using TMPro;
using UnityEngine;
using UnityEngine.UI;

// References are authored in the prefab; backgrounds remain nested shared prefabs.
public sealed class TrainerLayout : MonoBehaviour
{
    public TextMeshProUGUI Balance, Preview;
    public Button PrimaryTab, SecondaryTab, Close;
    public TrainerPreviewScroll PreviewControl;
    public ScrollRect ListScroll, PreviewScroll;
    public SkillGridItem SkillTemplate;
    public TextMeshProUGUI HeadingTemplate;

    private void Awake() => SelectToActivateButton.MarkCancel(Close);

    public void ApplyPresentation()
    {
        // The parchment is the page; the preview is a quiet inset on that page.
        var panel = PreviewScroll.transform.parent;
        foreach (Transform child in panel)
        {
            var image = child.GetComponent<Image>();
            if (image == null || child == PreviewControl.transform) continue;
            image.sprite = null;
            image.color = new Color(.18f, .10f, .045f, .08f);
            image.raycastTarget = false;
        }
        Balance.rectTransform.anchorMin = new Vector2(.10f, .89f);
        Balance.rectTransform.anchorMax = new Vector2(.90f, .97f);
        Balance.rectTransform.offsetMin = Balance.rectTransform.offsetMax = Vector2.zero;
        Balance.enableAutoSizing = true; Balance.fontSizeMin = 16; Balance.fontSizeMax = 23;
        GameUISkin.RibbonHeader(Balance);
        HeadingTemplate.GetComponent<LayoutElement>().preferredHeight = 50;
        HeadingTemplate.enableAutoSizing = true;
        HeadingTemplate.fontSizeMin = 14; HeadingTemplate.fontSizeMax = 18;
        Fit((RectTransform)ListScroll.transform, .06f, .17f, .55f, .77f);
        Fit((RectTransform)panel, .58f, .17f, .94f, .77f);
        Fit((RectTransform)PreviewScroll.transform, .10f, .21f, .90f, .92f);
        Preview.fontSize = 18; Preview.enableAutoSizing = false;
        Preview.textWrappingMode = TextWrappingModes.Normal;
        Preview.margin = new Vector4(0, 4, 0, 8);
        Fit((RectTransform)PreviewControl.transform, .10f, .04f, .90f, .17f);
        Fit((RectTransform)Close.transform, .72f, .055f, .94f, .135f);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.transform.parent == transform && label != Balance)
            {
                Fit(label.rectTransform, .06f, .055f, .68f, .135f);
                label.fontSize = 17; label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.Normal;
            }
        GameUISkin.PadButtonLabel(PrimaryTab);
        GameUISkin.PadButtonLabel(SecondaryTab);
        GameUISkin.PadButtonLabel(Close);
    }

    private static void Fit(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
