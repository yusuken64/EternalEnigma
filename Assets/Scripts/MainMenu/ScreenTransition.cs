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

    private void Awake()
    {
        ShutterScreen.gameObject.SetActive(false);
        BlockScreen.gameObject.SetActive(false);
    }

    public void DoTransition(Action postTransition, bool autoOpen = true, string destinationTitle = null)
    {
        CancelAnimation();
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
        {
            if (autoOpen)
            {
                ShutterScreen.gameObject.SetActive(false);
                BlockScreen.gameObject.SetActive(false);
                postTransition?.Invoke();
            }
            else
            {
                // Skipping the fade must not skip the loading screen or load
                // synchronously before the cover has had a chance to render.
                HoldClosed(destinationTitle ?? "Loading...");
                StartCoroutine(DoInstantLoadRoutine(postTransition));
            }
            return;
        }
        SetDestinationTitle(destinationTitle);
        StartCoroutine(DoTransitionRoutine(postTransition, autoOpen));
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
        StopAllCoroutines();
        ShutterScreen.DOKill();
    }

    internal void HoldClosed(string destinationTitle = null)
    {
        CancelAnimation();
        SetDestinationTitle(destinationTitle);
        BlockScreen.SetActive(true);
        ShutterScreen.gameObject.SetActive(true);
        ShutterScreen.color = Color.black;
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

    private IEnumerator DoInstantLoadRoutine(Action postTransition)
    {
        yield return PresentLoadingScreen();
        postTransition?.Invoke();
    }

    private IEnumerator DoTransitionRoutine(Action postTransition, bool autoOpen = true)
    {
        BlockScreen.gameObject.SetActive(true);
        ShutterScreen.gameObject.SetActive(true);
        ShutterScreen.color = new Color(0, 0, 0, 0);

        //AudioManager.Instance?.PlaySound(CloseClip);
        var closeTween = ShutterScreen.DOFade(1, TransitionTimeSeconds);
        yield return closeTween.WaitForCompletion();

        postTransition?.Invoke();

        if (autoOpen)
        {
            yield return new WaitForSeconds(OpenDelayTimeSeconds);

            //AudioManager.Instance?.PlaySound(OpenClip);
            var openTween = ShutterScreen.DOFade(0, TransitionTimeSeconds);
            yield return openTween.WaitForCompletion();

            ShutterScreen.gameObject.SetActive(false);
            BlockScreen.gameObject.SetActive(false);
        }
    }

    internal void DoOpen()
    {
        CancelAnimation();
        if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations)
        {
            ShutterScreen.gameObject.SetActive(false);
            BlockScreen.gameObject.SetActive(false);
            return;
        }
        StartCoroutine(DoOpenRoutine());
    }
    private IEnumerator DoOpenRoutine()
    {
        BlockScreen.gameObject.SetActive(true);
        ShutterScreen.gameObject.SetActive(true);
        ShutterScreen.color = new Color(0, 0, 0, 1);

        var openTween = ShutterScreen.DOFade(0, TransitionTimeSeconds);
        yield return openTween.WaitForCompletion();

        ShutterScreen.gameObject.SetActive(false);
        BlockScreen.gameObject.SetActive(false);
    }

    public IEnumerator FadeTo(float alpha, float seconds)
    {
        SetDestinationTitle(null);
        BlockScreen.SetActive(true); ShutterScreen.gameObject.SetActive(true);
        float start = ShutterScreen.color.a;
        for (float elapsed=0; elapsed<seconds; elapsed+=Time.unscaledDeltaTime)
        { ShutterScreen.color = new Color(0,0,0,Mathf.Lerp(start,alpha,elapsed/seconds)); yield return null; }
        ShutterScreen.color = new Color(0,0,0,alpha);
    }
    public void ReleaseFade()
    {
        ShutterScreen.color = Color.clear;
        ShutterScreen.gameObject.SetActive(false); BlockScreen.SetActive(false);
    }
    [ContextMenu("Test Transition")]
    public void TestTransition()
    {
        DoTransition(null);
    }
}
