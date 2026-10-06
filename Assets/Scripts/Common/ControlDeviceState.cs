using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>The last deliberately used control device, shared by prompts and cursor.</summary>
public static class ControlDeviceState
{
    public static bool Gamepad { get; private set; }
    public static event Action Changed;

    private static int polledFrame = -1;
    private static Gamepad previousPad;
    private static Vector2 previousLeft, previousRight, previousDpad;

    internal static void Set(bool gamepad)
    {
        if (Gamepad == gamepad) return;
        Gamepad = gamepad;
        Changed?.Invoke();
    }

    public static void Poll()
    {
        if (polledFrame == Time.frameCount) return;
        polledFrame = Time.frameCount;

        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool padUsed = false;
        if (pad != null)
        {
            var left = pad.leftStick.ReadValue();
            var right = pad.rightStick.ReadValue();
            var dpad = pad.dpad.ReadValue();
            if (pad == previousPad)
                padUsed = DeliberateMovement(left, previousLeft) ||
                    DeliberateMovement(right, previousRight) || DeliberateMovement(dpad, previousDpad);
            foreach (var control in pad.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame)
                    padUsed = true;
            previousLeft = left;
            previousRight = right;
            previousDpad = dpad;
        }
        previousPad = pad;

        var mouse = Mouse.current;
        bool mouseUsed = mouse != null && (mouse.delta.ReadValue().sqrMagnitude >= 1f ||
            mouse.scroll.ReadValue().sqrMagnitude > 0f ||
            mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
            mouse.middleButton.wasPressedThisFrame);
        bool keyboardUsed = Keyboard.current?.anyKey.wasPressedThisFrame == true;

        // Keyboard and mouse take priority if both kinds of device act this frame.
        if (keyboardUsed || mouseUsed) Set(false);
        else if (padUsed) Set(true);
    }

    private static bool DeliberateMovement(Vector2 value, Vector2 previous) =>
        value.sqrMagnitude >= 0.35f * 0.35f &&
        (value - previous).sqrMagnitude >= 0.15f * 0.15f;
}
