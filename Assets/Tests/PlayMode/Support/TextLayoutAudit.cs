#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Measure rendered text separately from TMP's layout flag: margins can report
// overflow even when every glyph fits, and scroll masks intentionally clip rows.
public static class TextLayoutAudit
{
    [Serializable] public sealed class LabelReport
    {
        public string path, text, font, overflowMode, menuScope, expectedFont;
        public float fontPixels, x, y, width, height;
        public bool overflow, glyphsOutsideTextBox, glyphsOutsideScreen, visible, fontChecked, fontMismatch;
        public string[] panelsOutside;
    }

    [Serializable] private sealed class ScreenReport
    {
        public int width, height;
        public LabelReport[] labels;
        public string[] issues;
    }

    public static string[] Capture(string path)
    {
        Canvas.ForceUpdateCanvases();
        var labels = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
            .Where(label => label.isActiveAndEnabled && label.color.a > .01f && !string.IsNullOrWhiteSpace(label.text) &&
                label.canvas != null && label.canvas.rootCanvas.renderMode != RenderMode.WorldSpace &&
                label.GetComponentsInParent<Canvas>().All(canvas => canvas.enabled) && label.canvasRenderer.GetInheritedAlpha() > .01f)
            .Select(Measure).OrderBy(label => label.path, StringComparer.Ordinal).ToArray();
        var issues = labels.Where(label => label.glyphsOutsideTextBox || label.glyphsOutsideScreen || label.panelsOutside.Length > 0 || label.fontMismatch)
            .Select(label => label.path + ": " +
                (label.glyphsOutsideTextBox ? "text box; " : "") +
                (label.glyphsOutsideScreen ? "screen; " : "") +
                (label.fontMismatch ? $"font {label.font}, expected {label.expectedFont}; " : "") +
                string.Join(", ", label.panelsOutside) + " | " + label.text.Substring(0, Math.Min(160, label.text.Length))).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path + ".json", JsonUtility.ToJson(new ScreenReport {
            width = Screen.width, height = Screen.height, labels = labels, issues = issues
        }, true));
        return issues;
    }

    private static LabelReport Measure(TextMeshProUGUI label)
    {
        label.ForceMeshUpdate();
        var canvas = label.canvas.rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var textRect = ScreenRect(label.rectTransform, label.rectTransform.rect, camera);
        var glyphs = ScreenRect(label.rectTransform, GlyphRect(label), camera);
        bool clippedByText = label.overflowMode == TextOverflowModes.Masking || label.overflowMode == TextOverflowModes.ScrollRect;
        if (clippedByText) glyphs = Intersect(glyphs, textRect);
        foreach (var mask in label.GetComponentsInParent<RectMask2D>())
            if (mask.isActiveAndEnabled) glyphs = Intersect(glyphs, ScreenRect(mask.rectTransform, mask.rectTransform.rect, camera));
        foreach (var mask in label.GetComponentsInParent<Mask>())
            if (mask.isActiveAndEnabled && mask.graphic != null)
                glyphs = Intersect(glyphs, ScreenRect(mask.graphic.rectTransform, mask.graphic.rectTransform.rect, camera));
        bool visible = glyphs.width > 0 && glyphs.height > 0;
        bool outsideBox = visible && !Contains(textRect, glyphs);
        var panels = new List<string>();
        if (visible)
            for (var parent = label.transform.parent; parent != null; parent = parent.parent)
            {
                var panel = GameUISkin.PanelGraphic(parent) ?? parent.Find("Background")?.GetComponent<Image>();
                if (panel != null && panel.isActiveAndEnabled && panel.color.a >= .01f && panel.canvasRenderer.GetInheritedAlpha() >= .01f)
                {
                    var panelRect = panel.rectTransform.rect;
                    if (panel.preserveAspect && panel.type == Image.Type.Simple && panel.sprite != null)
                    {
                        float aspect = panel.sprite.rect.width / panel.sprite.rect.height;
                        if (panelRect.width / panelRect.height > aspect)
                        {
                            float width = panelRect.height * aspect;
                            panelRect.x += (panelRect.width - width) * panel.rectTransform.pivot.x;
                            panelRect.width = width;
                        }
                        else
                        {
                            float height = panelRect.width / aspect;
                            panelRect.y += (panelRect.height - height) * panel.rectTransform.pivot.y;
                            panelRect.height = height;
                        }
                    }
                    if (!Contains(ScreenRect(panel.rectTransform, panelRect, camera), glyphs)) panels.Add(PathOf(panel.transform));
                }
                // A dropdown popup has its own sorting canvas and intentionally extends beyond its owning button.
                if (parent.TryGetComponent<Canvas>(out var sortingCanvas) && sortingCanvas.overrideSorting) break;
            }
        var dialog = label.GetComponentInParent<JuicyChickenGames.Menu.Dialog>();
        var focus = MenuUIInputModule.Active?.FocusRoot;
        var autoplay = label.GetComponentInParent<AutoplayPanel>();
        string scope = dialog != null ? dialog.GetType().Name : autoplay != null ? nameof(AutoplayPanel) :
            focus != null && label.transform.IsChildOf(focus) ? focus.name : null;
        // Terminal styling belongs to the current focus hierarchy. Covered
        // parent dialogs have their original style restored when a child opens.
        bool checkFont = visible && scope != null && (!TerminalMode.Effective || autoplay != null ||
            focus == null || label.transform.IsChildOf(focus));
        var expected = TerminalMode.Effective && autoplay == null
            ? Common.InstanceOrNull?.GetComponentInChildren<TerminalScreen>(true)?.FontAsset
            : TMP_Settings.defaultFontAsset;
        return new LabelReport {
            path = PathOf(label.transform), text = label.text, font = label.font != null ? label.font.name : "MISSING",
            menuScope = scope, expectedFont = expected != null ? expected.name : "MISSING",
            fontChecked = checkFont, fontMismatch = checkFont && (expected == null || label.font != expected),
            fontPixels = label.fontSize * label.rectTransform.lossyScale.y, overflow = label.isTextOverflowing,
            overflowMode = label.overflowMode.ToString(), x = textRect.x, y = textRect.y, width = textRect.width, height = textRect.height,
            glyphsOutsideTextBox = outsideBox, visible = visible, panelsOutside = panels.ToArray(),
            glyphsOutsideScreen = visible && !Contains(new Rect(0, 0, Screen.width, Screen.height), glyphs)
        };
    }

    private static Rect GlyphRect(TMP_Text label)
    {
        var points = new List<Vector3>();
        for (int i = 0; i < label.textInfo.characterCount; i++)
        {
            var character = label.textInfo.characterInfo[i];
            if (!character.isVisible) continue;
            var vertices = label.textInfo.meshInfo[character.materialReferenceIndex].vertices;
            int index = character.vertexIndex;
            if (index + 3 >= vertices.Length || vertices[index] == vertices[index + 2]) continue;
            float insetX = 0, insetY = 0;
            if (character.elementType == TMP_TextElementType.Character)
            {
                // SDF quads include transparent shader padding. Measure the glyph face,
                // retaining the quad's italic shear and horizontal autosize compression.
                float padding = ShaderUtilities.GetPadding(character.material, label.extraPadding, label.isUsingBold);
                var metrics = (character.alternativeGlyph ?? character.textElement.glyph).metrics;
                insetX = padding / Mathf.Max(1, metrics.width + 2 * padding);
                insetY = padding / Mathf.Max(1, metrics.height + 2 * padding);
            }
            foreach (float x in new[] { insetX, 1 - insetX })
                foreach (float y in new[] { insetY, 1 - insetY })
                    points.Add(Vector3.Lerp(Vector3.Lerp(vertices[index], vertices[index + 3], x),
                        Vector3.Lerp(vertices[index + 1], vertices[index + 2], x), y));
        }
        return points.Count == 0 ? Rect.zero : Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
            points.Max(point => point.x), points.Max(point => point.y));
    }

    private static Rect ScreenRect(RectTransform transform, Rect rect, Camera camera)
    {
        var corners = new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
            new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax) };
        var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(point))).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }

    private static bool Contains(Rect outer, Rect inner) => inner.width <= 0 || inner.height <= 0 ||
        (inner.xMin >= outer.xMin - 2 && inner.xMax <= outer.xMax + 2 && inner.yMin >= outer.yMin - 2 && inner.yMax <= outer.yMax + 2);

    private static Rect Intersect(Rect a, Rect b)
    {
        float x = Mathf.Max(a.xMin, b.xMin), y = Mathf.Max(a.yMin, b.yMin);
        return new Rect(x, y, Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - x), Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - y));
    }

    private static string PathOf(Transform transform) => transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;
}
#endif
