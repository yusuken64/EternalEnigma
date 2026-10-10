using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reversible styling; neither callbacks, layout, nor raycast routing are replaced.</summary>
internal sealed class TerminalMenuStyle
{
    private readonly Dictionary<TMP_Text, (TMP_FontAsset Font, Color Color)> texts = new();
    private readonly Dictionary<Image, (Sprite Sprite, Image.Type Type, Color Color)> images = new();
    private readonly Dictionary<Selectable, ColorBlock> controls = new();
    internal void Refresh(Transform root, TMP_FontAsset font)
    {
        if (root == null || font == null) return;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>())
        {
            if (!texts.ContainsKey(text)) texts.Add(text, (text.font, text.color));
            text.font = font;
            text.color = new Color(.82f, .95f, .87f);
        }
        foreach (var image in root.GetComponentsInChildren<Image>())
        {
            // Portraits and item icons retain their meanings alongside the text labels.
            if (image.preserveAspect || image.color.a == 0) continue;
            if (!images.ContainsKey(image)) images.Add(image, (image.sprite, image.type, image.color));
            image.sprite = null; image.type = Image.Type.Simple;
            image.color = new Color(.015f, .035f, .025f, .98f);
        }
        foreach (var selectable in root.GetComponentsInChildren<Selectable>())
        {
            if (!controls.ContainsKey(selectable)) controls.Add(selectable, selectable.colors);
            var colors = selectable.colors;
            colors.normalColor = new Color(.035f, .07f, .045f);
            colors.highlightedColor = colors.selectedColor = new Color(.15f, .34f, .22f);
            colors.pressedColor = new Color(.25f, .48f, .32f);
            colors.disabledColor = new Color(.1f, .1f, .1f, .65f);
            selectable.colors = colors;
        }
    }
    internal void Restore()
    {
        foreach (var pair in texts) if (pair.Key != null) { pair.Key.font = pair.Value.Font; pair.Key.color = pair.Value.Color; }
        foreach (var pair in images) if (pair.Key != null)
        { pair.Key.sprite = pair.Value.Sprite; pair.Key.type = pair.Value.Type; pair.Key.color = pair.Value.Color; }
        foreach (var pair in controls) if (pair.Key != null) pair.Key.colors = pair.Value;
        texts.Clear(); images.Clear(); controls.Clear();
    }
}
