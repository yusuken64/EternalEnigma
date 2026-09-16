#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.TestTools;

// Runs before Test Runner enters Play Mode, unlike UnitySetUp.
public sealed class HarnessSceneBootstrap : IPrebuildSetup, IPostBuildCleanup
{
    private const string PreviousKey = "EternalEnigma.Tests.PreviousSceneLoadingOverride";
    public void Setup()
    {
        if (!SessionState.GetBool(PreviousKey + ".Owned", false))
            SessionState.SetBool(PreviousKey, LoadingSceneIntegration.SuppressAutomaticSceneLoading);
        SessionState.SetBool(PreviousKey + ".Owned", true);
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = true;
    }

    public void Cleanup()
    {
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = SessionState.GetBool(PreviousKey, false);
        SessionState.EraseBool(PreviousKey);
        SessionState.EraseBool(PreviousKey + ".Owned");
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Restore Normal Startup")]
    public static void RestoreNormalStartup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Stop the test run before restoring startup.");
        LoadingSceneIntegration.SuppressAutomaticSceneLoading = false;
        SessionState.EraseBool(PreviousKey);
        SessionState.EraseBool(PreviousKey + ".Owned");
    }
}
#endif
