using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class TownDungeonIntegrationTests
    {
        [TestCase(OverworldBiome.Grassland)] [TestCase(OverworldBiome.Desert)] [TestCase(OverworldBiome.Water)] [TestCase(OverworldBiome.Mountain)]
        [TestCase(OverworldBiome.Forest)] [TestCase(OverworldBiome.Tundra)] [TestCase(OverworldBiome.Marsh)] [TestCase(OverworldBiome.Volcanic)]
        public void SharedTemplatePublishesCombatGeometryAndCleansOwnedOutput(OverworldBiome biome)
        {
            var template = TownDungeonAssets.SharedTemplate;
            Assert.That(AssetDatabase.GetAssetPath(template), Is.EqualTo("Assets/TileWorldCreator/VillageLSystemAsset.asset"));
            int width = template.mapWidth, layers = template.mapBlueprintLayers.Count;
            var host = new GameObject("Town dungeon integration");
            var creator = host.AddComponent<TileWorldCreator>();
            creator.twcAsset = DungeonPresentation.CloneTemplate(template);
            creator.worldObject = new GameObject("District output");
            var options = DungeonLayoutProfile.Options(42, biome, 1, variant: DungeonVariant.Town);
            try
            {
                int before = CoreLayoutCache.Generations;
                TownDungeonPresentation.Configure(creator, options);
                creator.SetCustomRandomSeed(options.Seed); creator.ExecuteAllBlueprintLayers();
                Assert.That(CoreLayoutCache.Generations, Is.EqualTo(before + 1), "All layers must share one authoritative floor.");
                Assert.That(CoreLayoutCache.TryGetDungeon(creator, out var floor), Is.True);
                Assert.That(CoreLayoutCache.TryGetTown(creator, out var town), Is.True);
                Assert.That(town, Is.SameAs(floor.TownPlan));
                foreach (var name in TownLayers.All.Concat(TownLayers.Detail).Concat(TownLayers.InteriorLayers))
                    Assert.That(creator.GetMapOutputFromBlueprintLayer(name), Is.EqualTo(town.Layers[name].ToArray()), name);
                Assert.That(creator.GetMapOutputFromBlueprintLayer("Combat floor"), Is.EqualTo(floor.Layers[DungeonLayers.Floor].ToArray()));
                CoreLayoutCache.ClearResultFlags(creator.twcAsset); creator.ExecuteAllBuildLayers(true);
                TownDungeonPresentation.Complete(creator, floor, true);
                Assert.That(creator.GetComponent<TownBiomeStyle>().Current, Is.EqualTo(biome));
                Assert.That(creator.worldObject.GetComponentsInChildren<TownRoofVisual>(true).Length, Is.EqualTo(town.BuildingSlots.Count));
                Assert.That(creator.worldObject.GetComponentInChildren<TownInteriorOutput>(), Is.Not.Null);
                Assert.That(creator.worldObject.GetComponentsInChildren<TownBuilding>(true), Is.Empty);
                Assert.That(creator.worldObject.GetComponentsInChildren<ShopVendor>(true), Is.Empty);
                Assert.That(creator.worldObject.GetComponentsInChildren<HomeBed>(true), Is.Empty);
                Assert.That(creator.twcAsset.mapBuildLayers.OfType<DungeonBoundaryLayer>(), Is.Empty);
                Assert.That(creator.worldObject.transform.Find("Theme cosmetics"), Is.Null);
                var owned = creator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>(true).SelectMany(o => o.Meshes).Where(m => m != null).ToArray();
                Assert.That(owned, Is.Not.Empty);
                DungeonPresentation.ClearOutput(creator.worldObject);
                Assert.That(owned.All(m => m == null), Is.True);
                Assert.That(creator.worldObject.transform.childCount, Is.Zero);
                Assert.That(template.mapWidth, Is.EqualTo(width)); Assert.That(template.mapBlueprintLayers.Count, Is.EqualTo(layers));
            }
            finally
            {
                DungeonPresentation.ClearOutput(creator.worldObject); Object.DestroyImmediate(creator.worldObject);
                DungeonPresentation.ReleaseTemplate(creator.twcAsset); CoreLayoutCache.Clear(creator); Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SavedVariantRoundTripsAndOlderRunsDefaultToStandard()
        {
            var run = new DungeonSaveData { Variant = DungeonVariant.Town, UseBiomeLayout = true, LayoutBiome = OverworldBiome.Desert, LayoutTier = 2, StartFloor = 10, EndFloor = 20 };
            var restored = JsonUtility.FromJson<DungeonSaveData>(JsonUtility.ToJson(run));
            Assert.That(restored.Variant, Is.EqualTo(DungeonVariant.Town)); Assert.That(restored.EndFloor, Is.EqualTo(20));
            Assert.That(JsonUtility.FromJson<DungeonSaveData>("{\"StartFloor\":1,\"EndFloor\":5}").Variant, Is.EqualTo(DungeonVariant.Standard));
        }

        [Test]
        public void CacheSeparatesTownFromStandardAndOverworldUsesHostileTownFootprints()
        {
            var host = new GameObject("Cache separation"); var creator = host.AddComponent<TileWorldCreator>();
            try
            {
                var town = DungeonLayoutProfile.Options(42, OverworldBiome.Forest, 0, variant: DungeonVariant.Town);
                var standard = new DungeonFloorOptions(42, town.Width, town.Height, trapCount: 0, layoutVersion: 1, biome: OverworldBiome.Forest);
                var a = CoreLayoutCache.GetDungeon(creator, town); var b = CoreLayoutCache.GetDungeon(creator, standard);
                Assert.That(a, Is.Not.SameAs(b)); Assert.That(b.Variant, Is.EqualTo(DungeonVariant.Standard));
                Assert.That(CoreLayoutCache.TryGetTown(creator, out _), Is.False);
                var campaign = CampaignGenerator.Generate(42); var grid = OverworldGridGenerator.Generate(campaign);
                var map = host.AddComponent<CampaignOverworld>();
                map.StyleProfile = Resources.Load<OverworldStyleProfile>("EnvironmentKit/OverworldStyle");
                map.Apply(grid);
                foreach (var projection in new[] { SmartEnvironmentMasks.Houses, SmartEnvironmentMasks.Walls })
                {
                    var binding = map.StyleProfile.ResolveLayers().Single(b => b.Projection == projection);
                    var actual = creator.GetMapOutputFromBlueprintLayer(System.Guid.Parse(binding.BlueprintGuid));
                    Assert.That(actual, Is.EqualTo(SmartEnvironmentMasks.World(grid, projection)), projection);
                }
                foreach (var location in campaign.Locations.Where(l => l.Variant == DungeonVariant.Town && l.ParentTownId == null))
                {
                    var miniature = grid.TownFootprints.Single(t => t.LocationId == location.Id);
                    Assert.That(miniature.IsHostile, Is.True); Assert.That(miniature.Walls.Count, Is.GreaterThan(0));
                    Assert.That(grid.IsGround(miniature.Entrance), Is.True);
                }
            }
            finally { CoreLayoutCache.Clear(creator); Object.DestroyImmediate(host); }
        }
    }
}
