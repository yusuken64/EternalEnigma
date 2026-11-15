using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public sealed class OverworldLockVisualTests
    {
        [Test]
        public void EveryAuthoredLockSkinSelectsExistingVisualAssets()
        {
            var catalog=DioramaCatalog.Load();
            Assert.That(catalog,Is.Not.Null);
            foreach(var skin in LockSkinCatalog.Skins)
            {
                var route=new CampaignRoute("test","a","b",Requirement.Open,skin.Form,skinId:skin.Id);
                var models=OverworldLockVisual.Models(route);
                Assert.That(catalog.Get(models.Model)?.Prefab,Is.Not.Null,skin.Id);
                if(models.Detail!=null)Assert.That(catalog.Get(models.Detail)?.Prefab,Is.Not.Null,skin.Id);
            }
            Assert.That(LockSkinCatalog.Skins.Select(s=>OverworldLockVisual.Models(new CampaignRoute("test","a","b",Requirement.Open,s.Form,skinId:s.Id))).Distinct().Count(),Is.GreaterThan(50));
        }

        [Test]
        public void EveryGalleryModelHasGroundedGeometryAndWorkingClosures()
        {
            var gallery = new LockTestWorld();
            foreach (var station in gallery.Stations)
            {
                var root = new GameObject("Audit " + station.Route.Id);
                try
                {
                    if (!station.Route.IsWarp)
                    {
                        root.transform.rotation = OverworldLockVisual.GateRotation(gallery.Grid, gallery.Grid.Locks.Single(gate => gate.RouteId == station.Route.Id));
                        Assert.That(Mathf.DeltaAngle(root.transform.eulerAngles.z, station.Horizontal ? 90 : 0), Is.EqualTo(0).Within(.001f), station.Label);
                    }
                    OverworldLockVisual.Build(root, station.Route, OverworldCosmetics.Biome(gallery.Grid, station.Approach.X, station.Approach.Y));
                    var visual = root.GetComponent<OverworldLockVisual>();
                    Assert.That(visual, Is.Not.Null, station.Label);
                    var renderers = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                    Assert.That(renderers, Is.Not.Empty, station.Label);
                    Assert.That(renderers.All(r => r.sharedMaterial != null && r.sharedMaterial.shader.isSupported), Is.True, station.Label);
                    var bounds = renderers.Select(r => r.bounds).Aggregate((a,b) => { a.Encapsulate(b); return a; });
                    Assert.That(Mathf.Max(bounds.size.x, bounds.size.y), Is.InRange(.2f, 3f), station.Label);
                    Assert.That(bounds.size.z, Is.InRange(.1f, 3f), station.Label);
                    Assert.That(bounds.max.z, Is.InRange(-.08f, .2f), "Ground contact: " + station.Label);
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                        Assert.That(filter.sharedMesh.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), Is.True, station.Label);
                    visual.SetOpen(true);
                    if (visual.Portcullis != null)
                    {
                        if (station.Horizontal)
                        {
                            var frame = root.transform.Find("Lock obstacle " + visual.ModelId);
                            var frameBounds = frame.GetComponentsInChildren<Renderer>().Where(r => r.transform.parent == frame).Select(r => r.bounds).Aggregate((a,b) => { a.Encapsulate(b); return a; });
                            Assert.That(frameBounds.size.z, Is.LessThan(.5f), "Horizontal frames must expose the closure: " + station.Label);
                        }
                        Assert.That(root.activeSelf, Is.True, "Opening retains the frame: " + station.Label);
                        Assert.That(visual.Portcullis.activeSelf, Is.False, station.Label);
                    }
                    else Assert.That(root.activeSelf, Is.EqualTo(station.Route.IsWarp), station.Label);
                    visual.SetOpen(false);
                    Assert.That(root.activeSelf, Is.True, station.Label);
                    if (visual.Portcullis != null) Assert.That(visual.Portcullis.activeSelf, Is.True, station.Label);
                }
                finally { Object.DestroyImmediate(root); }
            }
            foreach (string skin in new[] { LockSkinCatalog.TownGateKeySkin, LockSkinCatalog.TownAreaKeySkin })
                Assert.That(gallery.Stations.Where(s => s.Route.SkinId == skin).Select(s => OverworldLockVisual.Models(s.Route).Model).Distinct().Count(), Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void ReusedCastleGateUsesPaintedMasonryWithoutChangingSourceMeshes()
        {
            var source = DioramaCatalog.Load().Get("CastleGate").Prefab.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh.uv.ToArray()).ToArray();
            var root = new GameObject("Castle gate texture audit");
            try
            {
                OverworldLockVisual.Build(root, new CampaignRoute("gate", "a", "b", Requirement.Open, LockForm.Obstacle), EternalEnigma.Core.World.OverworldBiome.Grassland);
                var frame = root.transform.Find("Lock obstacle CastleGate");
                Assert.That(frame.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterial.mainTexture != null), Is.True);
                Assert.That(frame.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.uv.All(uv => uv.x > .75f && uv.x < 1 && uv.y > 0 && uv.y < .25f)), Is.True);
                var original = DioramaCatalog.Load().Get("CastleGate").Prefab.GetComponentsInChildren<MeshFilter>();
                for (int i=0;i<original.Length;i++) Assert.That(original[i].sharedMesh.uv, Is.EqualTo(source[i]));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
