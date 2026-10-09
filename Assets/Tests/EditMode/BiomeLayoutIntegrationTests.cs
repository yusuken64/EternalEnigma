using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class BiomeLayoutIntegrationTests
    {
        [TestCase(OverworldBiome.Grassland)] [TestCase(OverworldBiome.Forest)]
        [TestCase(OverworldBiome.Desert)] [TestCase(OverworldBiome.Water)]
        [TestCase(OverworldBiome.Mountain)] [TestCase(OverworldBiome.Tundra)]
        [TestCase(OverworldBiome.Marsh)] [TestCase(OverworldBiome.Volcanic)]
        public void LootAndSceneryStandOnRenderedFloor(OverworldBiome biome)
        {
            var template=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/DungeonAsset.asset");
            foreach(int seed in new[]{42,12345})
            foreach(var environment in new[]{DungeonEnvironmentKind.Interior,DungeonEnvironmentKind.Outdoor})
            foreach(bool legacy in new[]{true,false})
            {
                var asset=DungeonPresentation.CloneTemplate(template);
                var host=new GameObject("Loot terrain regression");var root=new GameObject("Loot terrain output");
                var creator=host.AddComponent<TileWorldCreator>();creator.twcAsset=asset;creator.worldObject=root;
                try
                {
                    CoreDungeonLayerGenerator.Configure(asset,legacy?new DungeonFloorOptions(seed):DungeonLayoutProfile.Options(seed,biome,0));
                    Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Apply(asset,
                        new DungeonVisualSelection {Biome=biome,Environment=environment,UseBiomePresentation=true},false);
                    creator.SetCustomRandomSeed(seed);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(asset);
                    creator.ExecuteAllBuildLayers(true);Assert.That(CoreLayoutCache.TryGetDungeon(creator,out var floor),Is.True);
                    foreach(var layer in asset.mapBlueprintLayers)
                    {
                        var generator=layer.stack.Select(s=>s.action).OfType<CoreDungeonLayerGenerator>().FirstOrDefault();
                        if(generator!=null) Assert.That(layer.map,Is.EqualTo(floor.Layers[generator.LayerName].ToArray()),layer.layerName);
                    }
                    var walls=root.transform.Find("Dungeon_layer").GetComponentsInChildren<MeshFilter>()
                        .Select(f=>{var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;return c;}).ToArray();
                    var paving=new[]{"Floor_layer","Carpet_layer"}.SelectMany(name=>root.transform.Find(name).GetComponentsInChildren<MeshFilter>())
                        .Select(f=>{var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;return c;}).ToArray();
                    Physics.SyncTransforms();
                    foreach(var cell in floor.Items.Select(p=>p.Cell).Concat(floor.Gold.Select(p=>p.Cell)).Concat(floor.Scenery.Select(p=>p.Cell)))
                    foreach(float dx in new[]{-.34f,0,.34f}) foreach(float dy in new[]{-.34f,0,.34f})
                    {
                        var ray=new Ray(new Vector3((cell.X+.5f+dx)*asset.cellSize,(cell.Y+.5f+dy)*asset.cellSize,-8),Vector3.forward);
                        Assert.That(walls.Any(c=>c.Raycast(ray,out var hit,8)),Is.False,$"{biome} {environment} legacy {legacy} seed {seed}: wall overlaps loot/scenery at {cell} ({dx},{dy}).");
                        Assert.That(paving.Any(c=>c.Raycast(ray,out var hit,9)),Is.True,$"{biome} {environment} legacy {legacy} seed {seed}: no rendered floor below loot/scenery at {cell} ({dx},{dy}).");
                        var foot=new Vector3(ray.origin.x,ray.origin.y,-.01f);
                        var view=new Vector3(0,12,14).normalized;
                        Assert.That(walls.Any(c=>c.Raycast(new Ray(foot-view*8,view),out var hit,7.99f) &&
                            c.GetComponentInParent<DungeonBoundaryCutaway>()?.Cuts(hit.point,view)!=true),Is.False,
                            $"{biome} {environment} legacy {legacy} seed {seed}: raised terrain covers loot/scenery at {cell} ({dx},{dy}) from the gameplay camera.");
                    }
                }
                finally {DungeonPresentation.ClearOutput(root);Object.DestroyImmediate(root);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(asset);}
            }
        }

        [Test]
        public void LayoutChoiceAndGrasslandPresentationAreIndependent()
        {
            var save = new DungeonSaveData { UseBiomeLayout=true,LayoutTier=4,LayoutBiome=OverworldBiome.Marsh };
            var copy = JsonUtility.FromJson<DungeonSaveData>(JsonUtility.ToJson(save));
            Assert.That(copy.UseBiomeLayout,Is.True); Assert.That(copy.LayoutTier,Is.EqualTo(4)); Assert.That(copy.LayoutBiome,Is.EqualTo(OverworldBiome.Marsh));
            Assert.That(new DungeonVisualSelection { Biome=OverworldBiome.Grassland,UseBiomePresentation=true }.IsLegacy,Is.False);
        }

        [Test]
        public void EveryLayerUsesSameProfileAndReskinPreservesResult()
        {
            var template=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/DungeonAsset.asset");
            int originalWidth=template.mapWidth;
            var asset=DungeonPresentation.CloneTemplate(template);
            var host=new GameObject("Biome test");var creator=host.AddComponent<TileWorldCreator>();creator.twcAsset=asset;
            try
            {
                var options=DungeonLayoutProfile.Options(42,OverworldBiome.Mountain,4);
                CoreDungeonLayerGenerator.Configure(asset,options);creator.SetCustomRandomSeed(42);creator.ExecuteAllBlueprintLayers();
                var actions=asset.mapBlueprintLayers.SelectMany(l=>l.stack).Select(s=>s.action).OfType<CoreDungeonLayerGenerator>().ToArray();
                Assert.That(actions.Length,Is.GreaterThan(0));
                foreach(var action in actions) Assert.That(action.Options(creator),Is.EqualTo(options));
                creator.ExecuteAllBlueprintLayers();Assert.That(CoreLayoutCache.TryGetDungeon(creator,out var first),Is.True);
                var expected=DungeonFloorGenerator.Generate(options);
                Assert.That(first.Layers[DungeonLayers.Floor].ToArray(),Is.EqualTo(expected.Layers[DungeonLayers.Floor].ToArray()));
                Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Apply(asset,new DungeonVisualSelection {Biome=OverworldBiome.Desert,UseBiomePresentation=true},false);
                creator.ExecuteAllBlueprintLayers();CoreLayoutCache.TryGetDungeon(creator,out var second);Assert.That(second,Is.SameAs(first));
                CoreDungeonLayerGenerator.Configure(asset,DungeonLayoutProfile.Options(42,OverworldBiome.Forest,4));
                creator.ExecuteAllBlueprintLayers();CoreLayoutCache.TryGetDungeon(creator,out var third);Assert.That(third,Is.Not.SameAs(first));
                Assert.That(template.mapWidth,Is.EqualTo(originalWidth));
            }
            finally { Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(asset); }
        }
    }
}
