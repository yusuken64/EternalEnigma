using System;
using System.Collections.Generic;
using UnityEngine;

// Production settings use PlayerPrefs unless an isolated diagnostic session owns this scope.
// No personal keys are touched while isolated, including on a process crash.
public static class GamePreferences
{
    private static Dictionary<string, object> isolated;
    public static bool IsIsolated => isolated != null;
    public static bool HasKey(string key) => isolated != null ? isolated.ContainsKey(key) : PlayerPrefs.HasKey(key);
    public static int GetInt(string key, int fallback = 0) => isolated != null ?
        isolated.TryGetValue(key, out var value) && value is int number ? number : fallback : PlayerPrefs.GetInt(key, fallback);
    public static float GetFloat(string key, float fallback = 0) => isolated != null ?
        isolated.TryGetValue(key, out var value) && value is float number ? number : fallback : PlayerPrefs.GetFloat(key, fallback);
    public static void SetInt(string key, int value) { if (isolated != null) isolated[key] = value; else PlayerPrefs.SetInt(key, value); }
    public static void SetFloat(string key, float value) { if (isolated != null) isolated[key] = value; else PlayerPrefs.SetFloat(key, value); }
    public static void Save() { if (isolated == null) PlayerPrefs.Save(); }
    public static IDisposable Isolate()
    {
        if (isolated != null) throw new InvalidOperationException("A preference isolation scope is already active.");
        isolated = new();
        return new Scope();
    }
    private sealed class Scope : IDisposable
    {
        private bool disposed;
        public void Dispose() { if (disposed) return; disposed = true; isolated = null; }
    }
}

// Shared with autoplay in release builds; chaos itself is development-only.
public static class ChaosSessionLock
{
    public static bool Held { get; internal set; }
}
