#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonThemeExplorerTests
    {
        Scene scene;
        [UnityTest]
        public IEnumerator PlaygroundExploresAllThemesAndRestoresOtherViews()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/EnvironmentPlayground.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            scene=SceneManager.GetSceneByName("EnvironmentPlayground");yield return null;
            var p=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EnvironmentPlayground>()).Single();
            var explorer=p.DungeonExplorer;
            var button=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true)).Single(b=>b.name=="Dungeon themes");
            button.onClick.Invoke();yield return Ready();
            Assert.That(p.View,Is.EqualTo(4));Assert.That(p.Gallery.gameObject.activeSelf,Is.False);Assert.That(explorer.Controls.activeSelf,Is.True);
            foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
            foreach(DungeonEnvironmentKind kind in Enum.GetValues(typeof(DungeonEnvironmentKind)))
            foreach(bool throne in new[]{false,true})
            {
                explorer.Biome=biome;explorer.Environment=kind;explorer.Throne=throne;explorer.Rebuild();yield return Ready();yield return null;
                Assert.That(explorer.Floor.IsThroneFloor,Is.EqualTo(throne));
                Assert.That(explorer.WorldRoot.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThan(0));
                Assert.That(explorer.WorldRoot.transform.Cast<Transform>().Count(t=>t.name=="Theme cosmetics"),Is.EqualTo(biome==OverworldBiome.Grassland && kind==DungeonEnvironmentKind.Interior?0:1));
            }
            explorer.SeedInput.onEndEdit.Invoke("90210");yield return Ready();Assert.That(explorer.Floor.Seed,Is.EqualTo(90210));
            Assert.That(p.Seed,Is.EqualTo(90210));
            p.ShowGallery();Assert.That(explorer.WorldRoot.activeSelf,Is.False);Assert.That(explorer.Controls.activeSelf,Is.False);
            p.ShowDungeon();Assert.That(explorer.WorldRoot.activeSelf,Is.True);Assert.That(explorer.Controls.activeSelf,Is.True);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<Common>(),Is.Null,"Explorer must not load Common or touch campaign saves.");
            System.IO.Directory.CreateDirectory("Docs/Art/Previews/DungeonThemes");
            ScreenCapture.CaptureScreenshot("Docs/Art/Previews/DungeonThemes/PlaygroundExplorer.png");yield return null;
            IEnumerator Ready()
            {
                float deadline=Time.realtimeSinceStartup+60;
                while(!explorer.IsReady && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(explorer.IsReady,Is.True,"Dungeon theme build timed out.");
            }
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
#endif
