using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class CursorManager : MonoBehaviour
{
    private Gamepad previousPad;
    private Vector2 previousLeftStick;
    private Vector2 previousRightStick;
    private Vector2 previousDpad;

    private void Update()
    {
        var pad = Gamepad.current;
        bool controllerUsed = false;
        if (pad != null)
        {
            if (pad != previousPad)
                previousLeftStick = previousRightStick = previousDpad = Vector2.zero;
            var left = pad.leftStick.ReadValue();
            var right = pad.rightStick.ReadValue();
            var dpad = pad.dpad.ReadValue();
            controllerUsed = DeliberateMovement(left, previousLeftStick)
                || DeliberateMovement(right, previousRightStick) || DeliberateMovement(dpad, previousDpad);
            foreach (var control in pad.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame)
                    controllerUsed = true;
            previousLeftStick = left;
            previousRightStick = right;
            previousDpad = dpad;
        }
        previousPad = pad;

        var mouse = Mouse.current;
        bool mouseUsed = mouse != null && (mouse.delta.ReadValue().sqrMagnitude >= 1f
            || mouse.scroll.ReadValue().sqrMagnitude > 0f
            || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
            || mouse.middleButton.wasPressedThisFrame);

        // Mouse wins simultaneous activity. A held stick or unchanged report
        // cannot steal the pointer back on the following frame.
        if (mouseUsed) Cursor.visible = true;
        else if (controllerUsed) Cursor.visible = false;
        // Hide without warping the pointer to the screen centre.
        Cursor.lockState = CursorLockMode.None;
    }

    private static bool DeliberateMovement(Vector2 value, Vector2 previous) =>
        value.sqrMagnitude >= 0.35f * 0.35f && (value - previous).sqrMagnitude >= 0.15f * 0.15f;
}
