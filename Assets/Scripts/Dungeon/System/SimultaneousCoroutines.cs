using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimultaneousCoroutines
{
	public SimultaneousCoroutines(UnityEngine.MonoBehaviour context)
	{
		this.context = context;
	}

    private readonly List<Coroutine> runningCoroutines = new();
	private readonly MonoBehaviour context;

	// Waits on each Coroutine handle instead of polling a list once per frame, so the batch
	// resumes in the frame its last routine finishes. Routines that complete synchronously
	// (no animation) are never yielded on and cost no frame at all.
	public IEnumerator RunCoroutines(List<IEnumerator> routines)
    {
        var done = new bool[routines.Count];
        var handles = new Coroutine[routines.Count];
        for (int i = 0; i < routines.Count; i++)
        {
            int index = i;
            handles[i] = context.StartCoroutine(Track(routines[i], () => done[index] = true));
            if (!done[i]) runningCoroutines.Add(handles[i]);
        }
        for (int i = 0; i < handles.Length; i++)
        {
            if (!done[i]) yield return handles[i];
            runningCoroutines.Remove(handles[i]);
        }
    }

    private static IEnumerator Track(IEnumerator routine, System.Action post)
    {
        // Step it (and nested routines) directly: work with no real wait finishes inside
        // StartCoroutine, in this frame; only tweens/timers/explicit nulls are handed to Unity.
        var flat = TurnManager.FlattenCompletedRoutines(routine);
        try { while (flat.MoveNext()) yield return flat.Current; }
        finally { post(); }
    }

    public void StopAllRunningCoroutines()
    {
        foreach (var coroutine in runningCoroutines)
            if (coroutine != null) context.StopCoroutine(coroutine);
        context.StopAllCoroutines();
        runningCoroutines.Clear();
    }
}
