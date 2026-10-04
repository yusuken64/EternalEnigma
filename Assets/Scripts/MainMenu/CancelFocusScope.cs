using UnityEngine;

/// <summary>An opt-in Back destination, bounded by the active dialog.</summary>
public sealed class CancelFocusScope : MonoBehaviour
{
    public GameObject ReturnTarget;

    public static GameObject Resolve(GameObject selected, Transform dialogRoot)
    {
        if (selected == null || dialogRoot == null || !selected.transform.IsChildOf(dialogRoot)) return null;
        for (var current = selected.transform; current != null; current = current.parent)
        {
            var scope = current.GetComponent<CancelFocusScope>();
            if (scope != null && scope.isActiveAndEnabled && scope.ReturnTarget != selected &&
                MenuUIInputModule.IsUsable(scope.ReturnTarget) && scope.ReturnTarget.transform.IsChildOf(dialogRoot))
                return scope.ReturnTarget;
            if (current == dialogRoot) break;
        }
        return null;
    }
}
