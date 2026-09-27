#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class GameMessageTests
    {
        private GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness(); yield return harness.LoadDungeon(new TestScenario()); }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

        [UnityTest]
        public IEnumerator ResolvedDamageAndOverheadMessagesReachNonBlockingFeed()
        {
            var ally = harness.Ally;
            ally.CharacterName = "Aria";
            var hit = new TakeDamageAction(ally, ally, 5) { Environmental = true };
            ally.ExecuteActionImmediate(hit);
            var feed = Object.FindFirstObjectByType<GameMessages>();
            Assert.That(feed, Is.Not.Null);
            Assert.That(feed.History, Does.Contain("Aria took 5 damage."));
            harness.Game.DoFloatingText("Level Up", Color.yellow, ally.transform.position);
            Assert.That(feed.History.Last(), Does.Contain("Level Up"));
            GameMessages.Post("Requires key", true); GameMessages.Post("Requires key", true);
            Assert.That(feed.History.Count(x => x == "Requires key"), Is.EqualTo(1));
            GameMessages.Post("Gate opened!");
            Assert.That(feed.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Gate opened!")), Is.True);
            Assert.That(feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>(), Is.Not.Null);
            Assert.That(feed.GetComponentsInChildren<TMP_Text>().Last().richText, Is.False);
            yield return null;
            System.IO.Directory.CreateDirectory("Temp/MessagePreview");
            ScreenCapture.CaptureScreenshot("Temp/MessagePreview/messages.png");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenEnemiesDoNotPublishAndHistoryIsBounded()
        {
            yield return harness.SpawnEnemy("Enemy_Slime", harness.Ally.TilemapPosition + Vector3Int.right);
            var enemy = harness.Game.Enemies.Last();
            enemy.SetPosition(new Vector3Int(-100, -100));
            GameMessages.Post("Party ready");
            var feed = Object.FindFirstObjectByType<GameMessages>();
            Assert.That(GameMessages.Visible(enemy), Is.False);
            GameMessages.ForCharacter(enemy, "Secret enemy action");
            Assert.That(feed.History, Does.Not.Contain("Secret enemy action"));
            GameMessages.Post("Aria took 5 damage."); GameMessages.Post("Aria took 5 damage.");
            Assert.That(feed.History.Count(x => x == "Aria took 5 damage."), Is.EqualTo(2), "Repeated hits must not be suppressed as repeated hints.");
            for (int i = 0; i < 120; i++) GameMessages.Post("Event " + i);
            Assert.That(feed.History.Count, Is.EqualTo(100));
            Assert.That(feed.History.Last(), Is.EqualTo("Event 119"));
        }
    }
}
#endif
