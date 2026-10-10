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

        [Test]
        public void ComposedStylesBindByGuidAndKeepSharedTemplatesIsolated()
        {
            var basis=ScriptableObject.CreateInstance<OverworldStyleProfile>();
            var variant=ScriptableObject.CreateInstance<OverworldStyleProfile>();
            var ground=ScriptableObject.CreateInstance<PaintedGroundStyle>();
            try
            {
                var layer=template.mapBlueprintLayers[0];
                basis.Template=template;basis.GroundStyle=ground;
                basis.Layers.Add(new OverworldStyleBinding {BlueprintGuid=layer.guid.ToString(),CoreLayer=OverworldLayers.Ground});
                basis.Layers.Add(new OverworldStyleBinding {BlueprintGuid=template.mapBlueprintLayers[1].guid.ToString(),CoreLayer=OverworldLayers.Towns});
                template.mapBuildLayers.Add(new OverworldGroundLayer {Inputs=new[] {new CampaignLayerBinding(OverworldLayers.Ground,"Floor")}});
                variant.BaseProfile=basis;
                adapter.StyleProfile=variant;
                layer.layerName="Artist renamed ground";
                var grid=OverworldGridGenerator.Generate(CampaignGenerator.Generate(42));
                string before=DioramaArtIdentity.For(adapter);
                adapter.Apply(grid);
                Assert.That(creator.GetMapOutputFromBlueprintLayer(layer.guid).Cast<bool>(),Is.EqualTo(grid.Layers[OverworldLayers.Ground].ToArray().Cast<bool>()));
                Assert.That(template.mapWidth,Is.EqualTo(9));
                Assert.That(((OverworldGroundLayer)template.mapBuildLayers[0]).Inputs[0].BlueprintLayer,Is.EqualTo("Floor"));
                variant.Layers.Add(new OverworldStyleBinding {BlueprintGuid=template.mapBlueprintLayers[1].guid.ToString(),CoreLayer=OverworldLayers.Roads});
                Assert.That(DioramaArtIdentity.For(adapter),Is.Not.EqualTo(before));
                adapter.Apply(grid);
                Assert.That(creator.GetMapOutputFromBlueprintLayer(template.mapBlueprintLayers[1].guid).Cast<bool>(),Is.EqualTo(grid.Layers[OverworldLayers.Roads].ToArray().Cast<bool>()));
                Assert.That(basis.Layers[0].CoreLayer,Is.EqualTo(OverworldLayers.Ground));
                variant.BaseProfile=variant;
                Assert.Throws<InvalidOperationException>(()=>adapter.Apply(grid));
                Assert.That(adapter.CurrentGrid,Is.SameAs(grid));
            }
            finally {Object.DestroyImmediate(variant);Object.DestroyImmediate(basis);Object.DestroyImmediate(ground);}
        }

        [Test]
        public void RequiredEmptyStyleOutputFailsAndReleasesItsClone()
        {
            var profile=ScriptableObject.CreateInstance<OverworldStyleProfile>();
            var ground=ScriptableObject.CreateInstance<PaintedGroundStyle>();
            try
            {
                var grid=OverworldGridGenerator.Generate(CampaignGenerator.Generate(42));
                var empty=grid.Layers.First(p=>!p.Value.ToArray().Cast<bool>().Any(v=>v)).Key;
                profile.Template=template;profile.GroundStyle=ground;
                profile.Layers.Add(new OverworldStyleBinding {BlueprintGuid=template.mapBlueprintLayers[0].guid.ToString(),CoreLayer=empty,AllowEmpty=false});
                profile.Layers.Add(new OverworldStyleBinding {BlueprintGuid=template.mapBlueprintLayers[1].guid.ToString(),CoreLayer=OverworldLayers.Towns});
                template.mapBuildLayers.Add(new OverworldGroundLayer {Inputs=Array.Empty<CampaignLayerBinding>()});
                adapter.StyleProfile=profile;
                Assert.Throws<InvalidOperationException>(()=>adapter.Apply(grid));
                Assert.That(adapter.LastGenerationError,Does.Contain("empty"));
                Assert.That(adapter.CurrentGrid,Is.Null);
                Assert.That(creator.twcAsset,Is.SameAs(template));
                Assert.That(creator.generatedBlueprintMaps,Is.Empty);
                profile.Layers[0].AllowEmpty=true;
                Assert.DoesNotThrow(()=>adapter.Apply(grid));
                Assert.That(adapter.LastGenerationError,Is.Null);
            }
            finally {Object.DestroyImmediate(profile);Object.DestroyImmediate(ground);}
        }

        [Test]
        public void CosmeticTransformsCannotOccupyReservedApproaches()
        {
            var grid=OverworldGridGenerator.Generate(CampaignGenerator.Generate(42));
            var reserved=OverworldCosmetics.Protected(grid);
            var cells=new bool[grid.Width,grid.Height];
            for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)cells[x,y]=true;
            var action=new ProtectOverworldApproaches(reserved);
            action.Execute(cells,creator);
            foreach(var cell in grid.Locations.Values)Assert.That(cells[cell.X,cell.Y],Is.False);
            foreach(var gate in grid.Locks)foreach(var cell in gate.Cells)Assert.That(cells[cell.X,cell.Y],Is.False);
            Assert.Throws<InvalidOperationException>(()=>action.Execute(new bool[1,1],creator));
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
            var unexpectedErrors = new List<string>();
            void CaptureLog(string message, string stackTrace, LogType type)
            {
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                    !message.StartsWith("Instantiating material due to calling renderer.material during edit mode."))
                    unexpectedErrors.Add(message);
            }
            var previousIgnore = LogAssert.ignoreFailingMessages;
            Application.logMessageReceived += CaptureLog;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                adapter.BuildMeshes();
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 20;
                while (!complete && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(complete, Is.True, "TWC did not finish its object build.");
                Assert.That(unexpectedErrors, Is.Empty);
                Assert.That(renderedWorld.GetComponentsInChildren<Transform>().Count(t => t.name == "CampaignTownMarker(Clone)"), Is.EqualTo(6));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
                Application.logMessageReceived -= CaptureLog;
            }
        }
    }
}
