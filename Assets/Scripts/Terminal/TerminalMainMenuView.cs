using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TerminalMainMenuView : MonoBehaviour
{
    private static readonly Dictionary<char, string[]> TitleGlyphs = new()
    {
        ['E'] = new[] { "#####", "##   ", "##   ", "#### ", "##   ", "##   ", "#####" },
        ['T'] = new[] { "#####", " ### ", " ### ", " ### ", " ### ", " ### ", " ### " },
        ['R'] = new[] { "#### ", "## ##", "## ##", "#### ", "## ##", "## ##", "## ##" },
        ['N'] = new[] { "## ##", "### ##", "#####", "#####", "## ###", "## ##", "## ##" },
        ['A'] = new[] { " ### ", "## ##", "## ##", "#####", "## ##", "## ##", "## ##" },
        ['L'] = new[] { "##   ", "##   ", "##   ", "##   ", "##   ", "##   ", "#####" },
        ['I'] = new[] { "#####", " ### ", " ### ", " ### ", " ### ", " ### ", "#####" },
        ['G'] = new[] { " ####", "##   ", "##   ", "## ##", "## ##", "## ##", " ####" },
        ['M'] = new[] { "## ##", "#####", "#####", "## ##", "## ##", "## ##", "## ##" }
    };

    private static string[] TitleRows(int columns)
    {
        const string name = "ETERNAL ENIGMA";
        const int glyphWidth = 5, glyphHeight = 7, depth = 2;
        int width = name.Count(letter => letter != ' ') * (glyphWidth + 1) + 3 - 1 + depth;
        if (columns < width) return new[] { "    #### ETERNAL ####", "     ### ENIGMA ###" };
        var cells = new char[glyphHeight + depth, width];
        int x = 0;
        foreach (char letter in name)
        {
            if (letter == ' ') { x += 3; continue; }
            var glyph = TitleGlyphs[letter];
            for (int y = 0; y < glyphHeight; y++)
            for (int column = 0; column < glyphWidth; column++)
                if (glyph[y][column] == '#') cells[y + depth, x + column + depth] = ':';
            for (int y = 0; y < glyphHeight; y++)
            for (int column = 0; column < glyphWidth; column++)
                if (glyph[y][column] == '#') cells[y, x + column] = '#';
            x += glyphWidth + 1;
        }
        var result = new string[cells.GetLength(0)];
        int left = (columns - width) / 2;
        for (int y = 0; y < cells.GetLength(0); y++)
        {
            var line = new StringBuilder();
            line.Append(' ', left);
            char previous = '\0';
            for (int column = 0; column < width; column++)
            {
                char current = cells[y, column];
                if (current != previous)
                {
                    if (previous == '#' || previous == ':') line.Append("</color>");
                    if (current == '#') line.Append("<color=#F6DA8B>");
                    if (current == ':') line.Append("<color=#426553>");
                    previous = current;
                }
                line.Append(current == '\0' ? ' ' : current);
            }
            if (previous == '#' || previous == ':') line.Append("</color>");
            result[y] = line.ToString();
        }
        return result;
    }

    private readonly struct RectState
    {
        private readonly Vector2 min, max, position, size;
        public RectState(RectTransform rect)
        {
            min = rect.anchorMin; max = rect.anchorMax;
            position = rect.anchoredPosition; size = rect.sizeDelta;
        }
        public void Restore(RectTransform rect)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }

    private readonly struct TextState
    {
        private readonly string value;
        private readonly TMP_FontAsset font;
        private readonly float size;
        private readonly Color color;
        private readonly TextAlignmentOptions alignment;
        public string Value => value;
        public TextState(TMP_Text text)
        {
            value = text.text; font = text.font; size = text.fontSize;
            color = text.color; alignment = text.alignment;
        }
        public void Restore(TMP_Text text)
        {
            text.text = value; text.font = font; text.fontSize = size;
            text.color = color; text.alignment = alignment;
        }
    }

    private sealed class Option
    {
        public Button Button;
        public TMP_Text Label;
        public TextState OriginalLabel;
        public Graphic Background;
        public Color OriginalBackground;
        public ColorBlock OriginalColors;
        public bool OriginalSingleClick;
    }

    private static readonly Color Ink = new(.58f, .91f, .72f);
    private static readonly Color SelectedInk = new(1f, .88f, .48f);
    private MainMenu menu;
    private TMP_FontAsset font;
    private RectTransform optionsRect;
    private RectState originalOptionsRect;
    private TMP_Text title;
    private RectState originalTitleRect;
    private TextState originalTitle;
    private Option[] options;
    private Canvas arrowCanvas;
    private bool originalArrowEnabled;
    private Graphic menuBacking;
    private bool originalBackingEnabled;
    private GameObject developerToggle;
    private bool originalDeveloperToggleActive;
    private TMP_Text hint;
    private bool shown;
    private float introStartedAt;

    public string CurrentTitleMarkup(int columns, int rows)
    {
        var titleRows = TitleRows(columns);
        int visible = Mathf.Clamp(1 + Mathf.FloorToInt((Time.unscaledTime - introStartedAt) / .14f), 1, titleRows.Length);
        var dungeon = DungeonRows(columns, rows);
        var result = new StringBuilder();
        for (int y = 0; y < dungeon.Length; y++)
        {
            if (y > 0 && y <= visible) result.Append(titleRows[y - 1]);
            else result.Append("<color=#344D47>").Append(dungeon[y]).Append("</color>");
            result.Append('\n');
        }
        return result.ToString();
    }

    private static string[] DungeonRows(int columns, int rows)
    {
        var cells = new char[rows, columns];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++) cells[y, x] = ' ';
            if (columns < 30 || rows < 15) continue;
            cells[y, 0] = cells[y, columns - 1] = '|';
            if (y == 0 || y == rows - 1)
                for (int x = 0; x < columns; x++) cells[y, x] = '=';
            if (y < 10 || y >= rows - 1) continue;
            int wallEdge = Mathf.Max(8, columns / 5 - (y - 10) / 2);
            int rightEdge = columns - 1 - wallEdge;
            for (int x = 1; x < wallEdge; x++)
                if ((x + 2 * y) % 7 == 0) cells[y, x] = '#';
            for (int x = rightEdge + 1; x < columns - 1; x++)
                if ((x - 2 * y) % 7 == 0) cells[y, x] = '#';
            cells[y, wallEdge] = '/';
            cells[y, rightEdge] = '\\';
            if (y == 13)
            {
                cells[y, 5] = cells[y, columns - 6] = '*';
            }
            if (y == 14) cells[y, 5] = cells[y, columns - 6] = '!';
            if (y >= rows - 5)
                for (int x = 2; x < columns - 2; x += 8)
                    if (x < columns / 2 - 26 || x > columns / 2 + 26)
                        cells[y, x] = y % 2 == 0 ? '+' : '-';
        }
        var result = new string[rows];
        for (int y = 0; y < rows; y++)
        {
            var line = new char[columns];
            for (int x = 0; x < columns; x++) line[x] = cells[y, x];
            result[y] = new string(line);
        }
        return result;
    }

    public void Initialize(MainMenu owner, TMP_FontAsset terminalFont)
    {
        if (menu != null || owner == null || terminalFont == null) return;
        var start = owner.StartButton;
        var canvas = start != null ? start.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;
        var titleTransform = canvas.transform.Find("EternalEnigma");
        title = titleTransform != null ? titleTransform.GetComponent<TMP_Text>() : null;
        if (title == null) return;
        menu = owner;
        font = terminalFont;
        optionsRect = (RectTransform)start.transform.parent;
        originalOptionsRect = new RectState(optionsRect);
        originalTitleRect = new RectState(title.rectTransform);
        originalTitle = new TextState(title);
        options = optionsRect.GetComponentsInChildren<Button>(true).Select(button =>
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            return new Option
            {
                Button = button,
                Label = label,
                OriginalLabel = label != null ? new TextState(label) : default,
                Background = button.targetGraphic,
                OriginalBackground = button.targetGraphic != null ? button.targetGraphic.color : Color.white,
                OriginalColors = button.colors,
                OriginalSingleClick = button is SelectToActivateButton select && select.ActivateOnFirstPointerPress
            };
        }).ToArray();
        arrowCanvas = owner.NavigationHandler != null && owner.NavigationHandler.selectionArrow != null
            ? owner.NavigationHandler.selectionArrow.GetComponentInParent<Canvas>() : null;
        if (arrowCanvas != null) originalArrowEnabled = arrowCanvas.enabled;
        var backing = owner.transform.Find("Menu button column/Backing");
        menuBacking = backing != null ? backing.GetComponent<Graphic>() : null;
        if (menuBacking != null) originalBackingEnabled = menuBacking.enabled;
        var developer = owner.GetComponent<MainMenuDeveloperControls>();
        developerToggle = developer != null && developer.Toggle != null ? developer.Toggle.gameObject : null;
        if (developerToggle != null) originalDeveloperToggleActive = developerToggle.activeSelf;
        var hintObject = new GameObject("Terminal menu hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintObject.transform.SetParent(canvas.transform, false);
        var hintRect = (RectTransform)hintObject.transform;
        SetBounds(hintRect, new Vector2(.08f, .08f), new Vector2(.92f, .17f));
        hint = hintObject.GetComponent<TextMeshProUGUI>();
        hint.font = font;
        hint.fontSize = 18;
        hint.color = Ink;
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
        hint.text = "UP/DOWN: SELECT    ENTER: CONFIRM    F11: 3D VIEW";
        hintObject.SetActive(false);
    }

    public void Show(bool enabled)
    {
        if (menu == null || shown == enabled) return;
        shown = enabled;
        if (title == null || optionsRect == null || options == null) return;
        if (enabled)
        {
            introStartedAt = Time.unscaledTime;
            SetBounds(title.rectTransform, new Vector2(.08f, .76f), new Vector2(.92f, .91f));
            title.text = "";
            title.font = font;
            title.fontSize = 54;
            title.color = Ink;
            title.alignment = TextAlignmentOptions.Center;
            SetBounds(optionsRect, new Vector2(.31f, .23f), new Vector2(.69f, .61f));
            foreach (var option in options)
            {
                if (option.Button is SelectToActivateButton select) select.ActivateOnFirstPointerPress = true;
                option.Button.colors = new ColorBlock
                {
                    normalColor = Color.clear, highlightedColor = Color.clear,
                    pressedColor = Color.clear, selectedColor = Color.clear,
                    disabledColor = Color.clear, colorMultiplier = 1, fadeDuration = 0
                };
                if (option.Background != null) option.Background.color = Color.clear;
                if (option.Label == null) continue;
                option.Label.font = font;
                option.Label.fontSize = 26;
                option.Label.alignment = TextAlignmentOptions.Center;
            }
            if (arrowCanvas != null) arrowCanvas.enabled = false;
            if (menuBacking != null) menuBacking.enabled = false;
            if (developerToggle != null) developerToggle.SetActive(false);
            if (hint != null) hint.gameObject.SetActive(true);
            RefreshSelection();
        }
        else
        {
            originalTitleRect.Restore(title.rectTransform);
            originalTitle.Restore(title);
            originalOptionsRect.Restore(optionsRect);
            foreach (var option in options)
            {
                if (option.Button == null) continue;
                if (option.Button is SelectToActivateButton select) select.ActivateOnFirstPointerPress = option.OriginalSingleClick;
                option.Button.colors = option.OriginalColors;
                if (option.Background != null) option.Background.color = option.OriginalBackground;
                if (option.Label != null) option.OriginalLabel.Restore(option.Label);
            }
            if (arrowCanvas != null) arrowCanvas.enabled = originalArrowEnabled;
            if (menuBacking != null) menuBacking.enabled = originalBackingEnabled;
            if (developerToggle != null) developerToggle.SetActive(originalDeveloperToggleActive);
            if (hint != null) hint.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!shown || options == null || menu == null) return;
        if (developerToggle != null && developerToggle.activeSelf) developerToggle.SetActive(false);
        var module = MenuUIInputModule.Active;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (module != null && !module.HasDialog && menu.IsReady &&
            menu.NavigationHandler != null && menu.NavigationHandler.isActiveAndEnabled &&
            !options.Any(option => option.Button != null && option.Button.gameObject == selected))
            menu.StartButton.GetComponent<Button>().Select();
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (options == null) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var option in options)
        {
            if (option.Button == null || option.Label == null) continue;
            bool focused = selected == option.Button.gameObject;
            string prefix = focused ? "> " : "  ";
            option.Label.text = prefix + option.OriginalLabel.Value.ToUpperInvariant();
            option.Label.color = focused ? SelectedInk : Ink;
        }
    }

    private static void SetBounds(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
