using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Works with both existing serialized menu prefabs and newly authored rows.
public static class SkillIconView
{
    public static Image Bind(Image image, TextMeshProUGUI label, Sprite sprite)
    {
        if (label == null) return image;
        if (image == null && sprite != null)
        {
            var go = new GameObject("Skill Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(label.transform, false);
            image = go.GetComponent<Image>();
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(4, 0);
            rect.sizeDelta = new Vector2(28, 28);
            image.raycastTarget = false;
            image.preserveAspect = true;
        }
        if (image != null)
        {
            image.sprite = sprite;
            image.gameObject.SetActive(sprite != null);
            var margin = label.margin; margin.x = sprite != null ? 38 : 0; label.margin = margin;
        }
        return image;
    }
}
