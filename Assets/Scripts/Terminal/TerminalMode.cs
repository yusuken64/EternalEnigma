using System;
using UnityEngine;

public static class TerminalMode
{
    public const string PreferenceKey = "Settings.TerminalMode";
    public static event Action Changed;
    public static bool Requested => PlayerPrefs.GetInt(PreferenceKey, 0) != 0;
    public static bool Effective { get; internal set; }
    public static void SetRequested(bool enabled)
    {
        if (Requested == enabled) return;
        PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Effective = false; Changed = null; }
}
