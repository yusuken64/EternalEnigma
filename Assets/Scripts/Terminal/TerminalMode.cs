using System;
using UnityEngine;

public static class TerminalMode
{
    public const string PreferenceKey = "Settings.TerminalMode";
    public static event Action Changed;
    public static bool Requested => GamePreferences.GetInt(PreferenceKey, 0) != 0;
    // The audio thread reads the applied mode without accessing preferences or Unity objects.
    private static volatile bool effective;
    public static bool Effective { get => effective; internal set => effective = value; }
    public static void SetRequested(bool enabled)
    {
        if (Requested == enabled) return;
        GamePreferences.SetInt(PreferenceKey, enabled ? 1 : 0);
        GamePreferences.Save();
        Changed?.Invoke();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Effective = false; Changed = null; }
}
