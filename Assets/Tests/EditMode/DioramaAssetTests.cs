using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class DioramaAssetTests
    {
        const string Root = "Assets/Art/Diorama";
        [Test]
        public void FitAdaptersKeepVendorMeshesAndUseColliderFreeBuiltInMaterials()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs",Root+"/Wave4"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths.Length,Is.GreaterThanOrEqualTo(21));
            foreach(var path in paths)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty,path);
                Assert.That(prefab.GetComponentsInChildren<Collider2D>(true),Is.Empty,path);
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
                    var mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    Assert.That(mesh,Is.Not.Null,path);
                    Assert.That(AssetDatabase.GetAssetPath(mesh),Does.StartWith(path.Contains("/Wave4/")?"Assets/RPGMonsterWave4Polyart/":"Assets/RPG Tiny Fantasy World 01 PA/"),path);
                    foreach(var material in renderer.sharedMaterials)
                    {
                        Assert.That(material,Is.Not.Null,path);
                        Assert.That(AssetDatabase.GetAssetPath(material),Does.StartWith(Root+"/Materials/"),path);
                        Assert.That(material.shader.name,Is.Not.EqualTo("Hidden/InternalErrorShader"),path);
                        Assert.That(material.shader.isSupported,Is.True,path);
                        Assert.That(material.mainTexture,Is.Not.Null,path);
                        if(path.Contains("/Wave4/"))Assert.That(AssetDatabase.GetAssetPath(material.mainTexture),Does.StartWith("Assets/RPGMonsterWave4Polyart/Texture/"),path);
                    }
                }
            }
        }

        [Test]
        public void EveryWave4CandidateHasWorkingMovementAttackDamageAndDeathClips()
        {
            var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Wave4"});
            Assert.That(prefabs.Length,Is.EqualTo(10));
            foreach(var guid in prefabs)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var root=Object.Instantiate(prefab);
                try
                {
                    var animator=root.GetComponentInChildren<Animator>();
                    Assert.That(animator,Is.Not.Null,prefab.name);
                    var clips=animator.runtimeAnimatorController.animationClips;
                    foreach(string fragment in new[]{"Idle","Attack","GetHit","Die"})
                        Assert.That(clips.Any(c=>c.name.Contains(fragment)),Is.True,prefab.name+" "+fragment);
                    var move=clips.FirstOrDefault(c=>c.name.EndsWith("WalkFWD")||c.name.EndsWith("MoveFWD")||c.name.EndsWith("FlyFWD"));
                    Assert.That(move,Is.Not.Null,prefab.name);
                    var attack=clips.First(c=>c.name.Contains("Attack"));
                    foreach(var clip in new[]{move,attack})
                    {
                        var bones=animator.GetComponentsInChildren<Transform>(true);
                        clip.SampleAnimation(animator.gameObject,0);
                        var before=bones.Select(b=>b.localToWorldMatrix).ToArray();
                        clip.SampleAnimation(animator.gameObject,clip.length*.37f);
                        Assert.That(bones.Where((b,i)=>b.localToWorldMatrix!=before[i]).Any(),Is.True,prefab.name+" "+clip.name+" must animate the rig");
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
        }
    }
}
