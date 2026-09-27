using UnityEngine;

/// <summary>Preserve the composition's width on narrower displays without stretching it.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class MenuCameraFraming : MonoBehaviour
{
    public float ReferenceAspect = 598f / 336f;
    public float VerticalSize = 3.5f;
    public float VerticalFieldOfView = 45;
    private void OnEnable() => Apply();
    private void LateUpdate() => Apply();
    public void Apply()
    {
        var camera = GetComponent<Camera>();
        float fit = Mathf.Max(1, ReferenceAspect / Mathf.Max(.01f, camera.aspect));
        if (camera.orthographic) camera.orthographicSize = VerticalSize * fit;
        else camera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(VerticalFieldOfView * Mathf.Deg2Rad * .5f) * fit) * Mathf.Rad2Deg;
    }
}
