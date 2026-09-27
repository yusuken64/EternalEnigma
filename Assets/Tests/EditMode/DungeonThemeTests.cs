using System;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class DungeonThemeTests
    {
        [TestCase(false)] [TestCase(true)]
        public void ThemedGroundIgnoresLegacyRaisedMapRoot(bool throne)
        {
            var source=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/"+(throne?"DungeonThroneAsset":"DungeonAsset")+".asset");
            var clone=DungeonPresentation.CloneTemplate(source);
            var host=new GameObject("Raised ground regression"); var root=new GameObject("Legacy raised map");
            DungeonPresentation.PrepareMapRoot(root.transform, true);
            Assert.That(root.transform.position.z,Is.EqualTo(-1.51f));
            Assert.That(root.transform.localScale.z,Is.EqualTo(3.35f));
            DungeonPresentation.PrepareMapRoot(root.transform, false);
            Assert.That(root.transform.position,Is.EqualTo(Vector3.zero));
            Assert.That(root.transform.localScale,Is.EqualTo(Vector3.one));
            var creator=host.AddComponent<TileWorldCreator>();creator.twcAsset=clone;creator.worldObject=root;
            try
            {
                var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
                catalog.Apply(clone,new DungeonVisualSelection {Biome=OverworldBiome.Volcanic,Environment=DungeonEnvironmentKind.Interior,UseBiomePresentation=true},throne);
                creator.SetCustomRandomSeed(12345);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(clone);creator.ExecuteAllBuildLayers(true);
                var floorLayer=clone.mapBuildLayers.OfType<DungeonThemeTileLayer>().Single(l=>l.layerName.ToLowerInvariant().Contains("floor"));
                var floor=root.transform.Find(floorLayer.layerName+"_layer");
                Assert.That(floor.position.z,Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.0001f));
                foreach(var renderer in floor.GetComponentsInChildren<MeshRenderer>())
                    Assert.That(renderer.bounds.min.z,Is.GreaterThan(-.001f),"The floor must not cut into units standing at Z=0.");
                var carpet=clone.mapBuildLayers.Single(l=>l.layerName.ToLowerInvariant().Contains("carpet"));
                if(!throne)
                {
                    Assert.That(carpet.active,Is.True,"Floor excludes the carpet mask: leaving it inactive makes holes.");
                    Assert.That(((DungeonThemeTileLayer)carpet).Preset,Is.SameAs(floorLayer.Preset),"Room centers must use textured paving too.");
                }
                else Assert.That(((DungeonThemeTileLayer)carpet).Offset.z,Is.LessThan(floorLayer.Offset.z));
            }
            finally { DungeonPresentation.ClearOutput(root);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(clone); }
        }
        [Test]
        public void SelectionMigratesAndRoundTripsWithoutOverworldGeneration()
        {
            var context=new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign,42));
            var location=context.Campaign.Locations.Last();context.State.PendingDungeon=location.Id;
            var legacy=JsonUtility.FromJson<DungeonSaveData>("{\"StartFloor\":1,\"EndFloor\":5}");
            var selected=DungeonVisualSelection.ResolveRun(legacy,context);
            Assert.That(selected.Biome,Is.EqualTo(OverworldGridGenerator.BiomeForRegion(context.Campaign,location.RegionId)));
            Assert.That(selected.Environment,Is.EqualTo(DungeonEnvironmentKind.Interior));
            Assert.That(context.IsGridGenerated,Is.False);
            var outdoor=new DungeonSaveData();
            DungeonVisualSelection.ResolveRun(outdoor,context,new DungeonEncounterVisualSettings {OverrideBiome=true,Biome=OverworldBiome.Water,Environment=DungeonEnvironmentKind.Outdoor});
            var resumed=JsonUtility.FromJson<DungeonSaveData>(JsonUtility.ToJson(outdoor));
            Assert.That(DungeonVisualSelection.ResolveRun(resumed,null).Biome,Is.EqualTo(OverworldBiome.Water));
            Assert.That(resumed.VisualSelection.Environment,Is.EqualTo(DungeonEnvironmentKind.Outdoor));
            Assert.That(DungeonVisualSelection.Resolve(null).IsLegacy,Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void AllThemesPreserveMasksSpawnsAndTemplates(bool throne)
        {
            var source=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/"+(throne?"DungeonThroneAsset":"DungeonAsset")+".asset");
            var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");Assert.That(catalog.Themes.Length,Is.EqualTo(16));
            var original=source.mapBuildLayers.OfType<InstantiateTiles>().SelectMany(t=>t.tiles).Select(t=>t.preset).ToArray();
            string expected=null;
            foreach(var theme in catalog.Themes)
            {
                var host=new GameObject("Theme test");var creator=host.AddComponent<TileWorldCreator>();var clone=DungeonPresentation.CloneTemplate(source);creator.twcAsset=clone;
                try
                {
                    creator.SetCustomRandomSeed(9173);
                    var selection=new DungeonVisualSelection {Biome=theme.Biome,Environment=theme.Environment};
                    catalog.Apply(clone,selection,throne);
                    creator.ExecuteAllBlueprintLayers();
                    Assert.That(CoreLayoutCache.TryGetDungeon(creator,out var floor),Is.True);
                    string signature=string.Join("/",floor.Layers.OrderBy(p=>p.Key).Select(p=>p.Key+":"+string.Join("",p.Value.ToArray().Cast<bool>().Select(v=>v?'1':'0'))))+floor.Start+"/"+floor.Stairs+"/"+string.Join(",",floor.Enemies)+"/"+string.Join(",",floor.Items)+"/"+string.Join(",",floor.Traps)+"/"+string.Join(",",floor.Gold);
                    if(expected==null) expected=signature;else Assert.That(signature,Is.EqualTo(expected),theme.Name);
                    Assert.That(source.mapBuildLayers.OfType<InstantiateTiles>().SelectMany(t=>t.tiles).Select(t=>t.preset),Is.EqualTo(original));
                    if(selection.IsLegacy) Assert.That(clone.mapBuildLayers.OfType<InstantiateTiles>().SelectMany(t=>t.tiles).Select(t=>t.preset),Is.EqualTo(original));
                }
                finally {UnityEngine.Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(clone);}
            }
        }

        [Test]
        public void NewTilesRespectBudgetsAndDecorationClearance()
        {
            var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
            foreach(var t in catalog.Themes) foreach(var p in new[]{t.RegularBoundary,t.ThroneBoundary,t.Floor,t.Accent})
            foreach(var go in new[]{p.edgeTile,p.exteriorCornerTile,p.interiorCornerTile,p.fillTile})
            {Assert.That(go.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3),Is.LessThanOrEqualTo(300));Assert.That(go.GetComponentsInChildren<Collider>(),Is.Empty);}
            var floor=DungeonFloorGenerator.Generate(new DungeonFloorOptions(12345));
            for(int y=0;y<floor.Height;y++) for(int x=0;x<floor.Width;x++) if(DungeonPresentation.DecorationAllowed(floor,x,y))
                Assert.That(floor.Layers[DungeonLayers.Floor][x,y],Is.False);
        }

        [Test]
        public void RepeatedThemeBuildsOwnMeshesAndDoNotConsumeGameplayRandom()
        {
            var source=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/DungeonAsset.asset");
            var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
            var host=new GameObject("Theme repeat test");var creator=host.AddComponent<TileWorldCreator>();var root=new GameObject("Theme test output");creator.worldObject=root;
            var state=UnityEngine.Random.state;
            try
            {
                foreach(var theme in catalog.Themes.Where(t=>t.Biome!=OverworldBiome.Grassland || t.Environment!=DungeonEnvironmentKind.Interior))
                {
                    var clone=DungeonPresentation.CloneTemplate(source);creator.twcAsset=clone;creator.SetCustomRandomSeed(12345);
                    try
                    {
                        catalog.Apply(clone,new DungeonVisualSelection {Biome=theme.Biome,Environment=theme.Environment},false);
                        creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(clone);CoreLayoutCache.TryGetDungeon(creator,out var floor);
                        for(int repeat=0;repeat<2;repeat++)
                        {
                            var old=root.GetComponentsInChildren<EnvironmentMeshOwner>().SelectMany(o=>o.Meshes).ToArray();
                            DungeonPresentation.ClearOutput(root);foreach(var mesh in old)Assert.That(mesh==null,Is.True);
                            var before=UnityEngine.Random.state;creator.ExecuteAllBuildLayers(true);DungeonPresentation.Decorate(creator,floor,theme);
                            Assert.That(UnityEngine.Random.state,Is.EqualTo(before));
                            var cosmetics=root.transform.Find("Theme cosmetics").GetComponent<EnvironmentMeshOwner>();
                            Assert.That(cosmetics.PropCount,Is.LessThanOrEqualTo(16*((floor.Width+31)/32)*((floor.Height+31)/32)));
                            foreach(var mesh in cosmetics.Meshes) foreach(var vertex in mesh.vertices)
                            {
                                int x=Mathf.FloorToInt(vertex.x/clone.cellSize),y=Mathf.FloorToInt(vertex.y/clone.cellSize);
                                Assert.That(DungeonPresentation.DecorationAllowed(floor,x,y),Is.True,"Full transformed prop footprint must stay outside walkable cells.");
                            }
                            Assert.That(root.transform.Cast<Transform>().Count(t=>t.name=="Theme cosmetics"),Is.EqualTo(1));
                        }
                    }
                    finally {DungeonPresentation.ReleaseTemplate(clone);}
                }
            }
            finally {DungeonPresentation.ClearOutput(root);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(host);UnityEngine.Random.state=state;}
        }
    }
}
