#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class NewCampaignPacingTests
    {
        private GameTestHarness harness;
        private DungeonAnimationMode? previousAnimation;
        private float previousTimeScale;

        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            previousAnimation = DungeonPreferences.AnimationOverride;
            previousTimeScale = Time.timeScale;
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return harness.Cleanup();
            DungeonPreferences.AnimationOverride = previousAnimation;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator ConfirmedNewJourneyResetsPacingButSaveCreationPreservesDebugSettings()
        {
            using var preferences = GamePreferences.Isolate();
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            DungeonPreferences.AnimationMode = DungeonAnimationMode.NoAnimations;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.AnimateControlledHeroActions;
            Time.timeScale = 32;

            // The fast autorunner uses save creation directly, not the player confirmation path.
            menu.CreateNewSave(42);
            Assert.That(Time.timeScale, Is.EqualTo(32));
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.AnimateControlledHeroActions));
            Assert.That(GamePreferences.GetInt("Dungeon.AnimationModeV2"), Is.EqualTo((int)DungeonAnimationMode.NoAnimations));

            menu.StartGame();
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(DungeonPreferences.AnimationOverride, Is.Null);
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
            Assert.That(GamePreferences.GetInt("Dungeon.AnimationModeV2"), Is.EqualTo((int)DungeonAnimationMode.Normal));
            Assert.That(GamePreferences.IsIsolated, Is.True, "Normal defaults must respect the diagnostic preference store.");
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !Common.Instance.Travel.IsTransitioning, "new journey town at normal speed");
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
        }
    }
}
#endif
