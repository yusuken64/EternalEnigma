using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A separate, bottom-anchored stat comparison. Item text scrolls in the space above it.</summary>
public sealed class EquipmentComparisonView : MonoBehaviour
{
    public static readonly Color Increase = new(.08f, .40f, .25f);
    public static readonly Color Decrease = new(.65f, .18f, .16f);
    private static readonly Color Muted = new(.40f, .32f, .23f);
    private static Color Ink => GameUISkin.Ink;
    private TMP_Text description, caption;
    private RectTransform panel, viewport, strip;
    private ScrollRect descriptionScroll, comparisonScroll;
    private readonly List<Cell> cells = new();
    private Vector2 previousPanelSize;
    private float descriptionFontSize;
    private bool descriptionAutoSizing;
    private string fittedDescription;
    private Vector2 fittedDescriptionArea;
    private const float HorizontalInset = .07f, Edge = 24, Gap = 20, MinimumDescriptionHeight = 128;
    private const float HeaderHeight = 36, RowHeight = 94;
    public EquipmentStatPreview Preview { get; private set; }
    public float RequiredPanelHeight => StatHeight(panel.rect.width * (1 - HorizontalInset * 2)) + Edge * 2 + Gap + MinimumDescriptionHeight;
    public RectTransform Strip => strip;
    public RectTransform Viewport => viewport;
    private sealed class Cell
    {
        public RectTransform Rect;
        public TMP_Text Label, Before, Arrow, After, Delta;
    }

    public static EquipmentComparisonView For(TMP_Text description, RectTransform panel, ScrollRect descriptionScroll)
    {
        if (!description.TryGetComponent<EquipmentComparisonView>(out var view))
        {
            view = description.gameObject.AddComponent<EquipmentComparisonView>();
            view.description = description;
            view.descriptionFontSize = description.fontSize;
            view.descriptionAutoSizing = description.enableAutoSizing;
            view.panel = panel;
            view.descriptionScroll = descriptionScroll;
            view.viewport = Rect("Equipment preview area", panel);
            view.viewport.anchorMin = new Vector2(HorizontalInset, 0);
            view.viewport.anchorMax = new Vector2(1 - HorizontalInset, 0);
            view.viewport.pivot = new Vector2(.5f, 0);
            view.viewport.gameObject.AddComponent<RectMask2D>();
            view.viewport.gameObject.AddComponent<Image>().color = Color.clear;
            view.strip = Rect("Equipment comparison", view.viewport);
            view.comparisonScroll = view.viewport.gameObject.AddComponent<ScrollRect>();
            view.comparisonScroll.viewport = view.viewport; view.comparisonScroll.content = view.strip;
            view.comparisonScroll.horizontal = false;
            view.comparisonScroll.movementType = ScrollRect.MovementType.Clamped;
            view.comparisonScroll.scrollSensitivity = 24;
            view.caption = view.Label("Preview caption", view.strip, 19, Muted);
            view.caption.alignment = TextAlignmentOptions.MidlineLeft;
            view.viewport.gameObject.SetActive(false);
        }
        return view;
    }

    public void Show(EquipmentStatPreview preview)
    {
        Preview = preview;
        viewport.gameObject.SetActive(preview != null);
        if (preview == null)
        {
            description.fontSize = descriptionFontSize;
            description.enableAutoSizing = descriptionAutoSizing;
            fittedDescription = null;
            return;
        }
        caption.text = preview.Caption + "  ·  Before → After";
        for (int i = 0; i < preview.Values.Count; i++)
        {
            if (i == cells.Count) cells.Add(CreateCell());
            var cell = cells[i]; var value = preview.Values[i];
            cell.Rect.gameObject.SetActive(true);
            cell.Label.text = value.Label;
            cell.Before.text = value.BeforeText;
            cell.After.text = value.AfterText;
            cell.Delta.text = value.DeltaText;
            cell.After.color = cell.Delta.color = value.Direction > 0 ? Increase : value.Direction < 0 ? Decrease : Ink;
            if (value.Direction == 0) cell.Delta.color = Muted;
        }
        for (int i = preview.Values.Count; i < cells.Count; i++) cells[i].Rect.gameObject.SetActive(false);
        Reflow();
        comparisonScroll.StopMovement(); comparisonScroll.verticalNormalizedPosition = 1;
    }

    private void LateUpdate()
    {
        if (Preview != null && (previousPanelSize != panel.rect.size || fittedDescription != description.text)) Reflow();
    }

    private static int Columns(float width) => width < 320 ? 2 : 3;
    private float StatHeight(float width) => HeaderHeight + Mathf.CeilToInt((float)(Preview?.Values.Count ?? 3) / Columns(width)) * RowHeight + 6;

    public void Reflow()
    {
        if (Preview == null) return;
        previousPanelSize = panel.rect.size;
        float width = Mathf.Max(1, panel.rect.width * (1 - HorizontalInset * 2));
        const float padding = 12;
        int columns = Columns(width);
        float height = StatHeight(width);
        float visibleHeight = Mathf.Min(height, Mathf.Max(0, panel.rect.height - Edge * 2 - Gap - MinimumDescriptionHeight));
        viewport.anchoredPosition = new Vector2(0, Edge);
        viewport.sizeDelta = new Vector2(0, visibleHeight);
        strip.sizeDelta = new Vector2(width, height);
        comparisonScroll.vertical = height > visibleHeight + .5f;
        // Neither the preview's position nor the panel's height depends on the item copy.
        var textArea = (RectTransform)descriptionScroll.transform;
        if (textArea.parent != panel) textArea.SetParent(panel, false);
        textArea.anchorMin = new Vector2(HorizontalInset, 0);
        textArea.anchorMax = new Vector2(1 - HorizontalInset, 1);
        textArea.offsetMin = new Vector2(0, Edge + visibleHeight + Gap);
        textArea.offsetMax = new Vector2(0, -Edge);
        FitDescription(new Vector2(width, Mathf.Max(1, panel.rect.height - Edge * 2 - visibleHeight - Gap)));
        Place(caption.rectTransform, padding, 4, width - padding * 2, 28);
        float cellWidth = (width - padding * 2) / columns;
        for (int i = 0; i < Preview.Values.Count; i++)
        {
            var cell = cells[i];
            Place(cell.Rect, padding + i % columns * cellWidth, HeaderHeight + i / columns * RowHeight, cellWidth, RowHeight);
            Place(cell.Label.rectTransform, 2, 0, cellWidth - 4, 23);
            float arrowWidth = 20, numberWidth = (cellWidth - arrowWidth - 8) / 2;
            Place(cell.Before.rectTransform, 4, 25, numberWidth, 32);
            Place(cell.Arrow.rectTransform, 4 + numberWidth, 25, arrowWidth, 32);
            Place(cell.After.rectTransform, 4 + numberWidth + arrowWidth, 25, numberWidth, 32);
            // Align the small change with the resulting absolute value.
            Place(cell.Delta.rectTransform, 4 + numberWidth + arrowWidth, 57, numberWidth, 25);
        }
    }

    private void FitDescription(Vector2 area)
    {
        if (fittedDescription == description.text && fittedDescriptionArea == area) return;
        fittedDescription = description.text; fittedDescriptionArea = area;
        description.enableAutoSizing = false;
        description.fontSize = descriptionFontSize;
        // TMP's explicit-width measurement expects the inner width, unlike the RectTransform.
        float textWidth = Mathf.Max(1, area.x - description.margin.x - description.margin.z);
        float PreferredHeight() => description.GetPreferredValues(description.text, textWidth, 0).y;
        if (PreferredHeight() > area.y)
        {
            // Fit ordinary equipment copy without tiny text. Unusually long copy can still scroll.
            float low = Mathf.Min(20, descriptionFontSize), high = descriptionFontSize;
            description.fontSize = low;
            if (PreferredHeight() <= area.y)
            {
                for (int i = 0; i < 8; i++)
                {
                    float candidate = (low + high) / 2;
                    description.fontSize = candidate;
                    if (PreferredHeight() <= area.y) low = candidate; else high = candidate;
                }
                description.fontSize = Mathf.Floor(low * 4) / 4;
            }
        }
        descriptionScroll.StopMovement();
        description.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void OnEnable() { if (viewport != null) viewport.gameObject.SetActive(Preview != null); }
    private void OnDisable() { if (viewport != null) viewport.gameObject.SetActive(false); }
    private void OnDestroy() { if (viewport != null) Destroy(viewport.gameObject); }

    private Cell CreateCell()
    {
        var rect = Rect("Stat", strip);
        var cell = new Cell { Rect = rect,
            Label = Label("Stat name", rect, 20, Muted), Before = Label("Before", rect, 28, Ink),
            Arrow = Label("Arrow", rect, 20, Muted), After = Label("After", rect, 28, Ink),
            Delta = Label("Change", rect, 21, Muted) };
        cell.Before.alignment = TextAlignmentOptions.MidlineRight;
        cell.After.alignment = cell.Delta.alignment = TextAlignmentOptions.MidlineLeft;
        cell.Arrow.text = "→";
        return cell;
    }
    private TMP_Text Label(string name, Transform parent, float size, Color color)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = description.font;
        label.fontSize = size; label.fontSizeMax = size; label.fontSizeMin = 14; label.enableAutoSizing = true;
        label.color = color; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Midline;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        return rect;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
