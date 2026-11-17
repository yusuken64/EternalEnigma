using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CampaignPlaytime : MonoBehaviour
{
    // Per-frame caches: the scene name, scene roots and the visited region only change occasionally,
    // so avoid allocating strings/closures and scanning the scene every frame.
    private Common common;
    private int sceneHandle = int.MinValue;
    private string sceneName;
    private Town town;
    private OverworldScene overworld;

    private EternalEnigma.Core.Progression.CampaignContext regionContext;
    private string regionLocationId;
    private string regionScene;
    private bool regionGridGenerated;
    private int regionX, regionY;
    private string cachedRegion;

    void Update()
    {
        if (common == null) common = GetComponent<Common>();
        if (!Application.isFocused || Time.timeScale == 0 || common.CampaignContext == null || common.CampaignContext.IsSandbox ||
            common.Travel.IsTransitioning || common.GlobalSettings.IsOpen || common.GameSaveData == null) return;
        var active = SceneManager.GetActiveScene();
        if (active.handle != sceneHandle)
        {
            sceneHandle = active.handle;
            sceneName = active.name;
            town = null; overworld = null;
        }
        var scene = sceneName;
        if (scene != "Town" && scene != "Overworld" && scene != "DungeonScene") return;
        if (scene == "Town")
        {
            if (town == null) town = FindFirstObjectByType<Town>();
            if (town == null || !town.IsReady) return;
        }
        if (scene == "DungeonScene" && Game.Instance?.IsReady != true) return;
        if (scene == "Overworld")
        {
            if (overworld == null) overworld = FindFirstObjectByType<OverworldScene>();
            if (overworld == null || !overworld.IsReady) return;
        }
        if (common.ScreenTransition.BlockScreen.activeSelf) return;
        common.GameSaveData.PlaytimeSeconds += Time.unscaledDeltaTime;
        var region = CurrentRegion(common.CampaignContext, scene);
        if (region != null && !common.GameSaveData.VisitedRegions.Contains(region)) common.GameSaveData.VisitedRegions.Add(region);
    }

    private string CurrentRegion(EternalEnigma.Core.Progression.CampaignContext context, string scene)
    {
        bool useGrid = scene == "Overworld" && context.IsGridGenerated;
        int x = 0, y = 0;
        if (useGrid) { var position = context.Position; x = position.X; y = position.Y; }
        var locationId = context.State.LocationId;
        if (ReferenceEquals(context, regionContext) && locationId == regionLocationId && scene == regionScene &&
            useGrid == regionGridGenerated && x == regionX && y == regionY) return cachedRegion;
        regionContext = context; regionLocationId = locationId; regionScene = scene;
        regionGridGenerated = useGrid; regionX = x; regionY = y;

        var location = context.ResolveDungeonLocation(locationId);
        if (location == null)
            foreach (var l in context.Campaign.Locations) if (l.Id == locationId) { location = l; break; }
        string region = location?.RegionId;
        if (useGrid)
        {
            region = null;
            foreach (var r in context.Campaign.Regions)
                if (context.Grid.Layers[EternalEnigma.Core.World.OverworldLayers.Region(r.Id)][x, y]) { region = r.Id; break; }
        }
        cachedRegion = region;
        return region;
    }
}
