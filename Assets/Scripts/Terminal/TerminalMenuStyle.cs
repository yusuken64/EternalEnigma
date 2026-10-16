using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reversible styling; neither callbacks, layout, nor raycast routing are replaced.</summary>
internal sealed class TerminalMenuStyle
{
    private static readonly Color Panel = new(.015f, .035f, .025f, 1f);
    private static readonly Color Ink = new(.82f, .95f, .87f);
    private static readonly Color Indicator = new(.75f, .92f, .63f);
    private readonly Dictionary<TMP_Text, (TMP_FontAsset Font, Color Color)> texts = new();
    private readonly Dictionary<TMP_Text, Color> appliedTextColors = new();
    private readonly Dictionary<Image, (Sprite Sprite, Sprite Override, Image.Type Type, Color Color)> images = new();
    private readonly Dictionary<Selectable, ColorBlock> controls = new();
    private readonly Dictionary<Selectable, ColorBlock> appliedControlColors = new();
    private readonly Dictionary<Transform, Image> backdrops = new();
    internal void Refresh(Transform root, TMP_FontAsset font, bool opaqueBackdrop = false)
    {
        if (root == null || font == null) return;
        if (opaqueBackdrop && root is RectTransform && !backdrops.ContainsKey(root))
        {
            var backing = new GameObject("Terminal modal background", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            backing.transform.SetParent(BackdropRoot((RectTransform)root), false);
            backing.transform.SetAsFirstSibling();
            var rect = (RectTransform)backing.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            backing.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = backing.GetComponent<Image>();
            image.color = Panel;
            image.raycastTarget = false;
            backdrops.Add(root, image);
        }
        var targetGraphics = new HashSet<Graphic>();
        var indicators = new HashSet<Graphic>();
        foreach (var navigation in root.GetComponentsInChildren<NavigationHandler>())
            if (navigation.selectionArrow != null)
                foreach (var graphic in navigation.selectionArrow.GetComponentsInChildren<Graphic>()) indicators.Add(graphic);
        foreach (var selectable in root.GetComponentsInChildren<Selectable>())
        {
            RememberControlColors(selectable);
            if (selectable.targetGraphic != null) targetGraphics.Add(selectable.targetGraphic);
            if (selectable is Toggle toggle && toggle.graphic != null) indicators.Add(toggle.graphic);
            if (selectable is Slider slider && slider.fillRect != null)
                foreach (var graphic in slider.fillRect.GetComponentsInChildren<Graphic>()) indicators.Add(graphic);
            var colors = selectable.colors;
            bool handle = selectable is Slider || selectable is Scrollbar;
            // The authored dungeon close button uses its cross artwork as the
            // tint target. Dark panel colors would hide the symbol itself.
            bool symbol = selectable.targetGraphic != null &&
                selectable.targetGraphic.TryGetComponent<DungeonUIRole>(out var role) && role.Role == DungeonVisualRole.Close;
            bool active = false;
            foreach (var label in selectable.GetComponentsInChildren<TMP_Text>())
                active |= (label.fontStyle & FontStyles.Underline) != 0;
            colors.normalColor = symbol ? Indicator : handle ? new Color(.36f, .60f, .45f) : active
                ? new Color(.12f, .27f, .19f) : new Color(.055f, .12f, .08f);
            colors.highlightedColor = colors.selectedColor = symbol ? Color.white : handle ? Indicator : new Color(.18f, .39f, .26f);
            colors.pressedColor = new Color(.43f, .65f, .42f);
            colors.disabledColor = symbol ? new Color(.36f, .48f, .41f) : new Color(.12f, .15f, .13f);
            colors.colorMultiplier = 1;
            selectable.colors = colors;
            appliedControlColors[selectable] = colors;
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>())
        {
            if (!texts.TryGetValue(text, out var original))
                texts.Add(text, original = (text.font, text.color));
            else if (appliedTextColors.TryGetValue(text, out var applied) && text.color != applied)
                texts[text] = original = (original.Font, text.color);
            text.font = font;
            text.color = ReadableInk(original.Color);
            appliedTextColors[text] = text.color;
        }
        foreach (var image in root.GetComponentsInChildren<Image>())
        {
            // Keep portraits, item icons and resource bars meaningful. A toggle's
            // checkmark retains its shape and its CanvasRenderer-controlled on/off alpha.
            if (backdrops.ContainsValue(image) || image.color.a == 0) continue;
            bool target = targetGraphics.Contains(image), indicator = indicators.Contains(image);
            bool textPanel = image.preserveAspect && BacksReadableText(image);
            // A border-only image carries live turn/selection feedback. Removing
            // its sprite would fill its center, and styling would fight its tint.
            if (!target && !indicator && (image.preserveAspect && !textPanel || image.type == Image.Type.Filled ||
                image.type == Image.Type.Sliced && !image.fillCenter)) continue;
            // Small authored symbols (dropdown arrows and close icons) are not panels.
            if (!target && image.sprite != null && image.GetComponentInParent<Selectable>() != null) indicator = true;
            if (!images.ContainsKey(image)) images.Add(image, (image.sprite,
                image.overrideSprite != image.sprite ? image.overrideSprite : null, image.type, image.color));
            if (!indicator && (!image.preserveAspect || textPanel))
            {
                image.overrideSprite = null;
                image.sprite = null;
                image.type = Image.Type.Simple;
            }
            // Selectable's tint multiplies Graphic.color. White makes its selected and
            // pressed states visible rather than multiplying two near-black colors.
            image.color = target ? Color.white : indicator ? Indicator : Panel;
        }
    }

    private static bool BacksReadableText(Image image)
    {
        // Imported dialog sheets also preserve their aspect ratio. Unlike a
        // portrait/icon, a sheet carrying text needs a dark terminal surface.
        foreach (var text in image.GetComponentsInChildren<TMP_Text>())
            if (text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text)) return true;
        return false;
    }

    private static Color ReadableInk(Color original)
    {
        // Preserve semantic warnings while raising their contrast on the dark surface.
        if (original.r > .4f && original.r > original.g * 1.7f && original.r > original.b * 1.2f)
            return new Color(1f, .62f, .48f, original.a);
        if (original.r > .45f && original.g > .25f && original.b < original.g * .6f)
            return new Color(1f, .86f, .5f, original.a);
        if (original.g > .4f && original.g > original.r * 1.25f && original.g > original.b * 1.15f)
            return new Color(.65f, .95f, .7f, original.a);
        return new Color(Ink.r, Ink.g, Ink.b, original.a);
    }

    private void RememberControlColors(Selectable selectable)
    {
        var current = selectable.colors;
        if (!controls.TryGetValue(selectable, out var original)) { controls.Add(selectable, current); return; }
        if (!appliedControlColors.TryGetValue(selectable, out var applied)) return;
        // Tab state may change while terminal mode is active. Restore its latest
        // authored colors, without mistaking our unchanged tint fields for new values.
        if (current.normalColor != applied.normalColor) original.normalColor = current.normalColor;
        if (current.highlightedColor != applied.highlightedColor) original.highlightedColor = current.highlightedColor;
        if (current.selectedColor != applied.selectedColor) original.selectedColor = current.selectedColor;
        if (current.pressedColor != applied.pressedColor) original.pressedColor = current.pressedColor;
        if (current.disabledColor != applied.disabledColor) original.disabledColor = current.disabledColor;
        if (current.colorMultiplier != applied.colorMultiplier) original.colorMultiplier = current.colorMultiplier;
        if (current.fadeDuration != applied.fadeDuration) original.fadeDuration = current.fadeDuration;
        controls[selectable] = original;
    }

    private static RectTransform BackdropRoot(RectTransform root)
    {
        // Gameplay dialogs keep a full-screen input shield but draw only the right
        // dock. Back that panel, leaving the map visible outside its actual bounds.
        RectTransform dock = null;
        float largestArea = 0;
        var corners = new Vector3[4];
        foreach (var image in root.GetComponentsInChildren<Image>())
        {
            if (!image.enabled || image.color.a == 0 || image.preserveAspect ||
                image.GetComponentInParent<Selectable>() != null) continue;
            image.rectTransform.GetWorldCorners(corners);
            var lower = root.InverseTransformPoint(corners[0]);
            var upper = root.InverseTransformPoint(corners[2]);
            float width = upper.x - lower.x, height = upper.y - lower.y;
            if (lower.x < root.rect.xMin + root.rect.width * .6f ||
                width > root.rect.width * .4f || height < root.rect.height * .5f) continue;
            float area = width * height;
            if (area <= largestArea) continue;
            largestArea = area;
            dock = image.rectTransform;
        }
        return dock != null ? dock : root;
    }

    internal void Restore()
    {
        foreach (var pair in texts) if (pair.Key != null)
        {
            pair.Key.font = pair.Value.Font;
            if (!appliedTextColors.TryGetValue(pair.Key, out var applied) || pair.Key.color == applied)
                pair.Key.color = pair.Value.Color;
        }
        foreach (var pair in images) if (pair.Key != null)
        { pair.Key.sprite = pair.Value.Sprite; pair.Key.overrideSprite = pair.Value.Override; pair.Key.type = pair.Value.Type; pair.Key.color = pair.Value.Color; }
        foreach (var control in new List<Selectable>(controls.Keys)) if (control != null) RememberControlColors(control);
        foreach (var pair in controls) if (pair.Key != null) pair.Key.colors = pair.Value;
        foreach (var backdrop in backdrops.Values) if (backdrop != null)
        {
            backdrop.gameObject.SetActive(false);
            if (Application.isPlaying) Object.Destroy(backdrop.gameObject);
            else Object.DestroyImmediate(backdrop.gameObject);
        }
        texts.Clear(); images.Clear(); controls.Clear();
        appliedTextColors.Clear(); appliedControlColors.Clear();
        backdrops.Clear();
    }
}
