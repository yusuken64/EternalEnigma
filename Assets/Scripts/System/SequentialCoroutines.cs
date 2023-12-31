using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SequentialCoroutines
{
    public SequentialCoroutines(UnityEngine.MonoBehaviour context)
    {
        this.context = context;
    }

    private List<Coroutine> runningCoroutines = new List<Coroutine>();
    private UnityEngine.MonoBehaviour context;

    public IEnumerator RunCoroutines(List<IEnumerator> routines)
    {
        foreach (var routine in routines)
        {
            Coroutine runningCoroutine = context.StartCoroutine(routine);
            runningCoroutines.Add(runningCoroutine);
            yield return runningCoroutine;
            runningCoroutines.Remove(runningCoroutine);
        }
        yield return null;
    }

    public void StopAllRunningCoroutines()
    {
        foreach (var coroutine in runningCoroutines)
        {
            context.StopCoroutine(coroutine);
        }
        runningCoroutines.Clear();
    }
}
