using System.Linq;
using UnityEngine;

/// <summary>Isolated model stage shared by the capture scene and editor exporter.</summary>
public sealed class HeroPortraitStudio : MonoBehaviour
{
    public TownAlly[] Heroes;
    public Camera PortraitCamera;
    public RenderTexture Output;
    public int Index;
    [SerializeField, HideInInspector] private GameObject model;

    private void Start() => Show(Index);
    public void PrepareCapture()
    {
        PortraitCamera.clearFlags = CameraClearFlags.SolidColor;
        PortraitCamera.backgroundColor = Color.clear;
        PortraitCamera.targetTexture = Output;
    }

    public void Show(int index)
    {
        if (Heroes == null || Heroes.Length == 0) return;
        Index = (index % Heroes.Length + Heroes.Length) % Heroes.Length;
        if (model != null) { model.SetActive(false); if (Application.isPlaying) Destroy(model); else DestroyImmediate(model); }
        model = Instantiate(Heroes[Index].AnimatedModel, transform);
        model.name = "Portrait model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0, -15, 0);
        foreach (var component in model.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 31;
        foreach (var animator in model.GetComponentsInChildren<Animator>())
        {
            animator.applyRootMotion = false;
            animator.Rebind(); animator.Update(0);
            animator.enabled = false;
        }
        var idle = Heroes[Index].GetComponent<HeroAnimator>()?.StanceAnimations
            .FirstOrDefault(s => s.Stance == Stance.NoWeapon)?.NamedAnimations
            .FirstOrDefault(a => a.AnimationAction == AnimatedAction.Idle)?.Animations.FirstOrDefault();
        var animated = model.GetComponentInChildren<Animator>();
        if (idle != null && animated != null) idle.SampleAnimation(animated.gameObject, 0);
        var renderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (renderers.Length == 0) return;
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        // Upper-body crop, with headwear included in the measured bounds.
        var focus = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * .27f, bounds.center.z);
        float halfWidth = 0;
        void Measure(Vector3[] vertices, Transform part)
        {
            foreach (var vertex in vertices)
            {
                var point = part.TransformPoint(vertex);
                if (point.y >= focus.y - bounds.size.y * .18f)
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(point.x - focus.x));
            }
        }
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled))
        {
            var mesh = new Mesh(); renderer.BakeMesh(mesh);
            Measure(mesh.vertices, renderer.transform);
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled))
            if (renderer.bounds.max.y >= focus.y - bounds.size.y * .18f)
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(renderer.bounds.min.x - focus.x), Mathf.Abs(renderer.bounds.max.x - focus.x));
        PortraitCamera.aspect = (float)Output.width / Output.height;
        PortraitCamera.orthographicSize = Mathf.Max(bounds.size.y * .32f, halfWidth * 1.08f / ((float)Output.width / Output.height));
        PortraitCamera.transform.position = focus + Vector3.forward * 12;
        PortraitCamera.transform.LookAt(focus);
        PrepareCapture();
        PortraitCamera.Render();
    }

    private void OnGUI()
    {
        if (Heroes == null || Heroes.Length == 0) return;
        GUI.Box(new Rect(15, 15, 340, 470), "Hero Portrait Studio");
        GUI.DrawTexture(new Rect(55, 50, 256, 320), Output, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(35, 380, 310, 25), $"{Index + 1}/{Heroes.Length}  {Heroes[Index].Name}");
        if (GUI.Button(new Rect(35, 415, 140, 40), "Previous")) Show(Index - 1);
        if (GUI.Button(new Rect(190, 415, 140, 40), "Next")) Show(Index + 1);
    }
}
