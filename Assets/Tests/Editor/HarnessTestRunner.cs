using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Persist results independently of the MCP socket, which reconnects at Play Mode transitions.
[InitializeOnLoad]
public static class HarnessTestRunner
{
    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation Final")]
    public static void RunPresentationFinal()=>Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.DungeonDockTests","EternalEnigma.Tests.GameMessageTests","EternalEnigma.Tests.MenuSelectionTests",
        "EternalEnigma.Tests.OverworldSceneTests.AuthoredSceneBuildsCampaignAndMovesHeroWithSealedGates");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation Input Followups")]
    public static void RunPresentationInputFollowups()=>Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MenuSelectionTests.MouseWinsSimultaneousInputAndControllerNoiseDoesNotStealIt",
        "EternalEnigma.Tests.OverworldSceneTests.AuthoredSceneBuildsCampaignAndMovesHeroWithSealedGates");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Dock Followups")]
    public static void RunDockFollowups()=>Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.InventorySkillTargetingTests",
        "EternalEnigma.Tests.ControllerFlowTests.CombatMovementAttackSkillTargetCancelAndConfirmUseControllerOnly",
        "EternalEnigma.Tests.MenuSceneNavigationTests.ContinueKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSelectionTests.MouseWinsSimultaneousInputAndControllerNoiseDoesNotStealIt",
        "EternalEnigma.Tests.OverworldSceneTests.AuthoredSceneBuildsCampaignAndMovesHeroWithSealedGates");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation Authoring")]
    public static void RunPresentationAuthoring()=>Run(TestMode.EditMode,"EternalEnigma.Tests.EditMode",
        "EternalEnigma.Tests.DungeonPresentationAuthoringTests","EternalEnigma.Tests.CoreIntegration.PaintedEnvironmentTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Overworld Presentation")]
    public static void RunOverworldPresentation()=>Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode","EternalEnigma.Tests.OverworldPresentationTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Dock")]
    public static void RunDungeonDock()=>Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode","EternalEnigma.Tests.DungeonDockTests","EternalEnigma.Tests.GameMessageTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Dock Regression")]
    public static void RunDockRegression()=>Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.UnifiedPartyMenuTests","EternalEnigma.Tests.InventorySkillTargetingTests",
        "EternalEnigma.Tests.MenuSelectionTests","EternalEnigma.Tests.MenuSceneNavigationTests","EternalEnigma.Tests.OverworldSceneTests",
        "EternalEnigma.Tests.ControllerFlowTests.CombatMovementAttackSkillTargetCancelAndConfirmUseControllerOnly");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Economy")]
    public static void RunEconomy() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EconomyGameplayTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Economy EditMode")]
    public static void RunEconomyEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.EconomyCatalogTests", "EternalEnigma.Tests.CoreIntegration.CampaignSaveTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Economy Return Regression")]
    public static void RunEconomyReturns() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.TownGameplayTests.VictoryReturnsRemainingStacksAndEquipment",
        "EternalEnigma.Tests.TownGameplayTests.DefeatLosesItemsButKeepsGold");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Core Bugfixes")]
    public static void RunCoreBugfixes() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "CoreBugfixTests", "HeldWalkingTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Core Bugfix EditMode")]
    public static void RunCoreBugfixEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode",
        "CoreStateRegressionTests", "EquipmentRegressionTests", "HeroClassTests", "SaveStoreTests",
        "CampaignSlotStoreTests", "FogVisibilityTests", "EternalEnigma.Tests.CoreIntegration.EquipmentTransferTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Held Walking")]
    public static void RunHeldWalking() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "HeldWalkingTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Held Walking Regression")]
    public static void RunHeldWalkingRegression() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "MovementRegressionTests", "EternalEnigma.Tests.DungeonControlTests", "EternalEnigma.Tests.SkillMovementTests",
        "EternalEnigma.Tests.CombatEffectPlaybackTests", "EnemyAnimationStateTests", "EternalEnigma.Tests.OverworldSceneTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Held Walking EditMode")]
    public static void RunHeldWalkingEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode",
        "DungeonCombatAnimationLoopTests", "GridMovementTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon UI Audit")]
    public static void RunDungeonUIAudit() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.DungeonVisualAuditTests", "EternalEnigma.Tests.DungeonHudPortraitTests",
        "EternalEnigma.Tests.ControllerFlowTests.CombatMovementAttackSkillTargetCancelAndConfirmUseControllerOnly");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Settings Interactions")]
    public static void RunSettingsInteractions() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.SettingsInteractionTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Settings EditMode")]
    public static void RunSettingsEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.SettingsPreferenceTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Settings PlayMode")]
    public static void RunSettingsPlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.SettingsInteractionTests", "EternalEnigma.Tests.MenuSelectionTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.MenuSceneNavigationTests.InventoryCanOpenAndCloseImmediatelyAndSettingsRestoreItsSelection");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Weapon Proficiency EditMode")]
    public static void RunWeaponProficiencyEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode",
        "HeroClassTests", "EquipmentRegressionTests", "EternalEnigma.Tests.CoreIntegration.EquipmentTransferTests", "EternalEnigma.Tests.ClassContentTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Weapon Proficiency PlayMode")]
    public static void RunWeaponProficiencyPlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.HeroPrefabTests", "EternalEnigma.Tests.InventorySkillTargetingTests", "EternalEnigma.Tests.ClassAssignmentTests");
    private const string SessionKey = "EternalEnigma.Tests.ActiveRun";
    private static readonly TestRunnerApi Api;

    static HarnessTestRunner()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Api.RegisterCallbacks(new Results());
        EditorApplication.update += StartQueuedRun;
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Run EditMode")]
    public static void RunEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Unified Presentation")]
    public static void RunUnifiedPresentation() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode","EternalEnigma.Tests.UnifiedPartyMenuTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation Rendering")]
    public static void RunPresentationRendering() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode","EternalEnigma.Tests.UnifiedPartyMenuTests.SilhouettesProjectGeometryWithoutDarkeningOverlapsOrHiddenCasters");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation Main Menu")]
    public static void RunPresentationMainMenu() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode","EternalEnigma.Tests.UnifiedPartyMenuTests.MainMenuCompositionKeepsDeveloperControlsClosed");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation EditMode")]
    public static void RunPresentationEditMode() => Run(TestMode.EditMode,"EternalEnigma.Tests.EditMode","EternalEnigma.Tests.CoreIntegration.EquipmentTransferTests","EternalEnigma.Tests.CoreIntegration.PaintedEnvironmentTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Painted Environment EditMode")]
    public static void RunPaintedEnvironmentEditMode() => Run(TestMode.EditMode,"EternalEnigma.Tests.EditMode",
        "EternalEnigma.Tests.CoreIntegration.PaintedEnvironmentTests","EternalEnigma.Tests.CoreIntegration.DungeonThemeTests",
        "EternalEnigma.Tests.CoreIntegration.EnvironmentKitTests","EternalEnigma.Tests.CoreIntegration.TownAndPropVisualTests",
        "EternalEnigma.Tests.CoreIntegration.BiomeDecorationTests","EternalEnigma.Tests.CoreIntegration.BiomeLayoutIntegrationTests",
        "EternalEnigma.Tests.CoreIntegration.TownLayoutIntegrationTests","EternalEnigma.Tests.CoreIntegration.BiomePreviewTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Painted Environment PlayMode")]
    public static void RunPaintedEnvironmentPlayMode() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.DungeonThemeTransitionTests","EternalEnigma.Tests.DungeonThemeExplorerTests","EternalEnigma.Tests.EnvironmentPlaygroundTests",
        "EternalEnigma.Tests.BiomeDecorationVisibilityTests","EternalEnigma.Tests.BiomeSceneryTests","EternalEnigma.Tests.CampaignTravelTests","EternalEnigma.Tests.OverworldSceneTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Painted Environment Regression")]
    public static void RunPaintedEnvironmentRegression() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.EnvironmentPlaygroundTests.ProductionTownUsesDetailedSmartHousesWithoutLegacyClusters",
        "EternalEnigma.Tests.BiomeSceneryTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Trainer EditMode")]
    public static void RunTrainerEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "TrainerOfferTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Trainer PlayMode")]
    public static void RunTrainerPlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.TownTrainerRankTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Floor Fix EditMode")]
    public static void RunDungeonFloorFixEditMode()=>Run(TestMode.EditMode,"EternalEnigma.Tests.EditMode",
        "EternalEnigma.Tests.CoreIntegration.DungeonThemeTests","EternalEnigma.Tests.CoreIntegration.TownAndPropVisualTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Floor Fix PlayMode")]
    public static void RunDungeonFloorFixPlayMode()=>Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.ControllerFlowTests.CombatMovementAttackSkillTargetCancelAndConfirmUseControllerOnly",
        "EternalEnigma.Tests.DungeonThemeTransitionTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Decorations")]
    public static void RunBiomeDecorations() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.BiomeDecorationVisibilityTests","EternalEnigma.Tests.DungeonThemeTransitionTests",
        "EternalEnigma.Tests.EnvironmentPlaygroundTests","EternalEnigma.Tests.CampaignTravelTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Travel")]
    public static void RunBiomeTravel() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.BiomeDecorationVisibilityTests",
        "EternalEnigma.Tests.CampaignTravelTests.OverworldBuildsOnFirstExitAndReusesTerrainAcrossTownAndDungeonTravel",
        "EternalEnigma.Tests.CampaignTravelTests.TownEntranceVictoryAndDuplicateCallbacksPersistKeyAndReturnInsideTown");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Main Menu Startup")]
    public static void RunMainMenuStartup() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.MainMenuStartupTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Controller Flows")]
    public static void RunControllerFlows() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.ControllerFlowTests", "EternalEnigma.Tests.MenuSelectionTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.MenuSceneNavigationTests.InventoryCanOpenAndCloseImmediatelyAndSettingsRestoreItsSelection");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Controller Combat")]
    public static void RunControllerCombat() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.ControllerFlowTests.CombatMovementAttackSkillTargetCancelAndConfirmUseControllerOnly");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Combat Effects")]
    public static void RunCombatEffects() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.CombatEffectPlaybackTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Combat Effects EditMode")]
    public static void RunCombatEffectsEditMode() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode",
        "CombatEffectAssignmentTests", "PresentationActionTests", "StatusEffectTests", "CombatMathTests", "ClassContentTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Combat Effects Regression")]
    public static void RunCombatEffectsRegression() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CombatEffectPlaybackTests", "EternalEnigma.Tests.CombatFoundationTests", "SightPlaybackTests", "EternalEnigma.Tests.SkillMovementTests", "EternalEnigma.Tests.SongCommandTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town And Prop Visuals")]
    public static void RunTownAndPropVisuals() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.TownAndPropVisualTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Fantasy Traps")]
    public static void RunFantasyTraps() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.FantasyTrapTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Fantasy Trap Playback")]
    public static void RunFantasyTrapPlayback() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.FantasyTrapPlaybackTests");

    // These synchronous geometry checks create isolated TWC output and can preserve a live run.
    [MenuItem("Tools/Eternal Enigma/Tests/Check Dungeon Ground Heights")]
    public static void CheckDungeonGroundHeights()
    {
        var assembly = System.Reflection.Assembly.Load("EternalEnigma.Tests.EditMode");
        var type = assembly.GetType("EternalEnigma.Tests.CoreIntegration.DungeonThemeTests", true);
        var fixture = Activator.CreateInstance(type);
        var method = type.GetMethod("ThemedGroundIgnoresLegacyRaisedMapRoot");
        method.Invoke(fixture, new object[] { false });
        method.Invoke(fixture, new object[] { true });
        Debug.Log("Dungeon ground-height regression checks passed: regular and throne floors.");
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Check Dungeon Reload Recovery")]
    public static void CheckDungeonReloadRecovery()
    {
        var type = System.Reflection.Assembly.Load("EternalEnigma.Tests.EditMode")
            .GetType("EternalEnigma.Tests.CoreIntegration.CoreLayerGeneratorTests", true);
        foreach (bool throne in new[] { false, true })
        {
            var fixture = Activator.CreateInstance(type);
            type.GetMethod("SetUp").Invoke(fixture, null);
            try { type.GetMethod("RuntimeFloorRecoversAfterScriptReload").Invoke(fixture, new object[] { throne }); }
            finally { type.GetMethod("TearDown").Invoke(fixture, null); }
        }
        Debug.Log("Dungeon reload recovery checks passed: regular and throne floors.");
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Run Scenery Gameplay")]
    public static void RunSceneryGameplay() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.BiomeSceneryTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Hero Portraits")]
    public static void RunHeroPortraits() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.HeroPortraitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon HUD Portraits")]
    public static void RunDungeonHudPortraits() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.DungeonHudPortraitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Controls")]
    public static void RunDungeonControls() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.DungeonControlTests", "EternalEnigma.Tests.DungeonHudPortraitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Movement Regression")]
    public static void RunMovementRegression() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "MovementRegressionTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Entry Visuals")]
    public static void RunDungeonEntryVisuals() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignTravelTests.TownToDungeonRemovesTransferredHeroPositionCircles");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Enemy Behaviors")]
    public static void RunEnemyBehaviors() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnemyBehaviorTests", "EternalEnigma.Tests.EnemyPrefabTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Enemy Animation States")]
    public static void RunEnemyAnimationStates() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EnemyAnimationStateTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Floor Message")]
    public static void RunFloorMessage() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "FloorMessageTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Roster Additions")]
    public static void RunRosterAdditions() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnemyBehaviorTests.SilverDevilActsTwiceAndGoopiRootsUntilKilled", "EternalEnigma.Tests.EnemyBehaviorTests.StatueAndMetalSlimeUseStaticAndRecoloredModels");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Messages")]
    public static void RunMessages() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.GameMessageTests", "EternalEnigma.Tests.OverworldSceneTests", "EternalEnigma.Tests.InventorySkillTargetingTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Message Display")]
    public static void RunMessageDisplay() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.GameMessageTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Regression")]
    public static void RunBiomeRegression() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.BiomeSceneryTests", "EternalEnigma.Tests.SkillRegressionTests", "EternalEnigma.Tests.SkillMovementTests", "EternalEnigma.Tests.InventorySkillTargetingTests", "EternalEnigma.Tests.DungeonThemeTransitionTests", "EternalEnigma.Tests.DungeonThemeExplorerTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Scenery")]
    public static void RunBiomeScenery() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.BiomeSceneryTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Biome Layouts")]
    public static void RunBiomeLayouts() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.BiomeLayoutIntegrationTests", "EternalEnigma.Tests.CoreIntegration.BiomePreviewTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Themes")]
    public static void RunDungeonThemes() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.DungeonThemeTransitionTests", "EternalEnigma.Tests.DungeonThemeExplorerTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Environment")]
    public static void RunEnvironment() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnvironmentPlaygroundTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Environment Assets")]
    public static void RunEnvironmentAssets() => Run(TestMode.EditMode, "EternalEnigma.Tests.EditMode", "EternalEnigma.Tests.CoreIntegration.EnvironmentKitTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run PlayMode")]
    public static void RunPlayMode() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Presentation")]
    public static void RunPresentation() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignPresentationTests", "EternalEnigma.Tests.MenuSelectionTests",
        "EternalEnigma.Tests.CampaignTravelTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Bamao Dialog Preview")]
    public static void RunBamaoDialogPreview() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.AutoplayTests.InputPromptCancelAndReturnPreserveOriginalSave");

    [MenuItem("Tools/Eternal Enigma/Tests/Run ButtonStyles")]
    public static void RunButtonStyles() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignPresentationTests", "EternalEnigma.Tests.MenuSelectionTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Shared UI Backgrounds")]
    public static void RunSharedUIBackgrounds() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignPresentationTests.SceneAndInstantiatedPrefabButtonsHaveSerializedStyles",
        "EternalEnigma.Tests.CampaignPresentationTests.CampaignUsesTravelHudAndPartyCanCloseBeforeLeavingTown",
        "EternalEnigma.Tests.DungeonControlTests.FullControlPromptsForEachActionAndRestoresLeader",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Skills")]
    public static void RunSkills() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.SkillRegressionTests", "EternalEnigma.Tests.InventorySkillTargetingTests", "EternalEnigma.Tests.SkillRankRegressionTests", "EternalEnigma.Tests.CombatFoundationTests", "EternalEnigma.Tests.AllySkillPolicyTests", "EternalEnigma.Tests.AllyAiClassPartyTests", "EternalEnigma.Tests.ClassSkillSmokeTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run AllyAI")]
    public static void RunAllyAI() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.AllySkillPolicyTests", "EternalEnigma.Tests.AllyAiClassPartyTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Overworld")]
    public static void RunOverworld() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.OverworldSceneTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Six Terrain")]
    public static void RunSixTerrain() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.EnvironmentPlaygroundTests.AuthoredPlaygroundBuildsBothGeneratorsAndRebuildsWithoutDuplicates");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Enemies")]
    public static void RunEnemies() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.EnemyPrefabTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Heroes")]
    public static void RunHeroes() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.HeroPrefabTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Classes")]
    public static void RunClasses() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.ClassAssignmentTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Travel Gate Routes")]
    public static void RunTravelGateRoutes() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.OverworldSceneTests.RequiredReturnTripSeed42",
        "EternalEnigma.Tests.OverworldSceneTests.RequiredReturnTripSeedNegativeOne",
        "EternalEnigma.Tests.OverworldSceneTests.RequiredReturnTripSeedZero");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Travel Interactions")]
    public static void RunTravelInteractions() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignTravelTests",
        "EternalEnigma.Tests.OverworldSceneTests.CompanionWithoutAnimatorCanWalkAfterCachedTerrainReload",
        "EternalEnigma.Tests.OverworldSceneTests.LockChooserOffersEveryAcquiredOptionAndCancelsWithoutOpening",
        "EternalEnigma.Tests.OverworldSceneTests.RequiredReturnTripSeed42",
        "EternalEnigma.Tests.OverworldSceneTests.RequiredReturnTripSeedNegativeOne",
        "EternalEnigma.Tests.OverworldSceneTests.RequiredReturnTripSeedZero",
        "EternalEnigma.Tests.TownAmbientGameplayTests",
        "EternalEnigma.Tests.TownGameplayTests.PartyWithoutAnimationComponentCanWalk",
        "EternalEnigma.Tests.TownGameplayTests.EnteringAndLeavingARoomOnlyChangesItsRoof",
        "EternalEnigma.Tests.ControllerFlowTests.WaitingAlliesBlockMovementAndRecruitFromAdjacentFacingInteraction");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Campaign")]
    public static void RunCampaign() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode", "EternalEnigma.Tests.CampaignTravelTests", "EternalEnigma.Tests.CampaignSleepTests", "EternalEnigma.Tests.CampaignTownServiceTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Campaign Slot UI")]
    public static void RunCampaignSlotUI() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode","EternalEnigma.Tests.CampaignSleepTests.ThreeSeedsHaveAccessibleHomesAndSlotBrowserPreservesCanceledReplacement");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Campaign Sleep")]
    public static void RunCampaignSleep() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode","EternalEnigma.Tests.CampaignSleepTests");
    [MenuItem("Tools/Eternal Enigma/Tests/Run Campaign Saves EditMode")]
    public static void RunCampaignSavesEditMode() => Run(TestMode.EditMode,"EternalEnigma.Tests.EditMode","CampaignSlotStoreTests","SaveStoreTests","EternalEnigma.Tests.CoreIntegration.CampaignSaveTests","TrainerOfferTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Autoplay")]
    public static void RunAutoplay() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.AutoplayTests.HidePanelAndTakeControlPreserveSessionAndSave",
        "EternalEnigma.Tests.AutoplayTests.InputPromptCancelAndReturnPreserveOriginalSave",
        "EternalEnigma.Tests.AutoplayTests.NormalModeAllowsDefeatAndWritesTuningReport",
        "EternalEnigma.Tests.AutoplayTests.DebugProtectsOnlyPartyAndRestoresResourceRulesOnExit");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Demo")]
    public static void RunDemo() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutOverwritingExistingSave");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Main Menu")]
    public static void RunMainMenu() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MainMenuPresentationTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutOverwritingExistingSave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.ContinueKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DungeonReturnKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.DungeonControlTests.FullControlPromptsForEachActionAndRestoresLeader");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Main Menu Presentation")]
    public static void RunMainMenuPresentation() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MainMenuPresentationTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Hero Selection")]
    public static void RunHeroSelection() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignPresentationTests.HeroPickerUsesRealArtworkAndBackDoesNotStartCampaign",
        "EternalEnigma.Tests.CampaignPresentationTests.SelectedHeroConfirmsExactlyOnce",
        "EternalEnigma.Tests.ClassAssignmentTests.NewSaveUsesTheChosenHeroesFixedClasses",
        "EternalEnigma.Tests.MainMenuPresentationTests.VisualsLoopWithoutGameplayAndMenuRemainsUsable");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Dungeon Startup")]
    public static void RunDungeonStartup() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DirectDungeonStartCreatesPartyWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DirectDungeonStartPreservesExistingSave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutASave",
        "EternalEnigma.Tests.MenuSceneNavigationTests.TestDungeonStartsWithoutOverwritingExistingSave");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town Interiors EditMode")]
    public static void RunTownInteriorsEditMode() => Run(TestMode.EditMode,"EternalEnigma.Tests.EditMode","EternalEnigma.Tests.CoreIntegration.TownInteriorIntegrationTests");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town Interiors PlayMode")]
    public static void RunTownInteriorsPlayMode() => Run(TestMode.PlayMode,"EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.TownAmbientGameplayTests","EternalEnigma.Tests.CampaignTownServiceTests",
        "EternalEnigma.Tests.TownGameplayTests.PartyWithoutAnimationComponentCanWalk",
        "EternalEnigma.Tests.TownTrainerRankTests.ConfirmationRevalidatesPointsAndClasslessStillPaysGold",
        "EternalEnigma.Tests.TownTrainerRankTests.RestoredHeroKeepsRanksAndHighestLevel",
        "EternalEnigma.Tests.TownTrainerRankTests.TrainerDialogShowsRanksAndRefreshesAfterPurchase");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town")]
    public static void RunTown() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.TownAmbientGameplayTests",
        "EternalEnigma.Tests.CampaignTownServiceTests",
        "EternalEnigma.Tests.TownGameplayTests",
        "EternalEnigma.Tests.TownTrainerRankTests",
        "EternalEnigma.Tests.MenuSceneNavigationTests.InventoryCanOpenAndCloseImmediatelyAndSettingsRestoreItsSelection",
        "EternalEnigma.Tests.MenuSceneNavigationTests.SettingsKeepCategoryFocusAndBackReturnsToGameplay",
        "EternalEnigma.Tests.MenuSceneNavigationTests.ContinueKeepsTownCoveredUntilHeroCameraIsReady",
        "EternalEnigma.Tests.MenuSceneNavigationTests.DungeonReturnKeepsTownCoveredUntilHeroCameraIsReady",
        "HarnessSmokeTests.TownScenarioLoadsSuppliedGoldAndAlly");

    [MenuItem("Tools/Eternal Enigma/Tests/Run Town Generation")]
    public static void RunTownGeneration() => Run(TestMode.PlayMode, "EternalEnigma.Tests.PlayMode",
        "EternalEnigma.Tests.CampaignTownServiceTests",
        "EternalEnigma.Tests.CampaignTravelTests",
        "EternalEnigma.Tests.EnvironmentPlaygroundTests");

    private static void Run(TestMode mode, string filter, params string[] testFilters)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!string.IsNullOrEmpty(SessionState.GetString(SessionKey, "")))
            throw new InvalidOperationException("A harness run is already active.");
        SessionState.SetBool("EternalEnigma.Autoplay.ManualChecks", Array.Exists(testFilters,
            name => name.StartsWith("EternalEnigma.Tests.AutoplayTests.", StringComparison.Ordinal)));
        var run = new RunSummary { runId = Guid.NewGuid().ToString("N"), mode = mode.ToString(), state = "Queued", filter = filter, testFilters = testFilters };
        Directory.CreateDirectory("Temp/HarnessResults");
        SessionState.SetString(SessionKey, JsonUtility.ToJson(run));
        Write(run);
    }

    private static void StartQueuedRun()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var json = SessionState.GetString(SessionKey, "");
        if (string.IsNullOrEmpty(json)) return;
        var run = JsonUtility.FromJson<RunSummary>(json);
        if (run.state != "Queued") return;
        run.state = "Running";
        SessionState.SetString(SessionKey, JsonUtility.ToJson(run));
        Write(run);
        try
        {
            var id = Api.Execute(new ExecutionSettings(new Filter {
                testMode = (TestMode)Enum.Parse(typeof(TestMode), run.mode), assemblyNames = new[] { run.filter },
                testNames = run.testFilters == null || run.testFilters.Length == 0 ? null : run.testFilters
            }));
            SessionState.SetString(SessionKey + ".Job", id);
        }
        catch
        {
            run.state = "Completed";
            run.failed = 1;
            Write(run);
            SessionState.EraseString(SessionKey);
            throw;
        }
    }

    [MenuItem("Tools/Eternal Enigma/Tests/Cancel Run")]
    public static void CancelRun()
    {
        var job = SessionState.GetString(SessionKey + ".Job", "");
        if (!string.IsNullOrEmpty(job)) TestRunnerApi.CancelTestRun(job);
        var json = SessionState.GetString(SessionKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            var run = JsonUtility.FromJson<RunSummary>(json);
            run.state = "Completed";
            run.failed = 1;
            Write(run);
        }
        SessionState.EraseString(SessionKey);
        SessionState.EraseString(SessionKey + ".Job");
        SessionState.EraseBool("EternalEnigma.Autoplay.ManualChecks");
    }

    private static void Write(RunSummary run) =>
        File.WriteAllText($"Temp/HarnessResults/{run.mode}.json", JsonUtility.ToJson(run, true));

    [Serializable]
    private sealed class RunSummary
    {
        public string runId;
        public string mode;
        public string state;
        public string filter;
        public string[] testFilters;
        public int passed;
        public int failed;
        public int skipped;
        public string xml;
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var run = JsonUtility.FromJson<RunSummary>(json);
            run.passed = result.PassCount;
            run.failed = result.FailCount;
            run.skipped = result.SkipCount;
            run.state = "Completed";
            run.xml = $"Temp/HarnessResults/{run.mode}.xml";
            TestRunnerApi.SaveResultToFile(result, run.xml);
            Write(run);
            SessionState.EraseString(SessionKey);
            SessionState.EraseString(SessionKey + ".Job");
            SessionState.EraseBool("EternalEnigma.Autoplay.ManualChecks");
        }
    }
}
