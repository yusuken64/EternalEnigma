using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local, non-modal event feed shared by dungeon, town and overworld.</summary>
public sealed class GameMessages : MonoBehaviour
{
    private static GameMessages instance;
    private readonly List<string> history = new();
    private TextMeshProUGUI text;
    private CanvasGroup group;
    private float lastMessage;
    private readonly List<string> turnEvents = new();
    private ScrollRect scroll;
    private bool expanded;
    private bool inDungeonTurn;
    public IReadOnlyList<string> TurnEvents => turnEvents;
    public static void BeginTurn()
    {
        if (instance == null) instance = new GameObject("Game messages").AddComponent<GameMessages>();
        instance.inDungeonTurn = true; instance.turnEvents.Clear(); instance.Render();
    }
    public static void FinishAction() { if (instance != null) instance.Render(); }
    public static void ToggleHistory() { if (instance != null) { instance.expanded = !instance.expanded; instance.Render(); } }

    public static void ShowHistory()
    {
        if (instance == null) instance = new GameObject("Game messages").AddComponent<GameMessages>();
        instance.expanded = true;
        instance.Render();
    }

    public IReadOnlyList<string> History => history;

    public static string Name(Character character)
    {
        if (character == null) return "Unknown";
        if (EnemyBehavior.IsDisguised(character)) return "Treasure chest";
        if (!string.IsNullOrWhiteSpace(character.CharacterName)) return character.CharacterName;
        if (character is Enemy enemy) return string.IsNullOrWhiteSpace(enemy.DisplayName) ? "Enemy" : enemy.DisplayName;
        return character.name.Replace("(Clone)", "").Trim();
    }

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
        var canvas = GameUISkin.Canvas("Message display", transform, 2);
        bool dungeon = Game.Instance != null;
        var panel = GameUISkin.Panel(canvas.transform, dungeon ? new Vector2(.24f,.008f) : new Vector2(.69f,.16f),
            dungeon ? new Vector2(.79f,.168f) : new Vector2(.99f,.45f));
        if (dungeon) panel.color = new Color(.12f,.32f,.29f,.80f);
        panel.raycastTarget = false;
        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true; group.interactable = true;
        var button = GameUISkin.Button(panel.transform, "Events / History", new Vector2(.71f,.80f), new Vector2(.98f,.98f), ToggleHistory);
        button.GetComponentInChildren<TMP_Text>().fontSize = 22;
        var viewport = GameUISkin.Rect("Event viewport", panel.transform, new Vector2(.035f,.03f), new Vector2(.965f,.80f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = new Color(0,0,0,.001f);
        text = GameUISkin.Label(viewport, "", new Vector2(0,1), Vector2.one, dungeon ? 30 : 23);
        if (dungeon) DungeonHud.WorldLabel(text);
        text.rectTransform.pivot = new Vector2(.5f,1);
        text.richText = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = text.rectTransform;
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    private void Render()
    {
        var entries = !expanded && inDungeonTurn ? turnEvents : history;
        int start = inDungeonTurn || expanded ? 0 : Mathf.Max(0,entries.Count-5);
        text.text = string.Join("\n", entries.GetRange(start, entries.Count-start));
        Canvas.ForceUpdateCanvases();
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scroll.viewport.rect.height, text.GetPreferredValues(text.text, scroll.viewport.rect.width, 0).y + 12));
        scroll.verticalNormalizedPosition = 0;
        lastMessage = Time.unscaledTime; group.alpha = 1;
    }

    private void Add(string message, bool coalesce)
    {
        // Holding movement against a locked gate should not flood the display.
        if (coalesce && history.Count > 0 && history[history.Count - 1] == message && Time.unscaledTime - lastMessage < 1f) return;
        history.Add(message);
        if (inDungeonTurn) turnEvents.Add(message);
        if (history.Count > 100) history.RemoveAt(0);
        Render();
    }

    private void Update() { if (!inDungeonTurn && !expanded) group.alpha = 1 - Mathf.Clamp01((Time.unscaledTime - lastMessage - 9f) / 2f); }
    private void OnDestroy() { if (instance == this) instance = null; }
}
