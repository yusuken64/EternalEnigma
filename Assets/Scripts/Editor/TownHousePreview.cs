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
                var options = TownLayout.Create(42, TownServiceCatalog.All, 2, 3,
                    CampaignContext.ResidentialTownBuildings).Options;
                CoreTownLayerGenerator.Configure(template, options);
                creator.SetCustomRandomSeed(42);
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
