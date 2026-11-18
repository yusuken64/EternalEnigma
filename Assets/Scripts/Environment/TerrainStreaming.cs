using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Builds generated terrain meshes near a focus point first and the rest over later frames.
/// Layers still plan the whole map; only the mesh and object creation for far chunks waits.
/// While no session is open, every build runs immediately, exactly as before.
/// </summary>
public static class TerrainStreaming
{
    /// <summary>Chunk edge in layer-local units; matches the grouping used by <see cref="EnvironmentBatch"/>.</summary>
    public const float ChunkSize = 64f;
    /// <summary>Chunks around the focus chunk that are always built before returning (1 = a 3x3 block).</summary>
    public const int NearChunks = 1;
    /// <summary>Milliseconds of deferred building allowed per frame.</summary>
    public const float FrameBudget = 4f;

    /// <summary>
    /// Player builds stream; the editor builds everything at once so tools, bakes and tests that
    /// inspect finished terrain see all of it. Set true in the editor to try streaming there.
    /// </summary>
    public static bool Enabled = !Application.isEditor;

    private sealed class Job
    {
        public Vector2 Center;
        public UnityEngine.Object Owner;
        public Action Build;
    }

    private static readonly List<Job> queue = new();
    private static bool open;
    private static Vector2 focus;
    private static Runner runner;
    private static int deferred, built;
    private static double buildMilliseconds, worstMilliseconds;
    private static float startedAt;

    /// <summary>True while a session is deferring far builds.</summary>
    public static bool IsOpen => open;

    /// <summary>Deferred builds still waiting.</summary>
    public static int Pending => queue.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        queue.Clear(); Drained = null; open = false; runner = null; deferred = built = 0; buildMilliseconds = worstMilliseconds = 0;
    }

    /// <summary>Opens a session: until <see cref="End"/>, builds far from <paramref name="focusPoint"/> are deferred.</summary>
    public static void Begin(Vector2 focusPoint)
    {
        if (!Enabled || !Application.isPlaying) return;
        open = true;
        focus = focusPoint;
    }

    /// <summary>Closes the session. Work already deferred keeps streaming in; new work builds immediately.</summary>
    public static void End() => open = false;

    /// <summary>
    /// Runs <paramref name="build"/> now when its chunk is near the focus or no session is open; otherwise
    /// queues it. A queued build is dropped if <paramref name="owner"/> is destroyed first.
    /// </summary>
    public static void Schedule(Vector2 center, UnityEngine.Object owner, Action build)
    {
        if (!open || Distance(center) <= NearChunks) { build(); return; }
        if (queue.Count == 0 && deferred == built) startedAt = Time.realtimeSinceStartup;
        queue.Add(new Job { Center = center, Owner = owner, Build = build });
        deferred++;
        if (runner == null)
        {
            var host = new GameObject("Terrain streaming") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(host);
            runner = host.AddComponent<Runner>();
        }
    }

    /// <summary>Moves the focus and immediately builds anything still waiting around it, so no gap is ever on screen.</summary>
    public static void Focus(Vector2 focusPoint)
    {
        focus = focusPoint;
        if (queue.Count == 0) return;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (Distance(queue[i].Center) > NearChunks) continue;
            var job = queue[i];
            queue.RemoveAt(i);
            Run(job);
        }
        ReportWhenDone();
    }

    /// <summary>Builds everything still waiting.</summary>
    public static void Flush()
    {
        while (queue.Count > 0)
        {
            var job = queue[queue.Count - 1];
            queue.RemoveAt(queue.Count - 1);
            Run(job);
        }
        ReportWhenDone();
    }

    private static int Chunk(float value) => Mathf.FloorToInt(value / ChunkSize);
    private static int Distance(Vector2 center) =>
        Mathf.Max(Mathf.Abs(Chunk(center.x) - Chunk(focus.x)), Mathf.Abs(Chunk(center.y) - Chunk(focus.y)));

    private static void Run(Job job)
    {
        built++;
        if (job.Owner == null) return;
        var watch = Stopwatch.StartNew();
        try { job.Build(); }
        catch (Exception exception) { Debug.LogException(exception); }
        double elapsed = watch.Elapsed.TotalMilliseconds;
        buildMilliseconds += elapsed;
        if (elapsed > worstMilliseconds) worstMilliseconds = elapsed;
    }

    private static void Tick()
    {
        if (queue.Count == 0) return;
        var watch = Stopwatch.StartNew();
        do
        {
            // Nearest first. The queue is at most a few thousand entries, so a scan per pick is cheap.
            int best = 0, bestDistance = int.MaxValue;
            for (int i = 0; i < queue.Count; i++)
            {
                int distance = Distance(queue[i].Center);
                if (distance < bestDistance) { best = i; bestDistance = distance; }
            }
            var job = queue[best];
            queue.RemoveAt(best);
            Run(job);
        }
        while (queue.Count > 0 && watch.Elapsed.TotalMilliseconds < FrameBudget);
        ReportWhenDone();
    }

    private static void ReportWhenDone()
    {
        if (queue.Count > 0 || deferred == 0 || deferred != built) return;
        Debug.Log($"[Timing] Terrain streaming finished: {deferred} deferred builds in {Time.realtimeSinceStartup - startedAt:0.0} s; " +
            $"{buildMilliseconds:0} ms of building, slowest {worstMilliseconds:0.0} ms.");
        deferred = built = 0; buildMilliseconds = worstMilliseconds = 0;
        var drained = Drained; Drained = null;
        drained?.Invoke();
    }

    /// <summary>
    /// Runs <paramref name="action"/> once every deferred build has run: now when nothing is waiting.
    /// </summary>
    public static void WhenDrained(Action action)
    {
        if (queue.Count == 0) { action(); return; }
        Drained += action;
    }

    private static event Action Drained;

    private sealed class Runner : MonoBehaviour
    {
        private void Update() => Tick();
    }
}
