using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScreenTransition : MonoBehaviour
{
    public GameObject BlockScreen;
    public Image ShutterScreen;

    public float TransitionTimeSeconds;
    public float OpenDelayTimeSeconds;

    private void Awake()
    {
        ShutterScreen.gameObject.SetActive(false);
        BlockScreen.gameObject.SetActive(false);
    }

    public void DoTransition(Action postTransition, bool autoOpen = true)
    {
        CancelAnimation();
        StartCoroutine(DoTransitionRoutine(postTransition, autoOpen));
    }

    private void CancelAnimation()
    {
        StopAllCoroutines();
        ShutterScreen.DOKill();
    }

    internal void HoldClosed()
    {
        CancelAnimation();
        BlockScreen.SetActive(true);
        ShutterScreen.gameObject.SetActive(true);
        ShutterScreen.color = Color.black;
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

    [ContextMenu("Test Transition")]
    public void TestTransition()
    {
        DoTransition(null);
    }
}
