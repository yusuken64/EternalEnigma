#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonThemeTransitionTests
    {
        GameTestHarness harness;
        bool? previousControl;
        [UnitySetUp] public IEnumerator Setup() {previousControl=DungeonPreferences.FullControlOverride;DungeonPreferences.FullControlOverride=false;harness=new GameTestHarness {TimeoutSeconds=100};yield return null;}
        [UnityTearDown] public IEnumerator Cleanup(){yield return harness.Cleanup();DungeonPreferences.FullControlOverride=previousControl;}
        [UnityTest]
        public IEnumerator InteriorOutdoorTransitionsKeepSelectionFogAndSingleOutput()
        {
            yield return harness.LoadDungeon(new TestScenario {Seed=12345});
            var generator=Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
            Assert.That(generator.CurrentVisuals.IsLegacy,Is.False);
            Directory.CreateDirectory("Docs/Art/Previews/DungeonThemes");
            ScreenCapture.CaptureScreenshot("Docs/Art/Previews/DungeonThemes/Gameplay_Grassland.png");yield return null;
            var choices=generator.ThemeCatalog.Themes.Select(t=>new DungeonVisualSelection{Biome=t.Biome,Environment=t.Environment,UseBiomePresentation=true}).ToArray();
            foreach(var choice in choices)
            foreach(bool throne in new[]{false,true})
            {
                var old=GameObject.Find("TileWorldCreator_Map").GetComponentsInChildren<EnvironmentMeshOwner>().SelectMany(o=>o.Meshes).ToArray();
                var save=Common.Instance.GameSaveData.DungeonSaveData;save.VisualSelection=choice;
                Game.Instance.PlayerController.Floor=1;save.StartFloor=1;save.EndFloor=throne?2:5;
                Game.Instance.AdvanceFloor();
                yield return harness.WaitForIdle();yield return null;
                Assert.That(generator.CurrentVisuals.Biome,Is.EqualTo(choice.Biome));
                Assert.That(generator.CurrentVisuals.Environment,Is.EqualTo(choice.Environment));
                Assert.That(generator.CurrentFloor.Seed,Is.EqualTo(12345));
                var root=GameObject.Find("TileWorldCreator_Map");
                Assert.That(Game.Instance.CurrentDungeon.IsThroneFloor,Is.EqualTo(throne));
                Assert.That(root.transform.Cast<Transform>().Count(t=>t.name=="Theme cosmetics"),Is.EqualTo(choice.IsLegacy?0:1));
                if(!choice.IsLegacy)Assert.That(root.GetComponentsInChildren<EnvironmentMeshOwner>().Count(o=>o.name!="Biome decorations"),Is.EqualTo(4));
                Assert.That(root.transform.Find("Biome decorations"),Is.Not.Null);
                var fog=Object.FindFirstObjectByType<FogOverlay>();
                foreach(var mount in root.GetComponentsInChildren<BiomeDecorationFog>()) {
                    Assert.That(mount.Preview,Is.False);
                    Assert.That(mount.GetComponent<BiomeDecorationEffect>().FloorVisible,Is.EqualTo(fog.IsCurrentlyVisible(mount.FloorPosition)));
                }
                foreach(var mesh in old) Assert.That(mesh==null,Is.True,"Old combined mesh must be destroyed.");
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()) Assert.That(renderer.bounds.min.z,Is.GreaterThan(-3.35f),renderer.name);
                Assert.That(Object.FindFirstObjectByType<FogOverlay>().fogOverlayQuad.transform.position.z,Is.EqualTo(-3.35f));
                if(choice.Biome==OverworldBiome.Desert&&choice.Environment==DungeonEnvironmentKind.Interior&&!throne)
                    yield return BiomeDecorationCapture.Audit(root,"Dungeon");
                ScreenCapture.CaptureScreenshot("Docs/Art/Previews/DungeonThemes/Gameplay_"+choice.Biome+"_"+choice.Environment+(throne?"_Throne":"_Regular")+".png");yield return null;
            }
        }
    }
}
#endif
