using EternalEnigma.Core.Terminal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using JuicyChickenGames.Menu;

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

    public static void Ensure(Common common)
    {
        if (instance != null || common == null) return;
        instance = common.GetComponent<TerminalRuntime>() ?? common.gameObject.AddComponent<TerminalRuntime>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { instance = null; }
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
        if (minimap != null) minimap.SetTerminalPresentation(false);
        if (messages != null) messages.SetTerminalPresentation(false);
        if (mainMenuView != null) mainMenuView.Show(false);
        SceneManager.sceneLoaded -= SceneChanged;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        TerminalMode.Changed -= PreferenceChanged;
        if (instance == this) { instance = null; TerminalMode.Effective = false; }
    }
    private void SceneChanged(Scene scene, LoadSceneMode mode) { BindScene(); lastRenderer = null; dirty = true; PreferenceChanged(); }
    private void SceneUnloaded(Scene scene) { BindScene(); lastRenderer = null; dirty = true; PreferenceChanged(); }
    private void BindScene()
    {
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
        targetDialog = FindFirstObjectByType<TargetDialog>();
    }
    private void PreferenceChanged()
    {
        bool supported = dungeon != null || town != null || overworld != null || mainMenu != null;
        if (supported && UnsafeToSwitch()) { dirty = true; return; }
        TerminalMode.Effective = TerminalMode.Requested && screen != null && screen.IsReady && supported;
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
        if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
            TerminalMode.SetRequested(!TerminalMode.Requested);
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
        if (!TerminalMode.Effective || screen == null) return;
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
    private bool UnsafeToSwitch() =>
        dungeon != null && dungeon.TurnManager != null && dungeon.TurnManager.IsProcessingTurn ||
        town != null && town.TownPlayer != null && town.TownPlayer.IsBusy ||
        overworld != null && overworld.IsMoving;
    private void Paint(TerminalMapRenderer renderer, TerminalMapSnapshot snapshot)
    {
        bool changed = renderer.Render(snapshot, screen.Columns, screen.Rows);
        if (changed || lastRenderer != renderer) screen.SetText(renderer.Frame.RichText);
        lastRenderer = renderer;
    }
}
