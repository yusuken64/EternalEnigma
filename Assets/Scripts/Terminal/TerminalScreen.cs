using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TerminalScreen : MonoBehaviour
{
    private Canvas canvas;
    private TextMeshProUGUI text;
    private TMP_FontAsset ownedFont;
    private int lastWidth, lastHeight;
    private Vector2 viewportMin = Vector2.zero, viewportMax = Vector2.one;
    public RectTransform ContentRect => text != null ? text.rectTransform : null;
    public bool IsReady => text != null && text.font != null;
    public TMP_FontAsset FontAsset => text != null ? text.font : null;
    public float FontSize => text != null ? text.fontSize : 18;
    public int Columns { get; private set; }
    public int Rows { get; private set; }

    public bool Initialize()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>().enabled = false;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(transform, false);
        var backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        var image = background.GetComponent<Image>(); image.color = Color.black; image.raycastTarget = false;
        var content = new GameObject("ASCII", typeof(RectTransform), typeof(TextMeshProUGUI));
        content.transform.SetParent(transform, false);
        var rect = (RectTransform)content.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 12); rect.offsetMax = new Vector2(-12, -12);
        text = content.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.fontSize = 18;
        var source = Resources.Load<Font>("Terminal/TerminalMono");
        if (source != null)
        {
            ownedFont = TMP_FontAsset.CreateFontAsset(source);
            if (ownedFont != null)
            {
                ownedFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                ownedFont.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
            }
        }
        text.font = ownedFont != null ? ownedFont : TMP_Settings.defaultFontAsset;
        if (text.font == null) { Destroy(gameObject); return false; }
        canvas.enabled = false;
        Resize();
        return true;
    }

    public bool Resize() => Resize(Screen.width, Screen.height);
    internal bool SetViewport(Vector2 min, Vector2 max)
    {
        if (text == null || viewportMin == min && viewportMax == max) return false;
        viewportMin = min; viewportMax = max;
        text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max;
        lastWidth = lastHeight = 0;
        return Resize();
    }
    internal bool Resize(int width, int height)
    {
        if (text == null) return false;
        if (width == lastWidth && height == lastHeight) return false;
        lastWidth = width; lastHeight = height;
        // Keep the same readable logical size as the surrounding UI on taller displays.
        text.fontSize = 18f * Mathf.Max(1, height / 720f);
        // Text area uses overlay canvas pixels and a fixed advance, including the fallback SDF.
        float cellWidth = text.fontSize;
        float lineHeight = text.fontSize * 21f / 18f;
        if (ownedFont != null && ownedFont.characterLookupTable.TryGetValue('M', out var letter))
        {
            float scale = text.fontSize / Mathf.Max(1, ownedFont.faceInfo.pointSize);
            cellWidth = Mathf.Max(1, letter.glyph.metrics.horizontalAdvance * scale);
            lineHeight = Mathf.Max(1, ownedFont.faceInfo.lineHeight * scale);
        }
        Columns = Mathf.Max(8, Mathf.FloorToInt((width * (viewportMax.x - viewportMin.x) - 24) / cellWidth));
        Rows = Mathf.Max(5, Mathf.FloorToInt((height * (viewportMax.y - viewportMin.y) - 24) / lineHeight));
        return true;
    }
    internal bool TryGetFrameCell(Vector2 position, out int column, out int row)
    {
        column = row = -1;
        if (text == null || canvas == null || !canvas.enabled || !RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform, position)) return false;
        text.ForceMeshUpdate();
        int index = TMP_TextUtilities.FindIntersectingCharacter(text, position, null, false);
        if (index < 0) return false;
        row = text.textInfo.characterInfo[index].lineNumber;
        column = index - text.textInfo.lineInfo[row].firstCharacterIndex;
        return column >= 0 && column < Columns && row >= 0 && row < Rows;
    }
    internal bool TryGetFramePosition(int column, int row, out Vector2 position)
    {
        position = default;
        if (text == null || column < 0 || column >= Columns || row < 0 || row >= Rows) return false;
        text.ForceMeshUpdate();
        if (row >= text.textInfo.lineCount) return false;
        int index = text.textInfo.lineInfo[row].firstCharacterIndex + column;
        if (index >= text.textInfo.characterCount) return false;
        var character = text.textInfo.characterInfo[index];
        position = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint(
            new Vector3((character.origin + character.xAdvance) * .5f, (character.ascender + character.descender) * .5f)));
        return true;
    }
    public void Show(bool visible) { if (canvas != null) canvas.enabled = visible; }
    public void SetText(string markup)
    {
        if (text == null) return;
        text.text = ownedFont == null ? "<mspace=" + text.fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + ">" + markup + "</mspace>" : markup;
    }
    private void OnDestroy()
    {
        if (ownedFont != null)
        {
            var material = ownedFont.material;
            var atlas = ownedFont.atlasTexture;
            if (Application.isPlaying)
            {
                Destroy(ownedFont);
                if (material != null) Destroy(material);
                if (atlas != null) Destroy(atlas);
            }
            else
            {
                DestroyImmediate(ownedFont);
                if (material != null) DestroyImmediate(material);
                if (atlas != null) DestroyImmediate(atlas);
            }
        }
    }
}
