using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Works with both existing serialized menu prefabs and newly authored rows.
public static class SkillIconView
{
    public static Image Bind(Image image, TextMeshProUGUI label, Sprite sprite)
    {
        if (label == null) return image;
        if(image==null)image=label.transform.Find("Skill Icon")?.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
            image.gameObject.SetActive(sprite != null);
            var margin = label.margin; margin.x = sprite != null ? 38 : 0; label.margin = margin;
        }
        return image;
    }
}
