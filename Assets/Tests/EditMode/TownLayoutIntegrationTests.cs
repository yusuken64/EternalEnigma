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
    public class TownLayoutIntegrationTests
    {
        [TestCase(42)] [TestCase(77)]
        public void CampaignServicesUseCoreSlotOrderAndEveryLayerUsesCompleteOptions(int seed)
        {
            var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed));
            var layout = context.TownLayout("town-0");
            var configuration = Object.Instantiate(TownSceneLoader.Default);
            var template = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset");
            int originalWidth = template.mapWidth;
            var asset = Object.Instantiate(template);
            var host = new GameObject("Town layout integration");
            var creator = host.AddComponent<TileWorldCreator>(); creator.twcAsset = asset;
            try
            {
                CampaignTownLayout.Configure(configuration, layout);
                Assert.That(context.IsGridGenerated, Is.False);
                Assert.That(configuration.Buildings.Select(d => d.Id), Is.Unique);
                Assert.That(configuration.PartySpawn, Is.EqualTo(layout.Options.PartySpawn.ToCell()));
                Assert.That(configuration.SlotBuildings.Count(d => d == null), Is.EqualTo(CampaignContext.ResidentialTownBuildings));
                for (int i = 0; i < layout.SlotServices.Count; i++)
                    if (layout.SlotServices[i] != null)
                    {
                        var definition = configuration.SlotBuildings[i];
                        Assert.That(definition.Id, Is.EqualTo(layout.SlotServices[i].Id));
                        Assert.That(definition.HasInterior, Is.True);
                        if (definition.DialogId == "shop") Assert.That(definition.ShopCatalog, Is.Not.Empty);
                    }
                CoreTownLayerGenerator.Configure(asset, configuration);
                creator.SetCustomRandomSeed(layout.Options.Seed);
                creator.ExecuteAllBlueprintLayers();
                Assert.That(asset.mapWidth, Is.EqualTo(layout.Options.Width));
                Assert.That(asset.mapHeight, Is.EqualTo(layout.Options.Height));
                Assert.That(asset.mapBlueprintLayers.Select(l => l.layerName), Is.SupersetOf(TownLayers.Detail));
                foreach (var action in asset.mapBlueprintLayers.SelectMany(l => l.stack).Select(s => s.action).OfType<CoreTownLayerGenerator>())
                    Assert.That(action.Options(creator), Is.EqualTo(layout.Options));
                Assert.That(CoreLayoutCache.TryGetTown(creator, out var actual), Is.True);
                var expected = TownPlanGenerator.Generate(layout.Options);
                var streets = asset.mapBlueprintLayers.Single(l => l.layerName == "Smart/Roads");
                var houseFloor = asset.mapBlueprintLayers.Single(l => l.layerName == "HouseFloor");
                foreach (var door in actual.BuildingSlots)
                    Assert.That(streets.map[door.X, door.Y], Is.True, "Street must reach each door approach.");
                foreach (var cell in actual.Footprints.SelectMany(f => f.Cells))
                    Assert.That(houseFloor.map[cell.X, cell.Y], Is.True, "Floor must reach the facade without grass strips.");
                Assert.That(streets.stack.Select(s => s.action).OfType<CoreTownLayerGenerator>()
                    .Any(g => g.LayerName == TownLayers.ShopFloor), Is.False, "Indoor floors must not use the street layer.");
                Assert.That(asset.mapBuildLayers.OfType<TownFloorTileLayer>().Single().Material.mainTexture,
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PaintedEnvironment/Floor.png")));
                foreach (string name in TownLayers.All.Concat(TownLayers.Detail))
                    Assert.That(actual.Layers[name].ToArray(), Is.EqualTo(expected.Layers[name].ToArray()), name);
                foreach (var service in layout.SlotServices.Select((s, i) => (s, i)).Where(p => p.s != null))
                    Assert.That(actual.TryGetVendorAnchor(actual.BuildingSlots[service.i], out _), Is.True);
                Assert.That(template.mapWidth, Is.EqualTo(originalWidth));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); Object.DestroyImmediate(configuration); }
        }
    }
}
