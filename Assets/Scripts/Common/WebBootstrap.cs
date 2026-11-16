using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// First scene of the deferred web build. Downloads the menu's bundles, opens the game, then
/// downloads everything else while the menu is on screen. The build script creates and fills it.
/// </summary>
public sealed class WebBootstrap : MonoBehaviour
{
    [Serializable]
    public struct Bundle
    {
        public string Name;
        public string Hash;
        public long Bytes;
    }

    [Serializable]
    public struct Track
    {
        public string Name;
        public Bundle Bundle;
    }

    public Bundle[] Boot = Array.Empty<Bundle>();
    public Bundle[] Game = Array.Empty<Bundle>();
    // Each music track is its own bundle, fetched when a scene first asks for it.
    public Track[] Music = Array.Empty<Track>();
    public string FirstScene = "Common";
    // Keeps the built-in Standard shader in the player for materials created by code.
    public Material StandardMaterial;

    private const int Attempts = 3;
    private string stage;
    private string error;
    private float progress;
    private GUIStyle style;

    private readonly System.Collections.Generic.Dictionary<string, AudioClip> tracks = new();
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Action<AudioClip>>> waiting = new();

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        GameContent.BeginDeferred();
        GameContent.TrackProvider = LoadTrack;
    }

    private void LoadTrack(string track, Action<AudioClip> done)
    {
        if (tracks.TryGetValue(track, out var clip)) { done(clip); return; }
        if (waiting.TryGetValue(track, out var callbacks)) { callbacks.Add(done); return; }
        int index = Array.FindIndex(Music, entry => entry.Name == track);
        if (index < 0) { Debug.LogWarning("Unknown music track: " + track); done(null); return; }
        waiting.Add(track, new System.Collections.Generic.List<Action<AudioClip>> { done });
        StartCoroutine(FetchTrack(Music[index]));
    }

    private IEnumerator FetchTrack(Track track)
    {
        AssetBundle bundle = null;
        yield return Fetch(track.Bundle, _ => { }, loaded => bundle = loaded, false);
        AudioClip clip = null;
        if (bundle != null)
        {
            var request = bundle.LoadAllAssetsAsync<AudioClip>();
            yield return request;
            clip = request.asset as AudioClip;
        }
        // A missing track is remembered as silence rather than retried on every scene.
        tracks[track.Name] = clip;
        var callbacks = waiting[track.Name];
        waiting.Remove(track.Name);
        foreach (var callback in callbacks) callback(clip);
    }

    /// <summary>Fetches the remaining music one track at a time so later scene changes find it ready.</summary>
    private IEnumerator PrefetchMusic()
    {
        foreach (var track in Music)
        {
            if (tracks.ContainsKey(track.Name)) continue;
            bool finished = false;
            LoadTrack(track.Name, _ => finished = true);
            while (!finished) yield return null;
        }
    }

    private IEnumerator Start()
    {
        stage = "Loading";
        yield return Download(Boot);
        if (error != null) yield break;
        SceneManager.LoadScene(FirstScene);
        stage = "Downloading the adventure";
        yield return Download(Game);
        if (error != null) yield break;
        stage = null;
        GameContent.EndDeferred();
        yield return PrefetchMusic();
    }

    private IEnumerator Download(Bundle[] bundles)
    {
        var loaded = new AssetBundle[bundles.Length];
        var received = new long[bundles.Length];
        long total = 0;
        foreach (var bundle in bundles) total += Math.Max(1, bundle.Bytes);
        int pending = bundles.Length;
        progress = 0;
        for (int i = 0; i < bundles.Length; i++)
        {
            int slot = i;
            StartCoroutine(Fetch(bundles[slot], value => received[slot] = value, bundle => { loaded[slot] = bundle; pending--; }, true));
        }
        while (pending > 0 && error == null)
        {
            long done = 0;
            for (int i = 0; i < bundles.Length; i++) done += Math.Min(received[i], Math.Max(1, bundles[i].Bytes));
            progress = Mathf.Clamp01((float)done / total);
            yield return null;
        }
        if (error != null) yield break;
        progress = 1;
        foreach (var bundle in loaded) GameContent.Register(bundle);
    }

    /// <summary>Downloads one bundle with retries. Only a <paramref name="required"/> bundle stops the game on failure.</summary>
    private IEnumerator Fetch(Bundle bundle, Action<long> report, Action<AssetBundle> finished, bool required)
    {
        string url = Application.streamingAssetsPath + "/" + GameContent.Folder + "/" + bundle.Name + "?v=" + bundle.Hash;
        string failure = null;
        for (int attempt = 1; attempt <= Attempts; attempt++)
        {
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    report((long)request.downloadedBytes);
                    yield return null;
                }
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var content = DownloadHandlerAssetBundle.GetContent(request);
                    if (content != null)
                    {
                        report(bundle.Bytes);
                        finished(content);
                        yield break;
                    }
                    failure = "unreadable";
                }
                else failure = request.error;
            }
            report(0);
            yield return new WaitForSecondsRealtime(attempt);
        }
        string message = "Could not download " + bundle.Name + " (" + failure + ").";
        if (required)
        {
            error = message + " Reload the page to try again.";
            Debug.LogError(error);
        }
        else
        {
            Debug.LogWarning(message);
            finished(null);
        }
    }

    private void OnGUI()
    {
        string text = error ?? (stage != null ? stage + "... " + Mathf.FloorToInt(progress * 100) + "%" : null);
        if (text == null) return;
        style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        style.fontSize = Mathf.Max(14, Screen.height / 36);
        var area = new Rect(Screen.width * .1f, Screen.height * .9f, Screen.width * .8f, Screen.height * .08f);
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(area.x + 2, area.y + 2, area.width, area.height), text, style);
        style.normal.textColor = Color.white;
        GUI.Label(area, text, style);
    }
}
