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
    [SerializeField, Min(1)] private int previewLines = 4;
    private EventHistoryDialog historyDialog;
    private readonly JuicyChickenGames.Menu.DialogController historyController = new();
    // Center the 55%-wide feed on screen. Menus never change these bounds;
    // expanded History changes only the top edge.
    private static readonly Vector2 normalMin=new(.225f,.016f), normalMax=new(.775f,.20f);
    private bool inDungeonTurn;
    private bool hasMessage;
    private Canvas displayCanvas;
    private bool displayCanvasWasEnabled;
    private bool terminalHidden;
    private bool historyWasOpen;
    private RectTransform feedPanel;
    private Vector2 authoredOffsetMin, authoredOffsetMax, layoutParentSize;
    private float layoutFontSize;
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
        if (instance.historyDialog == null)
        {
            instance.historyDialog = Instantiate(GameUITheme.Current.HistoryPrefab, instance.transform);
            instance.historyDialog.UseFeed((RectTransform)instance.group.transform);
        }
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
        if (!string.IsNullOrWhiteSpace(character.CharacterName)) return GameplayText.Name(character.CharacterName);
        if (character is Enemy enemy) return string.IsNullOrWhiteSpace(enemy.DisplayName) ? "Enemy" : GameplayText.Name(enemy.DisplayName);
        return GameplayText.Name(character.name);
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
        string source = string.IsNullOrEmpty(itemName) ? "" : $" using {GameplayText.Name(itemName)}";
        ForCharacter(caster, $"{Name(caster)} casts {skill.DisplayName}{source}.");
    }

    public static void Post(string message, bool coalesce = false)
    {
        if (!Application.isPlaying || string.IsNullOrWhiteSpace(message)) return;
        if (instance == null) instance = Resolve();
        instance.Add(message, coalesce);
    }

    /// <summary>Explain an attempted action without dismissing its menu or changing focus.</summary>
    public static void Reject(string reason)
    {
        AudioManager.Instance?.PlayCue(SoundCue.Denied);
        Post(string.IsNullOrWhiteSpace(reason) ? "That action is no longer available." : reason, true);
        Common.Instance?.MenuInputHandler?.ClearInputThisFrame();
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
        AuthorCompactLayout();
    }

    // Defaults captured from the user's Play Mode layout. Runtime respects the
    // serialized prefab values, so subsequent Inspector edits are not overwritten.
    public void AuthorCompactLayout()
    {
        previewLines=4;
        JuicyChickenGames.Menu.Dialog.Fit(group.transform,normalMin.x,normalMin.y,normalMax.x,normalMax.y);
        JuicyChickenGames.Menu.Dialog.Fit(historyButton.transform,.70f,.72f,.975f,.98f);
        var label=historyButton.GetComponentInChildren<TMP_Text>(true);
        label.fontSize=26;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=26;
        label.margin=new Vector4(0,1.789804f,0,1.78980434f);
        JuicyChickenGames.Menu.Dialog.Fit(scroll.viewport,.035f,.03f,.965f,.70f);
        scroll.viewport.anchoredPosition=new Vector2(0,26.23f);
        scroll.viewport.sizeDelta=new Vector2(0,19.8808f);
        historyButton.transform.SetAsLastSibling();
        text.fontSize=33.11f;text.enableAutoSizing=false;text.color=GameUITheme.Ink;
        text.lineSpacing=text.paragraphSpacing=0;
        GameUISkin.PanelGraphic(group.transform).color=new Color(1,1,1,.94f);
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
        feedPanel=(RectTransform)group.transform;
        authoredOffsetMin=feedPanel.offsetMin;authoredOffsetMax=feedPanel.offsetMax;
        historyButton.onClick.AddListener(ToggleHistory);
        ControlDeviceState.Changed += RefreshText;
        ControlBindings.Changed += RefreshText;
        SetTerminalPresentation(TerminalMode.Effective);
    }
    private void Render()
    {
        hasMessage=true; group.blocksRaycasts=true; group.interactable=true;
        RefreshText();
        lastMessage = Time.unscaledTime; group.alpha = 1;
    }

    private void RefreshText()
    {
        if (!hasMessage) return;
        var entries = history;
        int lines=Mathf.Max(1,previewLines);
        int start = Mathf.Max(0,entries.Count-lines);
        text.text = InputPrompts.Format(string.Join("\n", entries.GetRange(start, entries.Count-start)));
        Canvas.ForceUpdateCanvases();
        FitPreviewHeight();
        Canvas.ForceUpdateCanvases();
        text.ForceMeshUpdate();
        // Count rendered lines, including wrapping and embedded newlines. The full
        // unabridged messages remain in History.
        if(text.textInfo.lineCount>lines)
        {
            int first=text.textInfo.lineInfo[text.textInfo.lineCount-lines].firstCharacterIndex;
            text.text=text.text.Substring(text.textInfo.characterInfo[first].index);
        }
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(scroll.viewport.rect.height, text.GetPreferredValues(text.text, scroll.viewport.rect.width, 0).y));
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
    }

    private bool FitPreviewHeight()
    {
        var parentSize=((RectTransform)feedPanel.parent).rect.size;
        if(parentSize.y<=0 || scroll.viewport.rect.width<=0)return false;
        if(parentSize==layoutParentSize && Mathf.Approximately(layoutFontSize,text.fontSize))return false;
        layoutParentSize=parentSize;layoutFontSize=text.fontSize;
        float height=parentSize.y*(feedPanel.anchorMax.y-feedPanel.anchorMin.y)+authoredOffsetMax.y-authoredOffsetMin.y;
        float stretch=scroll.viewport.anchorMax.y-scroll.viewport.anchorMin.y;
        if(stretch<=0)return false;
        var sample=new string[Mathf.Max(1,previewLines)];
        for(int i=0;i<sample.Length;i++)sample[i]="Ag";
        float required=text.GetPreferredValues(string.Join("\n",sample),scroll.viewport.rect.width,0).y+1;
        float available=height*stretch+scroll.viewport.sizeDelta.y;
        // Preserve the authored size at the reference aspect ratio. Only add
        // vertical room if a wider window would otherwise clip the fourth line.
        var maximum=authoredOffsetMax;maximum.y+=Mathf.Max(0,(required-available)/stretch);
        feedPanel.offsetMin=authoredOffsetMin;feedPanel.offsetMax=maximum;
        return true;
    }

    private void LateUpdate()
    {
        if(FitPreviewHeight() && hasMessage)RefreshText();
    }

    private void Add(string message, bool coalesce)
    {
        message = GameplayText.Plain(message);
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
        bool historyOpen = historyDialog != null && historyDialog.Owner != null;
        if (historyWasOpen && !historyOpen) Render();
        historyWasOpen = historyOpen;
        if (historyOpen || Object.FindFirstObjectByType<TownMenuManager>()?.CoversGameplayHud == true)
        {
            group.alpha = 0;
            group.blocksRaycasts = group.interactable = false;
            return;
        }
        if (!hasMessage) return;
        if (!inDungeonTurn) group.alpha = 1 - Mathf.Clamp01((Time.unscaledTime - lastMessage - 9f) / 2f);
        group.blocksRaycasts=group.interactable=group.alpha>0;
    }
    private void OnDestroy()
    {
        ControlDeviceState.Changed -= RefreshText;
        ControlBindings.Changed -= RefreshText;
        if (instance == this) instance = null;
    }
}
