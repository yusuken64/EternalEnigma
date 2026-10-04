using UnityEngine;
using UnityEngine.Audio;

public static class AudioPreferences
{
    public const float MuteFloor = -80f;
    public static string Key(string parameter) => "Settings.Audio." + parameter;
    public static float ToDecibels(float level) => level <= 0 ? MuteFloor : Mathf.Max(MuteFloor, 20 * Mathf.Log10(Mathf.Clamp01(level)));
    public static float ToLevel(float decibels) => decibels <= MuteFloor ? 0 : Mathf.Clamp01(Mathf.Pow(10, decibels / 20));
    public static float Read(AudioMixerGroup group, string parameter) =>
        group != null && group.audioMixer.GetFloat(parameter, out var db) ? ToLevel(db) : 1;
    public static void Restore(AudioMixerGroup group, string parameter, float? fallback = null)
    {
        if (group == null) return;
        if (PlayerPrefs.HasKey(Key(parameter))) group.audioMixer.SetFloat(parameter, ToDecibels(PlayerPrefs.GetFloat(Key(parameter))));
        else if (fallback.HasValue) group.audioMixer.SetFloat(parameter, ToDecibels(fallback.Value));
    }
    public static void Set(AudioMixerGroup group, string parameter, float level)
    {
        if (group == null) return;
        level = Mathf.Clamp01(level);
        group.audioMixer.SetFloat(parameter, ToDecibels(level));
        PlayerPrefs.SetFloat(Key(parameter), level);
        PlayerPrefs.Save();
    }
}
