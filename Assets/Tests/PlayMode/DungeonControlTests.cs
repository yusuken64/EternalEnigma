#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonControlTests
    {
        private GameTestHarness harness;
        private bool? previousControl;
        private DungeonAnimationMode? previousSpeed;
        [UnitySetUp] public IEnumerator Setup()
        {
            previousControl=DungeonPreferences.FullControlOverride; previousSpeed=DungeonPreferences.AnimationOverride;
            DungeonPreferences.FullControlOverride=false; DungeonPreferences.AnimationOverride=DungeonAnimationMode.Current;
            harness=new GameTestHarness(); yield return harness.LoadDungeon(new TestScenario {AdditionalAllies=new[] {"Avery"}});
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return harness.Cleanup();
            DungeonPreferences.FullControlOverride=previousControl; DungeonPreferences.AnimationOverride=previousSpeed;
        }
        private IEnumerator AwaitOrder(Ally expected)
        {
            float end=Time.realtimeSinceStartup+8;
            while (!harness.Game.TurnManager.AwaitingCommand && Time.realtimeSinceStartup<end) yield return null;
            Assert.That(harness.Game.TurnManager.AwaitingCommand,Is.True);
            Assert.That(harness.Game.PlayerController.ControlledAlly,Is.SameAs(expected));
        }
        [UnityTest] public IEnumerator FullControlPromptsForEachActionAndRestoresLeader()
        {
            var game=harness.Game;var leader=harness.Ally;var second=game.Allies.First(a=>a!=leader);
            leader.BaseStats.ActionsPerTurnMax=2;leader.InvalidateCachedStats();leader.StartTurn();leader.SyncDisplayedStats();
            int hunger=leader.Vitals.HungerAccumulate, secondHunger=second.Vitals.HungerAccumulate;
            DungeonPreferences.FullControlOverride=true;
            leader.SetAction(new WaitAction());yield return AwaitOrder(leader);
            Assert.That(leader.Vitals.ActionsPerTurnLeft,Is.EqualTo(1));
            Assert.That(leader.DisplayedVitals.ActionsPerTurnLeft,Is.EqualTo(1));
            DungeonHudPortraitTests.CheckIndicator(game,leader,true);
            DungeonHudPortraitTests.CheckIndicator(game,second,false);
            Assert.That(second.Vitals.ActionsPerTurnLeft,Is.EqualTo(second.FinalStats.ActionsPerTurnMax));
            Assert.That(game.TurnManager.SubmitCommand(second,new WaitAction()),Is.False);
            Assert.That(leader.CanMove(),Is.True);
            leader.SetAction(new WaitAction());yield return null;yield return AwaitOrder(second);
            DungeonHudPortraitTests.CheckIndicator(game,leader,false,true);
            DungeonHudPortraitTests.CheckIndicator(game,second,true);
            Assert.That(game.PlayerController.PartyLeader,Is.SameAs(leader));
            Assert.That(game.PlayerController.CanOpenMenu(),Is.True);
            // A preference change must not switch the unfinished round into AI mode.
            DungeonPreferences.FullControlOverride=false;
            Assert.That(game.TurnManager.UsesFullControl,Is.True);
            second.SetAction(new WaitAction());yield return harness.WaitForIdle();
            Assert.That(game.PlayerController.ControlledAlly,Is.SameAs(leader));
            Assert.That(second.Vitals.HungerAccumulate,Is.EqualTo(secondHunger));
            Assert.That(leader.Vitals.HungerAccumulate,Is.EqualTo(hunger+2));
            Assert.That(game.TurnManager.UsesFullControl,Is.False);
            DungeonHudPortraitTests.CheckIndicator(game,leader,true);
            DungeonHudPortraitTests.CheckIndicator(game,second,false);
            yield return null;
            System.IO.Directory.CreateDirectory("Temp/DungeonUI");
            ScreenCapture.CaptureScreenshot("Temp/DungeonUI/party.png");yield return null;
        }
        [UnityTest] public IEnumerator NormalAllyPlaybackKeepsIndicatorsAlignedAtEverySpeed()
        {
            var game = harness.Game;
            var leader = harness.Ally;
            var second = game.Allies.First(a => a != leader);
            second.AllyStrategy = AllyStrategy.HoldPosition;
            foreach (DungeonAnimationMode mode in System.Enum.GetValues(typeof(DungeonAnimationMode)))
            {
                DungeonPreferences.AnimationOverride = mode;
                var probe = new IndicatorProbeAction(() =>
                {
                    Assert.That(leader.Vitals.ActionsPerTurnLeft, Is.Zero);
                    Assert.That(leader.DisplayedVitals.ActionsPerTurnLeft, Is.GreaterThan(0));
                    DungeonHudPortraitTests.CheckIndicator(game,leader,true);
                    DungeonHudPortraitTests.CheckIndicator(game,second,false);
                });
                leader.SetAction(probe);
                yield return harness.WaitForIdle();
                Assert.That(probe.Played, Is.True);
                foreach (var ally in game.Allies)
                    Assert.That(ally.DisplayedVitals.ActionsPerTurnLeft, Is.EqualTo(ally.Vitals.ActionsPerTurnLeft));
                DungeonHudPortraitTests.CheckIndicator(game,leader,true);
                DungeonHudPortraitTests.CheckIndicator(game,second,false);
            }
        }
        private sealed class IndicatorProbeAction : GameAction
        {
            private readonly System.Action check;
            internal bool Played;
            internal IndicatorProbeAction(System.Action check) { this.check = check; }
            internal override bool IsValid(Character actor) => true;
            internal override System.Collections.Generic.List<GameAction> ExecuteImmediate(Character actor) => new();
            internal override IEnumerator ExecuteRoutine(Character actor, bool skipAnimation = false)
            {
                check(); Played = true;
                yield return null;
            }
        }
        [UnityTest] public IEnumerator ImmediateMovementKeepsOrderedOutcomesAfterNextPrompt()
        {
            var hero = harness.Ally;
            var origin = hero.TilemapPosition;
            var destination = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down }
                .Select(d => origin + d).First(p => harness.Game.CurrentDungeon.CanWalkTo(origin,p) &&
                    harness.Game.CurrentDungeon.OverlapsAnyOtherCharacter(hero,Character.ToBounds(p),false) == null);
            GameMessages.BeginTurn();
            var move = new MovementAction(hero,origin,destination);
            move.SetPlaybackContext(DungeonAnimationMode.YourActionOnly,false,hero,hero);
            hero.ExecuteActionImmediate(move);
            Assert.That(move.ShouldAnimate(hero),Is.False);
            yield return hero.ExecuteActionRoutine(move);
            Assert.That(Object.FindFirstObjectByType<GameMessages>().TurnEvents,Is.Empty,"Movement itself must not appear in the event log.");
            var damage = new TakeDamageAction(hero,hero,5,false) { Environmental = true };
            damage.SetPlaybackContext(DungeonAnimationMode.YourActionOnly,false,hero,hero);
            hero.ExecuteActionImmediate(damage);
            yield return hero.ExecuteActionRoutine(damage);
            var heal = new TakeHealAction(hero,hero,2,false);
            heal.SetPlaybackContext(DungeonAnimationMode.YourActionOnly,false,hero,hero);
            hero.ExecuteActionImmediate(heal);
            yield return hero.ExecuteActionRoutine(heal);
            GameMessages.FinishAction();
            var log = Object.FindFirstObjectByType<GameMessages>();
            Assert.That(log.TurnEvents[0],Does.Contain("took 5 damage"));
            Assert.That(hero.transform.position,Is.EqualTo(harness.Game.CurrentDungeon.CellToWorld(destination)));
            DungeonPreferences.FullControlOverride=true;
            yield return AwaitOrder(hero);
            // Merely opening the next command prompt must not erase the results.
            Assert.That(log.TurnEvents.Count,Is.EqualTo(2));
            Assert.That(log.TurnEvents[1],Does.Contain("recovered 2 HP"));
            Assert.That(log.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains("recovered 2 HP") && t.text.Contains("took 5 damage")),Is.True);
            DungeonPreferences.FullControlOverride=false;
            yield return harness.WaitForIdle();
        }
        [UnityTest] public IEnumerator SpeedPolicyChangesOnlyAtRootActionBoundaries()
        {
            var hero=harness.Ally; var other=harness.Game.Allies.First(a=>a!=hero);
            var context=new DungeonActionPlayback(false,hero,other);
            var root=new WaitAction(); root.SetPlaybackContext(context);
            var consequence=new TakeDamageAction(other,hero,1,false); consequence.SetPlaybackContext(context);
            hero.ExecuteActionImmediate(consequence);
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.ControllingHero;
            Assert.That(root.ShouldAnimate(other),Is.True,"Incoming consequences animate their root action too.");
            DungeonPreferences.AnimationOverride=DungeonAnimationMode.YourActionOnly;
            Assert.That(consequence.ShouldAnimate(other),Is.True,"A running chain keeps its captured speed.");
            var next=new WaitAction();next.SetPlaybackContext(new DungeonActionPlayback(false,hero,other));
            Assert.That(next.ShouldAnimate(other),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator AnimationModesKeepOutcomesAndInstantEvents()
        {
            var leader=harness.Ally;var other=harness.Game.Allies.First(a=>a!=leader);
            var hit=new TakeDamageAction(other,leader,1,false);
            var hp=leader.Vitals.HP;
            foreach (DungeonAnimationMode mode in System.Enum.GetValues(typeof(DungeonAnimationMode)))
            {
                hit=new TakeDamageAction(other,leader,1,false);
                hit.SetPlaybackContext(mode,false,leader,other);
                leader.ExecuteActionImmediate(hit);
                Assert.That(hit.ShouldAnimate(other),Is.EqualTo(mode!=DungeonAnimationMode.YourActionOnly));
                yield return leader.ExecuteActionRoutine(hit);
            }
            Assert.That(leader.Vitals.HP,Is.EqualTo(hp-3));
            Assert.That(leader.DisplayedVitals.HP,Is.EqualTo(leader.Vitals.HP));
            var consequence=new WaitAction();consequence.SetPlaybackContext(DungeonAnimationMode.YourActionOnly,true,leader,leader);
            Assert.That(consequence.ShouldAnimate(leader),Is.True);
            var otherAction=new WaitAction();otherAction.SetPlaybackContext(DungeonAnimationMode.ControllingHero,false,leader,other);
            Assert.That(otherAction.ShouldAnimate(other),Is.False);
            GameMessages.BeginTurn();
            for(int i=0;i<120;i++) GameMessages.Post("Instant event "+i);
            GameMessages.FinishAction();
            var log=Object.FindFirstObjectByType<GameMessages>();
            Assert.That(log.TurnEvents.Count,Is.EqualTo(120));
            Assert.That(log.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("Instant event 0")&&t.text.Contains("Instant event 119")),Is.True);
            Common.Instance.GlobalSettings.ShowDialog();yield return null;
            Assert.That(Common.Instance.GlobalSettings.GetComponent<DungeonOptions>(),Is.Not.Null);
            Common.Instance.GlobalSettings.TabGroup.SetToTab(Common.Instance.GlobalSettings.TabGroup.TabContents.Count-1);
            yield return new WaitForSeconds(.3f);
            System.IO.Directory.CreateDirectory("Temp/DungeonUI");ScreenCapture.CaptureScreenshot("Temp/DungeonUI/options.png");yield return null;
            Common.Instance.GlobalSettings.Exit_Clicked();
        }
    }
}
#endif
