using System.Collections;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class OverworldPresentationTests
    {
        GameTestHarness harness;Scene scene;
        [UnitySetUp]public IEnumerator Setup(){harness=new GameTestHarness();yield return harness.LoadCommon();}
        [UnityTearDown]public IEnumerator Cleanup(){if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);yield return harness.Cleanup();}
        [UnityTest]public IEnumerator FixedSeedsAndRestoredTerrainKeepRoutesAndLocationsVisible()
        {
            string stage=File.Exists("Temp/OverworldPresentationStage.txt")?File.ReadAllText("Temp/OverworldPresentationStage.txt").Trim():"After";
            Directory.CreateDirectory("Temp/OverworldPresentation/"+stage);
            foreach(int seed in new[]{42,12345})
            {
                Common.Instance.BeginSandbox(seed);
                var context=Common.Instance.CampaignContext;
                var companion=context.Campaign.Companions.First();context.Roster.Add(companion.Id);context.SetParty(new[]{companion.Id});
                for(int visit=0;visit<2;visit++)
                {
                    yield return SceneManager.LoadSceneAsync("Overworld",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("Overworld");
                    var world=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<OverworldScene>()).Single();
                    harness.TimeoutSeconds=120;yield return harness.WaitUntil(()=>world.IsReady,"overworld presentation");
                    var grid=world.Map.CurrentGrid;
                    foreach(var location in context.Campaign.Locations.Where(l=>l.ParentTownId==null))
                        Assert.That(world.transform.Find(location.Id).gameObject.activeSelf,Is.True,"Occupied locations must keep their models.");
                    var camera=world.ViewCamera;var original=camera.transform.position;
                    foreach(var location in context.Campaign.Locations.Where(l=>l.ParentTownId==null && l.Kind!=LocationKind.Town))
                    {
                        var marker=world.transform.Find(location.Id);
                        var plaza=marker.Find("Point of interest plaza");
                        Assert.That(plaza.GetComponent<Renderer>().sharedMaterial,Is.SameAs(EnvironmentKit.Load().Paving));
                        foreach(var mesh in marker.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.EndsWith("perimeter post")))
                            foreach(var uv in mesh.sharedMesh.uv)Assert.That(Vector2Int.FloorToInt(uv*4),Is.EqualTo(new Vector2Int(3,0)),"A location post must not sample the whole atlas.");
                    }
                    var nearby=context.Campaign.Locations.Where(l=>l.ParentTownId==null && l.Kind!=LocationKind.Town)
                        .OrderBy(l=>(world.transform.Find(l.Id).position-world.Player.transform.position).sqrMagnitude).First();
                    camera.transform.position=world.transform.Find(nearby.Id).position+world.CameraOffset;camera.orthographicSize=5;
                    Capture(camera,$"{stage}/location-platform-{seed}-{visit}");
                    var terrain=world.Map.GetComponent<TWC.TileWorldCreator>().worldObject;
                    var mountain=terrain.GetComponentsInChildren<EnvironmentMeshOwner>().FirstOrDefault(o=>o.name.StartsWith("Smart/Mountains"));
                    if(mountain!=null)
                    {
                        var renderer=mountain.GetComponentsInChildren<MeshRenderer>().First();
                        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                        var center=renderer.transform.TransformPoint(mesh.vertices[0]);
                        camera.transform.position=center+world.CameraOffset;camera.orthographicSize=6;
                        Capture(camera,$"{stage}/terrain-{seed}-{visit}");
                    }
                    var gate=grid.Locks.First(g=>g.Cells.Any(c=>!grid.RequiresBoat(c)));
                    var cell=gate.Cells.First(c=>!grid.RequiresBoat(c));camera.transform.position=world.CellCenterToWorld(cell)+world.CameraOffset;
                    camera.orthographicSize=4;
                    Capture(camera,$"{stage}/gate-{seed}-{visit}");
                    camera.transform.position=original;camera.orthographicSize=8;
                    Capture(camera,$"{stage}/occupied-{seed}-{visit}");
                    Assert.That(context.Gates.IsWalkable(cell,context.Held),Is.False,"Presentation cannot unlock a route.");
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
        }
        static void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes("Temp/OverworldPresentation/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
        }
    }
}
