using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class CoreLayerGeneratorTests
    {
        private const string DungeonPath = "Assets/Prefabs/Dungeon/DungeonAsset.asset";
        private const string ThronePath = "Assets/Prefabs/Dungeon/DungeonThroneAsset.asset";
        private const string VillagePath = "Assets/TileWorldCreator/VillageLSystemAsset.asset";

        private GameObject host;
        private TileWorldCreator creator;
        private readonly List<TileWorldCreatorAsset> clones = new();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Core layer generator test");
            creator = host.AddComponent<TileWorldCreator>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var clone in clones)
            {
                if (clone == null) continue;
                foreach (var layer in clone.mapBlueprintLayers)
                {
                    if (layer.previewTextureMap != null) Object.DestroyImmediate(layer.previewTextureMap);
                }
                Object.DestroyImmediate(clone);
            }
            clones.Clear();
            if (host != null) Object.DestroyImmediate(host);
        }

        /// Loads the asset at `path`, instantiates a DontSave clone, assigns it to `creator.twcAsset`,
        /// and registers it for TearDown cleanup. Mirrors CampaignOverworldTests's template/clone pattern.
        private TileWorldCreatorAsset LoadClone(string path)
        {
            var original = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(path);
            Assert.That(original, Is.Not.Null, $"Could not load asset at '{path}'.");
            var clone = Object.Instantiate(original);
            clone.hideFlags = HideFlags.DontSave;
            clones.Add(clone);
            creator.twcAsset = clone;
            return clone;
        }

        [Test]
        public void AssetsAreCoreBackedAndBuildLayerGuidsResolve()
        {
            var dungeon = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(DungeonPath);
            var throne = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(ThronePath);
            var village = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(VillagePath);

            Assert.That(CoreLayerAuthoring.Verify(dungeon, CoreLayerNames.Dungeon), Is.Empty);
            Assert.That(CoreLayerAuthoring.Verify(throne, CoreLayerNames.Dungeon), Is.Empty);
            Assert.That(CoreLayerAuthoring.Verify(village, CoreLayerNames.Town), Is.Empty);
        }

        [Test]
        public void DungeonAssetExecutesToCoreLayersOnce()
        {
            var clone = LoadClone(DungeonPath);
            creator.SetCustomRandomSeed(1234);
            int generationsBefore = CoreLayoutCache.Generations;

            creator.ExecuteAllBlueprintLayers();

            var expected = DungeonFloorGenerator.Generate(new DungeonFloorOptions(1234, 32, 32));
            foreach (var layer in clone.mapBlueprintLayers)
            {
                if (!CoreLayerNames.Dungeon.Contains(layer.layerName)) continue;

                var actual = creator.GetMapOutputFromBlueprintLayer(layer.layerName);
                var expectedCells = expected.Layers[layer.layerName].ToArray();
                Assert.That(actual.Cast<bool>(), Is.EqualTo(expectedCells.Cast<bool>()), $"Layer '{layer.layerName}' mismatch.");
            }

            Assert.That(CoreLayoutCache.Generations, Is.EqualTo(generationsBefore + 1));
            Assert.That(CoreLayoutCache.TryGetDungeon(creator, out var floor), Is.True);
            Assert.That(floor.Seed, Is.EqualTo(1234));
        }

        [Test]
        public void ThroneAssetIsTwelveByTwelveThrone()
        {
            LoadClone(ThronePath);
            creator.SetCustomRandomSeed(1234);

            creator.ExecuteAllBlueprintLayers();

            Assert.That(CoreLayoutCache.TryGetDungeon(creator, out var floor), Is.True);
            Assert.That(floor.IsThroneFloor, Is.True);
            Assert.That(floor.Width, Is.EqualTo(12));
            Assert.That(floor.Start, Is.EqualTo(new GridPoint(6, 4)));
            Assert.That(floor.Stairs, Is.EqualTo(new GridPoint(6, 9)));
        }

        [Test]
        public void SameSeedSameMasksDifferentSeedDiffers()
        {
            LoadClone(DungeonPath);

            creator.SetCustomRandomSeed(7);
            creator.ExecuteAllBlueprintLayers();
            var firstSeedSevenFloor = creator.GetMapOutputFromBlueprintLayer(DungeonLayers.Floor);

            creator.SetCustomRandomSeed(7);
            creator.ExecuteAllBlueprintLayers();
            var secondSeedSevenFloor = creator.GetMapOutputFromBlueprintLayer(DungeonLayers.Floor);

            Assert.That(secondSeedSevenFloor.Cast<bool>(), Is.EqualTo(firstSeedSevenFloor.Cast<bool>()));

            creator.SetCustomRandomSeed(8);
            creator.ExecuteAllBlueprintLayers();
            var seedEightFloor = creator.GetMapOutputFromBlueprintLayer(DungeonLayers.Floor);

            Assert.That(seedEightFloor.Cast<bool>(), Is.Not.EqualTo(firstSeedSevenFloor.Cast<bool>()));
        }

        [Test]
        public void TownAssetConfiguredFromDefaultTownMatchesCorePlan()
        {
            var clone = LoadClone(VillagePath);
            var config = Resources.Load<TownConfiguration>("Towns/DefaultTown");
            Assert.That(config, Is.Not.Null, "Resources/Towns/DefaultTown.asset must exist.");

            CoreTownLayerGenerator.Configure(clone, config);
            creator.SetCustomRandomSeed(99);

            creator.ExecuteAllBlueprintLayers();

            int allyCount = clone.mapBlueprintLayers
                .SelectMany(l => l.stack)
                .Select(s => s.action)
                .OfType<CoreTownLayerGenerator>()
                .First()
                .AllyCount;

            var shopFlags = config.Buildings
                .Select(b => b != null && b.ShopCatalog != null && b.ShopCatalog.Count > 0)
                .ToArray();

            var expectedOptions = new TownPlanOptions(99, 15, 15, shopFlags, allyCount,
                new GridPoint(config.PartySpawn.x, config.PartySpawn.y), new GridPoint(config.PartySpawn.x, 0));
            var expected = TownPlanGenerator.Generate(expectedOptions);

            foreach (var name in new[] { TownLayers.ShopFloor, TownLayers.ShopWalls, TownLayers.Walkable, TownLayers.Buildings, TownLayers.Allies })
            {
                var actual = creator.GetMapOutputFromBlueprintLayer(name);
                var expectedCells = expected.Layers[name].ToArray();
                Assert.That(actual.Cast<bool>(), Is.EqualTo(expectedCells.Cast<bool>()), $"Layer '{name}' mismatch.");
            }

            Assert.That(CoreLayoutCache.TryGetTown(creator, out var plan), Is.True);
            Assert.That(plan.BuildingSlots.Count, Is.EqualTo(config.Buildings.Count));

            CoreLayoutCache.ClearResultFlags(clone);
            foreach (var layer in clone.mapBlueprintLayers)
            {
                Assert.That(layer.mapResultFailed, Is.False, $"Layer '{layer.layerName}' still marked failed after ClearResultFlags.");
            }
        }
    }
}
