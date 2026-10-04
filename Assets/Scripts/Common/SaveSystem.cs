using System;
using UnityEngine;

public static class SaveSystem
{
    public const int SlotCount = 3;
    private static ISaveStore store = new PlayerPrefsSaveStore();
    private static int activeSlot;
    public static int ActiveSlot { get => activeSlot; set { ValidateSlot(value); activeSlot = value; } }
    public static void ValidateSlot(int slot) { if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot)); }
    public static IDisposable UseStore(ISaveStore replacement)
    {
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));
        var scope = new StoreScope(store, replacement, activeSlot);
        store = replacement; activeSlot = 0; return scope;
    }
    public static GameSaveData LoadData() => LoadData(ActiveSlot);
    public static GameSaveData LoadData(int slot)
    {
        ValidateSlot(slot);
        string json = store.Read(slot);
        if (json == null) return null;
        var data = JsonUtility.FromJson<GameSaveData>(json);
        if (data == null || data.TownSaveData == null || !json.Contains("\"TownSaveData\""))
            throw new InvalidOperationException("Unreadable campaign save.");
        return data;
    }
    public static GameSaveData Inspect(int slot, out string error)
    {
        try { var data = LoadData(slot); error = null; return data; }
        catch (Exception e) { error = "Unable to read this save: " + e.Message; return null; }
    }
    // Capturing never writes; writing never consults live state.
    public static void Capture(Common common)
    {
        if (common?.GameSaveData != null && common.CampaignContext != null)
            common.GameSaveData.Campaign = common.CampaignContext.Capture();
    }
    public static void SaveData(GameSaveData data) => SaveData(ActiveSlot, data);
    public static void SaveData(int slot, GameSaveData data)
    {
        ValidateSlot(slot);
        if (data == null || data.IsSandbox || UnityEngine.Object.FindFirstObjectByType<Common>()?.CampaignContext?.IsSandbox == true) return;
        store.Write(slot, JsonUtility.ToJson(data));
    }
    public static void ClearData()
    {
        if (UnityEngine.Object.FindFirstObjectByType<Common>()?.CampaignContext?.IsSandbox == true) return;
        store.Clear(ActiveSlot);
    }
    private sealed class PlayerPrefsSaveStore : ISaveStore
    {
        private static string Key(int slot) => "CampaignSlot_v2_" + slot;
        public string Read(int slot) => PlayerPrefs.HasKey(Key(slot)) ? PlayerPrefs.GetString(Key(slot)) : null;
        public void Write(int slot, string json)
        {
            var previous = Read(slot);
            try { PlayerPrefs.SetString(Key(slot), json); PlayerPrefs.Save(); }
            catch { if (previous == null) PlayerPrefs.DeleteKey(Key(slot)); else PlayerPrefs.SetString(Key(slot), previous); throw; }
        }
        public void Clear(int slot) { PlayerPrefs.DeleteKey(Key(slot)); PlayerPrefs.Save(); }
    }
    private sealed class StoreScope : IDisposable
    {
        private readonly ISaveStore previous, installed;
        private readonly int slot;
        private bool disposed;
        public StoreScope(ISaveStore previous, ISaveStore installed, int slot) { this.previous = previous; this.installed = installed; this.slot = slot; }
        public void Dispose()
        {
            if (disposed) return;
            if (!ReferenceEquals(store, installed)) throw new InvalidOperationException("Save store scopes must be disposed in reverse order.");
            store = previous; activeSlot = slot; disposed = true;
        }
    }
}

// Write atomically replaces a slot, or throws leaving its previous content intact.
public interface ISaveStore
{
    string Read(int slot);
    void Write(int slot, string json);
    void Clear(int slot);
}
