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

    private void Start()
    {
        ShutterScreen.gameObject.SetActive(false);
        BlockScreen.gameObject.SetActive(false);
    }

    public void DoTransition(Action postTransition)
    {
        StartCoroutine(DoTransitionRoutine(postTransition));
    }

    private IEnumerator DoTransitionRoutine(Action postTransition)
    {
        BlockScreen.gameObject.SetActive(true);
        ShutterScreen.gameObject.SetActive(true);
        ShutterScreen.color = new Color(0, 0, 0, 0);

        //AudioManager.Instance?.PlaySound(CloseClip);
        var closeTween = ShutterScreen.DOFade(1, TransitionTimeSeconds);
        yield return closeTween.WaitForCompletion();

        postTransition?.Invoke();
        yield return new WaitForSeconds(OpenDelayTimeSeconds);

        //AudioManager.Instance?.PlaySound(OpenClip);
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