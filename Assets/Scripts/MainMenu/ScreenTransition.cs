using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public bool BlocksInput => BlockScreen.activeInHierarchy ||
        Game.Instance?.NewFloorMessage != null && Game.Instance.NewFloorMessage.gameObject.activeInHierarchy;

    private void Awake()
    {
        ShutterScreen.gameObject.SetActive(false);
        BlockScreen.gameObject.SetActive(false);
    }

    public void DoTransition(Action postTransition, bool autoOpen = true, string destinationTitle = null)
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
        sceneRoutine = StartCoroutine(DoTransitionRoutine(postTransition, autoOpen, animationVersion));
    }

    private void SetDestinationTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            if (destinationLabel != null) destinationLabel.gameObject.SetActive(false);
            return;
        }
        destinationLabel.text = title;
        destinationLabel.alpha = 0;
        destinationLabel.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (destinationLabel != null && destinationLabel.gameObject.activeSelf)
            destinationLabel.alpha = ShutterScreen.color.a;
    }

    private void CancelAnimation()
    {
        animationVersion++;
        loadPending = false;
        if (sceneRoutine != null) StopCoroutine(sceneRoutine);
        sceneRoutine = null;
        ShutterScreen.DOKill();
    }

    private void ShowCover(float alpha)
    {
        BlockScreen.SetActive(true);
        ShutterScreen.gameObject.SetActive(true);
        ShutterScreen.color = new Color(0,0,0,alpha);
    }

    internal void HoldClosed(string destinationTitle = null)
    {
        CancelAnimation();
        SetDestinationTitle(destinationTitle);
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
        postTransition?.Invoke();
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
        postTransition?.Invoke();
        if (autoOpen && version == animationVersion)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0,OpenDelayTimeSeconds));
            yield return AnimateTo(0, TransitionTimeSeconds, version);
            if (version == animationVersion) ReleaseFade();
        }
    }

    internal void DoOpen()
    {
        CancelAnimation();
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
        {
            ReleaseFade();
            return;
        }
        // Opening an already visible screen should never insert a new black frame.
        if (!ShutterScreen.gameObject.activeSelf) { ReleaseFade(); return; }
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
    [ContextMenu("Test Transition")]
    public void TestTransition()
    {
        DoTransition(null);
    }
}
