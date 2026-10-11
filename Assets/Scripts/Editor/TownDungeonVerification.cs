using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class TownDungeonVerification
{
    private const string Folder = "Temp/TownDungeon";

    [MenuItem("Tools/Eternal Enigma/Town Dungeons/Capture Overworld")]
    public static void CaptureOverworld()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture comparisons outside Play Mode.");
        Directory.CreateDirectory(Folder);
        var host = new GameObject("Town district overworld comparison");
        var creator = host.AddComponent<TileWorldCreator>();
        var adapter = host.AddComponent<CampaignOverworld>();
        adapter.StyleProfile = Resources.Load<OverworldStyleProfile>("EnvironmentKit/OverworldStyle");
        adapter.Template = adapter.StyleProfile.ResolveTemplate();
        creator.twcAsset = adapter.Template;
        try
        {
            var campaign = CampaignGenerator.Generate(42);
            var grid = adapter.Generate(campaign); adapter.BuildMeshes();
            var hostile = grid.TownFootprints.First(t => t.IsHostile);
            var friendly = grid.TownFootprints.First(t => !t.IsHostile && grid.BiomeAt(t.Entrance) == grid.BiomeAt(hostile.Entrance));
            foreach (var town in new[] { hostile, friendly })
            {
                var cells = town.Walls.Concat(town.Interior).ToArray();
                var center = new Vector3((cells.Min(p => p.X) + cells.Max(p => p.X) + 1) * .5f,
                    (cells.Min(p => p.Y) + cells.Max(p => p.Y) + 1) * .5f, 0) * creator.twcAsset.cellSize;
                TownInteriorPreview.Capture(creator.worldObject, center, creator.twcAsset.cellSize * 7,
                    Folder + "/overworld-" + (town.IsHostile ? "hostile" : "friendly") + ".png");
            }
        }
        finally
        {
            if (creator.worldObject != null) { DungeonPresentation.ClearOutput(creator.worldObject); Object.DestroyImmediate(creator.worldObject); }
            Object.DestroyImmediate(host);
        }
    }

    [MenuItem("Tools/Eternal Enigma/Town Dungeons/Configure Shared Assets")]
    public static void ConfigureAssets()
    {
        const string path = "Assets/Resources/TownDungeonAssets.asset";
        var assets = AssetDatabase.LoadAssetAtPath<TownDungeonAssets>(path);
        if (assets == null) { assets = ScriptableObject.CreateInstance<TownDungeonAssets>(); AssetDatabase.CreateAsset(assets, path); }
        assets.Template = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset");
        if (assets.Template == null) throw new InvalidOperationException("Shared village template not found.");
        EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
        Debug.Log("Town dungeons reference the friendly-town template. Campaign fingerprint: " + CampaignFingerprint.Compute(CampaignGenerator.Generate(42)));
    }

    [MenuItem("Tools/Eternal Enigma/Town Dungeons/Capture Comparison")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture comparisons outside Play Mode.");
        Directory.CreateDirectory(Folder);
        var rows = new List<string> { "biome,variant,rooms,generation_ms,render_ms,renderers,triangles,owned_meshes" };
        foreach (var biome in new[] { OverworldBiome.Grassland, OverworldBiome.Desert, OverworldBiome.Tundra })
        foreach (bool district in new[] { false, true })
        {
            var host = new GameObject("Town comparison generator");
            var creator = host.AddComponent<TileWorldCreator>();
            var asset = DungeonPresentation.CloneTemplate(TownDungeonAssets.SharedTemplate);
            creator.twcAsset = asset; creator.worldObject = new GameObject("Town comparison");
            var style = host.AddComponent<TownBiomeStyle>(); style.OverrideBiome = true; style.Biome = biome;
            try
            {
                var clock = Stopwatch.StartNew();
                if (district) TownDungeonPresentation.Configure(creator, DungeonLayoutProfile.Options(42, biome, 2, variant: DungeonVariant.Town));
                else CoreTownLayerGenerator.Configure(asset, TownLayout.Create(42, TownServiceCatalog.All, 1, 0, 7).Options);
                creator.SetCustomRandomSeed(42); creator.ExecuteAllBlueprintLayers();
                long generation = clock.ElapsedMilliseconds; clock.Restart();
                CoreLayoutCache.ClearResultFlags(asset); creator.ExecuteAllBuildLayers(true);
                if (district)
                {
                    CoreLayoutCache.TryGetDungeon(creator, out var floor);
                    TownDungeonPresentation.Complete(creator, floor, true);
                    DungeonPresentation.PreviewScenery(creator, floor, Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Get(
                        new DungeonVisualSelection { Biome = biome, Environment = DungeonEnvironmentKind.Outdoor, UseBiomePresentation = true }));
                }
                long rendering = clock.ElapsedMilliseconds;
                CoreLayoutCache.TryGetTown(creator, out var plan);
                string prefix = Folder + "/" + biome + (district ? "-district" : "-friendly");
                float size = asset.cellSize;
                TownInteriorPreview.Capture(creator.worldObject, new Vector3(plan.Width, plan.Height, 0) * size * .5f, plan.Height * size * .50f, prefix + "-overview.png");
                foreach (var roof in creator.worldObject.GetComponentsInChildren<TownRoofVisual>(true)) roof.gameObject.SetActive(false);
                var interior = plan.Interiors.First(i => i.Spec.Kind == TownInteriorKind.Inn);
                TownInteriorPreview.Capture(creator.worldObject, new Vector3(interior.Door.X + .5f, interior.Door.Y + 4, 0) * size, size * 6, prefix + "-interior.png");
                TownInteriorPreview.Capture(creator.worldObject, new Vector3(plan.DungeonEntrance.X + .5f, plan.DungeonEntrance.Y, 0) * size, size * 6, prefix + "-gate.png");
                var filters = creator.worldObject.GetComponentsInChildren<MeshFilter>(true);
                rows.Add($"{biome},{(district ? "Town" : "Friendly")},{plan.Interiors.Count},{generation},{rendering},{creator.worldObject.GetComponentsInChildren<Renderer>(true).Length},{filters.Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3)},{creator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>(true).Sum(o => o.Meshes.Count)}");
            }
            finally
            {
                DungeonPresentation.ClearOutput(creator.worldObject); Object.DestroyImmediate(creator.worldObject);
                CoreLayoutCache.Clear(creator); Object.DestroyImmediate(host); DungeonPresentation.ReleaseTemplate(asset);
            }
        }
        File.WriteAllLines(Folder + "/render-costs.csv", rows);
        Debug.Log("Town comparisons captured in " + Folder + ".\n" + string.Join("\n", rows));
    }
}
