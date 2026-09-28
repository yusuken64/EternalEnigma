#if UNITY_EDITOR
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FloorMessageTests
{
    [Test]
    public void VictoryMessageHasRoomAndRendersAboveTheDungeonHud()
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
            player.Floor = 40;
            player.Gold = 1000000;
            screen.Setup(player, true);

            Assert.That(screen.GetComponent<Canvas>().overrideSorting, Is.True);
            Assert.That(screen.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(2));
            Assert.That(screen.MessageText.rectTransform.anchorMax.x - screen.MessageText.rectTransform.anchorMin.x,
                Is.GreaterThan(.6f));
            Assert.That(screen.MessageText.text, Is.EqualTo("Victory!\nFinal dungeon cleared\nTreasure: 1,000,000"));
            Assert.That(root.transform.Find("Result message backdrop"), Is.Not.Null);
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(root); }
    }

    [Test]
    public void TransitionFadeShowsTheDestinationFloorNumber()
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
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Current;
            message.ShowNewFloor(1);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 1"));
            message.HideScreen(2);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 2"));
            message.ShowNewFloor(2);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 2"));

            DungeonPreferences.AnimationOverride = DungeonAnimationMode.None;
            message.HideScreen(3);
            Assert.That(message.gameObject.activeSelf, Is.True);
            Assert.That(message.FloorMessage.text, Is.EqualTo("Floor 3"));
            message.ShowNewFloor(3);
            Assert.That(message.gameObject.activeSelf, Is.False);
        }
        finally
        {
            label.transform.DOKill();
            DungeonPreferences.AnimationOverride = previousMode;
            Object.DestroyImmediate(root);
        }
    }
}
#endif
