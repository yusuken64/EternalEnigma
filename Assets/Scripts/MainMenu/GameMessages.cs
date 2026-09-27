using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Scene-local, non-modal event feed shared by dungeon, town and overworld.</summary>
public sealed class GameMessages : MonoBehaviour
{
    private static GameMessages instance;
    private readonly List<string> history = new();
    private TextMeshProUGUI text;
    private CanvasGroup group;
    private float lastMessage;
    public IReadOnlyList<string> History => history;

    public static string Name(Character character) => character == null ? "Unknown" :
        string.IsNullOrWhiteSpace(character.CharacterName) ? character.name.Replace("(Clone)", "").Trim() : character.CharacterName;

    public static bool Visible(Character character) => character != null &&
        (character is Ally || FogOverlay.Instance == null || FogOverlay.Instance.IsCurrentlyVisible(character.transform.position, character.FootPrint));

    public static string VisibleName(Character character) => Visible(character) ? Name(character) : "an unseen target";

    public static void ForCharacter(Character character, string message)
    {
        if (Visible(character)) Post(message);
    }

    public static void Post(string message, bool coalesce = false)
    {
        if (!Application.isPlaying || string.IsNullOrWhiteSpace(message)) return;
        if (instance == null) instance = new GameObject("Game messages").AddComponent<GameMessages>();
        instance.Add(message, coalesce);
    }

    private void Awake()
    {
        instance = this;
        var canvas = GameUISkin.Canvas("Message display", transform, 55);
        var panel = GameUISkin.Panel(canvas.transform, new Vector2(.32f, .76f), new Vector2(.72f, .97f));
        panel.raycastTarget = false;
        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;
        var heading = GameUISkin.Label(panel.transform, "RECENT EVENTS", new Vector2(.035f, .8f), new Vector2(.965f, .95f), 20);
        heading.color = new Color(.68f, .74f, .76f);
        text = GameUISkin.Label(panel.transform, "", new Vector2(.035f, .06f), new Vector2(.965f, .78f), 24);
        text.richText = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.BottomLeft;
    }

    private void Add(string message, bool coalesce)
    {
        // Holding movement against a locked gate should not flood the display.
        if (coalesce && history.Count > 0 && history[history.Count - 1] == message && Time.unscaledTime - lastMessage < 1f) return;
        history.Add(message);
        if (history.Count > 100) history.RemoveAt(0);
        lastMessage = Time.unscaledTime;
        int start = Mathf.Max(0, history.Count - 5);
        text.text = string.Join("\n", history.GetRange(start, history.Count - start));
        group.alpha = 1;
    }

    private void Update() => group.alpha = 1 - Mathf.Clamp01((Time.unscaledTime - lastMessage - 9f) / 2f);
    private void OnDestroy() { if (instance == this) instance = null; }
}
