using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local, non-modal event feed shared by dungeon, town and overworld.</summary>
public sealed class GameMessages : MonoBehaviour
{
    private static GameMessages instance;
    private readonly List<string> history = new();
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private CanvasGroup group;
    private float lastMessage;
    private readonly List<string> turnEvents = new();
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button historyButton;
    private EventHistoryDialog historyDialog;
    private readonly JuicyChickenGames.Menu.DialogController historyController = new();
    private static readonly Vector2 normalMin=new(.24f,.016f), normalMax=new(.79f,.20f);
    private bool docked;
    private bool inDungeonTurn;
    private bool hasMessage;
    private Canvas displayCanvas;
    private bool displayCanvasWasEnabled;
    private bool terminalHidden;
    public IReadOnlyList<string> TurnEvents => turnEvents;
    public static void BeginTurn()
    {
        if (instance == null) instance = Resolve();
        instance.inDungeonTurn = true; instance.turnEvents.Clear(); instance.Render();
    }
    public static void FinishAction() { if (instance != null) instance.Render(); }
    public static void ToggleHistory() => ShowHistory();

    public static void ShowHistory()
    {
        if (instance == null) instance = Resolve();
        if (instance.historyDialog != null && instance.historyDialog.Owner != null) return;
        if (instance.historyDialog == null) instance.historyDialog = Instantiate(GameUITheme.Current.HistoryPrefab, instance.transform);
        instance.historyDialog.Setup(instance.history);
        if (Game.Instance != null) MenuManager.Open(instance.historyDialog);
        else if (Object.FindFirstObjectByType<TownMenuManager>() is { } townMenus) townMenus.Open(instance.historyDialog);
        else if (Object.FindFirstObjectByType<OverworldMenuManager>() is { } worldMenus) worldMenus.Open(instance.historyDialog);
        else instance.historyController.Open(instance.historyDialog);
    }

    public IReadOnlyList<string> History => history;

    public void SetTerminalPresentation(bool hidden)
    {
        if (terminalHidden == hidden) return;
        displayCanvas ??= group.GetComponentInParent<Canvas>();
        if (displayCanvas == null) return;
        if (hidden) displayCanvasWasEnabled = displayCanvas.enabled;
        terminalHidden = hidden;
        displayCanvas.enabled = hidden ? false : displayCanvasWasEnabled;
    }

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

    public static void AbilityCast(Character caster, Skill skill, string itemName = null)
    {
        if (skill == null) return;
        string source = string.IsNullOrEmpty(itemName) ? "" : $" using {itemName}";
        ForCharacter(caster, $"[Cast] {Name(caster)} casts {skill.SkillName}{source}.");
    }

    public static void Post(string message, bool coalesce = false)
    {
        if (!Application.isPlaying || string.IsNullOrWhiteSpace(message)) return;
        if (instance == null) instance = Resolve();
        instance.Add(message, coalesce);
    }

    #if UNITY_EDITOR
    public void AuthorLayout(bool dungeon)
    {
        instance = this;
        var canvas = GameUISkin.Canvas("Message display", transform, 2);

        var panel = GameUISkin.Panel(canvas.transform, normalMin, normalMax);
        panel.color = new Color(1,1,1,.94f);
        panel.raycastTarget = false;
        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true; group.interactable = true;
        var button = GameUISkin.Button(panel.transform, "Events / History", new Vector2(.70f,.72f), new Vector2(.975f,.98f), ToggleHistory);
        historyButton=button;
        button.GetComponentInChildren<TMP_Text>().fontSize = 24;
        var viewport = GameUISkin.Rect("Event viewport", panel.transform, new Vector2(.035f,.03f), new Vector2(.965f,.70f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = new Color(0,0,0,.001f);
        text = GameUISkin.Label(viewport, "", new Vector2(0,1), Vector2.one, 30);
        text.color = GameUITheme.Ink;
        text.rectTransform.pivot = new Vector2(.5f,1);
        text.richText = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = text.rectTransform;
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }
#endif

    private static GameMessages Resolve()
    {
        var owner = Game.Instance != null ? Game.Instance.transform : Object.FindFirstObjectByType<Town>()?.transform ?? Object.FindFirstObjectByType<OverworldScene>()?.transform;
        return AuthoredUI.Require<GameMessages>(owner);
    }
    private void Awake()
    {
        instance=this; group.alpha=0; group.blocksRaycasts=false; group.interactable=false;
        JuicyChickenGames.Menu.Dialog.Fit(group.transform,normalMin.x,normalMin.y,normalMax.x,normalMax.y);
        JuicyChickenGames.Menu.Dialog.Fit(historyButton.transform,.70f,.72f,.975f,.98f);
        JuicyChickenGames.Menu.Dialog.Fit(scroll.viewport,.035f,.03f,.965f,.70f);
        historyButton.GetComponentInChildren<TMP_Text>().fontSize=24;
        text.fontSize=30;text.color=GameUITheme.Ink;
        GameUISkin.PanelGraphic(group.transform).color=new Color(1,1,1,.94f);
        historyButton.onClick.AddListener(ToggleHistory);
        SetTerminalPresentation(TerminalMode.Effective);
    }
    private void Render()
    {
        hasMessage=true; group.blocksRaycasts=true; group.interactable=true;
        var entries = inDungeonTurn ? turnEvents : history;
        int start = Mathf.Max(0,entries.Count-3);
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

    private void Update()
    {
        historyController.Tick();
        bool showDock = MenuUIInputModule.Active?.HasDialog==true &&
            (Game.Instance==null || MenuManager.Instance==null || MenuManager.Instance.CurrentDialog!=MenuManager.Instance.TargetDialog);
        if (showDock != docked)
        {
            docked = showDock;
            var rect = (RectTransform)group.transform;
            rect.anchorMin = docked ? new Vector2(.24f,.016f) : normalMin;
            rect.anchorMax = docked ? new Vector2(.58f,.20f) : normalMax;
            if (hasMessage) Render();
        }
        if (!hasMessage) return;
        if (!inDungeonTurn) group.alpha = 1 - Mathf.Clamp01((Time.unscaledTime - lastMessage - 9f) / 2f);
        group.blocksRaycasts=group.interactable=group.alpha>0;
    }
    private void OnDestroy() { if (instance == this) instance = null; }
}
