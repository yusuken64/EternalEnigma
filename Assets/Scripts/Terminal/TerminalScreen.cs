using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TerminalScreen : MonoBehaviour
{
    private Canvas canvas;
    private TextMeshProUGUI text;
    private TMP_FontAsset ownedFont;
    private int lastWidth, lastHeight;
    public bool IsReady => text != null && text.font != null;
    public TMP_FontAsset FontAsset => text != null ? text.font : null;
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

    public bool Resize()
    {
        if (text == null) return false;
        int width = Screen.width, height = Screen.height;
        if (width == lastWidth && height == lastHeight) return false;
        lastWidth = width; lastHeight = height;
        // Text area uses overlay canvas pixels and a fixed advance, including the fallback SDF.
        float cellWidth = 18f;
        float lineHeight = 21f;
        if (ownedFont != null && ownedFont.characterLookupTable.TryGetValue('M', out var letter))
        {
            float scale = text.fontSize / Mathf.Max(1, ownedFont.faceInfo.pointSize);
            cellWidth = Mathf.Max(1, letter.glyph.metrics.horizontalAdvance * scale);
            lineHeight = Mathf.Max(1, ownedFont.faceInfo.lineHeight * scale);
        }
        Columns = Mathf.Max(8, Mathf.FloorToInt((width - 24) / cellWidth));
        Rows = Mathf.Max(5, Mathf.FloorToInt((height - 24) / lineHeight));
        return true;
    }
    public void Show(bool visible) { if (canvas != null) canvas.enabled = visible; }
    public void SetText(string markup)
    {
        if (text == null) return;
        text.text = ownedFont == null ? "<mspace=18>" + markup + "</mspace>" : markup;
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
