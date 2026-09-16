using UnityEngine;
using UnityEngine.EventSystems;

public class NavigationHandler : MonoBehaviour
{
    public GameObject defaultSelectable;
    public RectTransform selectionArrow;
    public ArrowAnchor arrowAnchor = ArrowAnchor.Center;
    public Vector3 arrowOffset = new Vector3(0, 40, 0);
    [Min(0)] public float arrowMoveDuration = 0.08f;

    private EventSystem es;
    private GameObject lastSelected;
    private Vector3 arrowStart;
    private float arrowStartedAt;
    private readonly Vector3[] corners = new Vector3[4];

    public void Init() => es = EventSystem.current;
    private void OnEnable() => Init();

    private void LateUpdate()
    {
        if (es == null || es != EventSystem.current) Init();
        if (es == null) return;
        var current = es.currentSelectedGameObject;
        if (!MenuUIInputModule.IsUsable(current))
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
            if (audio != null) audio.PlaySoundEffect(audio.SoundEffects.Hover);
        }

        if (selectionArrow == null) return;
        var target = current.GetComponent<RectTransform>();
        if (target == null) return;
        Vector3 destination = GetAnchorWorldPosition(target) + arrowOffset;
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
        return arrowAnchor switch
        {
            ArrowAnchor.Left => (corners[0] + corners[1]) * 0.5f,
            ArrowAnchor.Right => (corners[2] + corners[3]) * 0.5f,
            ArrowAnchor.Top => (corners[1] + corners[2]) * 0.5f,
            ArrowAnchor.Bottom => (corners[0] + corners[3]) * 0.5f,
            _ => (corners[0] + corners[2]) * 0.5f
        };
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
