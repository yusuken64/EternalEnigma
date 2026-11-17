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
        // An instant routine (e.g. a hunger tick after a step) must not cost a frame:
        // yielding on an already-finished Coroutine still waits for the next frame.
        foreach (var routine in routines)
        {
            bool done = false;
            Coroutine runningCoroutine = context.StartCoroutine(Track(routine, () => done = true));
            if (done) continue;
            runningCoroutines.Add(runningCoroutine);
            yield return runningCoroutine;
            runningCoroutines.Remove(runningCoroutine);
        }
    }

    private static IEnumerator Track(IEnumerator routine, System.Action post)
    {
        var flat = TurnManager.FlattenCompletedRoutines(routine);
        try { while (flat.MoveNext()) yield return flat.Current; }
        finally { post(); }
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
