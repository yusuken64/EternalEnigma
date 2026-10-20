using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(32000)]
public class ScreenTransition : MonoBehaviour
{
    public const int FloorOverlayOrder = 30000;
    public const int SceneOverlayOrder = 31000;
    public GameObject BlockScreen;
    public Image ShutterScreen;

    public float TransitionTimeSeconds;
    public float OpenDelayTimeSeconds;
    [SerializeField] private TMP_Text destinationLabel;
    private Coroutine sceneRoutine;
    private int animationVersion;
    private bool loadPending;
    private bool opening;
    private SceneTravelPresentation travelPresentation;
    private string travelTitle;
    private static Camera controlledCamera;
    private CameraPose? cameraRestore;
    private readonly Dictionary<Canvas, bool> hiddenCanvases = new();
    public bool HasTravelPresentation => travelPresentation != null;
    public bool CameraMotionActive => controlledCamera != null;
    internal static bool ControlsCamera(Camera camera) => camera != null && controlledCamera == camera;
    public bool BlocksInput => BlockScreen.activeInHierarchy ||
        Game.Instance?.NewFloorMessage != null && Game.Instance.NewFloorMessage.gameObject.activeInHierarchy;

    private void Awake()
    {
        ShutterScreen.gameObject.SetActive(false);
        BlockScreen.gameObject.SetActive(false);
    }

    public void DoTransition(Action postTransition, bool autoOpen = true, string destinationTitle = null,
        SceneTravelPresentation travel = null)
    {
        // Repeated Submit/clicks must not replace a destination already being covered.
        if (loadPending) return;
        CancelAnimation();
        loadPending = true;
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
        {
            ShowCover(1);
            SetDestinationTitle(destinationTitle ?? "Loading...");
            sceneRoutine = StartCoroutine(DoInstantLoadRoutine(postTransition, autoOpen, animationVersion));
            return;
        }
        SetDestinationTitle(destinationTitle);
        if (travel != null && !TerminalMode.Effective && travel.SourceCamera != null)
        {
            travelPresentation = travel;
            travelTitle = destinationTitle;
            sceneRoutine = StartCoroutine(DoTravelRoutine(postTransition, autoOpen, animationVersion));
        }
        else sceneRoutine = StartCoroutine(DoTransitionRoutine(postTransition, autoOpen, animationVersion));
    }

    private void SetDestinationTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            if (destinationLabel != null) destinationLabel.gameObject.SetActive(false);
            return;
        }
        if (destinationLabel == null) return;
        destinationLabel.text = title;
        destinationLabel.alpha = ShutterScreen.gameObject.activeSelf ? ShutterScreen.color.a : 0;
        destinationLabel.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (travelPresentation != null) HideGameplayCanvases();
        if (destinationLabel != null && destinationLabel.gameObject.activeSelf)
            destinationLabel.alpha = ShutterScreen.color.a;
    }

    private void CancelAnimation(bool preserveTravel = false)
    {
        animationVersion++;
        loadPending = false;
        if (sceneRoutine != null) StopCoroutine(sceneRoutine);
        sceneRoutine = null;
        opening = false;
        ShutterScreen.DOKill();
        RestorePresentation();
        if (!preserveTravel) { travelPresentation = null; travelTitle = null; }
    }

    private void ShowCover(float alpha)
    {
        BlockScreen.SetActive(true);
        ShutterScreen.gameObject.SetActive(true);
        ShutterScreen.color = new Color(0,0,0,alpha);
    }

    internal void HoldClosed(string destinationTitle = null)
    {
        // Town and dungeon startup take ownership of readiness while retaining
        // the travel description. Their new camera is resolved only at DoOpen.
        CancelAnimation(preserveTravel: true);
        SetDestinationTitle(destinationTitle ?? travelTitle);
        ShowCover(1);
        if (destinationLabel != null) destinationLabel.alpha = 1;
    }

    // Use frame yields rather than WaitForEndOfFrame so loading also advances
    // in editor/headless runs. Two yields allow a complete covered frame even
    // when the caller resumes late in Unity's coroutine update.
    internal static IEnumerator PresentLoadingScreen()
    {
        yield return null;
        yield return null;
    }

    private IEnumerator DoInstantLoadRoutine(Action postTransition, bool autoOpen, int version)
    {
        yield return PresentLoadingScreen();
        if (version != animationVersion) yield break;
        loadPending = false;
        InvokeLoad(postTransition);
        if (autoOpen && version == animationVersion)
        {
            yield return PresentLoadingScreen();
            if (version == animationVersion) ReleaseFade();
        }
    }

    private IEnumerator DoTransitionRoutine(Action postTransition, bool autoOpen, int version)
    {
        ShowCover(ShutterScreen.gameObject.activeSelf ? ShutterScreen.color.a : 0);
        yield return AnimateTo(1, TransitionTimeSeconds, version);
        yield return PresentLoadingScreen();
        if (version != animationVersion) yield break;
        loadPending = false;
        InvokeLoad(postTransition);
        if (autoOpen && version == animationVersion)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0,OpenDelayTimeSeconds));
            yield return AnimateTo(0, TransitionTimeSeconds, version);
            if (version == animationVersion) ReleaseFade();
        }
    }

    internal void DoOpen()
    {
        if (opening && travelPresentation != null) return;
        CancelAnimation(preserveTravel: true);
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
        {
            ReleaseFade();
            return;
        }
        // Opening an already visible screen should never insert a new black frame.
        if (!ShutterScreen.gameObject.activeSelf) { ReleaseFade(); return; }
        if (travelPresentation != null && !TerminalMode.Effective)
        {
            opening = true;
            HideGameplayCanvases();
            sceneRoutine = StartCoroutine(DoTravelOpenRoutine(animationVersion));
            return;
        }
        // Dungeon entry already has a floor title beneath this cover. Hand the
        // reveal to that label rather than fading two centered titles together.
        if (Game.Instance?.NewFloorMessage != null && Game.Instance.NewFloorMessage.gameObject.activeInHierarchy)
            SetDestinationTitle(null);
        sceneRoutine = StartCoroutine(DoOpenRoutine(animationVersion));
    }
    private IEnumerator DoOpenRoutine(int version)
    {
        ShowCover(ShutterScreen.color.a);
        // Let the destination finish its first rendered frame while opaque.
        // Shader/UI initialization during that frame must not skip the reveal.
        if (ShutterScreen.color.a >= .999f) yield return PresentLoadingScreen();
        if (version != animationVersion) yield break;
        yield return AnimateTo(0, TransitionTimeSeconds, version);
        if (version == animationVersion) ReleaseFade();
    }

    public IEnumerator FadeTo(float alpha, float seconds)
    {
        CancelAnimation();
        int version = animationVersion;
        SetDestinationTitle(null);
        ShowCover(ShutterScreen.gameObject.activeSelf ? ShutterScreen.color.a : 0);
        yield return AnimateTo(Mathf.Clamp01(alpha), seconds, version);
    }

    private IEnumerator AnimateTo(float alpha, float seconds, int version)
    {
        float start = ShutterScreen.color.a;
        // The frame that starts a reveal may have spent seconds generating a
        // scene. Its inherited delta must not consume the new fade's duration.
        double started = Time.realtimeSinceStartupAsDouble;
        for (double elapsed=0; elapsed<seconds; elapsed=Time.realtimeSinceStartupAsDouble-started)
        {
            if (version != animationVersion) yield break;
            ShutterScreen.color = new Color(0,0,0,Mathf.Lerp(start,alpha,(float)(elapsed/seconds)));
            yield return null;
        }
        if (version == animationVersion) ShutterScreen.color = new Color(0,0,0,alpha);
    }
    public void ReleaseFade()
    {
        CancelAnimation();
        SetDestinationTitle(null);
        ShutterScreen.color = Color.clear;
        ShutterScreen.gameObject.SetActive(false); BlockScreen.SetActive(false);
    }

    private void InvokeLoad(Action load)
    {
        try { load?.Invoke(); }
        catch { ReleaseFade(); throw; }
    }

    private IEnumerator DoTravelRoutine(Action load, bool autoOpen, int version)
    {
        var request = travelPresentation;
        ShowCover(ShutterScreen.gameObject.activeSelf ? ShutterScreen.color.a : 0);
        HideGameplayCanvases();
        CaptureCamera(request.SourceCamera);
        if (request.PrepareDeparture != null) yield return request.PrepareDeparture();
        if (version != animationVersion) yield break;
        if (controlledCamera != null)
        {
            var normal = cameraRestore.Value;
            var zoom = normal.Zoomed(request.SourceAnchor, request.DepartureScale);
            yield return AnimateTravelCamera(normal, zoom, true, version);
        }
        else ShowCover(1);
        yield return PresentLoadingScreen();
        if (version != animationVersion) yield break;
        // Keep the source at its original pose if a caller performs a same-scene
        // transition. Scene loading will discard this camera in ordinary travel.
        RestorePresentation();
        loadPending = false;
        InvokeLoad(load);
        if (autoOpen && version == animationVersion) DoOpen();
    }

    private IEnumerator DoTravelOpenRoutine(int version)
    {
        ShowCover(ShutterScreen.color.a);
        if (ShutterScreen.color.a >= .999f) yield return PresentLoadingScreen();
        if (version != animationVersion) yield break;
        SceneTravelArrival arrival;
        try
        {
            arrival = travelPresentation.ResolveArrival?.Invoke() ?? default;
        }
        catch
        {
            ReleaseFade();
            throw;
        }
        if (arrival.Camera == null)
        {
            yield return AnimateTo(0, TransitionTimeSeconds, version);
        }
        else
        {
            CaptureCamera(arrival.Camera);
            var normal = cameraRestore.Value;
            var zoom = normal.Zoomed(arrival.Anchor, travelPresentation.ArrivalScale);
            yield return AnimateTravelCamera(zoom, normal, false, version);
        }
        if (version == animationVersion) ReleaseFade();
    }

    private IEnumerator AnimateTravelCamera(CameraPose from, CameraPose to, bool departing, int version)
    {
        float seconds = departing ? SceneTravelPresentation.DepartureSeconds : SceneTravelPresentation.ArrivalSeconds;
        double started = Time.realtimeSinceStartupAsDouble;
        for (double elapsed = 0; elapsed < seconds; elapsed = Time.realtimeSinceStartupAsDouble - started)
        {
            if (version != animationVersion) yield break;
            float t = (float)(elapsed / seconds);
            float eased = departing ? SceneTravelPresentation.DepartureEase(t) : SceneTravelPresentation.ArrivalEase(t);
            CameraPose.Lerp(from, to, eased).Apply(controlledCamera);
            // The load is hidden by the last third of departure. Reveal early
            // on arrival so the fast part of OutExpo remains visible.
            float alpha = departing ? Mathf.InverseLerp(2f / 3, 1, t) : 1 - Mathf.Clamp01((float)elapsed / .12f);
            ShutterScreen.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        if (version == animationVersion)
        {
            to.Apply(controlledCamera);
            ShutterScreen.color = new Color(0, 0, 0, departing ? 1 : 0);
        }
    }

    private void CaptureCamera(Camera camera)
    {
        controlledCamera = camera;
        cameraRestore = camera != null ? new CameraPose(camera) : null;
    }

    private void HideGameplayCanvases()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.WorldSpace || canvas.sortingOrder >= FloorOverlayOrder ||
                canvas.rootCanvas.sortingOrder >= FloorOverlayOrder ||
                canvas.transform == transform || transform.IsChildOf(canvas.transform)) continue;
            if (!hiddenCanvases.ContainsKey(canvas)) hiddenCanvases.Add(canvas, canvas.enabled);
            canvas.enabled = false;
        }
    }

    private void RestorePresentation()
    {
        if (cameraRestore.HasValue) cameraRestore.Value.Apply(controlledCamera);
        cameraRestore = null;
        controlledCamera = null;
        foreach (var canvas in hiddenCanvases) if (canvas.Key != null) canvas.Key.enabled = canvas.Value;
        hiddenCanvases.Clear();
    }

    private void OnDisable()
    {
        CancelAnimation();
        if (ShutterScreen != null) { ShutterScreen.color = Color.clear; ShutterScreen.gameObject.SetActive(false); }
        if (BlockScreen != null) BlockScreen.SetActive(false);
    }

    private readonly struct CameraPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Size;
        public CameraPose(Camera camera) : this(camera.transform.position, camera.transform.rotation, camera.orthographicSize) { }
        private CameraPose(Vector3 position, Quaternion rotation, float size) { Position = position; Rotation = rotation; Size = size; }
        public CameraPose Zoomed(Vector3 anchor, float scale)
        {
            var forward = Rotation * Vector3.forward;
            float depth = Mathf.Max(.1f, Vector3.Dot(anchor - Position, forward));
            return new CameraPose(anchor - forward * depth * scale, Rotation, Mathf.Max(.01f, Size * scale));
        }
        public static CameraPose Lerp(CameraPose from, CameraPose to, float t) =>
            new CameraPose(Vector3.LerpUnclamped(from.Position, to.Position, t), from.Rotation, Mathf.LerpUnclamped(from.Size, to.Size, t));
        public void Apply(Camera camera)
        {
            if (camera == null) return;
            camera.transform.SetPositionAndRotation(Position, Rotation);
            camera.orthographicSize = Size;
        }
    }
    [ContextMenu("Test Transition")]
    public void TestTransition()
    {
        DoTransition(null);
    }
}
