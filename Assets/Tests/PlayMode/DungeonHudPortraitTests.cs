#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonHudPortraitTests
    {
        private GameTestHarness harness;
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

        [UnityTest] public IEnumerator PartyHudBindsDistinctPortraitsAndKeepsThemAcrossFloors()
        {
            harness = new GameTestHarness();
            var scenario = new TestScenario { AdditionalAllies = new[] { "Avery", "Reese", "Sage" } };
            yield return harness.LoadDungeon(scenario);
            void Check()
            {
                Assert.That(harness.Game.CharacterStatsDisplays.Count, Is.EqualTo(4));
                foreach (var display in harness.Game.CharacterStatsDisplays)
                {
                    var ally = (Ally)display.Character;
                    Assert.That(ally.Portrait, Is.Not.Null);
                    Assert.That(display.PortraitImage, Is.Not.Null);
                    Assert.That(display.PortraitImage.sprite, Is.SameAs(ally.Portrait));
                    Assert.That(display.PortraitImage.isActiveAndEnabled, Is.True);
                }
                Assert.That(harness.Game.CharacterStatsDisplays.Select(d => d.PortraitImage.sprite).Distinct().Count(), Is.EqualTo(4));
            }
            Check();
            harness.Game.AdvanceFloor(); yield return harness.WaitForIdle();
            Check();
            System.IO.Directory.CreateDirectory("Temp/HeroPortraitPreview");
            yield return new WaitForSeconds(.25f);
            GameMessages.BeginTurn();
            GameMessages.Post("Rowan attacked Slime!");
            GameMessages.Post("Slime took 12 damage.");
            GameMessages.Post("Slime died.");
            GameMessages.Post("Rowan leveled up.");
            DungeonFloatingText.Show(harness.Game,"Level Up",new Color(.84f,.76f,1f),harness.Ally.transform.position,true);
            Canvas.ForceUpdateCanvases();
            var party = DungeonHud.Ensure(harness.Game).PartyRoot as RectTransform;
            foreach(var display in harness.Game.CharacterStatsDisplays)
            {
                var rect=(RectTransform)display.transform;
                Assert.That(rect.anchorMin.y,Is.GreaterThanOrEqualTo(0),"All four heroes fit in the reserved party column.");
                Assert.That(rect.anchorMax.y,Is.LessThanOrEqualTo(1));
            }
            yield return null;
            Assert.That(Object.FindFirstObjectByType<DungeonFloatingText>(),Is.Not.Null);
            ScreenCapture.CaptureScreenshot("Temp/HeroPortraitPreview/dungeon-hud.png");
            yield return null;
        }
    }
}
#endif
