using UnityEngine;
using UnityEngine.EventSystems;

public class NavigationHandler : MonoBehaviour
{
    public GameObject defaultSelectable;
    public RectTransform selectionArrow;
    public ArrowAnchor arrowAnchor = ArrowAnchor.Center;
    public Vector3 arrowOffset = new Vector3(0, 40, 0);
    [Min(0)] public float arrowMoveDuration = 0.08f;
    public Transform FocusRoot;
    public MonoBehaviour FocusOwner;
    [Min(0)] public float edgeSpacing;

    private EventSystem es;
    private GameObject lastSelected;
    private Vector3 arrowStart;
    private float arrowStartedAt;
    private readonly Vector3[] corners = new Vector3[4];

    public void Init()
    {
        es = EventSystem.current;
        if (selectionArrow != null)
            foreach (var graphic in selectionArrow.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
    }
    private void OnEnable() => Init();
    private void OnDisable()
    {
        lastSelected = null;
        if (selectionArrow != null) selectionArrow.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (es == null || es != EventSystem.current) Init();
        if (es == null) return;
        var current = es.currentSelectedGameObject;
        if (!MenuUIInputModule.IsUsable(current) ||
            (FocusRoot != null && !current.transform.IsChildOf(FocusRoot)) ||
            (FocusOwner != null && MenuUIInputModule.Active != null && !MenuUIInputModule.Active.OwnsFocus(FocusOwner)))
        {
            if (selectionArrow != null) selectionArrow.gameObject.SetActive(false);
            return;
        }

        bool changed = lastSelected != current;
        if (changed)
        {
            lastSelected = current;
            arrowStartedAt = Time.unscaledTime;
            if (selectionArrow != null) arrowStart = selectionArrow.position;
            var audio = AudioManager.Instance;
            if (audio != null) audio.PlayUISound(audio.SoundEffects.Hover);
        }

        if (selectionArrow == null) return;
        var target = current.GetComponent<RectTransform>();
        if (target == null) return;
        Vector3 destination = GetAnchorWorldPosition(target) + arrowOffset;
        if (edgeSpacing > 0 && (arrowAnchor == ArrowAnchor.Left || arrowAnchor == ArrowAnchor.Right))
        {
            var direction = arrowAnchor == ArrowAnchor.Left ? -1 : 1;
            destination += target.right * direction * (edgeSpacing * target.lossyScale.x + selectionArrow.rect.width * selectionArrow.lossyScale.x * .5f);
            // Authored arrows need not have a centered pivot. Space their visible bounds, not their pivot.
            destination -= selectionArrow.TransformVector(selectionArrow.rect.center);
        }
        if (!selectionArrow.gameObject.activeSelf)
        {
            selectionArrow.position = destination;
            arrowStart = destination;
            selectionArrow.gameObject.SetActive(true);
        }
        float progress = arrowMoveDuration <= 0 ? 1 : Mathf.Clamp01((Time.unscaledTime - arrowStartedAt) / arrowMoveDuration);
        selectionArrow.position = Vector3.Lerp(arrowStart, destination, Mathf.SmoothStep(0, 1, progress));
    }

    private Vector3 GetAnchorWorldPosition(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        var anchor = arrowAnchor switch
        {
            ArrowAnchor.Left => (corners[0] + corners[1]) * 0.5f,
            ArrowAnchor.Right => (corners[2] + corners[3]) * 0.5f,
            ArrowAnchor.Top => (corners[1] + corners[2]) * 0.5f,
            ArrowAnchor.Bottom => (corners[0] + corners[3]) * 0.5f,
            _ => (corners[0] + corners[2]) * 0.5f
        };
        // At 100%, a slider's handle can extend beyond its selectable rectangle.
        if (edgeSpacing > 0 && (arrowAnchor == ArrowAnchor.Left || arrowAnchor == ArrowAnchor.Right) &&
            rect.TryGetComponent<UnityEngine.UI.Slider>(out var slider) && slider.handleRect != null)
        {
            slider.handleRect.GetWorldCorners(corners);
            var handleAnchor = arrowAnchor == ArrowAnchor.Left ? (corners[0] + corners[1]) * .5f : (corners[2] + corners[3]) * .5f;
            float direction = arrowAnchor == ArrowAnchor.Left ? -1 : 1;
            float extension = Vector3.Dot(handleAnchor - anchor, rect.right) * direction;
            if (extension > 0) anchor += rect.right * direction * extension;
        }
        return anchor;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) MenuUIInputModule.Active?.RestoreFocus(defaultSelectable);
    }

#if UNITY_EDITOR
    [ContextMenu("Preview Arrow Position")]
    public void PreviewArrowPosition()
    {
        var current = EventSystem.current?.currentSelectedGameObject ?? defaultSelectable;
        if (selectionArrow != null && current != null && current.TryGetComponent<RectTransform>(out var target))
            selectionArrow.position = GetAnchorWorldPosition(target) + arrowOffset;
    }
#endif
}

public enum ArrowAnchor { Center, Left, Right, Top, Bottom }
