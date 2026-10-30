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
    private bool keepOnScreen;

    public static void EnsureDialogPointer(MonoBehaviour owner, Transform root)
    {
        // Settings already has a pointer positioned for its sliders and tabs.
        if (owner is GlobalSettings || owner is JuicyChickenGames.Menu.TargetDialog) return;
        var navigation = root.GetComponent<NavigationHandler>();
        if (navigation == null) navigation = root.gameObject.AddComponent<NavigationHandler>();
        if (navigation.selectionArrow == null)
        {
            var pointer = new GameObject("Menu selection cursor", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            pointer.transform.SetParent(root, false);
            pointer.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            navigation.selectionArrow = (RectTransform)pointer.transform;
        }
        navigation.ConfigureCompactPointer(owner);
        navigation.FocusRoot = root;
        // A separate canvas keeps the arrow above scroll masks and external HUD tabs.
        var canvas = navigation.selectionArrow.GetComponent<Canvas>();
        if (canvas == null) canvas = navigation.selectionArrow.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        navigation.enabled = true;
    }

    public void Init()
    {
        es = EventSystem.current;
        if (selectionArrow != null)
            foreach (var graphic in selectionArrow.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
    }

    public void ConfigureCompactPointer(MonoBehaviour owner)
    {
        FocusRoot = owner.transform;
        FocusOwner = owner;
        arrowAnchor = ArrowAnchor.Left;
        arrowOffset = Vector3.zero;
        edgeSpacing = 8;
        keepOnScreen = true;
        if (selectionArrow == null) return;

        selectionArrow.anchorMin = selectionArrow.anchorMax = new Vector2(.5f, .5f);
        selectionArrow.pivot = new Vector2(.5f, .5f);
        selectionArrow.sizeDelta = new Vector2(28, 28);
        selectionArrow.localScale = Vector3.one;
        // The shared dropdown triangle points down; turn its tip toward the selected row.
        selectionArrow.localRotation = Quaternion.Euler(0, 0, 90);
        var pointer = selectionArrow.GetComponent<UnityEngine.UI.Image>();
        if (pointer != null)
        {
            pointer.sprite = GameUITheme.Current.Arrow;
            pointer.overrideSprite = null;
            pointer.type = UnityEngine.UI.Image.Type.Simple;
            pointer.color = Color.white;
            pointer.preserveAspect = true;
            var outline = pointer.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = pointer.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.98f, .92f, .74f, 1);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }
        lastSelected = null;
        selectionArrow.gameObject.SetActive(false);
        Init();
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
        var module = MenuUIInputModule.Active;
        bool externalPartyTab = FocusOwner is PartyMenu party && party.HudTabs != null && party.HudTabs.Allows(current);
        if (!MenuUIInputModule.IsUsable(current) ||
            (FocusRoot != null && !current.transform.IsChildOf(FocusRoot) && !externalPartyTab) ||
            (FocusOwner != null && module != null && !module.OwnsFocus(FocusOwner)) ||
            (FocusOwner == null && module != null && module.HasDialog))
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
        }

        if (selectionArrow == null) return;
        var pointerCanvas = selectionArrow.GetComponent<Canvas>();
        var ownerCanvas = FocusRoot != null ? FocusRoot.GetComponentInParent<Canvas>() : null;
        if (pointerCanvas != null && ownerCanvas != null)
        {
            var selectedCanvas = current.GetComponentInParent<Canvas>();
            pointerCanvas.sortingOrder = Mathf.Max(ownerCanvas.sortingOrder, selectedCanvas != null ? selectedCanvas.sortingOrder : 0) + 2;
        }
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
        if (keepOnScreen) ClampToCanvas();
    }

    private void ClampToCanvas()
    {
        var canvas = selectionArrow.GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null || canvas.transform is not RectTransform viewport) return;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selectionArrow);
        var view = viewport.rect;
        float dx = bounds.min.x < view.xMin + 4 ? view.xMin + 4 - bounds.min.x :
            bounds.max.x > view.xMax - 4 ? view.xMax - 4 - bounds.max.x : 0;
        float dy = bounds.min.y < view.yMin + 4 ? view.yMin + 4 - bounds.min.y :
            bounds.max.y > view.yMax - 4 ? view.yMax - 4 - bounds.max.y : 0;
        selectionArrow.position += viewport.TransformVector(new Vector3(dx, dy, 0));
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
