using System;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class TownInteriorIntegrationTests
    {
        [Test]
        public void CatalogHasDistinctCharactersBirdsAndCompleteDependencies()
        {
            var kit=TownInteriorCatalog.Load();Assert.That(kit,Is.Not.Null);Assert.That(kit.Characters.Length,Is.EqualTo(7));
            Assert.That(kit.Characters.Select(c=>c.Id),Is.Unique);Assert.That(kit.BiomeMaterials.Length,Is.EqualTo(8));
            foreach(var c in kit.Characters){Assert.That(c.Portrait,Is.Not.Null,c.Id);Assert.That(c.Greeting,Is.Not.Empty);Assert.That(c.Prefab,Is.Not.Null);}
            foreach(var entry in kit.Assets)
            {
                Assert.That(entry.Mesh,Is.Not.Null,entry.Id);Assert.That(entry.Prefab.GetComponentsInChildren<Collider>(),Is.Empty);
                var animator=entry.Prefab.GetComponent<Animator>();if(animator==null)continue;
                bool bird=entry.Id.StartsWith("Bird");int triangles=entry.Mesh.triangles.Length/3;
                Assert.That(triangles,Is.InRange(bird?500:3000,bird?1200:6000),entry.Id);
                var renderer=entry.Prefab.GetComponent<SkinnedMeshRenderer>();Assert.That(renderer.sharedMaterials.Length,Is.EqualTo(1));Assert.That(renderer.sharedMaterial,Is.SameAs(kit.CharacterMaterial));
                Assert.That(animator.applyRootMotion,Is.False);
                foreach(var weight in entry.Mesh.boneWeights)Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1).Within(.001));
                var idle=animator.runtimeAnimatorController.animationClips.First(c=>c.name.EndsWith("Idle",StringComparison.Ordinal));
                Assert.That(AnimationUtility.GetAnimationClipSettings(idle).loopTime,Is.True);
                Assert.That(AnimationUtility.GetCurveBindings(idle).Any(b=>b.path==""||b.path=="Root"),Is.False,"Placement/root must not animate");
            }
            Assert.That(kit.Carpet,Is.Not.Null);Assert.That(kit.Counter,Is.Not.Null);
        }

        [Test]
        public void BirdLoopsKeepFeetAboveSupportAndRootsFixed()
        {
            foreach(var entry in TownInteriorCatalog.Load().Assets.Where(a=>a.Id.StartsWith("Bird")))
            {
                var bird=Object.Instantiate(entry.Prefab);var mesh=new Mesh();
                try
                {
                    var origin=new Vector3(7,11,-3);bird.transform.position=origin;
                    var renderer=bird.GetComponent<SkinnedMeshRenderer>();var clip=bird.GetComponent<Animator>().runtimeAnimatorController.animationClips.First();
                    for(int frame=0;frame<=96;frame++)
                    {
                        clip.SampleAnimation(bird,frame/24f);renderer.BakeMesh(mesh);
                        Assert.That(bird.transform.position,Is.EqualTo(origin));
                        int lowest=Array.IndexOf(mesh.vertices,mesh.vertices.OrderByDescending(v=>v.z).First());
                        Assert.That(mesh.bounds.max.z,Is.LessThan(.006f),$"{entry.Id} penetrates perch at frame {frame}, bone {renderer.bones[entry.Mesh.boneWeights[lowest].boneIndex0].name}");
                        Assert.That(mesh.bounds.size.x*.6f,Is.LessThan(.83f));
                        Assert.That(mesh.bounds.size.y*.6f,Is.LessThan(.44f));
                    }
                }
                finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(bird);}
            }
        }

        [Test]
        public void HundredSeedsPublishCoreOccupancyToUnityAndPreserveCacheIdentity()
        {
            var template=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset");
            var asset=Object.Instantiate(template);var host=new GameObject("Interior mask sweep");var creator=host.AddComponent<TileWorldCreator>();creator.twcAsset=asset;
            try
            {
                for(int seed=0;seed<100;seed++)
                {
                    var options=TownLayout.Create(seed,TownServiceCatalog.All,2,3,CampaignContext.ResidentialTownBuildings).Options;
                    CoreTownLayerGenerator.Configure(asset,options);creator.SetCustomRandomSeed(seed);creator.ExecuteAllBlueprintLayers();
                    Assert.That(CoreLayoutCache.TryGetTown(creator,out var plan),Is.True);
                    foreach(var generator in asset.mapBlueprintLayers.SelectMany(l=>l.stack).Select(s=>s.action).OfType<CoreTownLayerGenerator>())Assert.That(generator.Options(creator),Is.EqualTo(options));
                    foreach(string layer in TownLayers.InteriorLayers.Append(TownLayers.Walkable))
                    {
                        var published=creator.GetMapOutputFromBlueprintLayer(layer);
                        for(int x=0;x<plan.Width;x++)for(int y=0;y<plan.Height;y++)Assert.That(published[x,y],Is.EqualTo(plan.Layers[layer][x,y]),$"{seed} {layer} {x},{y}");
                    }
                    var smart=asset.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().Where(l=>l.TownPalette).ToArray();
                    Assert.That(smart.Length,Is.EqualTo(2));Assert.That(smart.Select(l=>l.guid),Is.Unique);
                }
            }
            finally {CoreLayoutCache.Clear(creator);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(asset);}
        }

        [Test]
        public void RebuildReleasesOwnedMeshesAndKeepsThreeSupportedSilentBirds()
        {
            var template=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset"));
            var host=new GameObject("Interior rebuild");var creator=host.AddComponent<TileWorldCreator>();creator.twcAsset=template;creator.worldObject=new GameObject("Output");
            try
            {
                CoreTownLayerGenerator.Configure(template,TownLayout.Create(42,TownServiceCatalog.All,2,3,7).Options);creator.SetCustomRandomSeed(42);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(template);creator.ExecuteAllBuildLayers(true);
                TownInteriorRendering.Build(creator);var old=creator.worldObject.GetComponentInChildren<TownInteriorOutput>();
                var meshes=old.GetComponentsInChildren<EnvironmentMeshOwner>().SelectMany(o=>o.Meshes).ToArray();var positions=old.Birds.Select(b=>b.Position).ToArray();
                Assert.That(old.Birds.Count,Is.EqualTo(3));Assert.That(old.Birds.Count(b=>!b.Interior),Is.EqualTo(2));Assert.That(old.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(old.GetComponentsInChildren<AudioSource>(),Is.Empty);
                Assert.That(old.WallMountCount,Is.InRange(1,24),"Inset timber panels must accept bounded wall decorations");
                TownInteriorRendering.Build(creator);Assert.That(old==null,Is.True);Assert.That(meshes.All(m=>m==null),Is.True);
                var next=creator.worldObject.GetComponentInChildren<TownInteriorOutput>();Assert.That(next.Birds.Select(b=>b.Position),Is.EqualTo(positions));
                Assert.That(next.Birds.Select(b=>b.Variant),Is.Unique);Assert.That(next.GetComponentsInChildren<Light>(),Is.Empty);
            }
            finally {DungeonPresentation.ClearOutput(creator.worldObject);Object.DestroyImmediate(creator.worldObject);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(template);}
        }
    }
}
