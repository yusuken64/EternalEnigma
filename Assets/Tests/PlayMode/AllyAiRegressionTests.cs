#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class AllyAiRegressionTests
    {
        private GameTestHarness harness;
        private Game game;
        private TileWorldDungeon dungeon;
        private Ally leader, ally, third;
        private Character enemy, second;
        private bool[,] mask;
        private readonly List<Skill> learned = new();

        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese", "Quinn" } });
            game = harness.Game;
            dungeon = game.CurrentDungeon;
            leader = harness.Ally;
            ally = game.Allies.First(a => a != leader);
            third = game.Allies.Last();
            Vector3Int Free() => DungeonPlacement.OpenCells(dungeon).First(p => !game.AllCharacters.Any(c => c.TilemapPosition == p));
            yield return harness.SpawnEnemy("Enemy_Slime", Free());
            enemy = game.Enemies.Last();
            yield return harness.SpawnEnemy("Enemy_Slime", Free());
            second = game.Enemies.Last();
            foreach (var e in game.Enemies.OfType<Enemy>()) e.Policies.Clear();
            dungeon.enabled = false;
            dungeon.Interactables.Clear();
            Layout((x,y) => x > 0 && y > 0 && x < 20 && y < 20);
            leader.SetPosition(new Vector3Int(3,4));
            ally.SetPosition(new Vector3Int(4,4));
            third.SetPosition(new Vector3Int(3,5));
            enemy.SetPosition(new Vector3Int(6,4));
            second.SetPosition(new Vector3Int(7,4));
            foreach (var actor in game.AllCharacters)
            {
                actor.Skills.Clear();
                actor.BaseStats.HPMax = actor.BaseStats.SPMax = actor.BaseStats.HungerMax = 100;
                actor.BaseStats.Strength = 10;
                actor.BaseStats.HitBonus = 1;
                actor.InvalidateCachedStats();
                actor.Vitals.HP = actor.Vitals.SP = actor.Vitals.Hunger = 100;
                actor.SyncDisplayedStats();
            }
            ally.AllyStrategy = AllyStrategy.HoldPosition;
            ally.Equipment.ClassFilter = null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return harness.Cleanup();
            foreach (var skill in learned) if (skill != null) Object.DestroyImmediate(skill);
            learned.Clear();
        }

        private void Layout(Func<int,int,bool> open)
        {
            mask = new bool[dungeon.dungeonWidth,dungeon.dungeonHeight];
            for (int x=0;x<mask.GetLength(0);x++) for (int y=0;y<mask.GetLength(1);y++) mask[x,y] = open(x,y);
            typeof(TileWorldDungeon).GetField("floorMask", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(dungeon, mask);
            var hallway = new bool[mask.GetLength(0),mask.GetLength(1)];
            var layer = new GridLayer(mask);
            for (int x=0;x<mask.GetLength(0);x++) for (int y=0;y<mask.GetLength(1);y++)
                hallway[x,y] = !GridSight.IsRoom(layer, new GridPoint(x,y));
            typeof(TileWorldDungeon).GetField("_isHallwayCache", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(dungeon, hallway);
            dungeon.DoorsChanged();
        }
        private void NoEnemies() { enemy.Vitals.HP = second.Vitals.HP = 0; }
        private Skill Learn(string name, Character actor = null)
        {
            var skill = Common.Instance.SkillManager.GetSkillInstanceByName(name);
            Assert.That(skill, Is.Not.Null, name);
            learned.Add(skill);
            (actor ?? ally).Skills.Add(skill);
            return skill;
        }
        private AllySkillChoice Choice() => new AllySkillPolicy(game, ally, 0).Decide();
        private void Resolve(GameAction action, Character actor = null)
        {
            actor ??= ally;
            Assert.That(action.IsValid(actor), Is.True, action.GetType().Name);
            var queue = new Queue<GameAction>();
            queue.Enqueue(action);
            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                if (!next.IsValid(actor)) continue; // Generated effects can deliberately exclude individual recipients.
                foreach (var effect in actor.ExecuteActionImmediate(next)) queue.Enqueue(effect);
                next.UpdateDisplayedStats();
            }
        }
        private AllySkillChoice Cast()
        {
            var choice = Choice();
            Assert.That(choice, Is.Not.Null);
            int sp = ally.Vitals.SP;
            Resolve(choice.ToAction(ally));
            while (ally.PendingCast != null)
            {
                ally.DetermineAction();
                Assert.That(ally.determinedActions.Single(), Is.TypeOf<AdvanceCastAction>());
                Resolve(ally.determinedActions.Single());
            }
            Assert.That(ally.Vitals.SP, Is.EqualTo(sp - choice.Option.Skill.SPCost));
            return choice;
        }
        private GameAction Decide() { ally.DetermineAction(); return ally.determinedActions.Single(); }
        private void Bow(int arrows)
        {
            ally.Equipment.Equip((EquipableInventoryItem)Common.Instance.ItemManager.GetAsInventoryItemByName("Bow"));
            if (arrows > 0) ally.Equipment.Equip((EquipableInventoryItem)Common.Instance.ItemManager.GetAsInventoryItemByName("Wooden Arrows", arrows));
        }
        private static Vector3Int Destination(GameAction action) => ((MovementAction)action).newMapPosition;

        [Test] public void LastSeenPursuitDoesNotTrackHiddenLeaderAndArrivalStartsWandering()
        {
            NoEnemies();
            ally.AllyStrategy = AllyStrategy.Follow;
            leader.SetPosition(new Vector3Int(8,4));
            ally.Awareness.Refresh(game);
            var remembered = leader.TilemapPosition;
            leader.SetPosition(new Vector3Int(18,18));
            var first = Decide();
            Assert.That(first, Is.TypeOf<MovementAction>());
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(remembered));
            Assert.That(ally.PursuitPosition, Is.EqualTo(remembered));
            var next = Destination(first);
            leader.SetPosition(new Vector3Int(17,18));
            game.PartyVisibleTiles.Add(leader.TilemapPosition);
            Assert.That(Destination(Decide()), Is.EqualTo(next), "Unseen movement and shared sight cannot redirect the ally.");
            for (int i=0;i<10 && ally.TilemapPosition != remembered;i++) Resolve(Decide());
            Assert.That(ally.TilemapPosition, Is.EqualTo(remembered));
            Assert.That(Decide(), Is.TypeOf<MovementAction>());
            Assert.That(ally.Awareness.SearchedLastSeen, Is.True);
            Assert.That(ally.PursuitPosition, Is.Null);
            leader.SetPosition(ally.TilemapPosition + Vector3Int.up);
            Assert.That(Decide(), Is.TypeOf<WaitAction>());
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(leader.TilemapPosition));
            Assert.That(ally.Awareness.SearchedLastSeen, Is.False);
        }

        [Test] public void NoMemoryWanderingIsSeededAvoidsBacktrackingAndWaitsInDeadEnds()
        {
            NoEnemies();
            third.SetPosition(new Vector3Int(18,17));
            leader.SetPosition(new Vector3Int(18,18));
            ally.AllyStrategy = AllyStrategy.Follow;
            ally.SetFacing(Facing.Right);
            Assert.That(Destination(Decide()), Is.EqualTo(new Vector3Int(5,4)), "Continue forward.");
            Resolve(Decide());
            Assert.That(ally.Awareness.LastSeenLeader, Is.Null);
            Layout((x,y) => y == 4 && x >= 4 && x <= 5 || x >= 17 && y >= 17 && x < 20 && y < 20);
            var back = Decide();
            Assert.That(Destination(back), Is.EqualTo(new Vector3Int(4,4)), "Backtracking is legal at a dead end.");
            Resolve(back);
            Layout((x,y) => x == 4 && y == 4 || x >= 17 && y >= 17 && x < 20 && y < 20);
            Assert.That(Decide(), Is.TypeOf<WaitAction>());
            Assert.That(Decide(), Is.TypeOf<WaitAction>(), "A trapped search retries without moving.");
            Layout((x,y) => x > 0 && y > 0 && x < 20 && y < 20 && !(x == 5 && y == 4));
            ally.SetFacing(Facing.Right);
            UnityEngine.Random.InitState(712);
            var state = UnityEngine.Random.state;
            var a = Destination(Decide());
            ally.SetFacing(Facing.Right);
            UnityEngine.Random.state = state;
            Assert.That(Destination(Decide()), Is.EqualTo(a));
            Assert.That(a, Is.Not.EqualTo(new Vector3Int(3,4)), "Do not immediately reverse when another step exists.");
        }

        [Test] public void UnreachableMemoryStartsWanderingWithoutInspectingTheHiddenLeader()
        {
            NoEnemies();
            ally.AllyStrategy = AllyStrategy.Follow;
            leader.SetPosition(new Vector3Int(8,4));
            ally.Awareness.Refresh(game);
            leader.SetPosition(new Vector3Int(18,18));
            Layout((x,y) => x > 0 && y > 0 && x < 20 && y < 20 && x != 6);
            Assert.That(Decide(), Is.TypeOf<MovementAction>());
            Assert.That(ally.Awareness.SearchedLastSeen, Is.True);
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(new Vector3Int(8,4)));
        }

        [Test] public void PersonalSightHandlesNearWallsFarRoomsCorridorsAndDoors()
        {
            NoEnemies();
            enemy.SetPosition(new Vector3Int(18,18));
            second.SetPosition(new Vector3Int(17,18));
            ally.AllyStrategy = AllyStrategy.Follow;
            leader.SetPosition(new Vector3Int(11,4));
            Assert.That(dungeon.CanSee(ally, leader), Is.True);
            Assert.That(Decide(), Is.TypeOf<MovementAction>(), "A visible leader beyond five cells is followed.");
            leader.SetPosition(new Vector3Int(6,4));
            Layout((x,y) => x > 0 && y > 0 && x < 20 && y < 20 && x != 5);
            Assert.That(dungeon.CanSee(ally, leader), Is.False, "Two cells away behind a wall is unseen.");
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(new Vector3Int(11,4)));
            Decide();
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(new Vector3Int(11,4)));
            Layout((x,y) => y == 4 && x >= 2 && x <= 12 || x >= 2 && x <= 4 && y >= 3 && y <= 5);
            ally.SetPosition(new Vector3Int(5,4));
            leader.SetPosition(new Vector3Int(8,4));
            ally.Awareness.Refresh(game);
            Assert.That(ally.Awareness.LeaderVisible, Is.False, "Corridor sight is local.");
            ally.SetPosition(new Vector3Int(4,4));
            ally.Awareness.Refresh(game);
            Assert.That(ally.Awareness.LeaderVisible, Is.True, "Room sight reaches along the corridor.");
            var door = DungeonProp.Create(dungeon, new DungeonScenery(new GridPoint(6,4), DungeonSceneryKind.Door, 1, 42, SceneryReward.None), null);
            dungeon.DoorsChanged();
            ally.Awareness.Refresh(game);
            Assert.That(ally.Awareness.LeaderVisible, Is.False);
            door.Unlock();
            Assert.That(Decide(), Is.TypeOf<MovementAction>());
            Assert.That(ally.Awareness.LeaderVisible, Is.True, "Opening the door immediately reacquires the leader.");
        }

        [Test] public void FollowSearchesAggressiveEngagesHoldWaitsAndModeChangesClearPursuit()
        {
            leader.SetPosition(new Vector3Int(18,18));
            second.Vitals.HP = 0;
            enemy.SetPosition(new Vector3Int(5,4));
            ally.AllyStrategy = AllyStrategy.Follow;
            Assert.That(Decide(), Is.TypeOf<MovementAction>());
            ally.AllyStrategy = AllyStrategy.Aggresive;
            Assert.That(Decide(), Is.TypeOf<AttackAction>());
            Assert.That(ally.PursuitTarget, Is.SameAs(enemy));
            ally.AllyStrategy = AllyStrategy.HoldPosition;
            enemy.SetPosition(new Vector3Int(6,4));
            Assert.That(Decide(), Is.TypeOf<WaitAction>());
            Assert.That(ally.PursuitTarget, Is.Null);
            Assert.That(ally.PursuitPosition, Is.Null);
            leader.SetPosition(new Vector3Int(3,4));
            Assert.That(Decide(), Is.TypeOf<WaitAction>());
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(leader.TilemapPosition), "Hold still observes.");
            leader.SetPosition(new Vector3Int(18,18));
            ally.AllyStrategy = AllyStrategy.Follow;
            Assert.That(Decide(), Is.TypeOf<MovementAction>());
            Assert.That(ally.PursuitPosition, Is.EqualTo(new Vector3Int(3,4)));
        }

        [Test] public void LostFollowStillHealsAndRevivesOnlyVisibleRecipientsInRange()
        {
            leader.SetPosition(new Vector3Int(18,18));
            ally.AllyStrategy = AllyStrategy.Follow;
            var heal = Learn("Heal");
            third.Vitals.HP = 10;
            var choice = Cast();
            Assert.That(choice.Evaluator, Is.EqualTo("EmergencyHeal"));
            Assert.That(third.Vitals.HP, Is.GreaterThan(10));
            third.Vitals.HP = 0;
            PartyRules.MarkDowned(game, third);
            var revive = Learn("Revive");
            choice = Cast();
            Assert.That(choice.Evaluator, Is.EqualTo("Revive"));
            Assert.That(third.IsDowned, Is.False);
            third.Vitals.HP = 0;
            PartyRules.MarkDowned(game, third);
            third.SetPosition(new Vector3Int(8,4));
            Assert.That(Choice(), Is.Null, "Adjacent revival cannot target a distant visible ally.");
            third.SetPosition(new Vector3Int(18,17));
            Learn("Mass Revive");
            Assert.That(Choice(), Is.Null, "No revival through shared party sight.");
        }

        [Test] public void BlockedRoutesDoNotSwapPushOrStepOnCompanions()
        {
            NoEnemies();
            Layout((x,y) => y == 4 && x >= 2 && x <= 10);
            ally.SetPosition(new Vector3Int(4,4));
            leader.SetPosition(new Vector3Int(5,4));
            third.SetPosition(new Vector3Int(3,4));
            ally.AllyStrategy = AllyStrategy.Follow;
            var before = leader.TilemapPosition;
            Assert.That(Decide(), Is.TypeOf<WaitAction>());
            Assert.That(AutoplayRunner.FollowerTravelAction(game, ally, leader), Is.TypeOf<WaitAction>());
            leader.SetPosition(new Vector3Int(10,4));
            var step = Decide();
            Assert.That(step, Is.TypeOf<MovementAction>());
            Assert.That(Destination(step), Is.EqualTo(before), "Travel to the remembered cell, never the live leader.");
            third.SetPosition(before);
            Layout((x,y) => y == 4 && x >= 4 && x <= 10);
            Assert.That(Decide(), Is.TypeOf<WaitAction>(), "Both corridor exits are occupied or remembered cell is unreachable.");
            Assert.That(third.TilemapPosition, Is.EqualTo(before));
        }

        [Test] public void SearchAvoidsKnownHazardsAndOccupiedForwardCells()
        {
            NoEnemies();
            leader.SetPosition(new Vector3Int(18,18));
            ally.AllyStrategy = AllyStrategy.Follow;
            ally.SetFacing(Facing.Right);
            DungeonProp.Create(dungeon, new DungeonScenery(new GridPoint(5,4), DungeonSceneryKind.Hazard, 0, 42, SceneryReward.None), null);
            var move = Decide();
            Assert.That(dungeon.IsHazard(Destination(move)), Is.False);
            third.SetPosition(Destination(move));
            ally.SetFacingByTargetPosition(third.TilemapPosition);
            Assert.That(Destination(Decide()), Is.Not.EqualTo(third.TilemapPosition));
        }

        [Test] public void EnemyLossDeathNeutralityAndDominationInvalidateCachedAttacks()
        {
            Bow(8);
            second.Vitals.HP = 0;
            var policy = new AllyAttackPolicy(game, ally, 0);
            Assert.That(policy.ShouldRun(), Is.True);
            enemy.Team = Team.Player;
            Assert.That(policy.ShouldRun(), Is.False);
            Assert.That(AllySkillContext.Build(ally, game).VisibleEnemies, Is.Empty);
            enemy.Team = Team.Neutral;
            Assert.That(policy.ShouldRun(), Is.False);
            enemy.Team = Team.Enemy;
            enemy.Vitals.HP = 0;
            Assert.That(policy.ShouldRun(), Is.False);
            enemy.Vitals.HP = 100;
            enemy.SetPosition(new Vector3Int(18,18));
            ally.PursuitTarget = enemy;
            ally.PursuitPosition = enemy.TilemapPosition;
            Assert.That(policy.ShouldRun(), Is.False);
            Assert.That(ally.PursuitTarget, Is.Null);
            Assert.That(policy.GetActions().Single(), Is.TypeOf<WaitAction>());
            Assert.That(ArrowSupply.Count(ally), Is.EqualTo(8));
        }

        [Test] public void RangedPositioningUsesValidLeastCostShotsAndTracksVisibleMovement()
        {
            Bow(8);
            second.Vitals.HP = 0;
            ally.AllyStrategy = AllyStrategy.Aggresive;
            enemy.SetPosition(new Vector3Int(7,5)); // Manhattan distance four; not a firing line.
            var move = Decide();
            Assert.That(move, Is.TypeOf<MovementAction>());
            Assert.That((Destination(move)-ally.TilemapPosition).magnitude, Is.EqualTo(1), "Choose a one-cost shot position.");
            Resolve(move);
            var shot = Decide();
            Assert.That(shot, Is.TypeOf<RangedAttackAction>());
            int hp = enemy.Vitals.HP;
            Resolve(shot);
            Assert.That(enemy.Vitals.HP, Is.LessThan(hp));
            Assert.That(ArrowSupply.Count(ally), Is.EqualTo(7));
            enemy.SetPosition(new Vector3Int(8,7));
            Assert.That(Decide(), Is.TypeOf<MovementAction>(), "Re-evaluate the moving target.");
            enemy.SetPosition(new Vector3Int(18,18));
            Decide();
            Assert.That(ally.PursuitTarget, Is.Null, "Losing personal sight ends enemy pursuit.");
        }

        [Test] public void AttackAvailabilityAndDamageScoringAgreeForBlockedShotsAndEmptyAmmo()
        {
            Bow(0);
            second.Vitals.HP = 0;
            var blast = Learn("Fire Bolt");
            Assert.That(AllySkillContext.Build(ally,game).NormalAttackValue, Is.Zero);
            int hp = enemy.Vitals.HP;
            Cast();
            Assert.That(enemy.Vitals.HP, Is.LessThan(hp));
            Bow(8);
            enemy.SetPosition(new Vector3Int(7,5));
            Assert.That(AllySkillContext.Build(ally,game).NormalAttackValue, Is.Zero, "An off-axis enemy is not an available bow attack.");
            ally.ApplyStatusEffect(StatusEffectRegistry.GetByName("Arm Bind"));
            enemy.SetPosition(new Vector3Int(6,4));
            Assert.That(AllySkillContext.Build(ally,game).NormalAttackValue, Is.Zero);
        }

        [Test] public void AuthoredChanceStatusesUseProbabilityExistingEffectsAndImmunity()
        {
            second.Vitals.HP = 0;
            var hex = Learn("Slumber Hex");
            var effect = hex.ActionEffects.OfType<ApplyStatusChanceAction>().Single();
            var choice = Choice();
            Assert.That(choice.Evaluator, Is.EqualTo("CrowdControl"));
            Assert.That(choice.Score, Is.EqualTo(CrowdControlEvaluator.Danger(enemy) * effect.Probability(ally,hex.RankContext)).Within(.001));
            for (int seed=0;;seed++)
            {
                UnityEngine.Random.InitState(seed);
                if (UnityEngine.Random.value < effect.Probability(ally,hex.RankContext)) { UnityEngine.Random.InitState(seed); break; }
            }
            Cast();
            Assert.That(enemy.StatusEffects.Any(s => s is SleepStatusEffect && !s.IsExpired()), Is.True);
            Assert.That(Choice(), Is.Null, "An existing status contributes no benefit.");
            foreach (var status in enemy.StatusEffects.ToArray()) enemy.RemoveStatusEffect(status);
            ally.Skills.Clear();
            Bow(8);
            var pin = Learn("Pinning Shot");
            Learn("Escape Artist", enemy);
            var bind = pin.ActionEffects.OfType<ApplyStatusChanceAction>().Single().StatusEffect;
            Assert.That(ClassPassives.IsImmune(enemy,bind), Is.True);
            Assert.That(new CrowdControlEvaluator().Evaluate(AllySkillContext.Build(ally,game)), Is.Null);
        }

        [Test] public void AuthoredBuffsOnCasterAndRecipientsApplyAndDoNotRefreshEarly()
        {
            second.Vitals.HP = 0;
            enemy.SetPosition(new Vector3Int(5,4));
            var stance = Learn("Parry Stance");
            Assert.That(SkillIntents.Classify(stance).Has(SkillIntent.Buff), Is.True);
            Cast();
            Assert.That(ally.StatusEffects.Any(s => s is ParryStatusEffect), Is.True);
            Assert.That(enemy.StatusEffects.Any(s => s is TauntStatusEffect), Is.True);
            Assert.That(Choice(), Is.Null);
            ally.Skills.Clear();
            var amplify = Learn("Amplify");
            Cast();
            Assert.That(ally.StatusEffects.Any(s => s is AmplifyStatusEffect), Is.True);
            Assert.That(Choice(), Is.Null);
        }

        [Test] public void AuthoredSpRestorationRequiresEffectiveRecipientsAndPaysItsCost()
        {
            NoEnemies();
            var inspire = Learn("Inspire");
            Assert.That(Choice(), Is.Null);
            third.Vitals.SP = 80;
            int sp = ally.Vitals.SP;
            var choice = Cast();
            Assert.That(choice.Option.Target, Is.SameAs(third));
            Assert.That(third.Vitals.SP, Is.EqualTo(82));
            Assert.That(ally.Vitals.SP, Is.EqualTo(sp-inspire.SPCost));
            ally.Skills.Clear();
            Learn("Rousing Chorus");
            leader.Vitals.SP = third.Vitals.SP = 80;
            Cast();
            Assert.That(leader.Vitals.SP, Is.EqualTo(82));
            Assert.That(third.Vitals.SP, Is.EqualTo(82));
        }

        [Test] public void AuthoredEnemyBuffRemovalAndAilmentSpreadScoreActualEffects()
        {
            var discord = Learn("Discord");
            Assert.That(Choice(), Is.Null);
            enemy.ApplyStatusEffect(StatusEffectRegistry.GetByName("Strength"));
            Cast();
            Assert.That(enemy.StatusEffects.Any(s => s != null && !s.IsExpired() && StatusCategories.IsBuff(s)), Is.False);
            Assert.That(Choice(), Is.Null);
            ally.Skills.Clear();
            var plague = Learn("Plague");
            enemy.ApplyStatusEffect(StatusEffectRegistry.GetByName("Dot"));
            var choice = Cast();
            Assert.That(choice.Option.Target, Is.SameAs(enemy));
            Assert.That(second.StatusEffects.Any(s => s is DotStatusEffect && !s.IsExpired()), Is.True);
            Assert.That(Choice(), Is.Null);
        }

        [Test] public void LearnedRecoveryReservesSurviveNoTargetsAndInsufficientSp()
        {
            var revive = Learn("Revive");
            var blast = Learn("Fire Bolt");
            ally.Vitals.SP = 2;
            foreach (var strategy in new[] { AllyStrategy.Follow, AllyStrategy.HoldPosition })
            {
                ally.AllyStrategy = strategy;
                var context = AllySkillContext.Build(ally,game);
                Assert.That(context.Castable.Contains(revive), Is.False);
                Assert.That(context.SpReserve, Is.EqualTo(revive.SPCost));
                Assert.That(Choice(), Is.Null);
            }
            ally.AllyStrategy = AllyStrategy.Aggresive;
            Assert.That(AllySkillContext.Build(ally,game).SpReserve, Is.EqualTo(revive.SPCost/2));
            Assert.That(Choice(), Is.Not.Null);
            Cast();
            Assert.That(ally.Vitals.SP, Is.EqualTo(2-blast.SPCost));
        }

        [Test] public void CommandsRefreshOnlyRecipientsTheyCanReach()
        {
            Learn("Command: Attack");
            third.SetPosition(new Vector3Int(18,18));
            Cast();
            Assert.That(leader.StatusEffects.OfType<CommandStatusEffect>().Any(), Is.True);
            Assert.That(ally.StatusEffects.OfType<CommandStatusEffect>().Any(), Is.True);
            Assert.That(third.StatusEffects.OfType<CommandStatusEffect>(), Is.Empty);
            Assert.That(Choice(), Is.Null, "The unseen unbuffed ally must not trigger recasting.");
            third.gameObject.AddComponent<SummonedUnit>();
            third.SetPosition(new Vector3Int(3,5));
            Assert.That(AllySkillContext.Build(ally,game).Party.Contains(third), Is.False);
            Cast();
            Assert.That(third.StatusEffects.OfType<CommandStatusEffect>().Any(), Is.True, "Commands also reach visible summons.");
        }

        [Test] public void FieldKitchenRequiresOneFullRankScaledDeficitAndConsumesOneFood()
        {
            NoEnemies();
            harness.AddItem("Bread");
            var kitchen = Learn("Field Kitchen");
            kitchen.Rank = 3;
            int amount = kitchen.RankContext.Scaling.ScalePower(kitchen.ActionEffects.OfType<FieldKitchenAction>().Single().HungerRestore,kitchen.Rank);
            int Foods() => game.PlayerController.Inventory.InventoryItems.Where(i=>i.ItemName=="Bread").Sum(i=>i.HasStacks?(i.StackStock ?? 0):1);
            int food = Foods();
            Assert.That(Choice(), Is.Null);
            leader.Vitals.Hunger = 100-amount+1;
            Assert.That(Choice(), Is.Null);
            leader.Vitals.Hunger--;
            Cast();
            Assert.That(leader.Vitals.Hunger, Is.EqualTo(100));
            Assert.That(Foods(), Is.EqualTo(food-1));
            Assert.That(Choice(), Is.Null);
        }

        [Test] public void LeaderReplacementAndFloorIdentityClearMemoryButHoldDoesNot()
        {
            NoEnemies();
            ally.Awareness.Refresh(game);
            var old = ally.Awareness.LastSeenLeader;
            leader.SetPosition(new Vector3Int(18,18));
            ally.AllyStrategy = AllyStrategy.Follow;
            Decide();
            ally.AllyStrategy = AllyStrategy.HoldPosition;
            Decide();
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(old));
            third.SetPosition(new Vector3Int(18,17));
            game.PlayerController.TakeControl(third);
            ally.Awareness.Refresh(game);
            Assert.That(ally.Awareness.LastSeenLeader, Is.Null);
            Assert.That(ally.PursuitPosition, Is.Null);
            third.SetPosition(new Vector3Int(3,5));
            ally.Awareness.Refresh(game);
            third.SetPosition(new Vector3Int(18,17));
            typeof(TileWorldDungeon).GetField("runtimeFloor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(dungeon,
                DungeonFloorGenerator.Generate(new DungeonFloorOptions(991,dungeon.dungeonWidth,dungeon.dungeonHeight)));
            ally.Awareness.Refresh(game);
            Assert.That(ally.Awareness.LastSeenLeader, Is.Null);
        }

        [Test] public void PendingCastsExplicitOrdersAndForcedStatusesRemainAuthoritative()
        {
            NoEnemies();
            var heal = Learn("Heal");
            third.Vitals.HP = 10;
            Resolve(Choice().ToAction(ally));
            ally.AllyStrategy = AllyStrategy.Follow;
            leader.SetPosition(new Vector3Int(18,18));
            Assert.That(Decide(), Is.TypeOf<AdvanceCastAction>());
            Resolve(ally.determinedActions.Single());
            Assert.That(third.Vitals.HP, Is.GreaterThan(10));
            ally.AllyStrategy = AllyStrategy.HoldPosition;
            ally._forcedAction = new MovementAction(ally,ally.TilemapPosition,ally.TilemapPosition+Vector3Int.right);
            Assert.That(Decide(), Is.TypeOf<MovementAction>(), "Explicit commands override Hold.");
            Resolve(ally.determinedActions.Single());
            ally.ApplyStatusEffect(StatusEffectRegistry.GetByName("Sleep"));
            Assert.That(Decide(), Is.TypeOf<SleepTurnAction>());
            foreach (var effect in ally.StatusEffects.ToArray()) ally.RemoveStatusEffect(effect);
            ally.ApplyStatusEffect(StatusEffectRegistry.GetByName("Stuck"));
            ally.AllyStrategy = AllyStrategy.Follow;
            third.Vitals.HP = third.FinalStats.HPMax;
            Assert.That(Decide(), Is.TypeOf<WaitAction>());
        }

        [Test] public void AutoplayCompanionUsesTheSameSightMemoryAndCombatModes()
        {
            NoEnemies();
            ally.AllyStrategy = AllyStrategy.Follow;
            ally.Awareness.Refresh(game);
            leader.SetPosition(new Vector3Int(18,18));
            var normal = Decide();
            var autoplay = ally.ChooseAutonomousActions(includeControlled:true).Single();
            Assert.That(Destination(autoplay), Is.EqualTo(Destination(normal)));
            Assert.That(ally.Awareness.LastSeenLeader, Is.EqualTo(new Vector3Int(3,4)));
            enemy.Vitals.HP = 100;
            enemy.SetPosition(new Vector3Int(5,4));
            ally.AllyStrategy = AllyStrategy.Aggresive;
            Assert.That(ally.ChooseAutonomousActions(true).Single(), Is.TypeOf<AttackAction>());
            ally.AllyStrategy = AllyStrategy.Follow;
            Assert.That(ally.ChooseAutonomousActions(true).Single(), Is.TypeOf<MovementAction>());
            ally.AllyStrategy = AllyStrategy.HoldPosition;
            enemy.SetPosition(new Vector3Int(6,4));
            Assert.That(ally.ChooseAutonomousActions(true).Single(), Is.TypeOf<WaitAction>());
        }

        [Test] public void FollowAndHoldFightFromTheirTileAndAggressiveCanUseAuthoredMovement()
        {
            second.Vitals.HP = 0;
            leader.SetPosition(new Vector3Int(3,4));
            var lunge = Learn("Lunge");
            foreach (var mode in new[] { AllyStrategy.Follow, AllyStrategy.HoldPosition })
            {
                ally.AllyStrategy = mode;
                Assert.That(Decide(), Is.TypeOf<WaitAction>());
                Assert.That(ally.TilemapPosition, Is.EqualTo(new Vector3Int(4,4)));
            }
            ally.AllyStrategy = AllyStrategy.Aggresive;
            int hp = enemy.Vitals.HP;
            Cast();
            Assert.That(ally.TilemapPosition, Is.Not.EqualTo(new Vector3Int(4,4)));
            Assert.That(enemy.Vitals.HP, Is.LessThan(hp));
        }

        [Test] public void ReachableTargetsWinThenRouteCostRetentionAndStableOrdering()
        {
            ally.AllyStrategy = AllyStrategy.Aggresive;
            enemy.SetPosition(new Vector3Int(6,4));
            second.SetPosition(new Vector3Int(4,7));
            third.SetPosition(new Vector3Int(18,17));
            leader.SetPosition(new Vector3Int(18,18));
            // Visible containers block every attack cell around the nearer enemy, but do not block sight.
            foreach (var offset in SkillCastOptions.Directions)
                DungeonProp.Create(dungeon, new DungeonScenery((enemy.TilemapPosition+offset).ToGridPoint(),
                    DungeonSceneryKind.Container, 1, 42, SceneryReward.None), null);
            Assert.That(Decide(), Is.TypeOf<MovementAction>());
            Assert.That(ally.PursuitTarget, Is.SameAs(second));
            dungeon.Interactables.Clear();
            enemy.SetPosition(new Vector3Int(7,4));
            second.SetPosition(new Vector3Int(4,7));
            ally.PursuitTarget = enemy;
            Decide();
            Assert.That(ally.PursuitTarget, Is.SameAs(enemy), "Retain the current target on equal route cost.");
            ally.PursuitTarget = null;
            Decide();
            Assert.That(ally.PursuitTarget, Is.SameAs(enemy), "Stable tile order breaks equal-cost ties.");
            second.SetPosition(new Vector3Int(4,6));
            Decide();
            Assert.That(ally.PursuitTarget, Is.SameAs(second), "A cheaper route outranks retention.");
            var behavior = enemy.GetComponent<EnemyBehavior>() ?? enemy.gameObject.AddComponent<EnemyBehavior>();
            behavior.Mimic = true;
            Assert.That(AllyCombat.VisibleHostiles(game,ally).Contains(enemy), Is.False);
        }

        [UnityTest] public IEnumerator RealFloorTransitionClearsOldLeaderMemoryAndPendingCast()
        {
            ally.Awareness.Refresh(game);
            var oldFloor = dungeon;
            Learn("Heal");
            third.Vitals.HP = 10;
            Resolve(Choice().ToAction(ally));
            Assert.That(ally.PendingCast, Is.Not.Null);
            game.AdvanceFloor();
            yield return null;
            yield return harness.WaitForIdle();
            Assert.That(game.CurrentDungeon, Is.Not.SameAs(oldFloor));
            Assert.That(ally.PendingCast, Is.Null);
            var remote = DungeonPlacement.OpenCells(game.CurrentDungeon).First(p =>
                !game.CurrentDungeon.GetVisibleTiles(ally,ally.TilemapPosition).Contains(p) &&
                !game.AllCharacters.Any(c => c.TilemapPosition == p));
            leader.SetPosition(remote);
            ally.Awareness.Refresh(game);
            Assert.That(ally.Awareness.LastSeenLeader, Is.Null);
            Assert.That(ally.PursuitTarget, Is.Null);
        }
    }
}
#endif
