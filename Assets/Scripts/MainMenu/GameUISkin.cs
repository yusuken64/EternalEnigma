using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI construction helpers. Button appearance is serialized in scenes and prefabs.</summary>
public static class GameUISkin
{
    private static GUISkin legacySkin;
    private static Button buttonPrefab;
    public static readonly Color Ink = new(1f, .91f, .72f);

    public static bool LegacyButton(string label)
    {
        if (legacySkin == null) legacySkin = Resources.Load<GUISkin>("UI/GameSkin");
        return GUILayout.Button(label, legacySkin.button);
    }
    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Canvas Canvas(string name, Transform parent, int order = 100)
    {
        var rect = Rect(name, parent, Vector2.zero, Vector2.one);
        var canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = rect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        rect.gameObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static Image Panel(Transform parent, Vector2 min, Vector2 max)
    {
        var image = Rect("Panel", parent, min, max).gameObject.AddComponent<Image>();
        image.color = new Color(.055f, .075f, .09f, .96f);
        return image;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, Vector2 min, Vector2 max, float size = 26)
    {
        var label = Rect("Text", parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text; label.fontSize = size; label.color = Ink; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    public static Button Button(Transform parent, string text, Vector2 min, Vector2 max, Action clicked)
    {
        if (buttonPrefab == null) buttonPrefab = Resources.Load<Button>("UI/GameButton");
        var button = UnityEngine.Object.Instantiate(buttonPrefab, parent, false);
        button.name = text;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        button.GetComponentInChildren<TMP_Text>().text = text;
        button.onClick.AddListener(() => clicked?.Invoke());
        return button;
    }
}