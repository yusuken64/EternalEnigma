using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Releases memory that finished, long-lived generated geometry no longer needs.
/// The web player keeps the CPU copy of every readable mesh inside a wasm heap that can never
/// shrink, so cached town and overworld terrain would otherwise hold their vertex data twice.
/// </summary>
public static class RuntimeMeshMemory
{
#if UNITY_WEBGL && !UNITY_EDITOR
    public static bool Enabled = true;
#else
    public static bool Enabled = false;
#endif

    /// <summary>
    /// [Memory] log lines and the per-frame watchdog: on in development players, and in the web player only
    /// when the page URL has a <c>memlog</c> query parameter (for example <c>index.html?memlog</c>).
    /// Off for players and in the editor.
    /// </summary>
    public static bool Logging => logging ??= ComputeLogging();
    private static bool? logging;
    private static bool ComputeLogging()
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        return true;
#elif UNITY_WEBGL && !UNITY_EDITOR
        var url = Application.absoluteURL ?? "";
        int query = url.IndexOf('?');
        if (query < 0) return false;
        foreach (var part in url.Substring(query + 1).Split('&', '#'))
            if (part == "memlog" || part.StartsWith("memlog=")) return true;
        return false;
#else
        return false;
#endif
    }

    // Desktop players can collect mid-frame, so their log lines report live memory, not garbage.
    private static readonly bool collectBeforeLog = !Enabled;

    /// <summary>
    /// Drops the CPU copy of generated meshes under <paramref name="root"/>: meshes registered with an
    /// <see cref="EnvironmentMeshOwner"/>, TWC's runtime cluster meshes and <paramref name="extra"/>.
    /// Shared imported meshes are never touched, and meshes used by colliders stay readable.
    /// Call only once nothing will read or combine these meshes again.
    /// </summary>
    public static int ReleaseCpuCopies(GameObject root, IEnumerable<Mesh> extra = null)
    {
        if (!Enabled || root == null) return 0;
        var owned = new HashSet<Mesh>();
        foreach (var owner in root.GetComponentsInChildren<EnvironmentMeshOwner>(true))
            foreach (var mesh in owner.Meshes) if (mesh != null) owned.Add(mesh);
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh != null && mesh.name.Contains("_Cluster_")) owned.Add(mesh);
        }
        if (extra != null) foreach (var mesh in extra) if (mesh != null) owned.Add(mesh);
        foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
            if (collider.sharedMesh != null) owned.Remove(collider.sharedMesh);
        foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skinned.sharedMesh != null) owned.Remove(skinned.sharedMesh);
        int released = 0;
        foreach (var mesh in owned)
        {
            if (!mesh.isReadable) continue;
            mesh.UploadMeshData(true);
            released++;
        }
        return released;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LogBuildLayers()
    {
        TWC.TileWorldCreator.BuildLayerStepped = null;
        TWC.TileWorldCreator.BlueprintLayerStepped = null;
        if (!Logging) return;
        TWC.TileWorldCreator.BuildLayerStepped = (creator, layer) => Log($"{creator.name} layer {layer} built");
        TWC.TileWorldCreator.BlueprintLayerStepped = (creator, layer) => Log($"{creator.name} blueprint {layer} done");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartWatchdog()
    {
        if (!Logging || Object.FindFirstObjectByType<MemoryWatchdog>() != null) return;
        var host = new GameObject("Memory watchdog") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(host);
        host.AddComponent<MemoryWatchdog>();
    }

    /// <summary>Logs a [Memory] line whenever managed or native use jumps between frames, naming the scene.</summary>
    private sealed class MemoryWatchdog : MonoBehaviour
    {
        const long Threshold = 64L << 20;
        long managed, mono, native;
        void LateUpdate()
        {
            long m = System.GC.GetTotalMemory(false), h = Profiler.GetMonoHeapSizeLong(), n = Profiler.GetTotalAllocatedMemoryLong();
            if (managed != 0 && (m - managed > Threshold || h - mono > Threshold || n - native > Threshold))
                Log($"Jump since last frame (managed {(m - managed) / 1048576:+0;-0} MB, mono heap {(h - mono) / 1048576:+0;-0} MB, " +
                    $"native {(n - native) / 1048576:+0;-0} MB) in scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            managed = m; mono = h; native = n;
        }
    }

    /// <summary>One log line of memory use, so web builds show where the heap goes.</summary>
    public static void Log(string label)
    {
        const float MB = 1024f * 1024f;
        Debug.Log($"[Memory] {label}: {(collectBeforeLog ? "live " : "")}managed {System.GC.GetTotalMemory(collectBeforeLog) / MB:0} MB " +
            $"(mono heap {Profiler.GetMonoHeapSizeLong() / MB:0} MB), native allocated {Profiler.GetTotalAllocatedMemoryLong() / MB:0} MB, " +
            $"reserved {Profiler.GetTotalReservedMemoryLong() / MB:0} MB.");
    }
}
