#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class EncounterGameplayTests
    {
        GameTestHarness harness;
        bool? previousControl;
        [UnitySetUp] public IEnumerator Setup()
        { previousControl = DungeonPreferences.FullControlOverride; DungeonPreferences.FullControlOverride = false; harness=new GameTestHarness { TimeoutSeconds=120 }; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        { yield return harness.Cleanup(); DungeonPreferences.FullControlOverride=previousControl; }

        IEnumerator LoadEncounter(EncounterKind kind)
        {
            yield return harness.LoadMainMenu(null);
            Object.FindFirstObjectByType<MainMenu>().StartGame();
            yield return harness.WaitUntil(()=>Object.FindFirstObjectByType<Town>()?.IsReady==true,"initial town");
            var common=Common.Instance;var context=common.CampaignContext;
            context.Completed.Add("repeatable-0");
            context.Position=context.Grid.Locations[context.Campaign.Locations.First(l=>l.Encounter?.Kind==kind).Id];
            context.State.Scene="Overworld";
            Assert.That(common.Travel.EnterLocation(),Is.True);
            yield return harness.WaitForIdle();
        }

        static void Resolve(Character actor,GameAction action)
        {
            var queue=new Queue<GameAction>();queue.Enqueue(action);
            while(queue.Count>0)
                foreach(var effect in actor.ExecuteActionImmediate(queue.Dequeue())??new List<GameAction>())queue.Enqueue(effect);
        }

        [UnityTest] public IEnumerator SokobanUsesProductionMovementAndRejectsUnfinishedExit()
        {
            yield return LoadEncounter(EncounterKind.Sokoban);
            var game=harness.Game;var player=harness.Ally;
            Assert.That(game.CurrentDungeon.IsThroneFloor,Is.False);
            Assert.That(game.CurrentDungeon.IsExitFloor,Is.True);
            Assert.That(game.CanExitFloor,Is.False);
            Assert.That(Common.Instance.Travel.FinishDungeon(true,game.PlayerController,false),Is.False);
            foreach(var position in game.CurrentDungeon.Floor.SolutionWitness)
            {
                var target=position.ToCell();
                var follower=game.Allies.FirstOrDefault(a=>a!=player&&a.TilemapPosition==target);
                GameAction move=follower!=null?new SwapAllyPositionAction(player,follower):new MovementAction(player,player.TilemapPosition,target);
                Assert.That(move.IsValid(player),Is.True,$"Witness step {player.TilemapPosition} -> {target}");
                Resolve(player,move);
            }
            Assert.That(game.CanExitFloor,Is.True);
            Assert.That(game.Encounter.Puzzle.Complete,Is.True);
        }

        [UnityTest] public IEnumerator LureCompletionRemovesTargetWithoutReplayingItsDestroyedAnimator()
            => CompleteTargetPuzzle(EncounterKind.Lure, DungeonAnimationMode.NoAnimations);

        [UnityTest] public IEnumerator HerdingCompletionRemovesTargetWithNormalAnimationPlayback()
            => CompleteTargetPuzzle(EncounterKind.Herding, DungeonAnimationMode.Normal);

        IEnumerator CompleteTargetPuzzle(EncounterKind kind, DungeonAnimationMode animationMode)
        {
            var previousAnimation = DungeonPreferences.AnimationOverride;
            try
            {
                DungeonPreferences.AnimationOverride = animationMode;
                yield return LoadEncounter(kind);
                var game = harness.Game;
                var target = game.Enemies.Single(e => e.GetComponent<EncounterActor>()?.Invulnerable == true);
                Assert.That(game.CanExitFloor, Is.False);
                for (int turn = 0; turn < 120 && !game.Encounter.Puzzle.Complete; turn++)
                {
                    var action = game.Encounter.AutoplayPuzzleAction(harness.Ally);
                    Assert.That(action, Is.Not.Null);
                    Assert.That(action.IsValid(harness.Ally), Is.True);
                    yield return harness.ExecuteAction(action);
                }
                yield return null; // Let the completed objective's deferred destruction finish.
                Assert.That(game.Encounter.Puzzle.Complete, Is.True);
                Assert.That(game.CanExitFloor, Is.True);
                Assert.That(target == null, Is.True);
                Assert.That(game.Enemies, Is.Empty);
                yield return harness.ExecuteAction(new WaitAction());
                Assert.That(game.TurnManager.IsProcessingTurn, Is.False);
            }
            finally { DungeonPreferences.AnimationOverride = previousAnimation; }
        }

        [UnityTest] public IEnumerator ResetRestoresInventoryIdentityGoldExperienceAndSamePuzzle()
        {
            yield return LoadEncounter(EncounterKind.LightPattern);
            var game=harness.Game;var player=game.PlayerController;
            string fingerprint=DungeonFloorFingerprint.Compute(game.CurrentDungeon.Floor);
            int gold=player.Gold,experience=harness.Ally.Vitals.Exp;
            var items=player.Inventory.InventoryItems.ToArray();
            player.Gold+=517;harness.Ally.Vitals.Exp+=999;
            player.Inventory.Clear();
            if(Common.Instance.ItemManager.StartingItems.Count>0)player.Inventory.Add(Common.Instance.ItemManager.StartingItems[0].AsInventoryItem(null));
            var step=game.CurrentDungeon.Floor.SolutionWitness.First().ToCell();
            Resolve(harness.Ally,new MovementAction(harness.Ally,harness.Ally.TilemapPosition,step));
            Assert.That(game.ResetFloor(),Is.True);
            Assert.That(game.ResetFloor(),Is.False,"A pending reset cannot be entered twice.");
            yield return harness.WaitForIdle();
            Assert.That(player.Gold,Is.EqualTo(gold));Assert.That(harness.Ally.Vitals.Exp,Is.EqualTo(experience));
            Assert.That(player.Inventory.InventoryItems,Is.EqualTo(items));
            Assert.That(DungeonFloorFingerprint.Compute(game.CurrentDungeon.Floor),Is.EqualTo(fingerprint));
            Assert.That(game.Encounter.Puzzle.Player,Is.EqualTo(game.CurrentDungeon.Floor.Start));
            Assert.That(harness.Ally.Skills.Single(s=>s.SkillName=="Reset Floor").SPCost,Is.Zero);
        }

        [UnityTest] public IEnumerator PrisonEscrowRetainsOwnersStacksAndOriginalItemsBeforeTemporaryLoot()
        {
            yield return harness.LoadDungeon(new TestScenario { IncludeStartingItems=true });
            var game=harness.Game;var player=game.PlayerController;
            var equipment=harness.Ally.Equipment.GetEquippedItems().ToArray();
            var bag=player.Inventory.InventoryItems.ToArray();
            foreach(var item in bag.Where(i=>i.HasStacks))item.StackStock=7;
            var stocks=bag.Select(i=>i.StackStock).ToArray();
            var escrow=new PrisonEscrow();escrow.Confiscate(game);
            Assert.That(player.Inventory.InventoryItems,Is.Empty);Assert.That(harness.Ally.Equipment.GetEquippedItems(),Is.Empty);
            while(player.Inventory.CanAdd())player.Inventory.Add(Common.Instance.ItemManager.StartingItems[0].AsInventoryItem(null));
            Assert.That(escrow.Recover(game),Is.True);Assert.That(escrow.Recover(game),Is.False);
            CollectionAssert.AreEqual(equipment,harness.Ally.Equipment.GetEquippedItems());
            CollectionAssert.AreEqual(bag,player.Inventory.InventoryItems.Take(bag.Length));
            CollectionAssert.AreEqual(stocks,bag.Select(i=>i.StackStock));
            Assert.That(escrow.Overflow.Count,Is.GreaterThanOrEqualTo(bag.Length));
        }

        [UnityTest] public IEnumerator ObjectiveTargetsResistDamageInstantDeathAndConversion()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var cell=harness.Game.CurrentDungeon.GetWalkableNeighborhoodTiles(harness.Ally.TilemapPosition).First(p=>p!=harness.Ally.TilemapPosition);
            yield return harness.SpawnEnemy("Enemy_Slime",cell);
            var enemy=(Enemy)harness.Game.Enemies.Last();
            harness.Game.Encounter.Register(enemy,"pursuer",true);
            int hp=enemy.Vitals.HP;
            Resolve(harness.Ally,new TakeDamageAction(harness.Ally,enemy,99999,false));
            Resolve(harness.Ally,new DeathAction(enemy,harness.Ally));
            Assert.That(enemy.Vitals.HP,Is.EqualTo(hp));Assert.That(harness.Game.Enemies,Contains.Item(enemy));
            Assert.That(DominateAction.CanDominate(enemy),Is.False);
        }

        [UnityTest] public IEnumerator EveryBossVariantHasFootprintWarningSpecialAndNormalDeath()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var game=harness.Game;var dungeon=game.CurrentDungeon;
            var center=Enumerable.Range(2,dungeon.dungeonWidth-4).SelectMany(x=>Enumerable.Range(2,dungeon.dungeonHeight-4).Select(y=>new Vector3Int(x,y)))
                .First(p=>Character.ToBounds(FootPrint.Size3x3,p).Cells().All(dungeon.IsWalkable)&&dungeon.IsWalkable(p+Vector3Int.right*3));
            harness.Ally.SetPosition(center+Vector3Int.right*3);harness.Ally.BaseStats.HPMax=100000;harness.Ally.Vitals.HP=100000;
            foreach(var profile in BossCatalog.All)
            {
                var prefab=LargeBossBehavior.Prefab(profile.Family);Assert.That(prefab,Is.Not.Null,profile.Family);
                Assert.That(prefab.FootPrint,Is.EqualTo(FootPrint.Size3x3));Assert.That(prefab.IsBoss,Is.True);
                var enemy=Object.Instantiate(prefab,game.transform);enemy.UpdateCachedStats();enemy.InitialzeVitalsFromStats();enemy.SetPosition(center);game.Enemies.Add(enemy);
                game.Encounter.Register(enemy,"boss-"+profile.Family);
                yield return null;
                var boss=enemy.GetComponent<LargeBossBehavior>();
                Resolve(enemy,boss.ChooseAction());Assert.That(boss.IsTelegraphing,Is.True,profile.Family);Assert.That(boss.MarkedTiles.Count,Is.GreaterThan(0));
                Resolve(enemy,boss.ChooseAction());Assert.That(boss.IsTelegraphing,Is.False,profile.Family);
                Resolve(harness.Ally,new TakeDamageAction(harness.Ally,enemy,1000000,false));
                Assert.That(enemy.GetComponent<EncounterActor>().Resolved,Is.True,profile.Family);
                Assert.That(game.Enemies,Has.No.Member(enemy));
                foreach(var effect in harness.Ally.StatusEffects.ToArray()){harness.Ally.RemoveStatusEffect(effect);Object.Destroy(effect.gameObject);}
                foreach(var prop in dungeon.Interactables.OfType<DungeonProp>().ToArray())prop.ApplyDamage(int.MaxValue);
                game.DeadUnits.Remove(enemy);Object.Destroy(enemy.gameObject);yield return null;
                harness.Ally.SetPosition(center+Vector3Int.right*3);
            }
        }
    }
}
#endif
