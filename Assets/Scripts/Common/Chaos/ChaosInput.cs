#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[Serializable]
public sealed class ChaosInput
{
    public string[] Keys = Array.Empty<string>(), Buttons = Array.Empty<string>();
    public Vector2 Stick, Look, Position, Scroll;
    public ushort MouseButtons;
    public string Device = "Keyboard", Label, Before, After;
    public int HoldFrames = 2, SettleFrames = 3, Index;
    public double Seconds;
    public static ChaosInput Key(Key key, string label = null) => new() { Keys = new[] { key.ToString() }, Label = label ?? key.ToString() };
    public static ChaosInput Pad(GamepadButton button, string label = null) => new() { Device = "Controller", Buttons = new[] { button.ToString() }, Label = label ?? button.ToString() };
    public static ChaosInput Click(Vector2 point, string label = null) => new() { Device = "Mouse", Position = point, MouseButtons = 1, Label = label ?? "click" };
}

// Only this class writes inputs. It never calls a gameplay action, UI callback, or selection setter.
public sealed class ChaosInputDevices : IDisposable
{
    public readonly Keyboard Keyboard;
    public readonly Mouse Mouse;
    public readonly Gamepad Pad;
    private readonly InputSettings previous, settings;
    private readonly List<InputDevice> suspended = new();
    private readonly Keyboard previousKeyboard;
    private readonly Mouse previousMouse;
    private readonly Gamepad previousPad;
    private readonly bool previousGamepad, previousBackground;
    private readonly CursorLockMode cursorLock;
    private readonly bool cursorVisible;
    private bool disposed;
    private Vector2 position;

    public ChaosInputDevices()
    {
        previous = InputSystem.settings; previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
        previousMouse = UnityEngine.InputSystem.Mouse.current; previousPad = Gamepad.current;
        previousGamepad = ControlDeviceState.Gamepad; previousBackground = Application.runInBackground;
        cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
        try
        {
            foreach (var device in InputSystem.devices.ToArray()) Suspend(device);
            settings = UnityEngine.Object.Instantiate(previous);
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = settings;
            Application.runInBackground = true;
            Keyboard = InputSystem.AddDevice<Keyboard>("ChaosKeyboard");
            Mouse = InputSystem.AddDevice<Mouse>("ChaosMouse");
            Pad = InputSystem.AddDevice<Gamepad>("ChaosGamepad");
            InputSystem.onDeviceChange += DeviceChanged;
        }
        catch { Dispose(); throw; }
    }
    private void Suspend(InputDevice device) { if (device.enabled) { suspended.Add(device); InputSystem.DisableDevice(device); } }
    private void DeviceChanged(InputDevice device, InputDeviceChange change)
    {
        if (device != Keyboard && device != Mouse && device != Pad &&
            (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled)) Suspend(device);
    }
    public void Send(ChaosInput input)
    {
        if (disposed) throw new ObjectDisposedException(nameof(ChaosInputDevices));
        if (input.HoldFrames < 1 || input.HoldFrames > 600 || input.SettleFrames < 1 || input.SettleFrames > 3600)
            throw new ArgumentException("Input frame duration is out of range.");
        var keys = (input.Keys ?? Array.Empty<string>()).Select(k => (Key)Enum.Parse(typeof(Key), k)).ToArray();
        var state = new GamepadState { leftStick = input.Stick, rightStick = input.Look };
        foreach (var button in input.Buttons ?? Array.Empty<string>()) state = state.WithButton((GamepadButton)Enum.Parse(typeof(GamepadButton), button));
        Vector2 delta = input.Device == "Mouse" ? input.Position - position : Vector2.zero;
        if (input.Device == "Mouse") position = input.Position;
        InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));
        InputSystem.QueueStateEvent(Pad, state);
        InputSystem.QueueStateEvent(Mouse, new MouseState { position = position, delta = delta, buttons = input.MouseButtons, scroll = input.Scroll });
    }
    public void Release()
    {
        if (Keyboard?.added == true) InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
        if (Pad?.added == true) InputSystem.QueueStateEvent(Pad, new GamepadState());
        if (Mouse?.added == true) InputSystem.QueueStateEvent(Mouse, new MouseState { position = position });
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        InputSystem.onDeviceChange -= DeviceChanged;
        Release();
        foreach (var device in new InputDevice[] { Keyboard, Mouse, Pad })
            if (device?.added == true) { InputSystem.ResetDevice(device); InputSystem.RemoveDevice(device); }
        InputSystem.settings = previous;
        foreach (var device in suspended.Distinct()) if (device.added) InputSystem.EnableDevice(device);
        if (previousKeyboard?.added == true) previousKeyboard.MakeCurrent();
        if (previousMouse?.added == true) previousMouse.MakeCurrent();
        if (previousPad?.added == true) previousPad.MakeCurrent();
        ControlDeviceState.Set(previousGamepad);
        Application.runInBackground = previousBackground;
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
    }
}
#endif
