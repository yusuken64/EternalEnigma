using UnityEngine;

/// <summary>The inn's save point: a full copy of the save taken at the inn, restored when the party is defeated.</summary>
public static class InnCheckpoint
{
    public static bool Exists(GameSaveData save) => save != null && !string.IsNullOrEmpty(save.InnSaveJson);

    public static void Create(Common common)
    {
        var save = common.GameSaveData;
        if (save == null || save.IsSandbox || common.CampaignContext?.IsSandbox == true) return;
        save.InnSaveJson = null; // never nest the previous checkpoint
        if (common.CampaignContext != null) save.Campaign = common.CampaignContext.Capture();
        save.InnSaveJson = JsonUtility.ToJson(save);
    }

    /// <summary>Replaces the live save with the checkpoint and reloads the town it was taken in. False when there is none.</summary>
    public static bool TryRestore(Common common)
    {
        var current = common.GameSaveData;
        if (!Exists(current) || current.IsSandbox || common.CampaignContext?.IsSandbox == true) return false;
        string json = current.InnSaveJson;
        var restored = JsonUtility.FromJson<GameSaveData>(json);
        restored.InnSaveJson = json; // later deaths return here too
        common.GameSaveData = restored;
        SaveSystem.SaveData(restored);
        common.Travel.Continue();
        return true;
    }
}
