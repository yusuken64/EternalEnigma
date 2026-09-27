using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders the actual protagonist artwork without spawning a gameplay ally.</summary>
public sealed class ProtagonistPreview : MonoBehaviour
{
    private GameObject stage;
    private RenderTexture texture;
    private Camera view;

    public void Show(TownAlly prefab, RawImage destination)
    {
        if (prefab == null || prefab.AnimatedModel == null) return;
        stage = new GameObject("Protagonist portrait stage");
        stage.transform.position = new Vector3(10000, 10000, 10000);
        var model = Instantiate(prefab.AnimatedModel, stage.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0, -25, 0);
        foreach (var component in model.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 31;
        foreach (var animator in model.GetComponentsInChildren<Animator>()) { animator.applyRootMotion = false; animator.updateMode = AnimatorUpdateMode.UnscaledTime; }
        var renderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (renderers.Length == 0) return;
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        texture = new RenderTexture(600, 800, 24) { name = "Protagonist portrait" };
        texture.Create(); destination.texture = texture;
        var cameraObject = new GameObject("Portrait camera", typeof(Camera)); cameraObject.transform.SetParent(stage.transform);
        view = cameraObject.GetComponent<Camera>();
        view.cullingMask = 1 << 31; view.targetTexture = texture;
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.065f, .095f, .11f);
        view.orthographic = true; view.orthographicSize = Mathf.Max(bounds.extents.y * 1.2f, bounds.extents.x * 1.6f);
        view.nearClipPlane = .01f; view.farClipPlane = 100;
        view.transform.position = bounds.center + Vector3.forward * 12;
        view.transform.LookAt(bounds.center);
        var lightObject = new GameObject("Portrait light", typeof(Light)); lightObject.transform.SetParent(stage.transform, false);
        var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f; light.cullingMask = 1 << 31;
        light.transform.rotation = Quaternion.Euler(35, 150, 0);
    }

    private void OnDisable() { if (stage != null) stage.SetActive(false); }
    private void OnDestroy()
    {
        if (view != null) view.targetTexture = null;
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (stage != null) Destroy(stage);
    }
}
