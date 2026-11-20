using UnityEngine;

/// <summary>Stacks the shared cards at their readable content height in reference pixels.</summary>
[ExecuteAlways, DisallowMultipleComponent]
internal sealed class CompactPartyHudLayout : MonoBehaviour
{
    private DungeonPartyCard[] cards;
    private int previousKey;
    private bool dirty = true;

    private void OnEnable()
    {
        cards = GetComponentsInChildren<DungeonPartyCard>(true);
        dirty = true;
        Canvas.preWillRenderCanvases += Reflow;
    }
    private void OnDisable() => Canvas.preWillRenderCanvases -= Reflow;
    private void OnTransformChildrenChanged() { cards = null; dirty = true; }
    private void LateUpdate() => Reflow();

    private void Reflow()
    {
        if (!isActiveAndEnabled) return;
        cards ??= GetComponentsInChildren<DungeonPartyCard>(true);
        var rect = (RectTransform)transform;
        int key = rect.rect.size.GetHashCode();
        foreach (var card in cards)
            key = unchecked(key * 31 + (card.isActiveAndEnabled ? card.LayoutKey : 0));
        if (!dirty && key == previousKey) return;
        dirty = false;
        previousKey = key;

        // Long names, charging, ammunition and badges grow rows. If a full party
        // needs more than the reserved height, use more of the existing column
        // before compromising any text or font size.
        float width = Mathf.Min(280, rect.rect.width);
        float maximumWidth = Mathf.Max(width, rect.rect.width);
        while (true)
        {
            float top = 0;
            foreach (var card in cards)
            {
                if (!card.isActiveAndEnabled) continue;
                float height = card.LayoutCompact(width);
                var row = (RectTransform)card.transform;
                row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
                row.anchoredPosition = new Vector2(0, -top);
                row.sizeDelta = new Vector2(width, height);
                top += height + 8;
            }
            if (top - 8 <= rect.rect.height || width >= maximumWidth) break;
            width = Mathf.Min(width + 20, maximumWidth);
        }
    }
}
