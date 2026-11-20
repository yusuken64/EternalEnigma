using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using TWC;
using Object = UnityEngine.Object;

public static class GameplayPresentationAudit
{
    [MenuItem("Tools/Eternal Enigma/Presentation/Capture Finale")]
    public static void CaptureFinale()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before capturing terrain.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var root = new GameObject("Finale presentation review");
        var creator = root.AddComponent<TileWorldCreator>();
        creator.worldObject = new GameObject("Finale terrain");
        creator.worldObject.transform.SetParent(root.transform, false);
        try
        {
            Directory.CreateDirectory("Temp/GameplayPresentation");
            var campaign = CampaignGenerator.Generate(42);
            var grid = OverworldGridGenerator.Generate(campaign);
            var map = root.AddComponent<CampaignOverworld>();
            map.StyleProfile = Resources.Load<OverworldStyleProfile>("EnvironmentKit/OverworldStyle");
            map.BridgeCapabilityCrossings = true;
            map.Apply(grid); map.BuildMeshes();
            float size = creator.twcAsset.cellSize;
            var crossings = grid.Locks.SelectMany(l => l.Cells).Where(grid.RequiresBoat).Distinct().ToArray();
            foreach (var cell in crossings.Where((p, i) => i == 0 || i == crossings.Length / 2).Append(grid.Locations[campaign.FinalLocationId]))
                TownInteriorPreview.Capture(creator.worldObject, new Vector3(cell.X + .5f, cell.Y + .5f, 0) * size,
                    size * 6, $"Temp/GameplayPresentation/finale-{cell.X}-{cell.Y}.png");
            var rows = creator.worldObject.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Distinct().Select(m => m == null ? "MISSING MATERIAL" :
                    $"{m.name} | {m.shader?.name} | " + string.Join(", ", m.GetTexturePropertyNames()
                        .Select(p => p + "=" + AssetDatabase.GetAssetPath(m.GetTexture(p)))));
            File.WriteAllLines("Temp/GameplayPresentation/finale-materials.txt", rows);
            Debug.Log("Captured final tower crossings and entrance terrain.");
        }
        catch (Exception exception) { Debug.LogException(exception); throw; }
        finally
        {
            DungeonPresentation.ClearOutput(creator.worldObject); Object.DestroyImmediate(root);
            EditorSceneManager.CloseScene(preview, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        }
    }

    [MenuItem("Tools/Eternal Enigma/Presentation/Apply Directional Effects")]
    public static void ApplyDirectionalEffects()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:CombatEffectProfile", new[] { "Assets/Resources/CombatEffects" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var stage in new[] { profile.Impact, profile.Area })
                if (stage.Prefab != null && (stage.Prefab.name.Contains("Slash") || stage.Prefab.name.Contains("Cleave")))
                { stage.AlignWithCastDirection = true; EditorUtility.SetDirty(profile); }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Eternal Enigma/Presentation/Capture Dungeons")]
    public static void CaptureDungeons()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before capturing dungeons.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Directory.CreateDirectory("Temp/GameplayPresentation");
        var rows = new List<string>();
        try
        {
            foreach (bool district in new[] { true, false })
            foreach (var biome in new[] { OverworldBiome.Grassland, OverworldBiome.Volcanic })
            {
                var host = new GameObject("Presentation review generator");
                var creator = host.AddComponent<TileWorldCreator>();
                var template = district ? TownDungeonAssets.SharedTemplate : AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/DungeonAsset.asset");
                var asset = DungeonPresentation.CloneTemplate(template);
                creator.twcAsset = asset; creator.worldObject = new GameObject("Presentation review");
                try
                {
                    var selection = new DungeonVisualSelection { Biome = biome, Environment = DungeonEnvironmentKind.Interior, UseBiomePresentation = true };
                    var catalog = Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
                    var options = DungeonLayoutProfile.Options(42, biome, 4, variant: district ? DungeonVariant.Town : DungeonVariant.Standard);
                    if (district) TownDungeonPresentation.Configure(creator, options);
                    else { catalog.Apply(asset, selection, false); CoreDungeonLayerGenerator.Configure(asset, options); }
                    creator.SetCustomRandomSeed(42); creator.ExecuteAllBlueprintLayers();
                    CoreLayoutCache.ClearResultFlags(asset); creator.ExecuteAllBuildLayers(true);
                    CoreLayoutCache.TryGetDungeon(creator, out var floor);
                    if (district) TownDungeonPresentation.Complete(creator, floor, true);
                    else DungeonPresentation.Decorate(creator, floor, catalog.Get(selection));
                    DungeonPresentation.PreviewScenery(creator, floor, catalog.Get(selection));
                    float size = asset.cellSize;
                    string prefix = "Temp/GameplayPresentation/" + biome + (district ? "-town" : "-dungeon");
                    TownInteriorPreview.Capture(creator.worldObject, new Vector3(floor.Width, floor.Height, 0) * size * .5f, floor.Height * size * .5f, prefix + ".png");
                    var center = district ? floor.Stairs : floor.Start;
                    TownInteriorPreview.Capture(creator.worldObject, new Vector3(center.X + .5f, center.Y + .5f, -1) * size, size * 6, prefix + "-detail.png");
                    foreach (var renderer in creator.worldObject.GetComponentsInChildren<Renderer>())
                        foreach (var material in renderer.sharedMaterials)
                            rows.Add($"{prefix} | {renderer.name} | {material?.name} | {material?.shader?.name} | main=" +
                                (material != null && material.HasProperty("_MainTex") ? AssetDatabase.GetAssetPath(material.GetTexture("_MainTex")) : "none"));
                }
                finally
                {
                    DungeonPresentation.ClearOutput(creator.worldObject); Object.DestroyImmediate(creator.worldObject);
                    CoreLayoutCache.Clear(creator); Object.DestroyImmediate(host); DungeonPresentation.ReleaseTemplate(asset);
                }
            }
            File.WriteAllLines("Temp/GameplayPresentation/generated-materials.txt", rows);
        }
        catch (Exception exception) { Debug.LogException(exception); throw; }
        finally
        {
            EditorSceneManager.CloseScene(preview, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        }
    }

    [MenuItem("Tools/Eternal Enigma/Presentation/Audit Report")]
    public static void Report()
    {
        try
        {
            var rows = new List<string>();
            var town = TownSceneLoader.Default;
            foreach (var ally in town.AllyCatalog)
                rows.Add($"ALLY {ally.Id} | {ally.Name} | {AssetDatabase.GetAssetPath(ally)} | starting={town.StartingParty.Contains(ally)} | recruit={town.Recruits.Any(r => r.Ally == ally)}");
            var roots = AssetDatabase.FindAssets("", new[] { "Assets/Resources", "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (var path in AssetDatabase.GetDependencies(roots, true).Where(p => p.EndsWith(".mat")))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                rows.Add($"MATERIAL {path} | {material.shader?.name} | renderType={material.GetTag("RenderType", false)} | textures=" +
                    string.Join(", ", material.GetTexturePropertyNames().Select(p => p + "=" + AssetDatabase.GetAssetPath(material.GetTexture(p)))));
            }
            var kit = EnvironmentKit.Load();
            foreach (var id in new[] { "Gate", "Wall", "DungeonPortal" })
                rows.Add($"BOUNDS {id} | {kit.Mesh(id).bounds}");
            void Labels(GameObject root, string path)
            {
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                    rows.Add($"LABEL {path} | {AnimationUtility.CalculateTransformPath(label.transform, root.transform)} | {label.font?.name} | {label.text?.Replace('\n', ' ')}");
            }
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources/UI" }).Select(AssetDatabase.GUIDToAssetPath))
                Labels(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
            foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try { foreach (var root in scene.GetRootGameObjects()) Labels(root, path); }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Directory.CreateDirectory("Temp/GameplayPresentation");
            File.WriteAllLines("Temp/GameplayPresentation/audit.txt", rows);
            Debug.Log($"Presentation audit: {rows.Count} rows in Temp/GameplayPresentation/audit.txt");
        }
        catch (Exception exception) { Debug.LogException(exception); throw; }
    }
}
