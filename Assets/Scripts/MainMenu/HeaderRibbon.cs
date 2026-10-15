using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A non-interactive ribbon that follows its title, including layout-driven headings.</summary>
[ExecuteAlways]
[RequireComponent(typeof(Image), typeof(LayoutElement))]
public sealed class HeaderRibbon : MonoBehaviour
{
    public TMP_Text Title;
    private Image image;

    public void Initialize(TMP_Text title)
    {
        Title = title;
        image = GetComponent<Image>();
        image.sprite = GameUITheme.Current.DungeonHeading;
        image.overrideSprite = null;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
        GetComponent<LayoutElement>().ignoreLayout = true;
        Title.alignment = TextAlignmentOptions.Center;
        Title.color = GameUISkin.Ink;
        Title.raycastTarget = false;
        Sync();
    }

    private void OnEnable() => Canvas.willRenderCanvases += Sync;
    private void OnDisable() => Canvas.willRenderCanvases -= Sync;

    private void Sync()
    {
        if (image == null) image = GetComponent<Image>();
        image.enabled = Title != null && Title.isActiveAndEnabled;
        if (Title == null) return;
        var target = Title.rectTransform;
        var rect = (RectTransform)transform;
        rect.anchorMin = target.anchorMin;
        rect.anchorMax = target.anchorMax;
        rect.pivot = target.pivot;
        rect.anchoredPosition3D = target.anchoredPosition3D;
        rect.sizeDelta = target.sizeDelta;
        rect.localRotation = target.localRotation;
        rect.localScale = target.localScale;
        int titleIndex = target.GetSiblingIndex();
        int ribbonIndex = rect.GetSiblingIndex();
        if (ribbonIndex > titleIndex) rect.SetSiblingIndex(titleIndex);
        else if (ribbonIndex < titleIndex - 1) rect.SetSiblingIndex(titleIndex - 1);
        // Keep lettering on the ribbon's central face, clear of the folded tails.
        float horizontal = target.rect.width * .13f;
        var margin = new Vector4(horizontal, target.rect.height * .04f, horizontal, target.rect.height * .24f);
        if (Title.margin != margin) Title.margin = margin;
    }
}
