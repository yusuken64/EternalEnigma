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
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness { TimeoutSeconds = 180 }; yield return harness.LoadDungeon(new TestScenario()); }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

        [UnityTest]
        public IEnumerator NewMessagesPushEarlierLinesUpFromTheBottom()
        {
            var feed = Object.FindFirstObjectByType<GameMessages>();
            var scroll = feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            var text = scroll.content.GetComponent<TMP_Text>();
            ((System.Collections.Generic.List<string>)feed.History).Clear();
            GameMessages.Post("First event");
            yield return null; yield return null; text.ForceMeshUpdate();
            Assert.That(text.alignment, Is.EqualTo(TextAlignmentOptions.BottomLeft));
            float bottomBaseline = text.textInfo.lineInfo[0].baseline;
            Assert.That(text.textInfo.lineInfo[0].descender, Is.EqualTo(text.rectTransform.rect.yMin + text.margin.w).Within(1));
            System.IO.Directory.CreateDirectory("Temp/HistoryLayoutCapture");
            ScreenCapture.CaptureScreenshot("Temp/HistoryLayoutCapture/bottom-first.png");
            yield return null;
            GameMessages.Post("Second event");
            yield return null; yield return null; text.ForceMeshUpdate();
            Assert.That(text.textInfo.lineCount, Is.EqualTo(2));
            Assert.That(text.textInfo.lineInfo[0].baseline, Is.GreaterThan(bottomBaseline));
            Assert.That(text.textInfo.lineInfo[1].baseline, Is.EqualTo(bottomBaseline).Within(.1f));
            GameMessages.Post("Third event");
            yield return null; yield return null;
            ScreenCapture.CaptureScreenshot("Temp/HistoryLayoutCapture/bottom-three.png");
            yield return null;
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator CompactFeedFitsWholeLinesKeepsPaddingAndScrollsHistory()
        {
            var feed=Object.FindFirstObjectByType<GameMessages>();
            var scroll=feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            var text=scroll.content.GetComponent<TMP_Text>();
            var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var sizeProperty=view.GetType().GetProperty("selectedSizeIndex",flags);
            var originalSize=sizeProperty.GetValue(view);
            bool originalTerminal=TerminalMode.Requested;
            try
            {
                TerminalMode.SetRequested(false);
                System.IO.Directory.CreateDirectory("Temp/HistoryLayoutCapture");
                foreach(var size in new[]{new Vector2(960,600),new Vector2(1280,720),new Vector2(1920,1080),new Vector2(1280,800),new Vector2(2560,1080)})
                {
                    view.GetType().GetMethod("SetCustomResolution",flags).Invoke(view,new object[]{size,"Scrollable event feed"});
                    yield return null; yield return null;
                    ((System.Collections.Generic.List<string>)feed.History).Clear();
                    GameMessages.BeginTurn();
                    for(int i=1;i<=14;i++) GameMessages.Post($"Event {i} from the party.");
                    yield return null; Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
                    UnifiedPartyMenuTests.AssertEventFeedWidth();
                    Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>(),Is.Not.Null);
                    Assert.That(scroll.viewport.offsetMin.y,Is.GreaterThanOrEqualTo(16));
                    Assert.That(text.fontSize,Is.EqualTo(33.11f).Within(.001f));
                    Assert.That(text.enableAutoSizing,Is.False);
                    Assert.That(text.textInfo.lineCount,Is.EqualTo(14));
                    Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(0).Within(.01f));
                    int fullyVisible=0;
                    for(int line=0;line<text.textInfo.lineCount;line++)
                    {
                        var info=text.textInfo.lineInfo[line];
                        float bottom=scroll.viewport.InverseTransformPoint(text.transform.TransformPoint(new Vector3(0,info.descender))).y;
                        float top=scroll.viewport.InverseTransformPoint(text.transform.TransformPoint(new Vector3(0,info.ascender))).y;
                        if(bottom>=scroll.viewport.rect.yMin-1 && top<=scroll.viewport.rect.yMax+1) fullyVisible++;
                    }
                    Assert.That(fullyVisible,Is.InRange(5,13),$"{size} must show five or more complete lines above the lower border.");
                    ScreenCapture.CaptureScreenshot($"Temp/HistoryLayoutCapture/compact-{size.x}x{size.y}.png");
                    yield return new WaitForSecondsRealtime(.15f);
                    scroll.verticalNormalizedPosition=1;
                    float readingOffset=scroll.content.anchoredPosition.y;
                    GameMessages.Post("An event while reading.");
                    yield return null; yield return null;
                    Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(readingOffset).Within(1));
                    scroll.verticalNormalizedPosition=0;
                    GameMessages.ShowHistory(); yield return null;
                    var history=(EventHistoryDialog)MenuManager.Instance.CurrentDialog;
                    UnifiedPartyMenuTests.AssertExpandedHistoryLayout(history);
                    Assert.That(history.Entries.text,Does.Contain("Event 1 from the party."));
                    history.CloseDialog(); yield return null; yield return null;
                }
                string longEvent=string.Join(" ",Enumerable.Repeat("The party continues exploring the dungeon.",30));
                GameMessages.Post(longEvent); GameMessages.Post("Newest event");
                yield return null; text.ForceMeshUpdate();
                Assert.That(text.text,Does.Contain(longEvent).And.EndWith("Newest event"));
                Assert.That(text.rectTransform.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(0).Within(.01f));
            }
            finally {sizeProperty.SetValue(view,originalSize);TerminalMode.SetRequested(originalTerminal);}
        }

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
            yield return null; // Model events coalesce into one refresh before drawing.
            Assert.That(feed.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Gate opened!")), Is.True);
            Assert.That(feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>(), Is.Not.Null);
            Assert.That(feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content.GetComponent<TMP_Text>().richText, Is.False);
            yield return null;
            System.IO.Directory.CreateDirectory("Temp/MessagePreview");
            ScreenCapture.CaptureScreenshot("Temp/MessagePreview/messages.png");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AbilitiesAndFloatingTextUseReadableCopy()
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            try
            {
                harness.Ally.CharacterName = "Aria";
                skill.SkillName = "Command: Advance";
                GameMessages.AbilityCast(harness.Ally, skill, "Bow02");
                var feed = Object.FindFirstObjectByType<GameMessages>();
                Assert.That(feed.History.Last(), Is.EqualTo("Aria casts Advance Order using Hunter's Bow."));
                harness.Game.DoFloatingText("[Info] HPMax +10 (3 turns)", Color.cyan, harness.Ally);
                yield return null;
                var callouts = Object.FindObjectsByType<DungeonFloatingText>(FindObjectsSortMode.None);
                Assert.That(callouts.Any(c => c.GetComponentInChildren<TMP_Text>().text.Trim() == "maximum HP +10 for 3 turns"), Is.True);
                Assert.That(feed.History.Last(), Does.Not.Match(@"[\[\]()_:{}]"));
                Assert.That(skill.SkillName, Is.EqualTo("Command: Advance"));
                System.IO.Directory.CreateDirectory("Temp/GameplayCopy");
                ScreenCapture.CaptureScreenshot("Temp/GameplayCopy/readable-combat.png");
                yield return null;
            }
            finally { Object.Destroy(skill); }
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
