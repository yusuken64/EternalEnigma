using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared Bamao assets for authored menus and runtime HUDs.</summary>
public sealed class GameUITheme : ScriptableObject
{
    public Sprite Button, Panel, Field, Track, Knob, Toggle, Check, Arrow;
    public TMP_FontAsset HeadingFont;
    private static GameUITheme instance;
    public static GameUITheme Current => instance != null ? instance : instance = Resources.Load<GameUITheme>("UI/BamaoTheme");
    public static readonly Color Ink = new(.23f, .14f, .085f);
    public static readonly Color Selected = new(1f, .88f, .59f);
    public static readonly Color LightInk = new(1f, .96f, .84f);

    public void Surface(Image image, Sprite sprite, float scale = 4)
    {
        if (image == null) return;
        image.sprite = sprite; image.overrideSprite = null;
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = scale;
        image.color = Color.white;
    }
    public void StyleButton(Button button)
    {
        var image = button.targetGraphic as Image;
        bool labelled = button.GetComponentInChildren<TMP_Text>(true) != null || button.GetComponentInChildren<Text>(true) != null;
        if (image == null || (!labelled && image.name != "Button frame"))
        {
            var frame = button.transform.Find("Button frame");
            image = frame != null ? frame.GetComponent<Image>() : GameUISkin.Rect("Button frame", button.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            image.transform.SetAsFirstSibling(); image.raycastTarget = false;
        }
        Surface(image, Button);
        button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(1,.96f,.80f),
            selectedColor = Selected, pressedColor = new Color(.77f,.67f,.52f), disabledColor = new Color(.65f,.65f,.65f,.65f),
            colorMultiplier = 1, fadeDuration = .1f };
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.color = Ink;
        foreach (var label in button.GetComponentsInChildren<Text>(true)) label.color = Ink;
    }
    public void StyleSlider(Slider slider)
    {
        var background = slider.transform.Find("Background")?.GetComponent<Image>();
        Surface(background, Track);
        if (slider.fillRect != null)
        {
            var fill = slider.fillRect.GetComponent<Image>();
            if (fill != null) { var color = fill.color; Surface(fill, Field); fill.color = color; }
        }
        if (slider.handleRect != null) Surface(slider.handleRect.GetComponent<Image>(), Knob);
    }
}
