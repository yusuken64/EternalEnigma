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
        [UnitySetUp] public IEnumerator Setup() {harness=new GameTestHarness {TimeoutSeconds=100};yield return null;}
        [UnityTearDown] public IEnumerator Cleanup()=>harness.Cleanup();
        [UnityTest]
        public IEnumerator InteriorOutdoorTransitionsKeepSelectionFogAndSingleOutput()
        {
            yield return harness.LoadDungeon(new TestScenario {Seed=12345});
            var generator=Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
            Assert.That(generator.CurrentVisuals.IsLegacy,Is.True);
            Directory.CreateDirectory("Docs/Art/Previews/DungeonThemes");
            ScreenCapture.CaptureScreenshot("Docs/Art/Previews/DungeonThemes/Gameplay_Grassland.png");yield return null;
            var choices=new[]{new DungeonVisualSelection {Biome=OverworldBiome.Water,Environment=DungeonEnvironmentKind.Outdoor},
                new DungeonVisualSelection {Biome=OverworldBiome.Desert,Environment=DungeonEnvironmentKind.Interior},
                new DungeonVisualSelection {Biome=OverworldBiome.Volcanic,Environment=DungeonEnvironmentKind.Outdoor},
                new DungeonVisualSelection {Biome=OverworldBiome.Water,Environment=DungeonEnvironmentKind.Interior}};
            foreach(var choice in choices)
            {
                var old=GameObject.Find("TileWorldCreator_Map").GetComponentsInChildren<EnvironmentMeshOwner>().SelectMany(o=>o.Meshes).ToArray();
                var save=Common.Instance.GameSaveData.DungeonSaveData;save.VisualSelectionVersion=1;save.VisualSelection=choice;
                Game.Instance.AdvanceFloor();
                yield return harness.WaitForIdle();yield return null;
                Assert.That(generator.CurrentVisuals.Biome,Is.EqualTo(choice.Biome));
                Assert.That(generator.CurrentVisuals.Environment,Is.EqualTo(choice.Environment));
                Assert.That(generator.CurrentFloor.Seed,Is.EqualTo(12345));
                var root=GameObject.Find("TileWorldCreator_Map");
                Assert.That(root.transform.Cast<Transform>().Count(t=>t.name=="Theme cosmetics"),Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<EnvironmentMeshOwner>().Length,Is.EqualTo(4));
                foreach(var mesh in old) Assert.That(mesh==null,Is.True,"Old combined mesh must be destroyed.");
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()) Assert.That(renderer.bounds.min.z,Is.GreaterThan(-3.35f),renderer.name);
                Assert.That(Object.FindFirstObjectByType<FogOverlay>().fogOverlayQuad.transform.position.z,Is.EqualTo(-3.35f));
                ScreenCapture.CaptureScreenshot("Docs/Art/Previews/DungeonThemes/Gameplay_"+choice.Biome+"_"+choice.Environment+".png");yield return null;
            }
        }
    }
}
#endif
