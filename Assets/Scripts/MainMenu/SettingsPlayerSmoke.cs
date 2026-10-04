#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Opt-in Windows player verification. The read phase restores the user's preferences.</summary>
public sealed class SettingsPlayerSmoke : MonoBehaviour
{
    [Serializable] private sealed class Saved
    {
        public bool[] present;
        public float[] values;
        public int width, height, mode;
    }
    private static readonly string[] Keys = { DisplayPreferences.ModeKey, DisplayPreferences.WidthKey, DisplayPreferences.HeightKey,
        "Settings.Audio.MusicVolume", "Settings.Audio.EffectVolume", "Settings.Audio.UIVolume" };
    private string folder;
    private bool read;
    private Saved saved;
    private bool success;
    private string limitations = "";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        if (!args.Contains("--settings-smoke-write") && !args.Contains("--settings-smoke-read")) return;
        var obj = new GameObject("Settings player smoke"); DontDestroyOnLoad(obj);
        var smoke = obj.AddComponent<SettingsPlayerSmoke>(); smoke.read = args.Contains("--settings-smoke-read");
    }
    private IEnumerator Start()
    {
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SettingsSmoke")); Directory.CreateDirectory(folder);
        Application.runInBackground = true;
        yield return new WaitForSecondsRealtime(8);
        var audio = Common.Instance.AudioManager;
        var settings = Common.Instance.GlobalSettings;
        try
        {
            if (read)
            {
                saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Path.Combine(folder, "preferences.json")));
                Require(Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == 1280 && Screen.height == 720, "Display restored at startup");
                Require(Mathf.Abs(AudioPreferences.Read(audio.MusicAudioMixerGroup, "MusicVolume") - .23f) < .001f, "Music restored at startup");
                Require(Mathf.Abs(AudioPreferences.Read(audio.EffectAudioMixerGroup, "EffectVolume") - .47f) < .001f, "Effects restored at startup");
                Require(AudioPreferences.Read(audio.UIAudioMixerGroup, "UIVolume") == 0, "UI mute restored at startup");
            }
            else
            {
                saved = new Saved { present = Keys.Select(PlayerPrefs.HasKey).ToArray(), values = Keys.Select((k,i) => i < 3 ? PlayerPrefs.GetInt(k) : PlayerPrefs.GetFloat(k)).ToArray(),
                    width = Screen.width, height = Screen.height, mode = (int)Screen.fullScreenMode };
                File.WriteAllText(Path.Combine(folder, "preferences.json"), JsonUtility.ToJson(saved, true));
                foreach (var mode in DisplayPreferences.Modes)
                {
                    var size = mode == FullScreenMode.Windowed ? new Vector2Int(1280,720) : DisplayPreferences.Current.Choices(mode).Last();
                    DisplayPreferences.Current.Commit(size, mode);
                    yield return new WaitForSecondsRealtime(2);
                    Debug.Log($"Settings smoke: requested {mode}; actual {Screen.fullScreenMode}; focus {Application.isFocused}; size {Screen.width} x {Screen.height}");
                    if (mode == FullScreenMode.ExclusiveFullScreen && Screen.fullScreenMode != mode)
                    {
                        limitations += $"Exclusive fullscreen was requested but this player reported {Screen.fullScreenMode}; exclusive mode remains unverified.\n";
                    }
                    else
                    {
                        Require(Screen.fullScreenMode == mode, "Mode " + mode);
                        Require(Screen.width == size.x && Screen.height == size.y, "Render size " + size);
                    }
                    settings.ShowDialog();
                    settings.TabGroup.TabContents.Single(t => t.Content.GetComponent<DisplayOptions>() != null).TabButton.Select();
                    yield return new WaitForSecondsRealtime(.3f);
                    Require(settings.NavigationHandler.selectionArrow.gameObject.activeInHierarchy, "Cursor visible in " + mode);
                    var arrowCorners = new Vector3[4]; settings.NavigationHandler.selectionArrow.GetWorldCorners(arrowCorners);
                    Require(arrowCorners.Min(c => c.x) >= 0 && arrowCorners.Max(c => c.x) <= Screen.width, "Cursor stays on screen in " + mode);
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, mode + ".png"));
                    yield return new WaitForSecondsRealtime(.3f);
                    settings.Exit_Clicked();
                }
                AudioPreferences.Set(audio.MusicAudioMixerGroup, "MusicVolume", .23f);
                AudioPreferences.Set(audio.EffectAudioMixerGroup, "EffectVolume", .47f);
                AudioPreferences.Set(audio.UIAudioMixerGroup, "UIVolume", 0);
            }
            success = true;
        }
        finally
        {
            if ((read || !success) && saved != null)
            {
                for (int i = 0; i < Keys.Length; i++)
                    if (!saved.present[i]) PlayerPrefs.DeleteKey(Keys[i]);
                    else if (i < 3) PlayerPrefs.SetInt(Keys[i], (int)saved.values[i]);
                    else PlayerPrefs.SetFloat(Keys[i], saved.values[i]);
                PlayerPrefs.Save();
                Screen.SetResolution(saved.width, saved.height, (FullScreenMode)saved.mode);
            }
            File.WriteAllText(Path.Combine(folder, read ? "read.txt" : "write.txt"), success ? (limitations.Length == 0 ? "PASS" : "PARTIAL\n" + limitations) : "FAIL (see player log)");
            StartCoroutine(QuitAfterRestoration());
        }
    }
    private IEnumerator QuitAfterRestoration()
    {
        // Screen.SetResolution is deferred; let restoration settle before Unity saves its window state.
        yield return new WaitForSecondsRealtime(1);
        Application.Quit(success ? 0 : 1);
    }
    private static void Require(bool condition, string message)
    {
        Debug.Log("Settings smoke: " + message + " = " + condition);
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
