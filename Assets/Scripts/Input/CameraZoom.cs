using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Bounded gameplay zoom shared by the follow and overworld cameras.</summary>
internal static class CameraZoom
{
    // Current authored overworld size and dungeon size after CameraController's pitch correction.
    internal const float OverworldSize = 13f;
    internal static readonly float DungeonSize = 7.5f * Mathf.Sqrt(296f / 340f);

    internal static void Update(Camera camera)
    {
        var input = MenuUIInputModule.Active;
        var common = Common.Instance;
        if (camera == null || !camera.orthographic || input == null || !camera.isActiveAndEnabled || TerminalMode.Effective ||
            ControlBindings.SuppressInput || input.HasDialog || input.InputConsumed || AutoplayRunner.BlocksPlayerInput ||
            common?.GlobalSettings?.IsOpen == true || common?.ScreenTransition?.BlocksInput == true ||
            common?.Travel?.IsTransitioning == true || ScreenTransition.ControlsCamera(camera)) return;

        float amount = Read(input.ZoomIn) - Read(input.ZoomOut);
        if (Mathf.Approximately(amount, 0)) return;
        // No saved or session-wide zoom: every scene starts from its authored camera size.
        camera.orthographicSize = Mathf.Clamp(camera.orthographicSize * Mathf.Exp(-amount), DungeonSize, OverworldSize);
    }

    private static float Read(InputAction action)
    {
        float value = Mathf.Max(0, action.ReadValue<float>());
        if (action.activeControl?.device is Mouse mouse && action.activeControl.parent == mouse.scroll)
        {
            // Wheel deltas already accumulate for the frame; never multiply them by deltaTime.
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer))
                value /= 120f;
            return value * .12f;
        }
        return Mathf.Clamp01(value) * Mathf.Min(Time.unscaledDeltaTime, .1f) * .8f;
    }
}
