#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class FeelPlaybackTests
    {
        private GameTestHarness harness;
        private DungeonAnimationMode? animation;
        [UnitySetUp] public IEnumerator SetUp()
        {
            animation=DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.Normal;
            harness=new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            try {yield return harness.Cleanup();}
            finally {DungeonPreferences.AnimationOverride=animation;}
        }
        [UnityTest] public IEnumerator VisualClipChoiceDoesNotChangeCombatRandomState()
        {
            var hero=harness.Ally.HeroAnimator;
            var state=Random.state;float expected=Random.value;Random.state=state;
            for(int i=0;i<12;i++){hero.PlayAttackAnimation();hero.PlayIdleAnimation();}
            Assert.That(Random.value,Is.EqualTo(expected));
            Assert.That(hero.LastPlayedAction,Is.EqualTo(AnimatedAction.Idle));
            yield return null;
        }
        [UnityTest] public IEnumerator CelebrationReturnsToCurrentStanceAndMissingClipsFallBack()
        {
            var hero=harness.Ally.HeroAnimator;
            Assert.That(hero.PlayOneShot(AnimatedAction.LevelUp),Is.True);
            hero.CurrentStance=Stance.SingleSword;
            yield return harness.WaitUntil(()=>hero.LastPlayedAction==AnimatedAction.Idle,"celebration return to idle");
            Assert.That(hero.Animator.runtimeAnimatorController,Is.SameAs(hero.StanceAnimations.Single(s=>s.Stance==hero.CurrentStance).AnimatorController));
            Assert.That(hero.PlayOneShot((AnimatedAction)(-1)),Is.False);
            Assert.That(hero.LastPlayedAction,Is.EqualTo(AnimatedAction.Idle));
        }
        [UnityTest] public IEnumerator StatusLoopIsNotRestartedAndSkippedCelebrationsDoNotPlay()
        {
            var hero=harness.Ally.HeroAnimator;
            hero.SetStatusLoop(AnimatedAction.Defend);
            int count=hero.PlaybackCount;
            hero.SetStatusLoop(AnimatedAction.Defend);
            Assert.That(hero.PlaybackCount,Is.EqualTo(count));
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.NoAnimations;
            Assert.That(hero.PlayOneShot(AnimatedAction.LevelUp),Is.False);
            Assert.That(hero.PlayOneShot(AnimatedAction.Victory),Is.False);
            hero.SetStatusLoop(AnimatedAction.Sleeping);
            Assert.That(hero.PlaybackCount,Is.EqualTo(count));
            var routine=new LevelUpAction(harness.Ally).ExecuteRoutine(harness.Ally,true);
            Assert.That(routine.MoveNext(),Is.False);
            Assert.That(hero.PlaybackCount,Is.EqualTo(count));
            yield return null;
        }
    }
}
#endif
