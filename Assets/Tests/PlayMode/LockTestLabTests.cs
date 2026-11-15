#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class LockTestLabTests
    {
        private GameTestHarness harness;
        private TestInputScope input;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            input = new TestInputScope();
            harness = new GameTestHarness { TimeoutSeconds = 150 };
            yield return harness.LoadCommon();
            Object.Destroy(Common.Instance.gameObject);
            yield return null;
        }

        [UnityTest, Timeout(420000)]
        public IEnumerator EveryLockAndKeyUsesProductionMenusAndCanBeReset()
        {
            int writesBefore = harness.Store.Writes;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/LockTestLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return harness.WaitUntil(() => LockTestLab.Active?.Ready == true, "all-locks test scene");
            var lab = LockTestLab.Active; var world = lab.World; var context = world.Context;
            var menu = world.GetComponent<OverworldMenuManager>();
            Assert.That(context.IsSandbox, Is.True);
            Assert.That(context.Held.Count, Is.EqualTo(CapabilityCatalog.All.Count));
            Assert.That(context.Keys.Count, Is.EqualTo(24));
            Assert.That(lab.Gallery.Stations.Count, Is.EqualTo((LockSkinCatalog.Skins.Count + 19) * 2 + 8));
            Assert.That(world.Map.GetComponent<TWC.TileWorldCreator>().twcAsset.mapBuildLayers.OfType<OverworldSettlementLayer>().All(layer => !layer.active), Is.True,
                "The lab must not generate extra settlement gates at the test endpoints.");
            Assert.That(world.GetComponentsInChildren<OverworldLockVisual>().Count(v => !v.name.StartsWith("Warp gate ")), Is.EqualTo(context.Grid.Locks.Count));
            Directory.CreateDirectory("Temp/LockTestLab");
            for (int index = 0; index < lab.Gallery.Stations.Count; index++)
            {
                var station = lab.Gallery.Stations[index]; var route = station.Route;
                Assert.That(lab.Visit(index), Is.True, station.Label);
                bool capture = new[] { "climb.area.1", "icewalk.area.1", "phaseshift.area.1", "grapple.obstacle.1", "engineering.interaction.1",
                    "beastspeech.interaction.1", "remedy.interaction.1", "ferrymansfavor.interaction.1", "bellkeepersoffice.interaction.1", LockSkinCatalog.TownGateKeySkin }.Contains(route.SkinId)
                    || route.KeyLabel == "Cairn Stone";
                if (capture)
                    CaptureStation(world, index + 1, "closed");
                if (!route.IsWarp)
                {
                    var visual = world.GetComponentsInChildren<OverworldLockVisual>().Single(v => v.name.EndsWith(" " + route.Id));
                    Assert.That(Mathf.DeltaAngle(visual.transform.eulerAngles.z, station.Horizontal ? 90 : 0), Is.EqualTo(0).Within(.001f), station.Label);
                }
                if (route.ShortcutKind == ShortcutKind.FarSide)
                {
                    Assert.That(lab.Visit(index, true), Is.True);
                    Assert.That(world.OpenShortcut(), Is.True);
                }
                else
                {
                    Assert.That(world.OpenGate(route.Id), Is.True, station.Label);
                    yield return null;
                    Assert.That(menu.PartyMenu.Tab, Is.EqualTo(PartyMenuTab.Capabilities));
                    Assert.That(menu.PartyMenu.ShowAllCapabilities, Is.True, "The lab keeps every acquired option available for auditing.");
                    Assert.That(menu.PartyMenu.EntryButtons.Count, Is.EqualTo(CapabilityCatalog.All.Count + 24));
                    if (index == 0)
                    {
                        ScreenCapture.CaptureScreenshot("Temp/LockTestLab/capabilities.png");
                        yield return new WaitForEndOfFrame();
                    }
                    var actions = route.KeyId != null ? new LockAction[] { new KeyLockAction(route.KeyId) } :
                        route.Requirement.Alternatives[0].Values.Select(capability => (LockAction)new CapabilityLockAction(capability)).ToArray();
                    foreach (var action in actions)
                    {
                        menu.UseGateAction(action, action is KeyLockAction key ? context.Campaign.KeyLabel(key.KeyId) :
                            CampaignRewardNames.Name(context, ((CapabilityLockAction)action).Capability));
                        Assert.That(menu.PartyMenu.Owner.Current, Is.SameAs(menu.TargetDialog));
                        Assert.That(menu.TargetDialog.TargetRouteId, Is.EqualTo(route.Id));
                        if (!route.IsWarp)
                        {
                            var cell = context.Grid.Locks.Single(g => g.RouteId == route.Id).Cells[0];
                            Assert.That(menu.TargetDialog.TryPointTarget(world.ViewCamera.WorldToScreenPoint(world.CellCenterToWorld(cell))), Is.True,
                                "Pointer targeting in either orientation: " + station.Label);
                        }
                        menu.TargetDialog.ConfirmTarget();
                        yield return null;
                    }
                    Assert.That(menu.Opened, Is.False, station.Label);
                }
                Assert.That(context.Gates.NeedsOpening(route), Is.False, station.Label);
                if (capture && (route.KeyId != null || station.Horizontal))
                    CaptureStation(world, index + 1, "open");
                lab.ResetLocks(false);
                Assert.That(context.Gates.NeedsOpening(route), Is.True, station.Label);
                yield return null;
            }
            Assert.That(context.Keys.Count, Is.EqualTo(24));
            Assert.That(context.Held.Count, Is.EqualTo(CapabilityCatalog.All.Count));
            Assert.That(harness.Store.Writes, Is.EqualTo(writesBefore), "The gallery never writes a player save.");
            lab.Visit(lab.Gallery.Stations.ToList().FindIndex(station => station.Route.SkinId == LockSkinCatalog.TownGateKeySkin));
            ScreenCapture.CaptureScreenshot("Temp/LockTestLab/gallery.png");
            yield return new WaitForEndOfFrame();
            File.WriteAllText("Temp/LockTestLab/verification.txt", $"Passed {lab.Gallery.Stations.Count} stations; 24 keys; {context.Held.Count} capabilities; every gate reset; no player save writes.");
        }

        private static void CaptureStation(OverworldScene world, int number, string state)
        {
            var camera = world.ViewCamera;
            var target = new RenderTexture(800, 600, 24);
            var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            float size = camera.orthographicSize;
            try
            {
                camera.orthographicSize = 5.2f;
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); image.Apply();
                File.WriteAllBytes($"Temp/LockTestLab/station-{number:000}-{state}.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; camera.orthographicSize = size; RenderTexture.active = previous;
                Object.Destroy(target); Object.Destroy(image);
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (LockTestLab.Active != null) { Object.Destroy(LockTestLab.Active.gameObject); yield return null; }
            yield return harness.Cleanup();
            input.Dispose();
        }
    }
}
#endif
