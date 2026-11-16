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

    public Bundle[] Boot = Array.Empty<Bundle>();
    public Bundle[] Game = Array.Empty<Bundle>();
    public string FirstScene = "Common";
    // Keeps the built-in Standard shader in the player for materials created by code.
    public Material StandardMaterial;

    private const int Attempts = 3;
    private string stage;
    private string error;
    private float progress;
    private GUIStyle style;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        GameContent.BeginDeferred();
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
            StartCoroutine(Fetch(bundles[slot], value => received[slot] = value, bundle => { loaded[slot] = bundle; pending--; }));
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

    private IEnumerator Fetch(Bundle bundle, Action<long> report, Action<AssetBundle> finished)
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
        error = "Could not download " + bundle.Name + " (" + failure + "). Reload the page to try again.";
        Debug.LogError(error);
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
