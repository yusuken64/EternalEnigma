using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

public static class TownDungeonPresentation
{
    public static void Configure(TileWorldCreator creator, DungeonFloorOptions options)
    {
        var asset = creator.twcAsset;
        CoreTownLayerGenerator.Configure(asset, TownDungeonGenerator.TownOptions(options));
        var combat = new TileWorldCreatorAsset.BlueprintLayerData("Combat floor", true);
        var source = new CoreDungeonLayerGenerator { LayerName = DungeonLayers.Floor };
        combat.stack = new List<TileWorldCreatorAsset.BlueprintLayerData.ActionStack> {
            new("Core combat geometry", source)
        };
        asset.mapBlueprintLayers.Add(combat);
        CoreDungeonLayerGenerator.Configure(asset, options);
        foreach (var town in asset.mapBlueprintLayers.SelectMany(l => l.stack).Select(s => s.action).OfType<CoreTownLayerGenerator>())
            town.DungeonSource = source;
        var plan = TownDungeonGenerator.TownOptions(options);
        foreach (var buildings in asset.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().Where(l => l.Buildings))
            buildings.BuildingServices = plan.Interiors.Select(i => i.Kind == TownInteriorKind.Inn ? "Inn" :
                i.Kind == TownInteriorKind.Trainer ? "Trainer" : i.Kind == TownInteriorKind.Shop ? "Shop" : "").ToArray();
        var style = creator.GetComponent<TownBiomeStyle>() ?? creator.gameObject.AddComponent<TownBiomeStyle>();
        style.OverrideBiome = true; style.Biome = options.Biome;
        CoreLayoutCache.Clear(creator);
    }

    public static void Complete(TileWorldCreator creator, DungeonFloor floor, bool preview = false)
    {
        BiomeDecorations.Town(creator);
        TownInteriorRendering.Build(creator);
        DungeonPresentation.TrackLegacyMeshes(creator.worldObject);
        // Buildings keep their authored roofs outside sight. Opening one reveals any interior
        // the party can see through its doorway, including enemies targeted from the street.
        var visibility = creator.worldObject.GetComponent<TownDungeonVisibility>() ?? creator.worldObject.AddComponent<TownDungeonVisibility>();
        visibility.Initialize(floor, creator.twcAsset.cellSize, preview);
        var kit = EnvironmentKit.Load();
        var biome = creator.GetComponent<TownBiomeStyle>().Current;
        TownGateVisuals.Create(kit, biome, creator.worldObject.transform, floor.Stairs,
            creator.twcAsset.cellSize, "District exit", false, true, TownGateVisuals.DistrictRotation(floor.TownPlan, floor.Stairs, true));
        ScenePresentation.RegisterWorld(creator.worldObject.transform);
    }

    public static void Boundary(EnvironmentBatch batch, BiomeDecorationSurfaceSet faces, EnvironmentKit kit,
        TownPlan plan, OverworldBiome biome, float size)
    {
        for (int y = 0; y < plan.Height; y++) for (int x = 0; x < plan.Width; x++)
        {
            if (!plan.Layers[TownLayers.Boundary][x, y]) continue;
            var position = new Vector3(x + .5f, y + .5f, 0) * size;
            float angle = x == 0 || x == plan.Width - 1 ? 90 : 0;
            batch.Add(kit.Mesh("Wall"), kit.Material(biome), position, Vector3.one * size, angle);
            faces.Add(kit.Mesh("Wall"), Matrix4x4.TRS(position, Quaternion.Euler(0, 0, angle), Vector3.one * size), biome, DecorationSurface.BuiltWall, size);
        }
    }
}
