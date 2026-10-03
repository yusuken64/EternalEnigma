using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class BiomeDecorationTests
    {
        [Test]
        public void CatalogIsCompleteBoundedAndNonInteractive()
        {
            var catalog=BiomeDecorationCatalog.Load();Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Assets.Length,Is.EqualTo(48));
            foreach(var a in catalog.Assets){Assert.That(a.Mesh,Is.Not.Null);Assert.That(a.Prefab,Is.Not.Null);
                Assert.That(a.Mesh.triangles.Length/3,Is.LessThanOrEqualTo(a.Kind==BiomeDecorationKind.LampPost||a.Kind==BiomeDecorationKind.SignPost?240:120));
                Assert.That(a.Prefab.GetComponentsInChildren<Collider>(true),Is.Empty);Assert.That(a.Prefab.GetComponentsInChildren<Light>(true),Is.Empty);
                Assert.That(a.Prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true),Is.Empty);
                Assert.That(a.Prefab.GetComponentsInChildren<MonoBehaviour>(true),Is.Empty);
                if(a.Effect!=null){Assert.That(a.Effect.GetComponentsInChildren<Light>(true),Is.Empty);Assert.That(a.Effect.GetComponentsInChildren<Collider>(true),Is.Empty);}
            }
            foreach(OverworldBiome b in Enum.GetValues(typeof(OverworldBiome)))Assert.That(catalog.Get(b,BiomeDecorationKind.SignPost).Mesh.triangles.Length/3+3*catalog.Get(b,BiomeDecorationKind.SignPanel).Mesh.triangles.Length/3,Is.LessThanOrEqualTo(240));
            Assert.That(catalog.Facades.Length,Is.GreaterThanOrEqualTo(3));
            foreach(var facade in catalog.Facades)foreach(var socket in facade.Sockets) {
                var bounds=new Bounds(socket.Center,new Vector3(socket.Normal.x==0?socket.Width:.02f,socket.Normal.y==0?socket.Width:.02f,socket.Height));
                Assert.That(facade.Exclusions.Any(b=>b.Intersects(bounds)),Is.False,facade.Model+" socket intersects an opening");
            }
        }
        [Test]
        public void SpacingCrossesChunkBoundariesAndDoesNotConsumeRandom()
        {
            var cells=Enumerable.Range(-35,105).SelectMany(x=>Enumerable.Range(-2,5).Select(y=>new Vector2Int(x,y))).ToArray();var before=UnityEngine.Random.state;
            var a=BiomeDecorationPlacement.SelectCells(cells,42,"walls",3,24);var b=BiomeDecorationPlacement.SelectCells(cells.Reverse(),42,"walls",3,24);
            Assert.That(a,Is.EqualTo(b));Assert.That(UnityEngine.Random.state,Is.EqualTo(before));
            for(int i=0;i<a.Count;i++)for(int j=0;j<i;j++)Assert.That((a[i]-a[j]).sqrMagnitude,Is.GreaterThanOrEqualTo(9));
        }
        [Test]
        public void GraphRejectsDiagonalWarpAndImmediateReverseAndCollapsesJunctions()
        {
            var graph=new BiomeRoadGraph();graph.Connect(Vector2Int.zero,new Vector2Int(1,1));Assert.That(graph.Edges,Is.Empty);
            for(int x=-3;x<=3;x++)graph.Connect(new Vector2Int(x,0),new Vector2Int(x+1,0));
            graph.Connect(Vector2Int.zero,Vector2Int.up);graph.Connect(Vector2Int.right,Vector2Int.one);
            Assert.That(graph.Junctions(),Has.Count.EqualTo(1));
            var towns=new Dictionary<Vector2Int,string>{{new Vector2Int(-3,0),"west"},{new Vector2Int(4,0),"east"}};
            Assert.That(graph.Nearest(Vector2Int.right,new HashSet<Vector2Int>{Vector2Int.zero},towns).town,Is.EqualTo("east"));
            graph.Connect(new Vector2Int(4,0),new Vector2Int(30,0));Assert.That(graph.Edges.ContainsKey(new Vector2Int(30,0)),Is.False);
        }
        [Test]
        public void RealWorldSignsAreDeterministicSupportedAndReachRealTowns()
        {
            var context=new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign,42));var grid=context.Grid;var graph=BiomeRoadGraph.From(grid);
            var towns=grid.TownFootprints.ToDictionary(t=>new Vector2Int(t.Entrance.X,t.Entrance.Y),t=>t.LocationId);
            var a=BiomeRoadSigns.Plan(grid);var b=BiomeRoadSigns.Plan(grid);Assert.That(a.Count,Is.GreaterThan(0));Assert.That(a.Select(s=>s.Verge),Is.EqualTo(b.Select(s=>s.Verge)));
            foreach(var s in a){Assert.That(graph.Edges.ContainsKey(s.Verge),Is.False);Assert.That(grid.IsGround(new GridPoint(s.Verge.x,s.Verge.y)),Is.True);
                Assert.That(grid.Layers[OverworldLayers.Water].At(new GridPoint(s.Verge.x,s.Verge.y)),Is.False);
                foreach(var layer in new[]{OverworldLayers.Bridges,OverworldLayers.TownFootprints,OverworldLayers.Mountains,OverworldLayers.Trees,OverworldLayers.Locks})
                    Assert.That(grid.Layers[layer].At(new GridPoint(s.Verge.x,s.Verge.y)),Is.False,layer);
                Assert.That(grid.Locks.All(l=>l.Cells.All(p=>Math.Max(Math.Abs(p.X-s.Verge.x),Math.Abs(p.Y-s.Verge.y))>2)),Is.True);
                Assert.That(s.Branches.Length,Is.InRange(1,3));foreach(var branch in s.Branches){Assert.That(context.GetTownDisplayName(branch.TownId),Is.Not.Empty);
                    Assert.That(s.JunctionCells.Contains(branch.FirstCell-branch.Direction),Is.True);
                    var nearest=graph.Nearest(branch.FirstCell,s.JunctionCells,towns);Assert.That(nearest.town,Is.EqualTo(branch.TownId));Assert.That(nearest.distance,Is.EqualTo(branch.Distance));}}
            for(int i=0;i<a.Count;i++)for(int j=0;j<i;j++)Assert.That((a[i].Verge-a[j].Verge).sqrMagnitude,Is.GreaterThanOrEqualTo(36));
            Assert.That(a.GroupBy(s=>(s.Verge.x/32,s.Verge.y/32)).All(g=>g.Count()<=8),Is.True);
        }
        [Test]
        public void RoadSearchHandlesBendsLoopsAndDisconnectedTowns()
        {
            var graph=new BiomeRoadGraph();var loop=new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(2,1),new Vector2Int(2,2),new Vector2Int(1,2),new Vector2Int(0,2),new Vector2Int(0,1),new Vector2Int(0,0)};
            for(int i=1;i<loop.Length;i++)graph.Connect(loop[i-1],loop[i]);
            graph.Connect(new Vector2Int(50,50),new Vector2Int(51,50));
            var towns=new Dictionary<Vector2Int,string>{{new Vector2Int(2,2),"loop-town"},{new Vector2Int(51,50),"disconnected"},{Vector2Int.zero,"behind"}};
            var answer=graph.Nearest(Vector2Int.right,new HashSet<Vector2Int>{Vector2Int.zero},towns);
            Assert.That(answer.town,Is.EqualTo("loop-town"));Assert.That(answer.distance,Is.EqualTo(4));
            Assert.That(graph.Nearest(new Vector2Int(50,50),new HashSet<Vector2Int>(),towns).town,Is.EqualTo("disconnected"));
        }
        [Test]
        public void TownRoadVergesRespectClearance()
        {
            for(int seed=0;seed<16;seed++) {
                var plan=TownPlanGenerator.Generate(TownLayout.Create(seed,TownServiceCatalog.All,CampaignContext.AuthoredTownBuildings,residentialBuildings:CampaignContext.ResidentialTownBuildings).Options);
                var posts=BiomeDecorations.TownPosts(plan);Assert.That(posts.Count,Is.LessThanOrEqualTo(24));
                foreach(var p in posts){Assert.That(plan.Layers[TownLayers.Roads][p.x,p.y],Is.False);Assert.That(plan.Layers[TownLayers.Walkable][p.x,p.y],Is.True);Assert.That(plan.BuildingSlots.All(q=>Math.Max(Math.Abs(q.X-p.x),Math.Abs(q.Y-p.y))>2),Is.True);}
                for(int i=0;i<posts.Count;i++)for(int j=0;j<i;j++)Assert.That((posts[i]-posts[j]).sqrMagnitude,Is.GreaterThanOrEqualTo(16));
            }
        }
        [Test]
        public void DerivedTownMasksUseTheSameLayoutContract()
        {
            var clone=DungeonPresentation.CloneTemplate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset"));
            try {
                var options=TownLayout.Create(42,TownServiceCatalog.All,CampaignContext.AuthoredTownBuildings,residentialBuildings:CampaignContext.ResidentialTownBuildings).Options;
                CoreTownLayerGenerator.Configure(clone,options);
                foreach(var action in clone.mapBlueprintLayers.SelectMany(l=>l.stack).Select(s=>s.action).OfType<CoreTownLayerGenerator>()) {
                    Assert.That(action.SpineX,Is.EqualTo(options.SpineX));Assert.That(action.Detailed,Is.EqualTo(options.Detailed));
                    Assert.That(action.BuildingCount,Is.EqualTo(options.BuildingCount));Assert.That(action.PartySpawnX,Is.EqualTo(options.PartySpawn.X));
                }
            }
            finally{DungeonPresentation.ReleaseTemplate(clone);}
        }
        [UnityTest] public IEnumerator RegularDungeonCombinations() => DungeonCombinations(false);
        [UnityTest] public IEnumerator ThroneDungeonCombinations() => DungeonCombinations(true);
        [Test]
        public void TownNamesRoundTripThroughUnitySaveSerialization()
        {
            var context=new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign,42));
            var snapshot=context.Capture();snapshot.TownNames=Array.Empty<TownDisplayName>();snapshot.NamingVersion=0;
            var restored=new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign),JsonUtility.FromJson<CampaignSnapshot>(JsonUtility.ToJson(snapshot)));
            var saved=JsonUtility.FromJson<CampaignSnapshot>(JsonUtility.ToJson(restored.Capture()));
            Assert.That(saved.TownNames.Select(t=>t.Name),Is.EqualTo(restored.State.TownNames.Select(t=>t.Name)));
            Assert.That(saved.NamingVersion,Is.EqualTo(TownNameGenerator.Version));Assert.That(saved.Fingerprint,Is.EqualTo(snapshot.Fingerprint));
        }
        [UnityTest]
        public IEnumerator LegacyReplacementPreservesCompleteBuildRandomState()
        {
            var source=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/DungeonAsset.asset");
            var selection=new DungeonVisualSelection{Biome=OverworldBiome.Grassland,Environment=DungeonEnvironmentKind.Interior};
            var savedRandom=UnityEngine.Random.state;bool savedEnabled=BiomeDecorations.Enabled;
            UnityEngine.Random.State? baseline=null;Dictionary<string,bool[,]> masks=null;
            try { foreach(bool enabled in new[]{false,true}) {
                BiomeDecorations.Enabled=enabled;
                var host=new GameObject("Random stream comparison");var creator=host.AddComponent<TileWorldCreator>();
                var clone=DungeonPresentation.CloneTemplate(source);creator.twcAsset=clone;creator.worldObject=new GameObject("Random comparison output");DungeonPresentation.PrepareMapRoot(creator.worldObject.transform,true);
                try {
                    Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Apply(clone,selection,false);creator.SetCustomRandomSeed(12345);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(clone);
                    CoreLayoutCache.TryGetDungeon(creator,out var floor);bool complete=false;creator.OnBuildLayersComplete+=_=>complete=true;creator.ExecuteAllBuildLayers(true);
                    for(int wait=0;!complete&&wait<600;wait++)yield return null;Assert.That(complete,Is.True);
                    BiomeDecorations.Dungeon(creator,floor,selection,true);
                    if(baseline.HasValue){Assert.That(UnityEngine.Random.state,Is.EqualTo(baseline.Value));foreach(var p in masks)Assert.That(floor.Layers[p.Key].ToArray(),Is.EqualTo(p.Value));}
                    else {baseline=UnityEngine.Random.state;masks=floor.Layers.ToDictionary(p=>p.Key,p=>p.Value.ToArray());}
                }
                finally {DungeonPresentation.ClearOutput(creator.worldObject);Object.DestroyImmediate(creator.worldObject);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(clone);}
            }} finally {BiomeDecorations.Enabled=savedEnabled;UnityEngine.Random.state=savedRandom;}
        }
        private IEnumerator DungeonCombinations(bool throne)
        {
            var source=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/"+(throne?"DungeonThroneAsset":"DungeonAsset")+".asset");
            var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
            foreach(var theme in catalog.Themes) {
                var host=new GameObject("Decoration dungeon test");var creator=host.AddComponent<TileWorldCreator>();var clone=DungeonPresentation.CloneTemplate(source);creator.twcAsset=clone;
                var selection=new DungeonVisualSelection{Biome=theme.Biome,Environment=theme.Environment};
                creator.worldObject=new GameObject("Dungeon test output");DungeonPresentation.PrepareMapRoot(creator.worldObject.transform,selection.IsLegacy);
                try {
                    catalog.Apply(clone,selection,throne);creator.SetCustomRandomSeed(12345);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(clone);CoreLayoutCache.TryGetDungeon(creator,out var floor);
                    bool complete=false;creator.OnBuildLayersComplete+=_=>complete=true;creator.ExecuteAllBuildLayers(true);
                    for(int wait=0;!complete&&wait<600;wait++)yield return null;
                    Assert.That(complete,Is.True,"TWC build timed out");
                    var before=UnityEngine.Random.state;var masks=floor.Layers.ToDictionary(p=>p.Key,p=>p.Value.ToArray());
                    BiomeDecorations.Dungeon(creator,floor,selection,true);var first=creator.worldObject.transform.Find("Biome decorations");
                    Assert.That(first,Is.Not.Null);var meshes=first.GetComponentsInChildren<EnvironmentMeshOwner>().SelectMany(o=>o.Meshes).ToArray();
                    foreach(var fog in first.GetComponentsInChildren<BiomeDecorationFog>()) {
                        var p=fog.FloorPosition/creator.twcAsset.cellSize;
                        Assert.That(p.x,Is.EqualTo(Mathf.Round(p.x)).Within(.001));Assert.That(p.y,Is.EqualTo(Mathf.Round(p.y)).Within(.001));
                        Assert.That(floor.Layers[DungeonLayers.Floor].At(new GridPoint(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y))),Is.True);
                    }
                    Assert.That(first.GetComponentsInChildren<EnvironmentMeshOwner>().Sum(o=>o.PropCount),Is.GreaterThan(0),theme.Name+" must have visible decorations. Surfaces="+creator.worldObject.GetComponentsInChildren<BiomeDecorationSurfaceSet>().Sum(s=>s.Faces.Count)+" Renderers="+string.Join(",",creator.worldObject.GetComponentsInChildren<MeshFilter>().Take(12).Select(f=>f.name+"/"+f.transform.parent.name+"/"+f.sharedMesh.bounds)));
                    BiomeDecorations.Dungeon(creator,floor,selection,true);Assert.That(meshes.All(m=>m==null),Is.True);
                    Assert.That(UnityEngine.Random.state,Is.EqualTo(before));foreach(var p in masks)Assert.That(floor.Layers[p.Key].ToArray(),Is.EqualTo(p.Value));
                    Assert.That(creator.worldObject.transform.Cast<Transform>().Count(t=>t.name=="Biome decorations"),Is.EqualTo(1));
                }
                finally {if(creator.worldObject!=null){DungeonPresentation.ClearOutput(creator.worldObject);Object.DestroyImmediate(creator.worldObject);}Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(clone);}
            }
        }
    }
}
