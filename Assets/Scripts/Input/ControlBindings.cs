using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Saved bindings shared by the gameplay PlayerInput and every menu input module.</summary>
public static class ControlBindings
{
    public const string PreferenceKey = "Controls.Bindings.v1";
    public static event Action Changed;
    public static bool IsCapturing { get; set; }
    public static bool SuppressInput => IsCapturing || Time.frameCount <= suppressedUntilFrame;
    private static int suppressedUntilFrame = -1;
    private static DungeonControls display;
    private static readonly List<InputActionAsset> registered = new();
    private static SavedBindings saved;

    [Serializable] private sealed class SavedBindings { public List<Binding> bindings = new(); }
    [Serializable] private sealed class Binding { public string action; public int index; public string path; }

    public static InputActionAsset DisplayAsset
    {
        get
        {
            if (display == null || display.asset == null)
            {
                display = new DungeonControls();
                ConfigureAdditionalActions(display.asset);
                Load();
                ApplyTo(display.asset);
            }
            return display.asset;
        }
    }

    public static void SuppressForFrame() => suppressedUntilFrame = Time.frameCount + 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        // Frame counts restart when entering play with domain reload disabled.
        // A previous capture must not suppress the beginning of the next session.
        IsCapturing = false;
        suppressedUntilFrame = -1;
        saved = null;
        Load();
        if (display != null && display.asset != null) ApplyTo(display.asset);
        registered.RemoveAll(asset => asset == null);
        foreach (var asset in registered) ApplyTo(asset);
    }

    // Dynamic actions use map/name + binding index for storage; their runtime GUIDs are not stable.
    public static void ConfigureAdditionalActions(InputActionAsset asset)
    {
        var ui = asset.FindActionMap("UI");
        if (ui != null)
        {
            Add(ui, "InspectNextHero", "<Keyboard>/tab", "<Gamepad>/rightShoulder");
            Add(ui, "InspectPreviousHero", "<Gamepad>/leftTrigger");
            Add(ui, "OpenEquipment", "<Keyboard>/i");
            Add(ui, "OpenStats", "<Keyboard>/t");
            Add(ui, "Presentation", "<Keyboard>/f11", "<Gamepad>/select");
            // The template wildcard includes the gamepad too. Separate its keyboard slot
            // so rebinding Back on one device cannot silently replace the other device.
            var cancel = ui.FindAction("Cancel");
            if (cancel != null && cancel.bindings.Count > 0 && cancel.bindings[0].path == "*/{Cancel}")
                cancel.ChangeBinding(0).WithPath("<Keyboard>/escape");
        }
        var player = asset.FindActionMap("Player");
        if (player != null) Add(player, "ToggleFullControl", "<Keyboard>/f", "<Gamepad>/rightStickPress");
    }

    private static void Add(InputActionMap map, string name, params string[] paths)
    {
        if (map.FindAction(name) != null) return;
        var action = map.AddAction(name, InputActionType.Button);
        foreach (var path in paths)
            action.AddBinding(path, groups: path.StartsWith("<Gamepad>") ? "Gamepad" : "Keyboard&Mouse");
    }

    public static void Register(InputActionAsset asset)
    {
        if (asset == null) return;
        var enabledMaps = asset.actionMaps.Where(map => map.enabled).ToArray();
        asset.Disable();
        ConfigureAdditionalActions(asset);
        foreach (var map in enabledMaps) map.Enable();
        if (!registered.Contains(asset)) registered.Add(asset);
        _ = DisplayAsset;
        ApplyTo(asset);
    }

    public static void Unregister(InputActionAsset asset) => registered.Remove(asset);

    public static void Reload()
    {
        saved = null;
        Load();
        Synchronize();
    }

    private static void Load()
    {
        if (saved != null) return;
        try { saved = JsonUtility.FromJson<SavedBindings>(GamePreferences.GetString(PreferenceKey)); }
        catch (ArgumentException) { /* A damaged or old preference must not disable controls. */ }
        saved ??= new SavedBindings();
        saved.bindings ??= new List<Binding>();
    }

    public static void ApplyBinding(string actionPath, int bindingIndex, string path)
    {
        var action = DisplayAsset.FindAction(actionPath, true);
        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
            throw new ArgumentOutOfRangeException(nameof(bindingIndex));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A control path is required.", nameof(path));
        Set(actionPath, bindingIndex, path);
        // Open/close and movement are shared player-facing commands across world and menus.
        string linked = actionPath switch
        {
            "Player/Menu" => "UI/OpenMenu", "UI/OpenMenu" => "Player/Menu",
            "Player/Skills" => "UI/OpenSkillMenu", "UI/OpenSkillMenu" => "Player/Skills",
            "Player/Options" => "UI/Options", "UI/Options" => "Player/Options",
            "Player/Move" => "UI/Navigate", "UI/Navigate" => "Player/Move", _ => null
        };
        if (linked != null)
        {
            var other = DisplayAsset.FindAction(linked);
            for (int i = 0; i < other.bindings.Count; i++)
                if (other.bindings[i].path == action.bindings[bindingIndex].path)
                    Set(linked, i, path);
        }
        GamePreferences.SetString(PreferenceKey, JsonUtility.ToJson(saved));
        GamePreferences.Save();
        Synchronize();
    }

    private static void Set(string action, int index, string path)
    {
        saved.bindings.RemoveAll(b => b != null && b.action == action && b.index == index);
        saved.bindings.Add(new Binding { action = action, index = index, path = path });
    }

    public static void ResetAll()
    {
        saved = new SavedBindings();
        GamePreferences.DeleteKey(PreferenceKey);
        GamePreferences.Save();
        Synchronize();
    }

    private static void Synchronize()
    {
        if (display != null) ApplyTo(display.asset);
        registered.RemoveAll(asset => asset == null);
        foreach (var asset in registered) ApplyTo(asset);
        SuppressForFrame();
        Changed?.Invoke();
    }

    private static void ApplyTo(InputActionAsset asset)
    {
        asset.RemoveAllBindingOverrides();
        foreach (var binding in saved.bindings)
        {
            if (binding == null || string.IsNullOrEmpty(binding.action) || string.IsNullOrWhiteSpace(binding.path)) continue;
            var action = asset.FindAction(binding.action);
            if (action != null && binding.index >= 0 && binding.index < action.bindings.Count && !action.bindings[binding.index].isComposite)
                action.ApplyBindingOverride(binding.index, binding.path);
        }
    }

    public static string BindingLabel(InputAction action, int index)
    {
        string path = action.bindings[index].effectivePath;
        return path switch
        {
            "<Gamepad>/buttonSouth" => "A / Cross", "<Gamepad>/buttonEast" => "B / Circle",
            "<Gamepad>/buttonWest" => "X / Square", "<Gamepad>/buttonNorth" => "Y / Triangle",
            "<Gamepad>/leftStickPress" => "Left stick press", "<Gamepad>/rightStickPress" => "Right stick press",
            "<Gamepad>/leftStick" => "Left stick", "<Gamepad>/rightStick" => "Right stick",
            "<Gamepad>/dpad" => "D-pad", "<Gamepad>/select" => "View / Share", "<Gamepad>/start" => "Menu / Options",
            "<Gamepad>/leftShoulder" => "LB / L1", "<Gamepad>/rightShoulder" => "RB / R1",
            "<Gamepad>/leftTrigger" => "LT / L2", "<Gamepad>/rightTrigger" => "RT / R2",
            "<Keyboard>/escape" => "Escape", "<Keyboard>/enter" => "Enter", "<Keyboard>/numpadEnter" => "Numpad Enter",
            "<Mouse>/leftButton" => "Left mouse", "<Mouse>/rightButton" => "Right mouse", "<Mouse>/middleButton" => "Middle mouse",
            _ => action.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontIncludeInteractions)
        };
    }

    public static string Prompt(string actionPath, string keyboardDefault, string gamepadDefault)
    {
        var action = DisplayAsset.FindAction(actionPath);
        bool pad = ControlDeviceState.Gamepad;
        var indices = Enumerable.Range(0, action.bindings.Count).Where(i => !action.bindings[i].isComposite &&
            action.bindings[i].path.StartsWith(pad ? "<Gamepad>" : "<Keyboard>", StringComparison.Ordinal)).ToArray();
        if (!indices.Any(i => action.bindings[i].overridePath != null)) return pad ? gamepadDefault : keyboardDefault;
        return string.Join(" / ", indices.Select(i => BindingLabel(action, i)).Distinct());
    }
}
