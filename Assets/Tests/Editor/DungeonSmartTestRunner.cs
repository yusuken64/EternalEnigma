using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

/// <summary>Use the existing durable harness reports across MCP/Play Mode reconnects.</summary>
public static class DungeonSmartTestRunner
{
    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Test Edit Mode")]
    public static void EditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", new[] {
        "EternalEnigma.Tests.EditMode.DungeonSmartLayerTests",
        "EternalEnigma.Tests.EditMode.DioramaItemTests",
        "EternalEnigma.Tests.CoreIntegration.DungeonThemeTests",
        "EternalEnigma.Tests.CoreIntegration.TownAndPropVisualTests",
        "EternalEnigma.Tests.CoreIntegration.BiomeDecorationTests",
        "EternalEnigma.Tests.CoreIntegration.BiomeLayoutIntegrationTests",
        "EternalEnigma.Tests.FogVisibilityTests"
    });
    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Test Play Mode")]
    public static void PlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", new[] {
        "EternalEnigma.Tests.DungeonThemeTransitionTests",
        "EternalEnigma.Tests.DungeonThemeExplorerTests",
        "EternalEnigma.Tests.BiomeSceneryTests",
        "EternalEnigma.Tests.BiomeDecorationVisibilityTests",
        "EternalEnigma.Tests.DioramaGameplayTests.CurrencyUsesProductionPlacementAndAwardsItsSeededAmount",
        "EternalEnigma.Tests.DioramaGameplayTests.IndividualAndFallbackItemsDropAndThrowThroughProductionPaths",
        "EternalEnigma.Tests.DioramaGameplayTests.KeyAndMimicUseAuthoredFloorPoses"
        ,"EternalEnigma.Tests.DioramaGameplayTests.FloorItemRatiosUseTheLiveDungeonHero"
    });
    static void Run(TestMode mode,string assembly,string[] filters) => typeof(HarnessTestRunner)
        .GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{mode,assembly,filters});
}
