using EternalEnigma.Core.Terminal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using JuicyChickenGames.Menu;
using UnityEngine.UI;
using TMPro;
using EternalEnigma.Core.World;

public sealed class TerminalRuntime : MonoBehaviour
{
    private static TerminalRuntime instance;
    private TerminalScreen screen;
    private readonly DungeonTerminalRenderer dungeonRenderer = new();
    private readonly OverworldTerminalRenderer overworldRenderer = new();
    private readonly TownTerminalRenderer townRenderer = new();
    private Game dungeon;
    private Town town;
    private OverworldScene overworld;
    private MainMenu mainMenu;
    private TerminalMainMenuView mainMenuView;
    private Minimap minimap;
    private GameMessages messages;
    private TargetDialog targetDialog;
    private float nextRefresh;
    private bool dirty = true;
    private TerminalMapRenderer lastRenderer;
    private readonly TerminalMenuStyle hudStyle = new();
    private PartyMenuLauncher launcher;
    private ResourceHUD resources;
    private DungeonHud dungeonHud;
    private Button historyButton;
    private float nextHudStyle;
    private readonly Vector3[] corners = new Vector3[4];
    private bool hasPresentedMode;

    public static void Ensure(Common common)
    {
        if (instance != null || common == null) return;
        instance = common.GetComponent<TerminalRuntime>() ?? common.gameObject.AddComponent<TerminalRuntime>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { instance = null; TerminalSceneSnapshots.ClearCache(); }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        SceneManager.sceneLoaded += SceneChanged;
        SceneManager.sceneUnloaded += SceneUnloaded;
        TerminalMode.Changed += PreferenceChanged;
    }
    private void Start()
    {
        var owner = new GameObject("Terminal screen");
        owner.transform.SetParent(transform, false);
        screen = owner.AddComponent<TerminalScreen>();
        if (!screen.Initialize()) screen = null;
        BindScene();
        PreferenceChanged();
    }
    private void OnDestroy()
    {
        hudStyle.Restore();
        if (minimap != null) minimap.SetTerminalPresentation(false);
        if (messages != null) messages.SetTerminalPresentation(false);
        if (mainMenuView != null) mainMenuView.Show(false);
        SceneManager.sceneLoaded -= SceneChanged;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        TerminalMode.Changed -= PreferenceChanged;
        TerminalSceneSnapshots.ClearCache();
        if (instance == this) { instance = null; TerminalMode.Effective = false; }
    }
    private void SceneChanged(Scene scene, LoadSceneMode mode) { BindScene(); lastRenderer = null; dirty = true; PreferenceChanged(); }
    private void SceneUnloaded(Scene scene) { BindScene(); lastRenderer = null; dirty = true; PreferenceChanged(); }
    private void BindScene()
    {
        hudStyle.Restore();
        var nextMinimap = FindFirstObjectByType<Minimap>();
        var nextMessages = FindFirstObjectByType<GameMessages>();
        if (minimap != null && minimap != nextMinimap) minimap.SetTerminalPresentation(false);
        if (messages != null && messages != nextMessages) messages.SetTerminalPresentation(false);
        var nextMainMenu = FindFirstObjectByType<MainMenu>();
        if (mainMenuView != null && (nextMainMenu == null || mainMenuView.gameObject != nextMainMenu.gameObject))
        {
            mainMenuView.Show(false);
            mainMenuView = null;
        }
        dungeon = FindFirstObjectByType<Game>();
        town = FindFirstObjectByType<Town>();
        overworld = FindFirstObjectByType<OverworldScene>();
        mainMenu = nextMainMenu;
        minimap = nextMinimap;
        messages = nextMessages;
        targetDialog = FindFirstObjectByType<TargetDialog>(FindObjectsInactive.Include);
        launcher = FindFirstObjectByType<PartyMenuLauncher>();
        resources = FindFirstObjectByType<ResourceHUD>();
        dungeonHud = FindFirstObjectByType<DungeonHud>();
        nextHudStyle = 0;
    }
    private void PreferenceChanged()
    {
        bool supported = dungeon != null || town != null || overworld != null || mainMenu != null;
        // Single-scene travel briefly has no loaded gameplay scene. Keep the
        // applied mode across that gap so the destination can bind under its cover.
        if (!supported && hasPresentedMode && Common.InstanceOrNull?.ScreenTransition?.BlocksInput == true)
        { dirty = true; return; }
        // A covered destination still needs its HUD bound to the current presentation.
        // Only changing presentation waits for the cover or gameplay action to finish.
        bool deferSwitch = supported && TerminalMode.Requested != TerminalMode.Effective && UnsafeToSwitch();
        if (!deferSwitch)
            TerminalMode.Effective = TerminalMode.Requested && screen != null && screen.IsReady && supported;
        hasPresentedMode |= supported;
        if (!TerminalMode.Effective) hudStyle.Restore();
        if (screen != null) screen.Show(TerminalMode.Effective);
        if (minimap != null) minimap.SetTerminalPresentation(TerminalMode.Effective);
        if (messages != null) messages.SetTerminalPresentation(TerminalMode.Effective);
        if (mainMenu != null && screen != null && screen.IsReady)
        {
            mainMenuView = mainMenu.GetComponent<TerminalMainMenuView>() ?? mainMenu.gameObject.AddComponent<TerminalMainMenuView>();
            mainMenuView.Initialize(mainMenu, screen.FontAsset);
            mainMenuView.Show(TerminalMode.Effective);
        }
        lastRenderer = null;
        dirty = true; nextRefresh = 0;
    }
    private void Update()
    {
        if (Keyboard.current?.f11Key.wasPressedThisFrame == true || Gamepad.current?.selectButton.wasPressedThisFrame == true)
        {
            TerminalMode.SetRequested(!TerminalMode.Requested);
            MenuUIInputModule.Active?.ConsumeInput();
            Common.InstanceOrNull?.MenuInputHandler?.ClearInputThisFrame();
        }
        // Common may be restored after the menu sceneLoaded event has already fired.
        if (dungeon == null && town == null && overworld == null && mainMenu == null)
        {
            BindScene();
            PreferenceChanged();
        }
        if (messages == null)
        {
            messages = FindFirstObjectByType<GameMessages>();
            if (messages != null) messages.SetTerminalPresentation(TerminalMode.Effective);
        }
        if ((dungeon != null || town != null || overworld != null || mainMenu != null) &&
            TerminalMode.Requested != TerminalMode.Effective && !UnsafeToSwitch()) PreferenceChanged();
        if (historyButton != null) historyButton.gameObject.SetActive(CanShowHistory());
        if (!TerminalMode.Effective || screen == null) return;
        if (CanShowHistory() && Keyboard.current?.hKey.wasPressedThisFrame == true) OpenHistory();
        if (screen.SetViewport(ViewportMin(), ViewportMax())) dirty = true;
        if (screen.Resize()) dirty = true;
        if (!dirty && Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .08f;
        dirty = false;
        if (dungeon != null && dungeon.IsReady && dungeon.CurrentDungeon != null && dungeon.PlayerController?.ControlledAlly != null)
            Paint(dungeonRenderer, TerminalSceneSnapshots.Dungeon(dungeon, minimap, messages, targetDialog));
        else if (town != null && town.IsReady && town.Plan != null && town.TownPlayer?.ControllingTownAlly != null)
            Paint(townRenderer, TerminalSceneSnapshots.Town(town, messages));
        else if (overworld != null && overworld.IsReady && overworld.Map?.CurrentGrid != null)
            Paint(overworldRenderer, TerminalSceneSnapshots.Overworld(overworld, messages));
        else if (mainMenu != null) { lastRenderer = null; screen.SetText(mainMenuView.CurrentTitleMarkup(screen.Columns, screen.Rows)); }
        else { lastRenderer = null; screen.SetText("Loading..."); }
    }
    private bool UnsafeToSwitch() => hasPresentedMode && Common.InstanceOrNull?.ScreenTransition?.BlocksInput == true ||
        dungeon != null && dungeon.TurnManager != null && dungeon.TurnManager.IsProcessingTurn ||
        town != null && town.TownPlayer != null && town.TownPlayer.IsBusy ||
        overworld != null && overworld.IsMoving;
    private void Paint(TerminalMapRenderer renderer, TerminalMapSnapshot snapshot)
    {
        var pointerFocus = targetDialog != null ? targetDialog.TerminalPointerFocus : null;
        bool changed = renderer.Render(snapshot, screen.Columns, screen.Rows,
            pointerFocus.HasValue ? new GridPoint(pointerFocus.Value.x, pointerFocus.Value.y) : null);
        if (changed || lastRenderer != renderer) screen.SetText(renderer.Frame.RichText);
        lastRenderer = renderer;
    }

    private Vector2 ViewportMin() => mainMenu != null ? Vector2.zero : new Vector2(dungeon != null ? .30f : 0, .08f);
    private Vector2 ViewportMax()
    {
        if (mainMenu != null) return Vector2.one;
        float right = 1;
        var scope = MenuUIInputModule.Active?.FocusRoot;
        if (scope != null)
        {
            foreach (var image in scope.GetComponentsInChildren<Image>())
            {
                if (!image.enabled || image.color.a < .01f) continue;
                image.rectTransform.GetWorldCorners(corners);
                float left = corners[0].x / Mathf.Max(1, Screen.width);
                float width = (corners[2].x - corners[0].x) / Mathf.Max(1, Screen.width);
                float height = (corners[2].y - corners[0].y) / Mathf.Max(1, Screen.height);
                if (left > .65f && width > .16f && height > .3f) right = Mathf.Min(right, left - .008f);
            }
        }
        // World interaction lives below the top shortcut row; keep the frame clear of it.
        return new Vector2(right, overworld != null || targetDialog != null && targetDialog.isActiveAndEnabled ? .78f : .88f);
    }

    private void LateUpdate()
    {
        if (!TerminalMode.Effective || screen == null) return;
        if (Time.unscaledTime < nextHudStyle) return;
        nextHudStyle = Time.unscaledTime + .1f;
        if (launcher != null) hudStyle.Refresh(launcher.transform, screen.FontAsset);
        if (resources != null) hudStyle.Refresh(resources.transform, screen.FontAsset);
        // DungeonHud lives on Game, which also owns dialogs. Styling that whole
        // hierarchy overlaps the menu bridge and captures already-terminal fonts.
        if (dungeonHud != null && dungeonHud.PartyRoot != null)
            hudStyle.Refresh(dungeonHud.PartyRoot.parent, screen.FontAsset);
        if (targetDialog != null && targetDialog.isActiveAndEnabled)
            hudStyle.Refresh(targetDialog.transform, screen.FontAsset);
        if (historyButton != null)
        {
            historyButton.GetComponentInChildren<TMP_Text>().fontSizeMax = screen.FontSize;
            hudStyle.Refresh(historyButton.transform, screen.FontAsset);
        }
        if (mainMenu == null && historyButton == null) CreateHistoryButton();
    }

    private bool CanShowHistory() => TerminalMode.Effective && mainMenu == null && messages != null &&
        !UnsafeToSwitch() && !AutoplayRunner.BlocksPlayerInput && MenuUIInputModule.Active?.HasDialog != true;
    private void OpenHistory()
    {
        if (!CanShowHistory()) return;
        MenuUIInputModule.Active?.ConsumeInput();
        GameMessages.ShowHistory();
    }
    private void CreateHistoryButton()
    {
        var owner = new GameObject("Terminal history", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        owner.transform.SetParent(transform, false);
        var canvas = owner.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var control = new GameObject("History", typeof(RectTransform), typeof(Image), typeof(Button)); control.transform.SetParent(owner.transform, false);
        var rect = (RectTransform)control.transform; rect.anchorMin = new Vector2(.79f, .012f); rect.anchorMax = new Vector2(.985f, .065f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        historyButton = control.GetComponent<Button>(); historyButton.targetGraphic = control.GetComponent<Image>();
        historyButton.navigation = new Navigation { mode = Navigation.Mode.None };
        historyButton.onClick.AddListener(OpenHistory);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(control.transform, false);
        var text = label.GetComponent<TextMeshProUGUI>(); text.text = "History [H]"; text.font = screen.FontAsset; text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 18; text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = new Vector2(8, 2); text.rectTransform.offsetMax = new Vector2(-8, -2);
        hudStyle.Refresh(owner.transform, screen.FontAsset);
        control.SetActive(CanShowHistory());
    }

    internal static bool TryGetMapCell(Vector2 position, out Vector3Int cell)
    {
        cell = default;
        if (!TerminalMode.Effective || instance == null || instance.screen == null || instance.lastRenderer == null ||
            !instance.screen.TryGetFrameCell(position, out int column, out int row) ||
            !instance.lastRenderer.TryMapCell(column, row, out var point)) return false;
        cell = new Vector3Int(point.X, point.Y); return true;
    }
    internal static bool TryGetCellScreenPosition(Vector3Int cell, out Vector2 position)
    {
        position = default;
        return TerminalMode.Effective && instance != null && instance.lastRenderer != null && instance.screen != null &&
            instance.lastRenderer.TryFrameCell(new GridPoint(cell.x, cell.y), out int column, out int row) &&
            instance.screen.TryGetFramePosition(column, row, out position);
    }
}
