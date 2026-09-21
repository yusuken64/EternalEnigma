using System.Linq;
using EternalEnigma.Core.Progression;
using UnityEngine;

public sealed class OverworldSandboxControls : MonoBehaviour
{
    public OverworldScene Scene;
    private void OnGUI()
    {
        if (!Scene.IsReady || !Scene.Context.IsSandbox) return;
        GUILayout.BeginArea(new Rect(16, 455, 580, 280), GUI.skin.box);
        GUILayout.Label("OVERWORLD SANDBOX ? saves disabled");
        if (GUILayout.Button("Claim eligible location rewards")) Scene.ClaimRewards();
        if (GUILayout.Button("Simulate dungeon victory at this marker")) Scene.SimulateDungeonVictory();
        if (Scene.Context.Location?.Kind == LocationKind.Town)
            foreach (string id in Scene.Context.Roster.ToArray())
                if (GUILayout.Button((Scene.Context.Active.Contains(id) ? "Bench " : "Select ") + id)) Scene.ToggleCompanion(id);
        if (GUILayout.Button("Exit sandbox")) Common.Instance.Travel.ReturnToMenu();
        GUILayout.EndArea();
    }
}

public static class OverworldLaunch
{
    public static int TakeSeed(int fallback)
    {
#if UNITY_EDITOR
        int seed = UnityEditor.SessionState.GetInt("EternalEnigma.SandboxSeed", fallback);
        UnityEditor.SessionState.EraseInt("EternalEnigma.SandboxSeed");
        return seed;
#else
        return fallback;
#endif
    }
}
