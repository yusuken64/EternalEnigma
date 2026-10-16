using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TownHousePreview
{
    [MenuItem("Tools/Eternal Enigma/Art/Capture Town Houses")]
    public static void CaptureAll()
    {
        const string folder = "Temp/TownHouseCaptures";
        const int seed = 5; // Includes square, wide, long and notched furnished houses.
        Directory.CreateDirectory(folder);
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
        {
            var host = new GameObject("House verification");
            var creator = host.AddComponent<TileWorldCreator>();
            var template = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(
                "Assets/TileWorldCreator/VillageLSystemAsset.asset"));
            creator.twcAsset = template;
            creator.worldObject = new GameObject("Furnished town");
            var style = host.AddComponent<TownBiomeStyle>();
            style.OverrideBiome = true;
            style.Biome = biome;
            try
            {
                var options = TownLayout.Create(seed, TownServiceCatalog.All, 2, 3,
                    CampaignContext.ResidentialTownBuildings).Options;
                CoreTownLayerGenerator.Configure(template, options);
                creator.SetCustomRandomSeed(seed);
                creator.ExecuteAllBlueprintLayers();
                CoreLayoutCache.ClearResultFlags(template);
                creator.ExecuteAllBuildLayers(true);
                if (!CoreLayoutCache.TryGetTown(creator, out var plan))
                    throw new InvalidOperationException("Town plan was not generated.");
                var roofGroups = creator.worldObject.GetComponentsInChildren<TownRoofTileOutput>()
                    .SelectMany(o => o.Roofs).ToArray();
                if (roofGroups.Length != plan.BuildingSlots.Count)
                    throw new InvalidOperationException($"{biome}: {roofGroups.Length} roof groups for {plan.BuildingSlots.Count} doors.");
                float size = template.cellSize;
                var door = plan.BuildingSlots.First();
                var facade = new Vector3(door.X + .5f, door.Y + 2.7f, -.6f) * size;
                TownInteriorPreview.Capture(creator.worldObject, facade, size * 7,
                    $"{folder}/{biome}_Facade.png", frontal: true);
                var overview = new Vector3(plan.Width / 2f, plan.Height / 2f, 0) * size;
                TownInteriorPreview.Capture(creator.worldObject, overview, size * 24,
                    $"{folder}/{biome}_Gameplay.png");
                if (biome == OverworldBiome.Grassland)
                {
                    var examples = plan.Footprints.Where(f => f.Cells.Count > 0)
                        .GroupBy(f => (f.Bounds.Width, f.Depth, f.HasNotch))
                        .Select(g => g.First()).OrderByDescending(f => f.HasNotch)
                        .ThenBy(f => f.Bounds.Width).ThenBy(f => f.Depth).Take(6).ToArray();
                    foreach (var footprint in examples)
                    {
                        var bounds = footprint.Bounds;
                        var center = new Vector3(bounds.X + bounds.Width * .5f,
                            bounds.Y + footprint.Depth * .5f, -.6f) * size;
                        string shape = footprint.HasNotch ? "L" : "Rectangle";
                        TownInteriorPreview.Capture(creator.worldObject, center, size * 7,
                            $"{folder}/{biome}_{shape}_{bounds.Width}x{footprint.Depth}.png");
                    }
                    File.WriteAllLines($"{folder}/Footprints.txt", plan.Footprints.Select(f =>
                        $"Door {f.Door}: {f.Bounds.Width}x{f.Depth}, {(f.HasNotch ? "L-shaped" : "rectangular")}"));
                }
            }
            finally
            {
                DungeonPresentation.ClearOutput(creator.worldObject);
                Object.DestroyImmediate(creator.worldObject);
                Object.DestroyImmediate(host);
                DungeonPresentation.ReleaseTemplate(template);
            }
        }
        Debug.Log("Town house captures written to " + folder);
    }
}
