#if UNITY_EDITOR
using DG.Tweening;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

[PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public sealed class FloorMessageTests
{
    private GameTestHarness harness;
    private DungeonAnimationMode? previousAnimation;

    [UnitySetUp] public IEnumerator Setup()
    {
        previousAnimation = DungeonPreferences.AnimationOverride;
        DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
        harness = new GameTestHarness();
        // These presentation components use the real settings, save context,
        // theme and EventSystem. Keep that context in the harness's memory store.
        yield return harness.LoadCommon();
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        DungeonPreferences.AnimationOverride = previousAnimation;
        yield return harness.Cleanup();
    }

    [UnityTest]
    public IEnumerator VictoryMessageHasRoomAndRendersAboveTheDungeonHud()
    {
        var root = new GameObject("Result screen", typeof(RectTransform));
        var screen = root.AddComponent<GameOverScreen>();
        var text = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(root.transform, false);
        var button = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(root.transform, false);
        var player = new GameObject("Player controller").AddComponent<PlayerController>();
        try
        {
            screen.MessageText = text.GetComponent<TextMeshProUGUI>();
            screen.OkButton = button.GetComponent<Button>();
            screen.AuthorCanvas();
            screen.AuthorLayout();
            player.Floor = 40;
            player.Gold = 1000000;
            screen.Setup(player, true);

            Assert.That(screen.GetComponent<Canvas>().isRootCanvas || screen.GetComponent<Canvas>().overrideSorting, Is.True);
            Assert.That(screen.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(2));
            Assert.That(screen.MessageText.rectTransform.anchorMax.x - screen.MessageText.rectTransform.anchorMin.x,
                Is.GreaterThan(.6f));
            Assert.That(screen.MessageText.text, Is.EqualTo("Victory!\nFinal dungeon cleared\nGold 1,000,000"));
            Assert.That(root.transform.Find("Result message backdrop"), Is.Not.Null);
            Common.Instance.GameSaveData.Completion = new CampaignCompletionRecord {
                CompletedUtc = "2026-10-15T00:00:00Z", DungeonsCleared = 24, Gold = 1000000
            };
            screen.Setup(player, true);
            Assert.That(screen.MessageText.text, Does.StartWith("Hall of Fame\n24 dungeons cleared"));
            Assert.That(screen.MessageText.text, Does.Contain("1,000,000 treasure"));
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(root); }
        yield break;
    }

    [UnityTest]
    public IEnumerator TransitionFadeShowsTheDestinationFloorNumber()
    {
        var previousMode = DungeonPreferences.AnimationOverride;
        var root = new GameObject("Floor overlay test", typeof(RectTransform), typeof(Canvas));
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var label = new GameObject("Floor label", typeof(RectTransform), typeof(TextMeshProUGUI));
        background.transform.SetParent(root.transform);
        label.transform.SetParent(root.transform);
        var message = root.AddComponent<NewFloorMessage>();
        message.BackgroundColor = background.GetComponent<Image>();
        message.FloorMessage = label.GetComponent<TextMeshProUGUI>();
        try
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            message.ShowNewFloor(1);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 1"));
            message.HideScreen(2);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 2"));
            message.ShowNewFloor(2);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 2"));

            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            message.HideScreen(3);
            Assert.That(message.gameObject.activeSelf, Is.True);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 3"));
            Assert.That(message.BackgroundColor.canvasRenderer.GetAlpha(), Is.EqualTo(1).Within(.001f),
                "No Animations still needs an opaque loading cover.");
            Assert.That(message.FloorMessage.canvasRenderer.GetAlpha(), Is.EqualTo(1).Within(.001f),
                "Reopening a cover must restore the label after an interrupted reveal.");
            message.ShowNewFloor(3);
            Assert.That(message.gameObject.activeSelf, Is.False);
        }
        finally
        {
            label.transform.DOKill();
            DungeonPreferences.AnimationOverride = previousMode;
            Object.DestroyImmediate(root);
        }
        yield break;
    }
}
#endif
