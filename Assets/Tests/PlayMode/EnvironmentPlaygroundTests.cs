#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class EnvironmentPlaygroundTests
    {
        private Scene scene;
        private GameTestHarness production;
        [UnityTest]
        public IEnumerator ProductionTownUsesDetailedSmartHousesWithoutLegacyClusters()
        {
            production=new GameTestHarness();yield return production.LoadTown(new TestScenario().CreateSave());
            var town=UnityEngine.Object.FindFirstObjectByType<Town>();var creator=town.WalkableMap.TileWorldCreator;
            Assert.That(creator.worldObject.GetComponentsInChildren<ClusterIdentifier>(),Is.Empty);
            var layer=creator.twcAsset.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().Single(l=>l.Buildings);
            Assert.That(layer.QuarterTiles.name,Is.EqualTo("House"));
            var owner=creator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o=>o.name=="Houses_layer");
            Assert.That(owner.PropCount,Is.GreaterThan(0));
            var material=EnvironmentKit.Load().BuildingMaterial(creator.GetComponent<TownBiomeStyle>().Current);
            Assert.That(owner.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial==material),Is.True);
            Assert.That(material.mainTexture.width,Is.EqualTo(2048));
        }
        [UnityTest]
        public IEnumerator AuthoredPlaygroundBuildsBothGeneratorsAndRebuildsWithoutDuplicates()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/EnvironmentPlayground.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByName("EnvironmentPlayground");
            yield return null;
            var p = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnvironmentPlayground>()).Single();
            Assert.That(p.Gallery.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(p.Kit.Models.Length + 48));
            var buttons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)).ToArray();
            Assert.That(buttons.Length, Is.EqualTo(21));
            Assert.That(buttons.All(b => b.image.sprite != null && b.onClick.GetPersistentEventCount() == 1), Is.True);
            int builds = 0;
            p.TownCreator.OnBuildLayersComplete += _ => builds++;
            p.ShowTown(); yield return Until(() => builds == 1);
            yield return null;
            var owner = p.TownCreator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o => o.name == "Medieval streets, houses and parks_layer");
            int triangles = owner.TriangleCount;
            var originalOwners=p.TownCreator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Where(o=>o.name!="Biome decorations").Select(o=>o.name).OrderBy(n=>n).ToArray();
            Assert.That(originalOwners.Distinct().Count(),Is.EqualTo(originalOwners.Length),"One owner per terrain layer");
            Assert.That(triangles, Is.GreaterThan(100));
            Assert.That(p.TownCreator.worldObject.GetComponentsInChildren<Collider>(), Is.Empty);
            for (int i = 0; i < 8; i++)
            {
                p.NextBiome(); yield return Until(() => builds == i + 2); yield return null;
                owner = p.TownCreator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o => o.name == "Medieval streets, houses and parks_layer");
                Assert.That(owner.TriangleCount, Is.GreaterThan(100));
                Assert.That(owner.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == p.Kit.Material(p.TownBiome)), Is.True);
            }
            Assert.That(p.TownBiome, Is.EqualTo(OverworldBiome.Grassland));
            Assert.That(owner.TriangleCount, Is.EqualTo(triangles));
            p.Rebuild(); yield return Until(() => builds == 10); yield return null;
            Assert.That(p.TownCreator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Where(o=>o.name!="Biome decorations").Select(o=>o.name).OrderBy(n=>n), Is.EqualTo(originalOwners));
            Assert.That(p.TownCreator.worldObject.transform.Find("Biome decorations"),Is.Not.Null);
            bool worldDone = false; p.Overworld.TerrainBuilt += _ => worldDone = true;
            p.ShowOverworld(); yield return Until(() => worldDone); yield return null;
            var wc = p.Overworld.GetComponent<TileWorldCreator>();
            var cosmetic = wc.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o => o.name == OverworldCosmetics.Layer + "_layer");
            Assert.That(cosmetic.PropCount, Is.GreaterThan(0));
            Assert.That(cosmetic.GetComponentsInChildren<MeshRenderer>().All(r => r.enabled), Is.True);
            Assert.That(cosmetic.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(p.TownCreator.worldObject.activeSelf, Is.False);
            int original = cosmetic.TriangleCount;
            worldDone = false; p.Rebuild(); yield return Until(() => worldDone); yield return null;
            Assert.That(wc.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o => o.name == OverworldCosmetics.Layer + "_layer").TriangleCount, Is.EqualTo(original));
            var ocean = wc.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o => o.name == "Ocean/Surrounding water_layer");
            var summit=wc.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o=>o.name==SmartEnvironmentMasks.Summits+"_layer");
            Assert.That(summit.TriangleCount,Is.GreaterThan(0));
            Assert.That(summit.TriangleCount,Is.EqualTo(summit.PropCount*8));
            Assert.That(summit.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled),Is.True);
            var summitLayer=wc.twcAsset.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().Single(l=>l.layerName==SmartEnvironmentMasks.Summits);
            var summitMask=wc.GetMapOutputFromBlueprintLayer(summitLayer.assignedGenerationLayerGuid);
            var plateau=wc.GetMapOutputFromBlueprintLayer(wc.twcAsset.mapBlueprintLayers.Single(l=>l.layerName=="Smart/Mountains Tier 3").guid);
            for(int y=0;y<summitMask.GetLength(1);y++) for(int x=0;x<summitMask.GetLength(0);x++) if(summitMask[x,y])
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) Assert.That(plateau[x+dx,y+dy],Is.True,"Summit requires a solid plateau footprint");
            Assert.That(ocean.TriangleCount, Is.EqualTo(8));
            Assert.That(ocean.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(wc.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o=>o.name=="Ocean/Noise variation_layer").TriangleCount,Is.EqualTo(8));
            var shore=wc.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o=>o.name==SmartEnvironmentMasks.Coast+"_layer");
            Assert.That(shore.TriangleCount,Is.GreaterThan(0));
            Assert.That(shore.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial==p.Kit.Shore),Is.True);
            // Render two frames of open sea to verify shader animation actually changes pixels in-engine.
            var camera=p.ViewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float zoom=camera.orthographicSize;
            var target=RenderTexture.GetTemporary(96,96,24);var image=new Texture2D(96,96,TextureFormat.RGB24,false);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            try
            {
                camera.transform.SetPositionAndRotation(new Vector3(-30,-30,-30),Quaternion.identity);camera.orthographicSize=6;camera.targetTexture=target;
                Color32[] Capture() {camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,96,96),0,0);image.Apply();return image.GetPixels32();}
                var first=Capture();yield return new WaitForSecondsRealtime(1.1f);var second=Capture();
                Assert.That(first.Where((c,i)=>!c.Equals(second[i])).Count(),Is.GreaterThan(50),"Ocean shader must visibly animate.");
            }
            finally
            {
                camera.targetTexture=previousTarget;RenderTexture.active=previousActive;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(image);
                camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=zoom;
            }
            Assert.That(p.Overworld.GetComponent<OverworldBiomeRenderer>().RenderedSurfaces.activeInHierarchy, Is.True);
            p.ShowRules(); yield return Until(() => p.RulePreview.IsReady); yield return null;
            var ruleCreator = p.RulePreview.Creator;
            Assert.That(ruleCreator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Length, Is.EqualTo(7));
            var pieces = ruleCreator.generatedBlueprintMaps.Values.SelectMany(m => m.clusters.Values).SelectMany(c => c.Values).Select(t => t.tileType).Distinct().ToArray();
            Assert.That(pieces.Length, Is.EqualTo(4), "Rule lab must exercise edge, outer corner, inner corner, and fill.");
            p.Rebuild(); yield return Until(() => p.RulePreview.IsReady); yield return null;
            Assert.That(ruleCreator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Length, Is.EqualTo(7));
            Assert.That(p.TownTemplate.mapBuildLayers.OfType<TownEnvironmentLayer>().Single().Kit, Is.SameAs(p.Kit));
            Assert.That(p.Overworld.Template.mapBuildLayers.OfType<OverworldCosmeticLayer>().Single().Kit, Is.SameAs(p.Kit));
            Assert.That(p.TownTemplate.mapBuildLayers.OfType<TownEnvironmentLayer>().Single().TreeModels,Is.SameAs(p.Kit.TreeModels));
            Assert.That(p.Overworld.Template.mapBuildLayers.OfType<OverworldCosmeticLayer>().Single().TreeModels,Is.SameAs(p.Kit.TreeModels));
            p.ShowGallery(); Assert.That(wc.worldObject.activeSelf, Is.False);
            Assert.That(p.Overworld.GetComponent<OverworldBiomeRenderer>().RenderedSurfaces.activeSelf, Is.False);
        }
        private static IEnumerator Until(Func<bool> done)
        {
            float deadline = Time.realtimeSinceStartup + 90;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(done(), Is.True, "TWC generation timed out.");
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if(production!=null) {yield return production.Cleanup();production=null;}
        }
    }
}
#endif

