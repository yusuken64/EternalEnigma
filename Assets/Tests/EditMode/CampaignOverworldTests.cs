using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using TWC.Actions;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class CampaignOverworldTests
    {
        private GameObject host;
        private GameObject marker;
        private GameObject renderedWorld;
        private TileWorldCreator creator;
        private TileWorldCreatorAsset template;
        private CampaignOverworld adapter;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Campaign map test");
            creator = host.AddComponent<TileWorldCreator>();
            template = ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
            template.mapWidth = template.mapHeight = 9;
            template.mapBlueprintLayers.Add(new TileWorldCreatorAsset.BlueprintLayerData("Floor", true) { map = new bool[9, 9] });
            template.mapBlueprintLayers.Add(new TileWorldCreatorAsset.BlueprintLayerData("Places", true) { map = new bool[9, 9] });
            creator.twcAsset = template;
            adapter = host.AddComponent<CampaignOverworld>();
            adapter.Template = template;
            adapter.LayerBindings = new List<CampaignLayerBinding>
            {
                new(OverworldLayers.Ground, "Floor"), new(OverworldLayers.Towns, "Places"), new(OverworldLayers.Walkable, "Passable")
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (renderedWorld != null) Object.DestroyImmediate(renderedWorld);
            if (marker != null) Object.DestroyImmediate(marker);
            if (template != null) Object.DestroyImmediate(template);
        }

        [Test]
        public void AdapterPreservesTemplateAndCreatesBothKindsOfTWCWorldMap()
        {
            var campaign = CampaignGenerator.Generate(42);
            var grid = OverworldGridGenerator.Generate(campaign);
            var state = UnityEngine.Random.state;
            float expectedNext = UnityEngine.Random.value;
            UnityEngine.Random.state = state;
            adapter.Apply(grid);
            Assert.That(UnityEngine.Random.value, Is.EqualTo(expectedNext));
            Assert.That(creator.twcAsset, Is.Not.SameAs(template));
            Assert.That(template.mapWidth, Is.EqualTo(9));
            Assert.That(template.mapBlueprintLayers.Count, Is.EqualTo(2));
            Assert.That(template.mapBlueprintLayers[0].stack, Is.Empty);
            Assert.That(template.mapBlueprintLayers[0].map.GetLength(0), Is.EqualTo(9));
            Assert.That(creator.twcAsset.mapWidth, Is.EqualTo(grid.Width));
            foreach (var source in template.mapBlueprintLayers)
            {
                var imported = creator.twcAsset.mapBlueprintLayers.Single(l => l.layerName == source.layerName);
                Assert.That(imported.guid, Is.EqualTo(source.guid));
                Assert.That(creator.GetGeneratedBlueprintMap(imported.guid.ToString()), Is.Not.Null);
                Assert.That(creator.GetGeneratedBlueprintMap(imported.guid + "_UNSUBD"), Is.Not.Null);
            }
            Assert.That(creator.GetMapOutputFromBlueprintLayer("Places").Cast<bool>().Count(v => v), Is.EqualTo(6));
            Assert.That(creator.GetMapOutputFromBlueprintLayer("Floor").Cast<bool>(), Is.EqualTo(grid.Layers[OverworldLayers.Ground].ToArray().Cast<bool>()));
            var gate = grid.Locks.First(g => !campaign.Routes.Single(r => r.Id == g.RouteId).IsStarterExit).Cells[0];
            Assert.That(creator.GetMapOutputFromBlueprintLayer("Passable")[gate.X, gate.Y], Is.False);
            var oldAsset = creator.twcAsset;
            adapter.Apply(grid, CapabilitySet.From(campaign.Manifest.Select(c => c.Id)));
            Assert.That(oldAsset == null, Is.True);
            Assert.That(creator.GetMapOutputFromBlueprintLayer("Passable")[gate.X, gate.Y], Is.True);
            Assert.That(creator.twcAsset.mapBlueprintLayers.Count, Is.EqualTo(3));
            Object.DestroyImmediate(adapter);
            Assert.That(creator.twcAsset, Is.SameAs(template));
            Assert.That(creator.generatedBlueprintMaps, Is.Empty);
        }

        [Test]
        public void InvalidBindingDoesNotReplaceTheCreatorAsset()
        {
            adapter.LayerBindings.Add(new CampaignLayerBinding("MissingLayer", "Bad"));
            Assert.Throws<ArgumentException>(() => adapter.Generate(CampaignGenerator.Generate(42)));
            Assert.That(creator.twcAsset, Is.SameAs(template));
        }

        [UnityTest]
        public IEnumerator RealTWCObjectBuildLayerPlacesTownPrefabsFromImportedMask()
        {
            marker = new GameObject("CampaignTownMarker");
            template.mapBuildLayers.Add(new InstantiateObjects
            {
                guid = Guid.NewGuid(), layerName = "Town markers", assignedGenerationLayerGuid = template.mapBlueprintLayers[1].guid,
                prefab = marker, disableTileRotation = true
            });
            adapter.Generate(CampaignGenerator.Generate(42));
            renderedWorld = creator.worldObject;
            bool complete = false;
            creator.OnBuildLayersComplete += _ => complete = true;
            adapter.BuildMeshes();
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 20;
            while (!complete && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(complete, Is.True, "TWC did not finish its object build.");
            Assert.That(renderedWorld.GetComponentsInChildren<Transform>().Count(t => t.name == "CampaignTownMarker(Clone)"), Is.EqualTo(6));
        }
    }
}
