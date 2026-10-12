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
        CurrentVisuals = new DungeonVisualSelection { Biome = plan.Grid.BiomeAt(plan.Start) ?? OverworldBiome.Grassland,
            UseBiomePresentation = true, Environment = DungeonEnvironmentKind.Outdoor };
        ThemeCatalog ??= Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
        RenderSettings.ambientLight = ScenePresentation.FillAmbient(originalAmbient);
        if (themeLight != null) { themeLight.color = originalLightColor; themeLight.intensity = originalLightIntensity; }
        var root = new GameObject("Finale floor " + plan.Number);
        FinaleTerrainRoot = root;
        var creator = root.AddComponent<TileWorldCreator>();
        creator.worldObject = new GameObject("Terrain"); creator.worldObject.transform.SetParent(root.transform, false);
        var map = root.AddComponent<CampaignOverworld>();
        map.StyleProfile = Resources.Load<OverworldStyleProfile>("EnvironmentKit/OverworldStyle");
        if (map.StyleProfile == null) throw new System.InvalidOperationException("Missing overworld style profile.");
        bool built = false;
        map.TerrainBuilt += _ => built = true;
        map.Apply(plan.Grid); map.BuildMeshes();
        while (!built) yield return null;
        CurrentFloor = plan.CreateFloor();
        GeneratedDungeon = Instantiate(TileWorldDungeonPrefab, root.transform);
        GeneratedDungeon.Setup(creator, CurrentFloor);
        // Capability-gate water becomes a visible bridge; the original campaign grid is untouched.
        foreach (var cell in plan.Grid.Locks.SelectMany(l => l.Cells).Where(plan.Grid.RequiresBoat).Distinct())
            EncounterRuntime.Marker(GeneratedDungeon, "Finale bridge", cell.ToCell(), new Color(.58f, .49f, .34f), .04f);
        ScenePresentation.RegisterWorld(root.transform);
    }
}
