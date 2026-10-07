using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TWC;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class DioramaGenerationTests
    {
        [Test]
        public void LandmarkAdaptersKeepImportedMeshesAndNormalizedGroundBounds()
        {
            foreach(var model in DioramaCatalog.Load().Models.Where(m=>m.Id.StartsWith("Castle")))
            {
                var obj=Object.Instantiate(model.Prefab);
                try
                {
                    var renderers=obj.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;
                    foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    Assert.That(bounds.size.z,Is.EqualTo(model.Height).Within(.005f),model.Id);
                    Assert.That(bounds.max.z,Is.EqualTo(0).Within(.005f),model.Id+" touches ground");
                    Assert.That(bounds.center.x,Is.EqualTo(0).Within(.005f),model.Id);
                    Assert.That(bounds.center.y,Is.EqualTo(0).Within(.005f),model.Id);
                    if(model.Id=="CastleGate")Assert.That(bounds.size.x,Is.GreaterThan(bounds.size.y),"Gate spans the entrance rather than facing sideways.");
                    Assert.That(obj.GetComponentsInChildren<Collider>(),Is.Empty);
                    foreach(var f in obj.GetComponentsInChildren<MeshFilter>())
                        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(f.sharedMesh),Does.StartWith("Assets/Art/KennyNL/Castle Kit/Models/"));
                }
                finally {Object.DestroyImmediate(obj);}
            }
        }

        [UnityTest]
        public IEnumerator RepeatedTownAndOverworldBuildsPreserveCountsAndChunkBudgets()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();bool oldIgnore=LogAssert.ignoreFailingMessages;
            var errors=new System.Collections.Generic.List<string>();
            void Log(string message,string stack,LogType type)
            {
                if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!message.StartsWith("Instantiating material due to calling renderer.material during edit mode."))errors.Add(message);
            }
            LogAssert.ignoreFailingMessages=true;Application.logMessageReceived+=Log;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/EnvironmentPlayground.unity");
                var playground=Object.FindFirstObjectByType<EnvironmentPlayground>();playground.Seed=12345;
                var report=new System.Text.StringBuilder("Seed 12345; actual production TWC output, two builds per surface.\n");
                foreach(bool town in new[]{true,false})
                {
                    if(town)playground.ShowTown();else playground.ShowOverworld();
                    yield return null;
                    var creator=town?playground.TownCreator:playground.Overworld.GetComponent<TileWorldCreator>();
                    var first=Snapshot(creator.worldObject);playground.Rebuild();yield return null;
                    var second=Snapshot(creator.worldObject);Assert.That(second,Is.EqualTo(first),town?"town":"overworld");
                    Assert.That(creator.worldObject.GetComponentsInChildren<Collider>(),Is.Empty,"Art must not add collision.");
                    Assert.That(creator.worldObject.GetComponentsInChildren<PaintedGroundOutput>().Length,Is.EqualTo(1));
                    if(town)
                    {
                        CoreLayoutCache.TryGetTown(creator,out var plan);
                        foreach(string id in new[]{"hen","duck","butterfly","bee","ladybug"})
                        {
                            var prop=creator.worldObject.GetComponentsInChildren<Transform>().Single(t=>t.name==id);
                            int x=Mathf.FloorToInt(prop.position.x/creator.twcAsset.cellSize),y=Mathf.FloorToInt(prop.position.y/creator.twcAsset.cellSize);
                            Assert.That(plan.Layers[EternalEnigma.Core.World.TownLayers.Roads][x,y],Is.False,id);
                            Assert.That(plan.IsReserved(new EternalEnigma.Core.World.GridPoint(x,y)),Is.False,id);
                        }
                    }
                    if(!town)
                    {
                        var layer=creator.twcAsset.mapBuildLayers.OfType<OverworldTreeWallLayer>().Single();
                        var trees=creator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o=>o.name==layer.layerName+"_layer");
                        Assert.That(trees.TriangleCount,Is.InRange(1,OverworldTreeWallLayer.MaxTriangles));
                        foreach(var chunk in trees.GetComponentsInChildren<MeshFilter>().GroupBy(f=>f.name.Split(' ')[1]))
                            Assert.That(chunk.Sum(f=>(int)f.sharedMesh.GetIndexCount(0)/3),Is.LessThanOrEqualTo(OverworldTreeWallLayer.ChunkTriangles),chunk.Key);
                        var settlements=creator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o=>o.name==OverworldSettlementLayer.Layer+"_layer");
                        Assert.That(settlements.PropCount,Is.GreaterThan(0));
                        report.AppendLine($"Forest triangles: {trees.TriangleCount}; trees: {trees.PropCount}; chunk cap: {OverworldTreeWallLayer.ChunkTriangles}.");
                    }
                    report.AppendLine((town?"Town":"Overworld")+": "+first);
                }
                Assert.That(errors,Is.Empty);
                Directory.CreateDirectory("Docs/Art/Previews/Diorama/Verification");File.WriteAllText("Docs/Art/Previews/Diorama/Verification/Determinism.txt",report.ToString());
            }
            finally
            {
                Application.logMessageReceived-=Log;LogAssert.ignoreFailingMessages=oldIgnore;
                if(setup.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }
        static string Snapshot(GameObject root)
        {
            var meshes=root.GetComponentsInChildren<MeshFilter>();var owners=root.GetComponentsInChildren<EnvironmentMeshOwner>();
            return $"meshes={meshes.Length}, triangles={meshes.Sum(f=>f.sharedMesh.triangles.Length/3)}, owners={owners.Length}, props={owners.Sum(o=>o.PropCount)}, ground="+
                string.Join(",",root.GetComponentsInChildren<PaintedGroundOutput>().SelectMany(g=>g.SurfaceCells));
        }
    }
}
