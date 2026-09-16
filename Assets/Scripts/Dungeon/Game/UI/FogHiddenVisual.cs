using System.Collections.Generic;
using UnityEngine;

// Keep simulation and animators running while hiding live objects under fog.
[DefaultExecutionOrder(1000)]
public class FogHiddenVisual : MonoBehaviour
{
    private readonly List<Renderer> renderers = new();
    private Character character;

    private void Awake() => character = GetComponent<Character>();

    private void LateUpdate()
    {
        var fog = FogOverlay.Instance;
        bool hidden = fog == null || !fog.IsCurrentlyVisible(transform.position,
            character != null ? character.FootPrint : FootPrint.Size1x1);
        // Reuse the list and include newly attached status effects and inactive trap visuals.
        GetComponentsInChildren(true, renderers);
        foreach (var renderer in renderers)
            renderer.forceRenderingOff = hidden;
    }

    private void OnDisable()
    {
        foreach (var renderer in renderers)
            if (renderer != null) renderer.forceRenderingOff = false;
    }
}
