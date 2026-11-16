using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Loads game content by its Resources path. The editor and ordinary builds read the Resources
/// folders directly. The deferred web build ships most of that folder in asset bundles that
/// download behind the menu; once loaded, those bundles are searched first.
/// </summary>
public static class GameContent
{
    /// <summary>Folder under StreamingAssets that holds the deferred bundles.</summary>
    public const string Folder = "content";
    /// <summary>Bundle asset names that begin with this stand in for a Resources path.</summary>
    public const string ResourcePrefix = "res/";

    private readonly struct Entry
    {
        public readonly AssetBundle Bundle;
        public readonly string Name;
        public Entry(AssetBundle bundle, string name) { Bundle = bundle; Name = name; }
    }

    private static SortedDictionary<string, List<Entry>> index;

    /// <summary>False only while a deferred build is still downloading the game beyond the menu.</summary>
    public static bool Ready { get; private set; } = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { index = null; Ready = true; }

    internal static void BeginDeferred()
    {
        index ??= new SortedDictionary<string, List<Entry>>(StringComparer.Ordinal);
        Ready = false;
    }

    internal static void EndDeferred() => Ready = true;

    internal static void Register(AssetBundle bundle)
    {
        if (bundle == null || bundle.isStreamedSceneAssetBundle) return;
        index ??= new SortedDictionary<string, List<Entry>>(StringComparer.Ordinal);
        foreach (var name in bundle.GetAllAssetNames())
        {
            string lower = name.ToLowerInvariant();
            if (!lower.StartsWith(ResourcePrefix, StringComparison.Ordinal)) continue;
            string key = lower.Substring(ResourcePrefix.Length);
            int dot = key.LastIndexOf('.');
            if (dot > key.LastIndexOf('/')) key = key.Substring(0, dot);
            if (!index.TryGetValue(key, out var entries)) index.Add(key, entries = new List<Entry>());
            entries.Add(new Entry(bundle, name));
        }
    }

    public static T Load<T>(string path) where T : Object
    {
        if (index != null && index.TryGetValue(Key(path), out var entries))
            foreach (var entry in entries)
            {
                var asset = From<T>(entry);
                if (asset != null) return asset;
            }
        return Resources.Load<T>(path);
    }

    public static T[] LoadAll<T>(string folder) where T : Object
    {
        var found = Resources.LoadAll<T>(folder);
        if (index == null) return found;
        string exact = Key(folder);
        string prefix = exact.Length > 0 ? exact + "/" : "";
        var all = new List<T>(found);
        foreach (var pair in index)
        {
            if (pair.Key != exact && !pair.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            foreach (var entry in pair.Value)
            {
                var asset = From<T>(entry);
                if (asset != null && !all.Contains(asset)) all.Add(asset);
            }
        }
        return all.ToArray();
    }

    /// <summary>Starts reading an asset without blocking; a later <see cref="Load{T}"/> returns it.</summary>
    public static void Warm<T>(string path) where T : Object
    {
        if (index != null && index.TryGetValue(Key(path), out var entries))
        {
            foreach (var entry in entries) entry.Bundle.LoadAssetAsync<Object>(entry.Name);
            return;
        }
        if (Ready) Resources.LoadAsync<T>(path);
    }

    private static string Key(string path) => (path ?? "").Replace('\\', '/').Trim('/').ToLowerInvariant();

    private static T From<T>(Entry entry) where T : Object
    {
        // Resources.Load resolves a component type through its prefab; bundles are asked for the prefab.
        if (typeof(Component).IsAssignableFrom(typeof(T)))
        {
            var prefab = entry.Bundle.LoadAsset<GameObject>(entry.Name);
            return prefab != null ? prefab.GetComponent<T>() : null;
        }
        return entry.Bundle.LoadAsset<T>(entry.Name);
    }
}
