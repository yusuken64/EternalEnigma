using System;
using UnityEngine;

public static class SaveSystem
{
    private const string SaveKey = "SaveData";
    private static ISaveStore store = new PlayerPrefsSaveStore();

    // A scope prevents a test/session from accidentally leaving its store installed.
    public static IDisposable UseStore(ISaveStore replacement)
    {
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));
        var scope = new StoreScope(store, replacement);
        store = replacement;
        return scope;
    }

    public static GameSaveData LoadData()
    {
        string json = store.Read();
        if (json == null)
        {
            Debug.Log("No save data found. Creating new save.");
            return null;
        }

        // Migrate the original public JSON keys without changing item/ally names.
        json = json.Replace("\"OverworldSaveData\":", "\"TownSaveData\":")
            .Replace("\"OverworldSeed\":", "\"TownSeed\":");
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
        if (data.CampaignFormatVersion == 0) data.Campaign = null;
        if (data.CampaignFormatVersion > 1) throw new InvalidOperationException("Unsupported campaign save format.");
        return data;
    }

    public static void SaveData(GameSaveData data)
    {
        if (data == null || data.IsSandbox) return;
        var common = UnityEngine.Object.FindFirstObjectByType<Common>();
        if (common != null && common.CampaignContext?.IsSandbox == true) return;
        if (common != null && ReferenceEquals(common.GameSaveData, data) && common.CampaignContext != null)
            data.Campaign = common.CampaignContext.Capture();
        if (!string.IsNullOrEmpty(data.Campaign?.Fingerprint)) data.CampaignFormatVersion = 1;
        string json = JsonUtility.ToJson(data);
        store.Write(json);
    }

    public static void ClearData()
    {
        if (UnityEngine.Object.FindFirstObjectByType<Common>()?.CampaignContext?.IsSandbox == true) return;
        store.Clear();
    }

    private sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public string Read() => PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
        public void Write(string json) { PlayerPrefs.SetString(SaveKey, json); PlayerPrefs.Save(); }
        public void Clear() => PlayerPrefs.DeleteKey(SaveKey);
    }

    private sealed class StoreScope : IDisposable
    {
        private readonly ISaveStore previous;
        private readonly ISaveStore installed;
        private bool disposed;
        public StoreScope(ISaveStore previous, ISaveStore installed)
        { this.previous = previous; this.installed = installed; }
        public void Dispose()
        {
            if (disposed) return;
            if (!ReferenceEquals(store, installed))
                throw new InvalidOperationException("Save store scopes must be disposed in reverse order.");
            store = previous;
            disposed = true;
        }
    }
}

public interface ISaveStore
{
    string Read();
    void Write(string json);
    void Clear();
}
