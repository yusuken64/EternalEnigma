using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Attribute benefits use the same stat labels, values and changes as equipment previews.</summary>
public sealed class LevelUpChoiceView : MonoBehaviour
{
    private static readonly Color Muted = new(.40f, .32f, .23f);
    private TMP_Text title, rank;
    private RectTransform grid;
    private readonly List<Cell> cells = new();
    private Vector2 previousSize;
    private int count;

    private sealed class Cell
    {
        public RectTransform Rect;
        public TMP_Text Label, Before, Arrow, After, Delta;
    }

    public void Show(HeroAttribute attribute, int beforeRank, int afterRank, Stats before, Stats after)
    {
        if (title == null)
        {
            title = GetComponentInChildren<TMP_Text>();
            rank = Label("Attribute rank", transform, 23, Muted);
            rank.rectTransform.anchorMin = new Vector2(.06f, .73f);
            rank.rectTransform.anchorMax = new Vector2(.94f, .84f);
            rank.rectTransform.offsetMin = rank.rectTransform.offsetMax = Vector2.zero;
            grid = Rect("Attribute benefits", transform);
            grid.anchorMin = new Vector2(.06f, .065f);
            grid.anchorMax = new Vector2(.94f, .68f);
            grid.offsetMin = grid.offsetMax = Vector2.zero;
        }
        title.text = attribute switch { HeroAttribute.Str => "Strength", HeroAttribute.Int => "Intelligence", _ => "Agility" };
        string green = ColorUtility.ToHtmlStringRGB(EquipmentComparisonView.Increase);
        rank.text = $"{attribute.ToString().ToUpperInvariant()}  {beforeRank} → <color=#{green}>{afterRank}</color>";
        var values = new EquipmentStatPreview(before, after, false).Values.Where(value => value.Direction != 0).ToArray();
        count = values.Length;
        for (int i = 0; i < count; i++)
        {
            if (i == cells.Count) cells.Add(CreateCell());
            var cell = cells[i]; var value = values[i];
            cell.Rect.gameObject.SetActive(true);
            cell.Label.text = value.Label;
            cell.Before.text = value.BeforeText;
            cell.After.text = value.AfterText;
            cell.Delta.text = value.DeltaText;
            // Fewer actions between recovery ticks is an improvement.
            int direction = value.Label is "Mana recovery" or "HP recovery" ? -value.Direction : value.Direction;
            cell.After.color = cell.Delta.color = direction > 0 ? EquipmentComparisonView.Increase : EquipmentComparisonView.Decrease;
        }
        for (int i = count; i < cells.Count; i++) cells[i].Rect.gameObject.SetActive(false);
        Reflow();
    }

    private void LateUpdate()
    {
        if (grid != null && previousSize != grid.rect.size) Reflow();
    }

    private void Reflow()
    {
        previousSize = grid.rect.size;
        float width = grid.rect.width / 2;
        float height = grid.rect.height / Mathf.Max(2, Mathf.CeilToInt(count / 2f));
        for (int i = 0; i < count; i++)
        {
            var cell = cells[i];
            Place(cell.Rect, i % 2 * width, i / 2 * height, width, height);
            Place(cell.Label.rectTransform, 2, 0, width - 4, height * .27f);
            float arrowWidth = 20, numberWidth = (width - arrowWidth - 8) / 2;
            Place(cell.Before.rectTransform, 4, height * .27f, numberWidth, height * .36f);
            Place(cell.Arrow.rectTransform, 4 + numberWidth, height * .27f, arrowWidth, height * .36f);
            Place(cell.After.rectTransform, 4 + numberWidth + arrowWidth, height * .27f, numberWidth, height * .36f);
            Place(cell.Delta.rectTransform, 4 + numberWidth + arrowWidth, height * .63f, numberWidth, height * .28f);
        }
    }

    private Cell CreateCell()
    {
        var rect = Rect("Stat", grid);
        var cell = new Cell { Rect = rect,
            Label = Label("Stat name", rect, 20, Muted), Before = Label("Before", rect, 28, GameUISkin.Ink),
            Arrow = Label("Arrow", rect, 20, Muted), After = Label("After", rect, 28, GameUISkin.Ink),
            Delta = Label("Change", rect, 21, Muted) };
        cell.Before.alignment = TextAlignmentOptions.MidlineRight;
        cell.After.alignment = cell.Delta.alignment = TextAlignmentOptions.MidlineLeft;
        cell.Arrow.text = "→";
        return cell;
    }

    private TMP_Text Label(string name, Transform parent, float size, Color color)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        GameTypography.Apply(label, GameTextRole.Interface);
        label.fontSize = label.fontSizeMax = size; label.fontSizeMin = 16; label.enableAutoSizing = true;
        label.color = color; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Midline;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        return rect;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
}
