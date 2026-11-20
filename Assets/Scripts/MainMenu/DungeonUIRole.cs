using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum DungeonVisualRole { Wood, Heading, Paper, WoodButton, Primary, Secondary, Close, IconFrame, Selection, Track, HP, Mana, Hunger, InputSurface }

// Serialized visual intent survives generic UI baking and background migration.
[RequireComponent(typeof(Image))]
public sealed class DungeonUIRole : MonoBehaviour
{
    public DungeonVisualRole Role;
    [SerializeField] private TMP_Text caption;
    public TMP_Text Caption { get => caption; set { caption = value; capturedMargin = false; FitCaption(); } }
    private Vector4 authoredMargin;
    private bool capturedMargin;
    private void OnEnable()
    {
        TerminalMode.Changed += FitCaption;
        Canvas.willRenderCanvases += FitCaption;
        FitCaption();
    }
    private void OnDisable()
    {
        TerminalMode.Changed -= FitCaption;
        Canvas.willRenderCanvases -= FitCaption;
    }
    private void OnRectTransformDimensionsChange() => FitCaption();

    // Decorative sprites include bevels, folds and lower shadows. Map their
    // readable faces through the same nine-slice scaling as the painted image.
    internal static bool TryCaptionRect(Image image, out Rect face)
    {
        face = image.rectTransform.rect;
        var sprite = image.sprite;
        if (sprite == null) return false;
        Vector4 region;
        if (sprite == GameUITheme.Current.DungeonWoodButton) region = new Vector4(.20f, .30f, .82f, .80f);
        else if (sprite == GameUITheme.Current.DungeonHeading) region = new Vector4(.13f, .33f, .87f, .88f);
        else return false;
        if (image.type != Image.Type.Sliced && image.type != Image.Type.Simple) return false;
        float pixels = sprite.pixelsPerUnit / (image.canvas != null ? image.canvas.referencePixelsPerUnit : 100) * image.pixelsPerUnitMultiplier;
        float Map(float fraction, float source, float min, float size, float near, float far)
        {
            float a = near / pixels, b = far / pixels;
            float scale = Mathf.Min(1, size / Mathf.Max(.001f, a + b)); a *= scale; b *= scale;
            float position = fraction * source;
            if (position <= near) return min + position / Mathf.Max(.001f, near) * a;
            if (position >= source - far) return min + size - (source - position) / Mathf.Max(.001f, far) * b;
            return min + a + (position - near) / (source - near - far) * (size - a - b);
        }
        var rect = face; var border = image.type == Image.Type.Sliced ? sprite.border : Vector4.zero;
        face = Rect.MinMaxRect(Map(region.x, sprite.rect.width, rect.xMin, rect.width, border.x, border.z),
            Map(region.y, sprite.rect.height, rect.yMin, rect.height, border.y, border.w),
            Map(region.z, sprite.rect.width, rect.xMin, rect.width, border.x, border.z),
            Map(region.w, sprite.rect.height, rect.yMin, rect.height, border.y, border.w));
        return true;
    }

    private void FitCaption()
    {
        if (!isActiveAndEnabled || (Role != DungeonVisualRole.WoodButton && Role != DungeonVisualRole.Heading) || TerminalMode.Effective) return;
        var image = GetComponent<Image>();
        if (!TryCaptionRect(image, out var face)) return;
        if (caption == null)
        {
            var button = GetComponentInParent<Button>(true);
            if (Role != DungeonVisualRole.WoodButton || button == null || button.targetGraphic != image) return;
            var labels = button.GetComponentsInChildren<TMP_Text>(true);
            if (labels.Length != 1) return;
            caption = labels[0];
        }
        if (!capturedMargin) { authoredMargin = caption.margin; capturedMargin = true; }
        var rect = caption.rectTransform;
        var minimum = rect.InverseTransformPoint(image.rectTransform.TransformPoint(face.min));
        var maximum = rect.InverseTransformPoint(image.rectTransform.TransformPoint(face.max));
        var margin = new Vector4(Mathf.Max(authoredMargin.x, minimum.x - rect.rect.xMin),
            Mathf.Max(authoredMargin.y, rect.rect.yMax - maximum.y),
            Mathf.Max(authoredMargin.z, rect.rect.xMax - maximum.x),
            Mathf.Max(authoredMargin.w, minimum.y - rect.rect.yMin));
        if (caption.margin != margin) caption.margin = margin;
        if (caption.verticalAlignment == VerticalAlignmentOptions.Middle) caption.verticalAlignment = VerticalAlignmentOptions.Geometry;
        if (Role == DungeonVisualRole.WoodButton) caption.textWrappingMode = TextWrappingModes.NoWrap;
    }
    public bool IsValid()
    {
        var image=GetComponent<Image>();
        if(Role==DungeonVisualRole.InputSurface)return image.sprite==null && image.color.a==0;
        bool square=Role==DungeonVisualRole.Close || Role==DungeonVisualRole.IconFrame || Role==DungeonVisualRole.Selection;
        return image.sprite==GameUITheme.Current.DungeonSprite(Role) && image.pixelsPerUnitMultiplier==1 &&
            image.type==(square?Image.Type.Simple:Image.Type.Sliced) && (!square || image.preserveAspect);
    }
    public void Apply()
    {
        var image = GetComponent<Image>();
        if(Role==DungeonVisualRole.InputSurface){image.sprite=null;image.overrideSprite=null;image.color=Color.clear;image.type=Image.Type.Simple;return;}
        GameUITheme.Current.Surface(image, GameUITheme.Current.DungeonSprite(Role), 1);
        image.preserveAspect = Role == DungeonVisualRole.Close || Role == DungeonVisualRole.IconFrame || Role == DungeonVisualRole.Selection;
        if (image.preserveAspect) image.type = Image.Type.Simple;
        var button = GetComponentInParent<Button>(true);
        if (button != null && button.targetGraphic == image)
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                label.color = Role == DungeonVisualRole.WoodButton ? GameUITheme.LightInk : GameUITheme.Ink;
        FitCaption();
    }
}
