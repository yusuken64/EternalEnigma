using System.Collections;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

public partial class TileWorldDungeonGenerator
{
    internal GameObject FinaleTerrainRoot { get; private set; }
    internal IEnumerator GenerateFinaleTerrain(FinaleFloorPlan plan)
    {
        GeneratedDungeon = null;
        if (RuntimeMeshMemory.Logging) RuntimeMeshMemory.Log($"Finale floor {plan.Number} terrain start");
        if (Common.Instance != null)
        {
            // The finale is the end of the campaign, so nothing cached for the overworld or towns is
            // needed again (both caches rebuild on demand if the party does retreat). The web heap cannot
            // hold them alongside a second overworld-sized world. Free them and unused assets, and collect,
            // before the build.
            Common.Instance.OverworldTerrain.Clear();
            Common.Instance.TownTerrain.Clear();
            yield return null;
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return null;
            if (RuntimeMeshMemory.Logging) RuntimeMeshMemory.Log("Finale: overworld and town caches released");
        }
        CurrentVisuals = new DungeonVisualSelection { Biome = plan.Grid.BiomeAt(plan.Start) ?? OverworldBiome.Grassland,
            UseBiomePresentation = true, Environment = DungeonEnvironmentKind.Outdoor };
        ThemeCatalog ??= GameContent.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
        RenderSettings.ambientLight = ScenePresentation.FillAmbient(originalAmbient);
        if (themeLight != null) { themeLight.color = originalLightColor; themeLight.intensity = originalLightIntensity; }
        var root = new GameObject("Finale floor " + plan.Number);
        FinaleTerrainRoot = root;
        var creator = root.AddComponent<TileWorldCreator>();
        creator.worldObject = new GameObject("Terrain"); creator.worldObject.transform.SetParent(root.transform, false);
        var map = root.AddComponent<CampaignOverworld>();
        map.StyleProfile = GameContent.Load<OverworldStyleProfile>("EnvironmentKit/OverworldStyle");
        if (map.StyleProfile == null) throw new System.InvalidOperationException("Missing overworld style profile.");
        bool built = false;
        map.TerrainBuilt += _ => built = true;
        map.Apply(plan.Grid);
        if (RuntimeMeshMemory.Logging) RuntimeMeshMemory.Log("Finale terrain layout applied");
        map.BuildMeshes();
        while (!built) yield return null;
        if (RuntimeMeshMemory.Logging) RuntimeMeshMemory.Log("Finale terrain built");
        // Nothing re-reads these meshes once the deferred chunks are done; drop their CPU copies on web.
        TerrainStreaming.WhenDrained(() =>
        {
            if (root == null) return;
            int released = RuntimeMeshMemory.ReleaseCpuCopies(root);
            if (RuntimeMeshMemory.Logging) RuntimeMeshMemory.Log($"Finale terrain streamed, {released} mesh CPU copies released");
        });
        CurrentFloor = plan.CreateFloor();
        GeneratedDungeon = Instantiate(TileWorldDungeonPrefab, root.transform);
        GeneratedDungeon.Setup(creator, CurrentFloor);
        // Capability-gate water becomes a visible bridge; the original campaign grid is untouched.
        foreach (var cell in plan.Grid.Locks.SelectMany(l => l.Cells).Where(plan.Grid.RequiresBoat).Distinct())
            EncounterRuntime.Marker(GeneratedDungeon, "Finale bridge", cell.ToCell(), new Color(.58f, .49f, .34f), .04f);
        ScenePresentation.RegisterWorld(root.transform);
    }
}
