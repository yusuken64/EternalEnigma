using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class CastingAndPiercingTests
    {
        GameTestHarness harness;
        Ally caster, friend;
        readonly List<Skill> skills = new();
        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese" } });
            caster = harness.Ally; friend = harness.Game.Allies.First(a => a != caster);
            var d = harness.Game.CurrentDungeon;
            var cell = Enumerable.Range(0,d.dungeonWidth).SelectMany(x => Enumerable.Range(0,d.dungeonHeight).Select(y=>new Vector3Int(x,y)))
                .First(p=>Enumerable.Range(-1,3).All(x=>Enumerable.Range(-1,3).All(y=>d.IsWalkable(p+new Vector3Int(x,y)))));
            caster.SetPosition(cell); friend.SetPosition(cell+Vector3Int.left);
            foreach(var c in harness.Game.AllCharacters)
            { c.BaseStats.HPMax=100; c.BaseStats.SPMax=100; c.InvalidateCachedStats(); c.Vitals.HP=50; c.Vitals.SP=100; c.SyncDisplayedStats(); }
            caster.AllyStrategy = friend.AllyStrategy = AllyStrategy.HoldPosition;
            caster.Equipment.ClassFilter = null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return harness.Cleanup();
            foreach(var s in skills) Object.DestroyImmediate(s);
            skills.Clear();
        }
        Skill Learn(string name, Character c = null)
        {
            var s = Common.Instance.SkillManager.GetSkillInstanceByName(name);
            Assert.That(s,Is.Not.Null,name); skills.Add(s); (c??caster).Skills.Add(s); return s;
        }
        void Resolve(GameAction action, Character actor = null)
        {
            actor ??= caster;
            var queue = new Queue<GameAction>(); queue.Enqueue(action);
            while(queue.Count>0) { var next=queue.Dequeue(); foreach(var child in next.ExecuteImmediate(actor)) queue.Enqueue(child); next.UpdateDisplayedStats(); }
        }
        void Bow(string ammo, int stock)
        {
            caster.Equipment.Equip((EquipableInventoryItem)Common.Instance.ItemManager.GetAsInventoryItemByName("Bow"));
            if(ammo != null) caster.Equipment.Equip((EquipableInventoryItem)Common.Instance.ItemManager.GetAsInventoryItemByName(ammo,stock));
        }
        [Test] public void CastTimeCountsChargingThenSeparateReleaseAndPaysOnce()
        {
            var heal=Learn("Heal"); heal.CastTime=2;
            int hp=friend.Vitals.HP,sp=caster.Vitals.SP;
            Resolve(new SkillAction(caster,heal,friend));
            Assert.That(caster.PendingCast.Remaining,Is.EqualTo(1)); Assert.That(friend.Vitals.HP,Is.EqualTo(hp));
            Assert.That(caster.Vitals.SP,Is.EqualTo(sp-heal.SPCost));
            Resolve(new AdvanceCastAction()); Assert.That(caster.PendingCast.Remaining,Is.Zero); Assert.That(friend.Vitals.HP,Is.EqualTo(hp));
            Resolve(new AdvanceCastAction()); Assert.That(caster.PendingCast,Is.Null); Assert.That(friend.Vitals.HP,Is.GreaterThan(hp));
            Assert.That(caster.Vitals.SP,Is.EqualTo(sp-heal.SPCost));
        }
        [Test] public void LockedCharacterFollowsMovementAndRangeChanges()
        {
            var heal=Learn("Heal"); Resolve(new SkillAction(caster,heal,friend));
            friend.TilemapPosition += Vector3Int.right*30;
            Resolve(new AdvanceCastAction()); Assert.That(friend.Vitals.HP,Is.GreaterThan(50));
        }
        [Test] public void InvalidLockedTargetFizzlesWithoutRefund()
        {
            var heal=Learn("Heal"); int sp=caster.Vitals.SP;
            Resolve(new SkillAction(caster,heal,friend)); friend.Vitals.HP=0; PartyRules.MarkDowned(harness.Game,friend);
            Resolve(new AdvanceCastAction()); Assert.That(friend.Vitals.HP,Is.Zero); Assert.That(caster.PendingCast,Is.Null);
            Assert.That(caster.Vitals.SP,Is.EqualTo(sp-heal.SPCost));
        }
        [Test] public void ForcedMovementCancelsAndQuickCastingCanBeInstant()
        {
            var heal=Learn("Heal"); Resolve(new SkillAction(caster,heal,friend));
            caster.TilemapPosition += Vector3Int.up; Assert.That(caster.PendingCast,Is.Null);
            Learn("Quick Casting"); Resolve(new SkillAction(caster,heal,friend));
            Assert.That(caster.PendingCast,Is.Null); Assert.That(friend.Vitals.HP,Is.GreaterThan(50));
        }
        [Test] public void SupportOnlyReducesChargeAndNeverReleases()
        {
            var heal=Learn("Full Heal",friend); Resolve(new SkillAction(friend,heal,friend),friend);
            var dance=Learn("Casting Dance"); Resolve(new SkillAction(caster,dance,friend));
            Assert.That(friend.PendingCast.Remaining,Is.EqualTo(1)); Resolve(new SkillAction(caster,dance,friend));
            Assert.That(friend.PendingCast.Remaining,Is.Zero); Assert.That(friend.Vitals.HP,Is.EqualTo(50));
            Assert.That(caster.CanCast(dance,out _),Is.False);
        }
        [Test] public void EmptyBowRejectsCommandEvenWithBagArrowsWithoutResourcesOrTurn()
        {
            Bow(null,0);
            if(caster.Equipment.EquippedShield?.ItemDefinition != null) caster.Equipment.UnEquip(caster.Equipment.EquippedShield);
            harness.Game.PlayerController.Inventory.Add(Common.Instance.ItemManager.GetAsInventoryItemByName("Wooden Arrows",20));
            var vitals=JsonUtility.ToJson(caster.Vitals);
            var action=new RangedAttackAction(caster,null,10,null);
            Assert.That(action.ValidateCommand(caster,out var reason),Is.False); Assert.That(reason,Is.EqualTo("no arrows"));
            Assert.That(harness.Game.TurnManager.SubmitCommand(caster,action),Is.False);
            Assert.That(JsonUtility.ToJson(caster.Vitals),Is.EqualTo(vitals)); Assert.That(harness.Game.TurnManager.IsProcessingTurn,Is.False);
            Assert.That(caster.IsWaitingForPlayerInput,Is.True);
        }
        [Test] public void EquippedStacksAreIndependentAndDepleteWithoutBagRefill()
        {
            Bow("Steel Arrows",1); var last=caster.Equipment.EquippedShield;
            harness.Game.PlayerController.Inventory.Add(Common.Instance.ItemManager.GetAsInventoryItemByName("Wooden Arrows",20));
            Assert.That(ArrowSupply.DamageMultiplier(caster),Is.EqualTo(1.5f)); Assert.That(ArrowSupply.Count(friend),Is.Zero);
            Assert.That(ArrowSupply.Consume(caster,1),Is.EqualTo(1)); Assert.That(caster.Equipment.EquippedShield,Is.Null);
            Assert.That(harness.Game.PlayerController.Inventory.InventoryItems.Contains(last),Is.False); Assert.That(ArrowSupply.Count(caster),Is.Zero);
        }
        [Test] public void ReleaseWithMissingArrowsPreservesCurrentAction()
        {
            Bow("Wooden Arrows",2); var shot=Learn("Aimed Shot"); shot.CastTime=2;
            Resolve(SkillAction.ForMissile(caster,shot,Vector3Int.right)); Resolve(new AdvanceCastAction());
            ArrowSupply.Consume(caster,2); var state=JsonUtility.ToJson(caster.Vitals);
            Assert.That(harness.Game.TurnManager.SubmitCommand(caster,new WaitAction()),Is.False);
            Assert.That(caster.PendingCast,Is.Null); Assert.That(JsonUtility.ToJson(caster.Vitals),Is.EqualTo(state));
        }
        [UnityTest] public IEnumerator PiercingPassesAlliesAndCountsOrderedEnemies()
        {
            var d=harness.Game.CurrentDungeon;
            var origin=Enumerable.Range(0,d.dungeonWidth).SelectMany(x=>Enumerable.Range(0,d.dungeonHeight).Select(y=>new Vector3Int(x,y)))
                .First(p=>Enumerable.Range(0,5).All(i=>d.IsWalkable(p+Vector3Int.right*i)));
            caster.SetPosition(origin); friend.SetPosition(origin+Vector3Int.right);
            yield return harness.SpawnEnemy("Enemy_Slime",origin+Vector3Int.right*2);
            yield return harness.SpawnEnemy("Enemy_Slime",origin+Vector3Int.right*3);
            var line=MissileTargeting.TraceLine(caster,Vector3Int.right,4,2);
            Assert.That(line.Encounters.Select(h=>h.Character),Is.EqualTo(harness.Game.Enemies.TakeLast(2)));
            Assert.That(line.Endpoint,Is.EqualTo(origin+Vector3Int.right*3));
        }
        [Test] public void DamageDoesNotInterruptButSilenceDoes()
        {
            var heal = Learn("Full Heal"); Resolve(new SkillAction(caster, heal, friend));
            Resolve(new TakeDamageAction(friend, caster, 1)); global::PendingCast.Validate(caster);
            Assert.That(caster.PendingCast, Is.Not.Null);
            caster.ApplyStatusEffect(StatusEffectRegistry.GetByName("Silence"));
            Assert.That(caster.PendingCast, Is.Null);
        }
        [Test] public void ArmBindInterruptsOnlyWeaponSkills()
        {
            var heal = Learn("Heal"); Resolve(new SkillAction(caster, heal, friend));
            caster.ApplyStatusEffect(StatusEffectRegistry.GetByName("Arm Bind"));
            Assert.That(caster.PendingCast, Is.Not.Null);
            global::PendingCast.Cancel(caster, "test reset");
            foreach (var effect in caster.StatusEffects.ToArray()) caster.RemoveStatusEffect(effect);
            var weapon = Learn("Heavy Strike"); caster.PendingCast = new PendingCast(new SkillAction(caster,weapon,friend),weapon.SkillName,1,weapon);
            caster.ApplyStatusEffect(StatusEffectRegistry.GetByName("Arm Bind"));
            Assert.That(caster.PendingCast, Is.Null);
        }
        [UnityTest] public IEnumerator MobileWalkingChargesAndDisplacedSwapParticipantCancels()
        {
            Learn("Mobile Casting"); var heal = Learn("Full Heal"); Resolve(new SkillAction(caster,heal,friend));
            var origin = caster.TilemapPosition;
            yield return harness.ExecuteAction(new MovementAction(caster,origin,origin+Vector3Int.up));
            Assert.That(caster.PendingCast.Remaining,Is.EqualTo(1));
            var other = Learn("Full Heal", friend); Resolve(new SkillAction(friend,other,friend),friend);
            yield return harness.ExecuteAction(new SwapAllyPositionAction(caster,friend));
            Assert.That(caster.PendingCast.Remaining,Is.Zero); Assert.That(friend.PendingCast,Is.Null);
            Assert.That(friend.Vitals.HP,Is.EqualTo(50));
            yield return harness.ExecuteAction(new WaitAction());
            Assert.That(caster.PendingCast,Is.Null); Assert.That(friend.Vitals.HP,Is.GreaterThan(50));
        }
        [Test] public void TileTargetCanBeEmptyAndRetainsOriginalCell()
        {
            var skill = Learn("Fireball"); skill.Targeting = SkillTargeting.Tile; skill.CastTime = 1;
            var cell = caster.TilemapPosition+Vector3Int.up;
            Assert.That(skill.GetTargetTiles(caster),Does.Contain(cell));
            var start = SkillAction.ForTile(caster,skill,cell); Assert.That(start.IsValid(caster),Is.True);
            Resolve(start);
            var release = new AdvanceCastAction().ExecuteImmediate(caster).Single();
            release.ExecuteImmediate(caster);
            Assert.That(release.Visuals.Sequence.Center,Is.EqualTo(CombatEffectPlayer.Body(null,cell)));
        }
        [Test] public void CompetingReviveFizzlesWhenTargetAlreadyStanding()
        {
            friend.Vitals.HP=0; PartyRules.MarkDowned(harness.Game,friend);
            var revive=Learn("Revive"); Resolve(new SkillAction(caster,revive,friend));
            PartyRules.MarkStanding(harness.Game,friend); friend.Vitals.HP=1;
            while(caster.PendingCast!=null) Resolve(new AdvanceCastAction());
            Assert.That(friend.Vitals.HP,Is.EqualTo(1));
            var history=Object.FindFirstObjectByType<GameMessages>().History;
            Assert.That(history.Last(),Does.Contain("fizzled"));
        }
        [Test] public void EnemySpellChargesAndKeepsOriginalTarget()
        {
            int releases=0;
            var spell=new CastSpellAction { SpellName="Sleep", Target=friend, GetActionsFunc=()=> { releases++; return new List<GameAction>(); } };
            Resolve(spell); Assert.That(releases,Is.Zero); Assert.That(caster.PendingCast.Remaining,Is.Zero);
            friend.TilemapPosition += Vector3Int.right*30;
            Resolve(new AdvanceCastAction()); Assert.That(releases,Is.EqualTo(1));
            Resolve(new CastSpellAction { SpellName="Frailty", Target=friend, GetActionsFunc=()=> { releases++; return new List<GameAction>(); } });
            friend.Vitals.HP=0;
            Resolve(new AdvanceCastAction()); Assert.That(releases,Is.EqualTo(1)); Assert.That(caster.PendingCast,Is.Null);
        }
        [UnityTest] public IEnumerator MultiplePrimaryActionsChargeAndReleaseInOneTurn()
        {
            var heal=Learn("Heal");
            caster.BaseStats.ActionsPerTurnMax=2; caster.InvalidateCachedStats(); caster.StartTurn(); caster.SyncDisplayedStats();
            int sp=caster.Vitals.SP;
            yield return harness.ExecuteAction(new SkillAction(caster,heal,friend));
            Assert.That(caster.PendingCast,Is.Null); Assert.That(friend.Vitals.HP,Is.GreaterThan(50));
            Assert.That(caster.Vitals.SP,Is.EqualTo(sp-heal.SPCost));
            var history=Object.FindFirstObjectByType<GameMessages>().History;
            Assert.That(history.Count(x=>x.Contains("starts Heal")),Is.EqualTo(1));
            Assert.That(history.Count(x=>x.Contains("releases Heal")),Is.EqualTo(1));
        }
        [Test] public void FinalArrowCapturesSkillDamageBonusAndEmptyRetreatStillSteps()
        {
            Bow("Steel Arrows",1); var shot=Learn("Aimed Shot"); shot.CastTime=0;
            // Inspect the released damage binding: depletion must not discard its captured modifier.
            friend.Team=Team.Enemy;
            var effects=SkillAction.ForMissile(caster,shot,Vector3Int.left).ExecuteImmediate(caster);
            Assert.That(effects.OfType<ScaledDamageAction>().Single().AmmunitionMultiplier,Is.EqualTo(1.5f));
            Assert.That(ArrowSupply.Count(caster),Is.Zero);
            friend.Team=caster.Team; friend.SetPosition(caster.TilemapPosition+Vector3Int.up);
            Bow("Wooden Arrows",1); var retreat=Learn("Retreat Shot"); var origin=caster.TilemapPosition;
            Resolve(SkillAction.ForMissile(caster,retreat,Vector3Int.right));
            Assert.That(caster.TilemapPosition,Is.EqualTo(origin+Vector3Int.left));
        }
        [Test] public void BlockedDiagonalStopsPiercing()
        {
            var d=harness.Game.CurrentDungeon;
            var corner=Enumerable.Range(1,d.dungeonWidth-2).SelectMany(x=>Enumerable.Range(1,d.dungeonHeight-2).Select(y=>new Vector3Int(x,y)))
                .First(p=>d.IsFloorCell(p) && d.IsFloorCell(p+new Vector3Int(1,1)) && (!d.IsFloorCell(p+Vector3Int.right)||!d.IsFloorCell(p+Vector3Int.up)));
            caster.SetPosition(corner);
            Assert.That(MissileTargeting.TraceLine(caster,new Vector3Int(1,1),10,0).Endpoint,Is.EqualTo(corner));
        }

        [Test] public void CastingSupportAiPrefersMostSavedActionsThenAffordableCost()
        {
            var go=new GameObject("Second charging ally"); var third=go.AddComponent<Ally>();
            third.enabled=false; third.Equipment=go.AddComponent<Equipment>(); third.Skills=new();
            third.Team=caster.Team; third.BaseStats=new Stats { HPMax=100,SPMax=100 }; third.Vitals=new Vitals(); third.Vitals.HP=100; third.Vitals.SP=100;
            third.DisplayedVitals=new Vitals(); third.SyncDisplayedStats(); third.SetPosition(caster.TilemapPosition+Vector3Int.right);
            harness.Game.Allies.Add(third);
            try
            {
                var heal=Learn("Full Heal",friend); Resolve(new SkillAction(friend,heal,friend),friend);
                third.PendingCast=new PendingCast(new WaitAction(),"Test charge",3);
                var dance=Learn("Casting Dance"); var chorus=Learn("Casting Chorus");
                var evaluator=new CastingSupportEvaluator();
                Assert.That(evaluator.Evaluate(AllySkillContext.Build(caster,harness.Game)).Option.Skill,Is.SameAs(chorus));
                third.PendingCast=null;
                Assert.That(evaluator.Evaluate(AllySkillContext.Build(caster,harness.Game)).Option.Skill,Is.SameAs(dance));
                caster.Vitals.SP=1;
                Assert.That(evaluator.Evaluate(AllySkillContext.Build(caster,harness.Game)).Option.Skill,Is.SameAs(dance));
                caster.Vitals.SP=0; Assert.That(evaluator.Evaluate(AllySkillContext.Build(caster,harness.Game)),Is.Null);
                Learn("Mobile Casting",friend); var position=friend.TilemapPosition; friend.DetermineAction();
                Assert.That(friend.determinedActions.Single(),Is.TypeOf<AdvanceCastAction>());
                Resolve(friend.determinedActions.Single(),friend); Assert.That(friend.TilemapPosition,Is.EqualTo(position));
                var remaining=friend.PendingCast.Remaining; friend.TickStatusEffects(); Assert.That(friend.PendingCast.Remaining,Is.EqualTo(remaining));
            }
            finally { harness.Game.Allies.Remove(third); Object.DestroyImmediate(go); }
        }
        [UnityTest] public IEnumerator FullControlReleasesOnItsNextPrimaryActionWithoutAnotherOrder()
        {
            bool? previous=DungeonPreferences.FullControlOverride;
            try
            {
                caster.BaseStats.ActionsPerTurnMax=2; caster.InvalidateCachedStats(); caster.StartTurn(); caster.SyncDisplayedStats();
                var heal=Learn("Heal"); DungeonPreferences.FullControlOverride=true;
                caster.SetAction(new SkillAction(caster,heal,friend));
                yield return harness.WaitUntil(()=>harness.Game.TurnManager.AwaitingCommand && harness.Game.TurnManager.ActiveActor==friend,"friend order after automatic release");
                Assert.That(caster.PendingCast,Is.Null); Assert.That(friend.Vitals.HP,Is.GreaterThan(50));
                DungeonPreferences.FullControlOverride=false; friend.SetAction(new WaitAction()); yield return harness.WaitForIdle();
            }
            finally { DungeonPreferences.FullControlOverride=previous; }
        }
        [Test] public void PiercingStopsAtDestroyedSceneryAndCountsLargeCharacterOnce()
        {
            var d=harness.Game.CurrentDungeon;
            var origin=Enumerable.Range(0,d.dungeonWidth).SelectMany(x=>Enumerable.Range(0,d.dungeonHeight).Select(y=>new Vector3Int(x,y)))
                .First(p=>Enumerable.Range(0,8).All(i=>d.IsWalkable(p+Vector3Int.right*i)));
            caster.SetPosition(origin); friend.SetPosition(origin+Vector3Int.right*5); friend.Team=Team.Enemy; friend.FootPrint=FootPrint.Size3x3;
            var cell=origin+Vector3Int.right*2;
            var prop=DungeonProp.Create(d,new EternalEnigma.Core.World.DungeonScenery(cell.ToGridPoint(),EternalEnigma.Core.World.DungeonSceneryKind.Container,1,42,EternalEnigma.Core.World.SceneryReward.None),null);
            var trace=MissileTargeting.TraceLine(caster,Vector3Int.right,7,0);
            Assert.That(trace.Endpoint,Is.EqualTo(cell)); Assert.That(trace.Encounters,Is.Empty);
            Bow("Steel Arrows",1); caster.SetFacing(Facing.Right); int hp=friend.Vitals.HP;
            Resolve(new RangedAttackAction(caster,null,20,null)); Assert.That(prop.Alive,Is.False); Assert.That(friend.Vitals.HP,Is.EqualTo(hp));
            trace=MissileTargeting.TraceLine(caster,Vector3Int.right,7,0);
            Assert.That(trace.Encounters.Select(h=>h.Character),Is.EqualTo(new[]{friend}));
        }

        [Test] public void MassReviveKeepsOnlyOriginalEligibleRecipients()
        {
            var mass=Learn("Mass Revive"); friend.Vitals.HP=0; PartyRules.MarkDowned(harness.Game,friend);
            Resolve(new SkillAction(caster,mass,caster)); Assert.That(caster.PendingCast,Is.Not.Null);
            friend.SetPosition(caster.TilemapPosition+Vector3Int.right*20);
            while(caster.PendingCast!=null) Resolve(new AdvanceCastAction());
            Assert.That(friend.Vitals.HP,Is.GreaterThan(0));
        }
    }
}
