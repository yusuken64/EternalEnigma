using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

public static class AudioTestRunner
{
    [MenuItem("Tools/Eternal Enigma/Audio/Test Play Mode")]
    public static void PlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", new[] {
        "EternalEnigma.Tests.TerminalAudioTests",
        "EternalEnigma.Tests.GameplayAudioTests",
        "EternalEnigma.Tests.CombatEffectPlaybackTests",
        "EternalEnigma.Tests.SettingsInteractionTests",
        "EternalEnigma.Tests.MenuSelectionTests",
        "EternalEnigma.Tests.TownGameplayTests.RoomAndTransactionAudioPlaysOnceOnTheCorrectChannelWithoutAnimations",
        "EternalEnigma.Tests.OverworldSceneTests.GateUnlockAudioOnlyPlaysForANewUnlock",
        "EternalEnigma.Tests.UnifiedPartyMenuTests.EquipmentConflictsKeepCopiesAndOverworldCommitsSurviveReload",
        "EternalEnigma.Tests.CampaignTravelTests.RosterAndTownLayoutSurviveBenchingAndRoundTrips",
        "EternalEnigma.Tests.GameplayFollowupTests.MimicRevealAndSpiderSplitReplayEffectsSoundAndHistory"
    });
    [MenuItem("Tools/Eternal Enigma/Audio/Test Menu Feedback")]
    public static void MenuFeedback() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", new[] {
        "EternalEnigma.Tests.SettingsInteractionTests",
        "EternalEnigma.Tests.MenuSelectionTests"
    });
    [MenuItem("Tools/Eternal Enigma/Audio/Test Edit Mode")]
    public static void EditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", new[] {
        "EternalEnigma.Tests.TerminalAudioProcessorTests",
        "CombatEffectAssignmentTests"
    });
    [MenuItem("Tools/Eternal Enigma/Audio/Test Terminal Audio")]
    public static void TerminalAudio() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", new[] {
        "EternalEnigma.Tests.TerminalAudioTests",
        "EternalEnigma.Tests.SettingsInteractionTests.TerminalToggleUsesOneUICueForButtonAndShortcuts"
    });
    static void Run(TestMode mode, string assembly, string[] filters) => typeof(HarnessTestRunner)
        .GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { mode, assembly, filters });
}
