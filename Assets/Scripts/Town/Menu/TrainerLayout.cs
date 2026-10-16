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
        Balance.rectTransform.anchorMax = new Vector2(.90f, .995f);
        Balance.rectTransform.offsetMin = Balance.rectTransform.offsetMax = Vector2.zero;
        Balance.enableAutoSizing = true; Balance.fontSizeMin = 16; Balance.fontSizeMax = 23;
        GameUISkin.RibbonHeader(Balance);
        HeadingTemplate.GetComponent<LayoutElement>().preferredHeight = 50;
        HeadingTemplate.enableAutoSizing = true;
        HeadingTemplate.fontSizeMin = 14; HeadingTemplate.fontSizeMax = 18;
    }
}
