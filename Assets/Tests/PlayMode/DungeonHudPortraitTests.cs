#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        internal static void CheckIndicator(Game game, Ally ally, bool active, bool exhausted = false)
        {
            var display = game.CharacterStatsDisplays.Single(d => d.Character == ally);
            display.GetComponent<DungeonPartyCard>().SendMessage("Update");
            ally.SendMessage("LateUpdate");
            var circle = ally.CirlcleRenderer;
            var ring = circle.transform.Find("Turn ring").GetComponent<SpriteRenderer>();
            Assert.That(display.PortraitImage.transform.Find("Turn circle"), Is.Null);
            Assert.That(ring.enabled, Is.EqualTo(active));
            var highlight = display.GetComponentsInChildren<Image>().Single(i => i.name == "Turn highlight");
            Assert.That(highlight.enabled, Is.EqualTo(active));
            Assert.That(highlight.color, Is.EqualTo(ring.color));
            Assert.That(highlight.raycastTarget, Is.False);
            var color = game.PlayerController.ControlledAlly == ally ? ally.PlayerColor : ally.AllyColor;
            if (exhausted) color = new Color(color.r*.35f,color.g*.35f,color.b*.35f,color.a);
            Assert.That(circle.color, Is.EqualTo(color));
            Assert.That(display.GetComponentsInChildren<TMPro.TMP_Text>().Any(t =>
                t.text == "Your order" || t.text == "Done" || t.text.EndsWith(" actions")), Is.False);
        }

        [UnityTest] public IEnumerator IndicatorsUseDisplayedVitalsAndSuppressDownedRing()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Avery" } });
            var game = harness.Game;
            var hero = harness.Ally;
            // Simulation has already consumed actions/damage, but playback has not shown them yet.
            hero.Vitals.ActionsPerTurnLeft = 0;
            hero.Vitals.HP = 0;
            CheckIndicator(game,hero,true);
            hero.DisplayedVitals.ActionsPerTurnLeft = 0;
            CheckIndicator(game,hero,true,true);
            hero.DisplayedVitals.HP = 0;
            var display = game.CharacterStatsDisplays.Single(d => d.Character == hero);
            display.GetComponent<DungeonPartyCard>().SendMessage("Update");
            hero.SendMessage("LateUpdate");
            Assert.That(hero.CirlcleRenderer.transform.Find("Turn ring").GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(display.GetComponentsInChildren<Image>().Single(i => i.name == "Turn highlight").enabled, Is.False);
            Assert.That(hero.CirlcleRenderer.color,
                Is.EqualTo(new Color(.42f,.44f,.44f)));
        }
    }
}
#endif
