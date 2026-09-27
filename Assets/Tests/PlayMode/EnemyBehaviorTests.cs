#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class EnemyBehaviorTests
    {
        GameTestHarness harness;
        Vector3Int center;
        Enemy Last => (Enemy)harness.Game.Enemies.Last();
        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness(); yield return harness.LoadDungeon(new TestScenario());
            foreach (var e in harness.Game.Enemies.ToArray()) Object.Destroy(e.gameObject);
            harness.Game.Enemies.Clear();
            var dungeon = harness.Game.CurrentDungeon;
            center = Enumerable.Range(3,dungeon.dungeonWidth-6).SelectMany(x => Enumerable.Range(3,dungeon.dungeonHeight-6).Select(y => new Vector3Int(x,y)))
                .First(p => Enumerable.Range(-2,5).All(x => Enumerable.Range(-2,5).All(y => dungeon.IsWalkable(p+new Vector3Int(x,y)))));
            harness.Ally.SetPosition(center);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

        [UnityTest] public IEnumerator LegBindBlocksMovementWithoutSkippingActionsAndExpires()
        {
            var restriction = harness.Ally.gameObject.AddComponent<StuckStatusEffect>();
            restriction.TurnsLeft = 2;
            harness.Ally.StatusEffects.Add(restriction);
            Assert.That(harness.Ally.IsMovementBlocked, Is.True);
            Assert.That(harness.Ally.CanMove(), Is.False);
            Assert.That(restriction.GetActionOverride(harness.Ally), Is.Null);
            Assert.That(restriction.PreventsMenu(), Is.False);
            Assert.That(StatusCategories.IsBind(restriction), Is.True);
            var destination = center + Vector3Int.left;
            var move = new MovementAction(harness.Ally, center, destination);
            Assert.That(move.IsValid(harness.Ally), Is.False);
            move.ExecuteImmediate(harness.Ally);
            SkillMovement.Place(harness.Ally, destination);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(center));
            Assert.That(SkillMovement.CanOccupy(harness.Ally, destination), Is.False);
            restriction.Tick(); restriction.Tick();
            Assert.That(harness.Ally.IsMovementBlocked, Is.False);
            Assert.That(new MovementAction(harness.Ally, center, destination).IsValid(harness.Ally), Is.True);
            harness.Ally.RemoveStatusEffect(restriction);
            Object.Destroy(restriction);
            yield return null;
        }

        void Resolve(Character actor, GameAction action)
        {
            var queue = new Queue<GameAction>(); queue.Enqueue(action);
            while (queue.Count > 0) foreach (var effect in actor.ExecuteActionImmediate(queue.Dequeue())) queue.Enqueue(effect);
        }

        [UnityTest] public IEnumerator DormantMageIgnoresMovementAndWakesOnAttackWithoutSleepStatus()
        {
            yield return harness.SpawnEnemy("Enemy_EvilMage", center+Vector3Int.right);
            var enemy = Last;
            Assert.That(enemy.IsDormant, Is.True);
            Assert.That(enemy.StatusEffects.OfType<SleepStatusEffect>(), Is.Empty);
            enemy.GetResponseTo(new MovementAction(harness.Ally, center+Vector3Int.left,center)).ToList();
            Assert.That(enemy.IsDormant, Is.True);
            enemy.DetermineAction(); Assert.That(enemy.GetDeterminedAction().Single(), Is.TypeOf<WaitAction>());
            Resolve(harness.Ally,new TakeDamageAction(harness.Ally,enemy,0, false,true));
            Assert.That(enemy.IsDormant, Is.False, "Even a missed attack provokes the dormant enemy.");
            enemy.StartTurn(); enemy.DetermineAction(); Assert.That(enemy.PursuitTarget, Is.SameAs(harness.Ally));
        }

        [UnityTest] public IEnumerator SilverDevilActsTwiceAndGoopiRootsUntilKilled()
        {
            yield return harness.SpawnEnemy("Enemy_LizardWarrior",center+Vector3Int.right);
            var devil=Last; devil.IsDormant=false;devil.StartTurn();
            Assert.That(devil.Vitals.ActionsPerTurnLeft,Is.EqualTo(2));
            Assert.That(devil.Vitals.AttacksPerTurnLeft,Is.EqualTo(2));
            for(int i=0;i<2;i++)
            {
                devil.DetermineAction();var action=devil.GetDeterminedAction().OfType<AttackAction>().Single();
                action.ExecuteImmediate(devil); // Exercise attack budget without killing the adjacent test ally.
            }
            Assert.That(devil.Vitals.ActionsPerTurnLeft,Is.Zero);Assert.That(devil.Vitals.AttacksPerTurnLeft,Is.Zero);
            Object.Destroy(devil.gameObject);harness.Game.Enemies.Remove(devil);yield return null;
            yield return harness.SpawnEnemy("Enemy_Skeleton",center+Vector3Int.right);
            var goopi=Last;goopi.IsDormant=false;goopi.StartTurn();goopi.PursuitTarget=harness.Ally;
            Resolve(goopi,goopi.GetComponent<EnemyBehavior>().ChooseAction());
            Assert.That(GoopiRootStatusEffect.IsRooted(harness.Ally),Is.True);
            var root=harness.Ally.StatusEffects.OfType<GoopiRootStatusEffect>().Single();
            for(int i=0;i<20;i++) root.Tick();
            Assert.That(root.IsExpired(),Is.False);Assert.That(root.PreventsMenu(),Is.False);
            var move=new MovementAction(harness.Ally,center,center+Vector3Int.left);
            Assert.That(move.IsValid(harness.Ally),Is.False);move.ExecuteImmediate(harness.Ally);
            Assert.That(harness.Ally.TilemapPosition,Is.EqualTo(center));
            Assert.That(SkillMovement.CanOccupy(harness.Ally,center+Vector3Int.left),Is.False);
            Resolve(harness.Ally,new TakeDamageAction(harness.Ally,goopi,9999,false));
            Assert.That(GoopiRootStatusEffect.IsRooted(harness.Ally),Is.False);
            Assert.That(new MovementAction(harness.Ally,center,center+Vector3Int.left).IsValid(harness.Ally),Is.True);
        }

        [UnityTest] public IEnumerator StatueAndMetalSlimeUseStaticAndRecoloredModels()
        {
            System.IO.Directory.CreateDirectory("Temp/EnemyBehaviorPreview");
            yield return harness.SpawnEnemy("Enemy_MetalSlime",center+Vector3Int.right);
            var metal=Last;metal.IsDormant=false;metal.StartTurn();metal.PursuitTarget=harness.Ally;
            Assert.That(metal.FinalStats.HPMax,Is.EqualTo(3));Assert.That(metal.FinalStats.Defense,Is.EqualTo(49));
            Assert.That(metal.FinalStats.EXPOnKill,Is.EqualTo(500));Assert.That(metal.FinalStats.ActionsPerTurnMax,Is.EqualTo(2));
            Assert.That(metal.GetComponent<EnemyBehavior>().ChooseAction(),Is.TypeOf<MovementAction>());
            Assert.That(harness.Game.EnemyManager.SpawnDefinitions.Any(s=>s.EnemyCharacterPrefab!=null && s.EnemyCharacterPrefab.name=="Enemy_MetalSlime"),Is.True);
            harness.Game.UpdateMiniMap();yield return null;
            yield return new WaitForSeconds(3f);
            ScreenCapture.CaptureScreenshot("Temp/EnemyBehaviorPreview/metal-slime.png");yield return null;
            harness.Game.AdvanceFloor();yield return harness.WaitForIdle();
            var dungeon=harness.Game.CurrentDungeon;
            var statue=dungeon.Interactables.OfType<DungeonProp>().Single(p=>p.name=="Stone Hulk statue");
            Assert.That(statue,Is.Not.Null);Assert.That(statue.GetComponentInChildren<Enemy>(),Is.Null);
            Assert.That(statue.GetComponentInChildren<Animator>(),Is.Null);
            Assert.That(dungeon.IsWalkable(statue.Position),Is.False);
            Assert.That(statue.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));
            var nearby=new[] {Vector3Int.left*2,Vector3Int.down*2,Vector3Int.right*2,Vector3Int.up*2}.Select(d=>statue.Position+d).First(dungeon.IsWalkable);
            harness.Ally.SetPosition(nearby);
            harness.Game.UpdateMiniMap();
            System.IO.Directory.CreateDirectory("Temp/EnemyBehaviorPreview");
            yield return new WaitForSeconds(.25f); ScreenCapture.CaptureScreenshot("Temp/EnemyBehaviorPreview/stone-hulk-statue.png");yield return null;

        }

        [UnityTest] public IEnumerator RangedAttackerUsesClearEightDirectionShotAndRespectsBlockers()
        {
            yield return harness.SpawnEnemy("Enemy_RatAssassin",center+Vector3Int.right*2);
            var archer=Last; archer.StartTurn(); var behavior=archer.GetComponent<EnemyBehavior>(); archer.IsDormant=false;
            archer.PursuitTarget=harness.Ally;
            Assert.That(behavior.ClearShot(harness.Ally), Is.True);
            var shot=behavior.ChooseAction(); Assert.That(shot, Is.Not.Null);
            int attacks=archer.Vitals.AttacksPerTurnLeft;
            var effects=shot.ExecuteImmediate(archer);
            Assert.That(effects.Single(), Is.TypeOf<RangedAttackAction>());
            Assert.That(archer.Vitals.AttacksPerTurnLeft, Is.EqualTo(attacks-1));
            yield return harness.SpawnEnemy("Enemy_Slime",center+Vector3Int.right);
            Assert.That(behavior.ClearShot(harness.Ally), Is.False,"Allies block arrows.");
            harness.Ally.SetPosition(center+Vector3Int.up);
            Assert.That(behavior.ClearShot(harness.Ally), Is.False,"Off-axis targets cannot be shot.");
        }

        [UnityTest] public IEnumerator ThievesStealTheirAssignedResourceAndReturnItExactlyOnce()
        {
            var player=harness.Game.PlayerController; player.Inventory.Clear(); player.Gold=100;
            yield return harness.SpawnEnemy("Enemy_MonsterPlant",center+Vector3Int.right);
            var thief=Last; thief.StartTurn(); thief.IsDormant=false; thief.PursuitTarget=harness.Ally;
            var behavior=thief.GetComponent<EnemyBehavior>();
            Resolve(thief,behavior.ChooseAction()); Assert.That(player.Gold,Is.EqualTo(80));
            Assert.That(behavior.CarryingLoot,Is.True); Assert.That(behavior.ChooseAction(),Is.TypeOf<MovementAction>());
            Resolve(harness.Ally,new TakeDamageAction(harness.Ally,thief,9999,false));
            behavior.DropStolenLoot(); behavior.DropStolenLoot();
            Assert.That(harness.Game.CurrentDungeon.Interactables.OfType<Gold>().Sum(g=>g.SeededAmount??0),Is.EqualTo(20));
            Object.Destroy(thief.gameObject); harness.Game.Enemies.Remove(thief); yield return null;
            yield return harness.SpawnEnemy("Enemy_Salamander",center+Vector3Int.right);
            thief=Last; thief.StartTurn(); thief.Provoke(); thief.PursuitTarget=harness.Ally; behavior=thief.GetComponent<EnemyBehavior>();
            var definition=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Potion_HealPotion.asset");
            var item=definition.AsInventoryItem(null); player.Inventory.Add(item);
            Resolve(thief,behavior.ChooseAction()); Assert.That(player.Inventory.InventoryItems,Has.No.Member(item));
            Assert.That(player.Gold,Is.EqualTo(80));
            behavior.DropStolenLoot(); behavior.DropStolenLoot();
            Assert.That(harness.Game.CurrentDungeon.Interactables.OfType<DroppedItem>().Count(d=>ReferenceEquals(d.InventoryItem,item)),Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ConfusionOverridesTurnsExpiresAndMimicOnlyRevealsWhenAttacked()
        {
            yield return harness.SpawnEnemy("Enemy_Beholder",center+Vector3Int.right*2);
            var eye=Last; eye.StartTurn(); eye.IsDormant=false; eye.PursuitTarget=harness.Ally;
            Resolve(eye,eye.GetComponent<EnemyBehavior>().ChooseAction());
            var confusion=harness.Ally.StatusEffects.OfType<ConfusionStatusEffect>().Single();
            Assert.That(StatusCategories.IsAilment(confusion),Is.True);
            bool walked = false;
            for (int i=0;i<32;i++)
            {
                var attempts=confusion.GetActionOverride(harness.Ally).ExecuteImmediate(harness.Ally);
                foreach(var move in attempts.OfType<MovementAction>()) { Assert.That(move.IsValid(harness.Ally),Is.True); walked=true; }
            }
            Assert.That(walked,Is.True,"Confusion must produce actual movement attempts.");
            for(int i=0;i<3;i++) { Assert.That(confusion.GetActionOverride(harness.Ally),Is.Not.Null); confusion.Tick(); }
            Assert.That(confusion.GetActionOverride(harness.Ally),Is.Null);
            harness.Ally.RemoveStatusEffect(confusion);
            Object.Destroy(eye.gameObject); harness.Game.Enemies.Remove(eye); yield return null;
            yield return harness.SpawnEnemy("Enemy_ChestMonster",center+Vector3Int.right);
            var mimic=Last; var behavior=mimic.GetComponent<EnemyBehavior>();
            Assert.That(behavior.Disguised,Is.True); Assert.That(mimic.IsDormant,Is.True);
            Assert.That(mimic.Animator.gameObject.activeInHierarchy,Is.False);
            Assert.That(GameMessages.Name(mimic),Is.EqualTo("Treasure chest"));
            Assert.That(AllySkillContext.Build(harness.Ally,harness.Game).VisibleEnemies,Has.No.Member(mimic));
            Assert.That(new AllyAttackPolicy(harness.Game,harness.Ally,0).ShouldRun(),Is.False);
            mimic.GetResponseTo(new MovementAction(harness.Ally,center+Vector3Int.left,center)).ToList();
            Assert.That(behavior.Disguised,Is.True);
            System.IO.Directory.CreateDirectory("Temp/EnemyBehaviorPreview");
            ScreenCapture.CaptureScreenshot("Temp/EnemyBehaviorPreview/mimic-disguised.png"); yield return null;
            Resolve(harness.Ally,new TakeDamageAction(harness.Ally,mimic,1,false));
            Assert.That(behavior.Disguised,Is.False); Assert.That(mimic.IsDormant,Is.False);
            Assert.That(mimic.Animator.gameObject.activeInHierarchy,Is.True);
            ScreenCapture.CaptureScreenshot("Temp/EnemyBehaviorPreview/mimic-revealed.png"); yield return null;
        }
    }
}
#endif
