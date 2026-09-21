using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Uses the authored menu button style; runtime setup is also available in player builds.</summary>
public sealed class WatchDemoMenu : MonoBehaviour
{
    private MainMenu menu;
    private bool configuring;
    private float openedAt;
    public bool IsConfiguring => configuring;
    private readonly AutoplayOptions options = new() { Speed = 1 };
    private string seed = "42";
    public Button WatchButton { get; private set; }

    public void Initialize(MainMenu mainMenu)
    {
        menu = mainMenu;
        var buttonObject = Instantiate(menu.StartButton, menu.StartButton.transform.parent);
        buttonObject.name = "Watch demo";
        buttonObject.SetActive(true);
        buttonObject.transform.SetSiblingIndex(menu.StartButton.transform.GetSiblingIndex()+1);
        WatchButton = buttonObject.GetComponent<Button>();
        WatchButton.onClick = new Button.ButtonClickedEvent();
        WatchButton.onClick.AddListener(Open);
        buttonObject.GetComponentInChildren<TMP_Text>().text = "Watch demo";
        // Include the new entry in keyboard/controller navigation without depending on authored sibling IDs.
        var buttons = menu.StartButton.transform.parent.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeSelf).ToArray();
        for (int i=0;i<buttons.Length;i++)
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i+buttons.Length-1)%buttons.Length], selectOnDown = buttons[(i+1)%buttons.Length] };
    }

    public void Open() { configuring = true; openedAt = Time.unscaledTime; menu.StartButton.transform.parent.gameObject.SetActive(false); }
    private void Cancel() { configuring = false; menu.StartButton.transform.parent.gameObject.SetActive(true); WatchButton.Select(); }
    public void StartDemo()
    {
        if (!int.TryParse(seed,out options.Seed)) return;
        configuring = false;
        AutoplayRunner.WatchDemo(options);
    }
    private void Update()
    {
        if (!configuring || Time.unscaledTime - openedAt < .2f) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true) Cancel();
        else if (Keyboard.current?.enterKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true) StartDemo();
        else if (Gamepad.current?.buttonWest.wasPressedThisFrame == true) options.DebugPlaythrough = !options.DebugPlaythrough;
        else if (options.DebugPlaythrough && Gamepad.current?.buttonNorth.wasPressedThisFrame == true) options.Godmode = !options.Godmode;
        else if (options.DebugPlaythrough && Gamepad.current?.rightShoulder.wasPressedThisFrame == true) options.InfiniteResources = !options.InfiniteResources;
    }
    private void OnGUI()
    {
        if (!configuring) return;
        GUILayout.BeginArea(new Rect((Screen.width-360)/2f,(Screen.height-340)/2f,360,340),GUI.skin.box);
        GUILayout.Label("WATCH DEMO");
        GUILayout.Label("An automated campaign with a separate save.");
        GUILayout.Label("Seed"); seed = GUILayout.TextField(seed);
        options.DebugPlaythrough = GUILayout.Toggle(options.DebugPlaythrough,"Debug playthrough");
        if (options.DebugPlaythrough)
        {
            options.Godmode = GUILayout.Toggle(options.Godmode,"Godmode: invincible + infinite strength");
            options.InfiniteResources = GUILayout.Toggle(options.InfiniteResources,"Infinite resources");
        }
        else GUILayout.Label("Normal play: the party can lose.");
        GUILayout.Label("Use playback controls to change speed or pause. Other input asks to return to the main menu.");
        if (GUILayout.Button("Watch")) StartDemo();
        if (GUILayout.Button("Back")) Cancel();
        GUILayout.Label("Controller: A watch · B back · X debug mode");
        if (options.DebugPlaythrough) GUILayout.Label("Y godmode · RB infinite resources");
        GUILayout.EndArea();
    }
}
