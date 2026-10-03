using UnityEngine;

public sealed class SafeAreaPanel : MonoBehaviour
{
    private Rect last;
    private Vector2 size;
    private void OnEnable() => Apply();
    private void Update()
    {
        if (last != Screen.safeArea || size.x != Screen.width || size.y != Screen.height) Apply();
    }
    private void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        last = Screen.safeArea; size = new Vector2(Screen.width, Screen.height);
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(last.xMin / size.x, last.yMin / size.y);
        rect.anchorMax = new Vector2(last.xMax / size.x, last.yMax / size.y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
