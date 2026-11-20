using UnityEditor;
using UnityEngine;

internal static class OverworldDiagnosticsMenu
{
    private const string Menu = "Tools/Eternal Enigma/Debug/Toggle Overworld Diagnostics";

    [MenuItem(Menu)]
    private static void Toggle()
    {
        var world = Object.FindFirstObjectByType<OverworldScene>();
        if (world?.Context?.IsSandbox != true) return;
        world.ShowSandboxControls = !world.ShowSandboxControls;
        world.GetComponent<OverworldSandboxControls>().enabled = world.ShowSandboxControls;
        HudRefresh.Request(HudChange.Visibility);
    }

    [MenuItem(Menu, true)]
    private static bool Available() => EditorApplication.isPlaying &&
        Object.FindFirstObjectByType<OverworldScene>()?.Context?.IsSandbox == true;
}
