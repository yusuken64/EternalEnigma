using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class DioramaVegetationTests
    {
        [Test]
        public void ImportedTreeBatchPreservesSourceMeshesUsesOneLodAndKeepsMeasuredHeight()
        {
            var catalog=DioramaCatalog.Load();
            foreach(var model in catalog.Models.Where(m=>m.Tree))
            {
                var root=new GameObject(model.Id);
                try
                {
                    Assert.That(model.Prefab.GetComponentsInChildren<Collider>(true),Is.Empty,model.Id);
                    if(model.Id.StartsWith("Pack"))
                        Assert.That(model.Prefab.GetComponentsInChildren<MeshFilter>(true).All(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/RPG Tiny Fantasy World 01 PA/Mesh/")),Is.True,model.Id);
                    var batch=new EnvironmentBatch(root.transform);catalog.Add(batch,model.Id,EternalEnigma.Core.World.OverworldBiome.Grassland,Vector3.zero);batch.Finish();
                    Assert.That(batch.Owner.PropCount,Is.EqualTo(1));Assert.That(batch.Owner.TriangleCount,Is.EqualTo(model.Triangles),model.Id+" must not batch both LODs");
                    var bounds=new Bounds();bool first=true;
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()) {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
                    Assert.That(bounds.size.z,Is.EqualTo(model.TargetHeight).Within(.025f),model.Id);
                    Assert.That(bounds.max.z,Is.EqualTo(0).Within(.025f),model.Id+" ground contact");
                }
                finally {Object.DestroyImmediate(root);}
            }
        }
        [Test]
        public void CanopyFittingKeepsProtectedRoadRectangleClear()
        {
            var model=DioramaCatalog.Load().Get("PackTree03");
            bool Protected(int x,int y)=>x==1;
            Assert.That(DioramaPlacement.TreePosition(model,3,3,2,1,Protected,out var position),Is.True);
            Assert.That(position.x-model.Width*model.Scale*.5f,Is.GreaterThanOrEqualTo(4));
            Assert.That(DioramaPlacement.TreePosition(model,1,3,2,1,Protected,out _),Is.False);
        }
    }
}
