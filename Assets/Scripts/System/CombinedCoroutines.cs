using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombinedCoroutines : MonoBehaviour
{
    private List<IEnumerator> runningCoroutines = new List<IEnumerator>();

    public IEnumerator RunCoroutines(List<IEnumerator> routines)
    {
        foreach (var routine in routines)
        {
            runningCoroutines.Add(routine);
            StartCoroutine(HandleCoroutine(routine, () =>
            {
                runningCoroutines.Remove(routine);
            }));
        }

        while (runningCoroutines.Any())
        {
            yield return null;
        }

        yield return null;
    }

    private IEnumerator HandleCoroutine(IEnumerator routine, Action post)
    {
        yield return StartCoroutine(routine);
        post?.Invoke();
    }

    public void StopAllRunningCoroutines()
    {
        foreach (var coroutine in runningCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        runningCoroutines.Clear();
    }
}
