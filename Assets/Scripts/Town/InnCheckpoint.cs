// Compatibility facade for callers: the selected slot is the only recovery source.
public static class InnCheckpoint
{
    public static bool Exists(GameSaveData save) => SaveSystem.Inspect(SaveSystem.ActiveSlot, out _)?.HasCampaign == true;
    public static bool TryRestore(Common common)
    {
        if (common.GameSaveData?.IsSandbox == true || common.CampaignContext?.IsSandbox == true) return false;
        var restored = SaveSystem.Inspect(SaveSystem.ActiveSlot, out var error);
        if (restored == null || !restored.HasCampaign || restored.Campaign.Finished) return false;
        var previous = common.GameSaveData;
        try
        {
            // Validate the complete logical context before replacing live objects.
            _ = new EternalEnigma.Core.Progression.CampaignContext(new(EternalEnigma.Core.Progression.OverworldLaunchMode.Campaign), restored.Campaign);
            common.GameSaveData = restored;
            common.Travel.SceneReady();
            common.Travel.Continue();
            return true;
        }
        catch (System.Exception) { common.GameSaveData = previous; return false; }
    }
}
