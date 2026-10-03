using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI construction helpers. Button appearance is serialized in scenes and prefabs.</summary>
public static class GameUISkin
{
    private static GUISkin legacySkin;
    private static Button buttonPrefab;
    private static Image panelPrefab;
    private static GUIStyle legacyButtonLabel;
    public static Color Ink => GameUITheme.Ink;

    public static void UseLegacySkin()
    {
        if (legacySkin == null) legacySkin = Resources.Load<GUISkin>("UI/GameSkin");
        GUI.skin = legacySkin;
    }

    public static void LegacyBeginArea(UnityEngine.Rect rect)
    {
        if (Event.current.type == EventType.Repaint) DrawLegacySprite(rect, GameUITheme.Current.Panel, 8);
        GUILayout.BeginArea(rect, new GUIStyle { padding = new RectOffset(16,16,12,12) });
    }
    private static void DrawLegacySprite(UnityEngine.Rect rect, Sprite sprite, float thickness)
    {
        var texture = sprite.texture;
        var source = sprite.border;
        float border = Mathf.Min(thickness, rect.height / 2f);
        var xs = new[] { rect.xMin, rect.xMin + border, rect.xMax - border, rect.xMax };
        var ys = new[] { rect.yMin, rect.yMin + border, rect.yMax - border, rect.yMax };
        var us = new[] { 0f, source.x / texture.width, 1f - source.z / texture.width, 1f };
        var vs = new[] { 1f, 1f - source.w / texture.height, source.y / texture.height, 0f };
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                GUI.DrawTextureWithTexCoords(UnityEngine.Rect.MinMaxRect(xs[x], ys[y], xs[x+1], ys[y+1]), texture,
                    UnityEngine.Rect.MinMaxRect(us[x], vs[y+1], us[x+1], vs[y]));
    }
    public static bool LegacyButton(string label)
    {
        if (legacyButtonLabel == null)
        {
            legacyButtonLabel = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 13,
                padding = new RectOffset(12,12,6,6), margin = new RectOffset(4,4,2,2) };
            legacyButtonLabel.normal.textColor = Ink;
        }
        var content = new GUIContent(label);
        var rect = GUILayoutUtility.GetRect(content, legacyButtonLabel);
        if (Event.current.type == EventType.Repaint)
        {
            var tint = GUI.color;
            if (GUI.enabled && rect.Contains(Event.current.mousePosition)) GUI.color = GameUITheme.Selected;
            DrawLegacySprite(rect, GameUITheme.Current.Button, 6);
            GUI.color = tint;
        }
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        GUI.Label(rect, content, legacyButtonLabel);
        return clicked;
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
        if (panelPrefab == null) panelPrefab = Resources.Load<Image>("UI/GamePanelBackground");
        var image = UnityEngine.Object.Instantiate(panelPrefab, parent, false);
        image.name = "Panel";
        image.rectTransform.anchorMin = min; image.rectTransform.anchorMax = max;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        return image;
    }

    // Authored containers retain their original disabled Image for reference compatibility.
    public static Image PanelGraphic(Transform container) =>
        container.Find("GamePanelBackground")?.GetComponent<Image>() ?? container.GetComponent<Image>();

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
