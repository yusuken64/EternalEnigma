using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimultaneousCoroutines
{
	public SimultaneousCoroutines(UnityEngine.MonoBehaviour context)
	{
		this.context = context;
	}

    private List<IEnumerator> runningCoroutines = new List<IEnumerator>();
	private readonly MonoBehaviour context;

	public IEnumerator RunCoroutines(List<IEnumerator> routines)
    {
        foreach (var routine in routines)
        {
            runningCoroutines.Add(routine);
            context.StartCoroutine(HandleCoroutine(routine, () =>
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
        yield return context.StartCoroutine(routine);
        post?.Invoke();
    }

    public void StopAllRunningCoroutines()
    {
        foreach (var coroutine in runningCoroutines)
        {
            if (coroutine != null)
            {
                context.StopCoroutine(coroutine);
            }
        }
        context.StopAllCoroutines();
        runningCoroutines.Clear();
    }
}
