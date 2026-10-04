using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public sealed class CampaignPlaytime : MonoBehaviour
{
    void Update()
    {
        var common = GetComponent<Common>();
        if (!Application.isFocused || Time.timeScale == 0 || common.CampaignContext == null || common.CampaignContext.IsSandbox ||
            common.Travel.IsTransitioning || common.GlobalSettings.IsOpen || common.GameSaveData == null) return;
        var scene = SceneManager.GetActiveScene().name;
        if (scene != "Town" && scene != "Overworld" && scene != "DungeonScene") return;
        if (scene == "Town" && FindFirstObjectByType<Town>()?.IsReady != true) return;
        if (scene == "DungeonScene" && Game.Instance?.IsReady != true) return;
        if (scene == "Overworld" && FindFirstObjectByType<OverworldScene>()?.IsReady != true) return;
        if (common.CampaignContext.State.Finished) return;
        if (common.ScreenTransition.BlockScreen.activeSelf) return;
        common.GameSaveData.PlaytimeSeconds += Time.unscaledDeltaTime;
        var context = common.CampaignContext;
        var location = context.Campaign.Locations.FirstOrDefault(l => l.Id == context.State.LocationId);
        var region = location?.RegionId;
        if (scene == "Overworld" && context.IsGridGenerated) region = context.Campaign.Regions.FirstOrDefault(r => context.Grid.Layers[EternalEnigma.Core.World.OverworldLayers.Region(r.Id)][context.Position.X, context.Position.Y])?.Id;
        if (region != null && !common.GameSaveData.VisitedRegions.Contains(region)) common.GameSaveData.VisitedRegions.Add(region);
    }
}
