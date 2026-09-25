using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OverworldSceneAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Launch Overworld Sandbox")]
    public static void LaunchSandbox()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Overworld.unity");
        var map = UnityEngine.Object.FindFirstObjectByType<CampaignOverworld>();
        SessionState.SetInt("EternalEnigma.SandboxSeed", map.Seed);
        EditorApplication.EnterPlaymode();
    }
}
