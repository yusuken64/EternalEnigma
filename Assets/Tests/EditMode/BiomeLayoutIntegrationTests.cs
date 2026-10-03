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
