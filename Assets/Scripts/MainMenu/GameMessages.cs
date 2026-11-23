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
    private const int MinimumVisibleLines = 5;
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
    private Vector2 authoredViewportMax, authoredHistoryPosition;
    private float dockInset;
    private float layoutFontSize;
    private float layoutWidth;
    private bool textDirty = true, refreshingText, fittingDock;
    private readonly Vector3[] dockCorners = new Vector3[4];
    private TownMenuManager townMenus;
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
        text.alignment = TextAlignmentOptions.BottomLeft;
        scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = text.rectTransform;
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        AuthorCompactLayout();
    }

    // Keep the event header and parchment border clear of the scrollable text.
    // Runtime reads the serialized prefab geometry, including Inspector edits.
    public void AuthorCompactLayout()
    {
        JuicyChickenGames.Menu.Dialog.Fit(group.transform,normalMin.x,normalMin.y,normalMax.x,normalMax.y);
        JuicyChickenGames.Menu.Dialog.Fit(historyButton.transform,.70f,1,.975f,1);
        var header = (RectTransform)historyButton.transform;
        header.offsetMin = new Vector2(0,-66);
        header.offsetMax = new Vector2(0,-6);
        var label=historyButton.GetComponentInChildren<TMP_Text>(true);
        label.fontSize=26;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=26;
        label.margin=new Vector4(0,1.789804f,0,1.78980434f);
        JuicyChickenGames.Menu.Dialog.Fit(scroll.viewport,.035f,0,.965f,1);
        scroll.viewport.offsetMin = new Vector2(0,20);
        scroll.viewport.offsetMax = new Vector2(0,-71);
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
        authoredViewportMax=scroll.viewport.offsetMax;
        var historyRect=(RectTransform)historyButton.transform;
        authoredHistoryPosition=historyRect.anchoredPosition;
        text.alignment = TextAlignmentOptions.BottomLeft;
        historyButton.onClick.AddListener(ToggleHistory);
        ControlDeviceState.Changed += InvalidateText;
        ControlBindings.Changed += InvalidateText;
        HudRefresh.Changed += Refresh;
        townMenus = GetComponentInParent<Town>()?.GetComponentInChildren<TownMenuManager>(true);
        feedPanel.gameObject.AddComponent<HudLayoutObserver>().Changed = LayoutChanged;
        scroll.viewport.gameObject.AddComponent<HudLayoutObserver>().Changed = LayoutChanged;
        historyButton.targetGraphic.gameObject.AddComponent<HudLayoutObserver>().Changed = LayoutChanged;
        // Menu notifications can arrive before the resized canvas has settled.
        Canvas.willRenderCanvases += FitDockClearance;
        SetTerminalPresentation(TerminalMode.Effective);
    }
    private void InvalidateText() { textDirty = true; HudRefresh.Request(HudChange.Events); }
    private void LayoutChanged()
    {
        if (refreshingText || fittingDock || feedPanel == null || scroll == null || text == null) return;
        FitDockClearance();
        if (layoutParentSize != ((RectTransform)feedPanel.parent).rect.size ||
            !Mathf.Approximately(layoutWidth, scroll.viewport.rect.width) || !Mathf.Approximately(layoutFontSize, text.fontSize))
            InvalidateText();
    }
    private void Refresh(HudChange changes)
    {
        if ((changes & HudChange.Visibility) != 0)
        {
            FitDockClearance();
            bool historyOpen = historyDialog != null && historyDialog.Owner != null;
            if (historyWasOpen && !historyOpen) Render();
            historyWasOpen = historyOpen;
        }
        if ((changes & HudChange.Events) != 0 && textDirty && hasMessage) RefreshText();
    }

    private void FitDockClearance()
    {
        if (fittingDock || feedPanel == null || scroll == null || historyButton == null) return;
        // Updating the viewport invokes layout observers synchronously. Apply
        // both offsets together before allowing another measurement.
        fittingDock=true;
        try
        {
        var focus=MenuUIInputModule.Active?.FocusRoot;
        var dock=focus != null ? focus.GetComponentInParent<JuicyChickenGames.Menu.Dialog>()?.GameplayDockPanel : null;
        var historyRect=(RectTransform)historyButton.transform;
        float inset=0;
        if (dock != null && dock.gameObject.activeInHierarchy)
        {
            var corners=dockCorners;dock.GetWorldCorners(corners);
            float left=float.PositiveInfinity;
            foreach(var corner in corners)left=Mathf.Min(left,feedPanel.InverseTransformPoint(corner).x);
            float textRight=feedPanel.rect.xMin+feedPanel.rect.width*scroll.viewport.anchorMax.x+authoredViewportMax.x;
            // Authored backgrounds can extend beyond their button's hit rectangle.
            historyButton.targetGraphic.rectTransform.GetWorldCorners(corners);
            float buttonRight=float.NegativeInfinity;
            foreach(var corner in corners)buttonRight=Mathf.Max(buttonRight,feedPanel.InverseTransformPoint(corner).x);
            buttonRight+=authoredHistoryPosition.x-historyRect.anchoredPosition.x;
            inset=Mathf.Max(0,Mathf.Max(textRight,buttonRight)-left+12);
        }
        var viewportMax=authoredViewportMax-Vector2.right*inset;
        var historyPosition=authoredHistoryPosition-Vector2.right*inset;
        if (Mathf.Approximately(inset,dockInset) && (scroll.viewport.offsetMax-viewportMax).sqrMagnitude<.01f &&
            (historyRect.anchoredPosition-historyPosition).sqrMagnitude<.01f) return;
        dockInset=inset;
        // Keep the feed's authored footprint. Reflow its contents into the area
        // still visible beside the current menu and restore them when it closes.
        scroll.viewport.offsetMax=viewportMax;
        historyRect.anchoredPosition=historyPosition;
        InvalidateText();
        }
        finally { fittingDock=false; }
    }
    private void Render()
    {
        // Keep every event in History, but rebuild the feed only once after a fast walk.
        if (DungeonFastWalk.DeferPresentation) return;
        hasMessage=true; group.blocksRaycasts=true; group.interactable=true;
        if (textDirty) HudRefresh.Request(HudChange.Events);
        lastMessage = Time.unscaledTime; group.alpha = 1;
    }

    private void RefreshText()
    {
        if (!hasMessage) return;
        using var measurement = new FinalePerformance.Scope("Event feed layout");
        refreshingText = true;
        try
        {
        textDirty = false;
        bool followNewest = scroll.content.rect.height <= scroll.viewport.rect.height + 1 ||
            scroll.verticalNormalizedPosition <= .001f;
        float readingOffset = scroll.content.anchoredPosition.y;
        text.text = InputPrompts.Format(string.Join("\n", history));
        FitPreviewHeight();
        Canvas.ForceUpdateCanvases();
        float contentHeight = Mathf.Max(scroll.viewport.rect.height,
            text.GetPreferredValues(text.text, scroll.viewport.rect.width, 0).y);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        if (followNewest)
        {
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 0;
        }
        else
        {
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(readingOffset, 0, contentHeight - scroll.viewport.rect.height);
            scroll.content.anchoredPosition = position;
        }
        layoutWidth = scroll.viewport.rect.width;
        }
        finally { refreshingText = false; }
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
        var sample=new string[MinimumVisibleLines];
        for(int i=0;i<sample.Length;i++)sample[i]="Ag";
        float required=text.GetPreferredValues(string.Join("\n",sample),scroll.viewport.rect.width,0).y+1;
        float available=height*stretch+scroll.viewport.sizeDelta.y;
        // Short windows grow just enough for the minimum. Taller viewports show every line that fits.
        var maximum=authoredOffsetMax;maximum.y+=Mathf.Max(0,(required-available)/stretch);
        feedPanel.offsetMin=authoredOffsetMin;feedPanel.offsetMax=maximum;
        return true;
    }

    private void Add(string message, bool coalesce)
    {
        message = GameplayText.PlainTemplate(message);
        // Holding movement against a locked gate should not flood the display.
        if (coalesce && history.Count > 0 && history[history.Count - 1] == message && Time.unscaledTime - lastMessage < 1f) return;
        history.Add(message);
        if (inDungeonTurn) turnEvents.Add(message);
        if (history.Count > 100) history.RemoveAt(0);
        textDirty = true;
        Render();
    }

    private void Update()
    {
        historyController.Tick();
        bool historyOpen = historyDialog != null && historyDialog.Owner != null;
        if (historyOpen || townMenus?.CoversGameplayHud == true)
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
        Canvas.willRenderCanvases -= FitDockClearance;
        ControlDeviceState.Changed -= InvalidateText;
        ControlBindings.Changed -= InvalidateText;
        HudRefresh.Changed -= Refresh;
        if (instance == this) instance = null;
    }
}
