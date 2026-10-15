using JuicyChickenGames.Menu;
using UnityEngine;
using UnityEngine.UI;

// Keeps compact town confirmations distinct from the service page behind them.
public sealed class TownModalBackdrop : MonoBehaviour
{
    private RectTransform viewport;
    private RectTransform rect;
    private readonly Vector3[] corners = new Vector3[4];

    public static void Ensure(Dialog dialog, bool replaceFullPage = false)
    {
        if (replaceFullPage)
            foreach (var image in dialog.GetComponentsInChildren<Image>(true))
            {
                bool fillsPage = true;
                for (Transform node = image.transform; node != dialog.transform; node = node.parent)
                {
                    if (!(node is RectTransform bounds) || bounds.anchorMin != Vector2.zero ||
                        bounds.anchorMax != Vector2.one || bounds.sizeDelta.sqrMagnitude > .01f)
                    { fillsPage = false; break; }
                }
                if (fillsPage) image.enabled = false;
            }
        var backdrop = dialog.transform.Find("Confirmation backdrop")?.GetComponent<TownModalBackdrop>();
        if (backdrop == null)
        {
            var go = new GameObject("Confirmation backdrop", typeof(RectTransform), typeof(Image), typeof(TownModalBackdrop));
            go.transform.SetParent(dialog.transform, false);
            go.transform.SetAsFirstSibling();
            backdrop = go.GetComponent<TownModalBackdrop>();
        }
        backdrop.rect = (RectTransform)backdrop.transform;
        backdrop.viewport = dialog.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        var graphic = backdrop.GetComponent<Image>();
        graphic.enabled = true; graphic.sprite = null;
        graphic.color = new Color(.045f, .025f, .015f, .68f);
        graphic.raycastTarget = true;
        backdrop.LateUpdate();
    }

    private void LateUpdate()
    {
        if (viewport == null || rect == null) return;
        viewport.GetWorldCorners(corners);
        var lower = transform.parent.InverseTransformPoint(corners[0]);
        var upper = transform.parent.InverseTransformPoint(corners[2]);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localPosition = (lower + upper) * .5f;
        rect.sizeDelta = new Vector2(upper.x - lower.x, upper.y - lower.y);
    }
}
