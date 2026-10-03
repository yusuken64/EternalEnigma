using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class BiomeDecorationIntegration
{
    [MenuItem("Tools/Eternal Enigma/Art/Inspect Decoration Runtime")]
    public static void InspectRuntime()
    {
        var game=Game.Instance;
        UnityEngine.Debug.Log($"Biome runtime: timeScale={Time.timeScale}, ready={game?.IsReady}, turn={game?.TurnManager.IsProcessingTurn}, overlay={game?.NewFloorMessage.gameObject.activeSelf}, tweens={DG.Tweening.DOTween.TotalActiveTweens()}, owners="+
            string.Join(",",Object.FindObjectsByType<EnvironmentMeshOwner>(FindObjectsSortMode.None).Select(o=>o.name)));
    }
    [MenuItem("Tools/Eternal Enigma/Art/Bind Biome Decoration Scenes")]
    public static void BindScenes()
    {
        foreach(string name in new[]{"Overworld","Town","DungeonScene","EnvironmentPlayground"}) {
            string path="Assets/Scenes/"+name+".unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try {
                foreach(var creator in scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<TileWorldCreator>(true))) {
                    var binding=creator.GetComponent<BiomeDecorationBinding>()??creator.gameObject.AddComponent<BiomeDecorationBinding>();binding.Catalog=BiomeDecorationCatalog.Load();EditorUtility.SetDirty(binding);
                }
                if(name=="Town") {
                    var creator=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<WalkableMap>(true)).First().TileWorldCreator;
                    EnvironmentTownPreview.Bake(creator,creator.twcAsset);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Eternal Enigma/Art/Audit Biome Decoration Towns")]
    public static void AuditTowns()
    {
        var lines=new List<string>{"biome,wall_props,lamp_posts,triangles,placement_ms"};
        foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) {
            var host=new GameObject("Biome audit "+biome);var creator=host.AddComponent<TileWorldCreator>();
            var asset=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset"));creator.twcAsset=asset;creator.worldObject=new GameObject("Biome audit output");
            var style=host.AddComponent<TownBiomeStyle>();style.OverrideBiome=true;style.Biome=biome;
            try {
                CoreTownLayerGenerator.Configure(asset,TownLayout.Create(42,TownServiceCatalog.All,CampaignContext.AuthoredTownBuildings,residentialBuildings:CampaignContext.ResidentialTownBuildings).Options);
                creator.SetCustomRandomSeed(42);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(asset);creator.ExecuteAllBuildLayers(true);
                var watch=Stopwatch.StartNew();BiomeDecorations.Town(creator);watch.Stop();
                var output=creator.worldObject.transform.Find("Biome decorations");var owners=output.GetComponentsInChildren<EnvironmentMeshOwner>();
                CoreLayoutCache.TryGetTown(creator,out var plan);int posts=BiomeDecorations.TownPosts(plan).Count;
                lines.Add($"{biome},{owners.Sum(o=>o.PropCount)-posts},{posts},{owners.Sum(o=>o.TriangleCount)},{watch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}");
                var first=output.GetComponentsInChildren<MeshRenderer>().FirstOrDefault();
                var center=first!=null?first.bounds.center:new Vector3(plan.Width,plan.Height,0)*asset.cellSize*.5f;
                // A district view keeps the actual road, walls and mounts in frame.
                BiomeDecorationPreview.Capture(creator.worldObject,center,12,"Docs/Art/Previews/BiomeTown_"+biome+".png");
            }
            finally {DungeonPresentation.ClearOutput(creator.worldObject);Object.DestroyImmediate(creator.worldObject);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(asset);}
        }
        Directory.CreateDirectory("Docs/Art/Verification");File.WriteAllLines("Docs/Art/Verification/BiomeTowns.csv",lines);
        UnityEngine.Debug.Log(string.Join("\n",lines));
    }
}
