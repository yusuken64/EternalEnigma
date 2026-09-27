using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.Generation;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class FantasyTrapTests
    {
        GameObject root;
        Game game;
        TileWorldDungeon dungeon;
        TileWorldCreatorAsset asset;
        Ally ally;
        bool[,] mask;
        UnityEngine.Random.State random;
        [SetUp] public void Setup()
        {
            random = UnityEngine.Random.state;
            root = new GameObject("Trap test fixture");
            game = root.AddComponent<Game>(); game.Allies = new(); game.Enemies = new(); game.DeadUnits = new(); game.StatusEffectPrefabs = new();
            game.PlayerController = root.AddComponent<PlayerController>();
            game.PlayerController.Inventory = root.AddComponent<Inventory>(); game.PlayerController.Floor = 1;
            var creator = root.AddComponent<TileWorldCreator>(); asset = ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
            asset.cellSize = 2.5f; creator.twcAsset = asset;
            dungeon = root.AddComponent<TileWorldDungeon>(); dungeon.Interactables = new(); game.CurrentDungeon = dungeon;
            dungeon.Setup(creator, DungeonFloorGenerator.Generate(new DungeonFloorOptions(42, 32, 32)));
            mask = new bool[32,32];
            for (int x = 1; x < 31; x++) for (int y = 1; y < 31; y++) mask[x,y] = true;
            typeof(TileWorldDungeon).GetField("floorMask", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(dungeon, mask);
            var drop = Child("Drop template").AddComponent<DroppedItem>(); drop.DroppedItemVisual = DroppedItemVisual.Bread;
            dungeon.DroppedItemPrefabs = new() { drop };
            ally = Actor<Ally>(new Vector3Int(10,10)); game.Allies.Add(ally);
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); UnityEngine.Random.state = random; }
        GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
        T Actor<T>(Vector3Int cell) where T : Character
        {
            var actor = Child(typeof(T).Name).AddComponent<T>();
            actor.Equipment = actor.gameObject.AddComponent<Equipment>(); actor.Skills = new();
            actor.VisualParent = new GameObject("Visual"); actor.VisualParent.transform.SetParent(actor.transform);
            actor.BaseStats = new Stats { HPMax = 100, Strength = 10, ActionsPerTurnMax = 1, HungerMax = 100 };
            actor.Vitals = new Vitals(); actor.Vitals.HP = 100; actor.DisplayedStats = actor.FinalStats;
            actor.DisplayedVitals = new Vitals(); actor.DisplayedVitals.HP = 100; actor.TilemapPosition = cell;
            actor.transform.position = dungeon.CellToWorld(cell);
            actor.Team = actor is Ally ? Team.Player : Team.Enemy;
            return actor;
        }
        FantasyTrap Trap(FantasyTrapKind kind, Vector3Int? cell = null)
        {
            var trap = Object.Instantiate(FantasyTrap.PrefabFor((int)kind), root.transform);
            trap.Position = cell ?? ally.TilemapPosition; trap.ActivationChance = 1;
            dungeon.Interactables.Add(trap); return trap;
        }
        TrapResolutionAction Trigger(FantasyTrap trap, Character target = null)
        { var action = new TrapResolutionAction(trap, target ?? ally, Vector3Int.right); action.ExecuteImmediate(target ?? ally); return action; }
        static void Drain(IEnumerator iterator)
        { while (iterator.MoveNext()) if (iterator.Current is IEnumerator child) Drain(child); }

        [Test] public void CatalogueContainsDistinctHiddenModelsAndProjectiles()
        {
            foreach (FantasyTrapKind kind in Enum.GetValues(typeof(FantasyTrapKind)))
            {
                var prefab = FantasyTrap.PrefabFor((int)kind);
                Assert.That(prefab.Kind, Is.EqualTo(kind)); Assert.That(prefab.ActivationChance, Is.EqualTo(.5f));
                Assert.That(prefab.VisualObject.activeSelf, Is.False);
                Assert.That(prefab.VisualObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(Resources.Load<Mesh>("DungeonProps/Trap" + kind)));
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(FantasyTrap.PrefabFor((int)kind + 18), Is.SameAs(prefab));
            }
            foreach (var name in new[] { "ArrowProjectile", "IronArrowProjectile", "LogProjectile" })
                Assert.That(Resources.Load<Mesh>("DungeonProps/" + name).vertexCount, Is.GreaterThan(20));
        }

        [Test] public void ModelsRenderAsDistinctThreeDimensionalTraps()
        {
            var gallery = Child("Trap gallery");
            foreach (FantasyTrapKind kind in Enum.GetValues(typeof(FantasyTrapKind)))
            {
                int i = (int)kind;
                var slot = new GameObject(kind.ToString()); slot.transform.SetParent(gallery.transform);
                slot.transform.localPosition = new Vector3(i % 6 * 2.5f, (2 - i / 6) * 3, 0);
                var visual = DungeonPropModels.Create("Trap" + kind, slot.transform, 2.5f);
                Assert.That(visual.GetComponent<Renderer>().bounds.size.z, Is.GreaterThan(.1f));
            }
            TownAndPropVisualTests.Capture(gallery, new Vector3(7.5f,4,0), 15, "FantasyTraps");
        }

        [Test] public void SummoningAndTransformationUseFreeCellsAndReturnExactItem()
        {
            var template = Actor<Enemy>(new Vector3Int(28,28));
            template.StartingStats = new StartingStats { HPMax = 50, Strength = 5, ActionsPerTurnMax = 1 };
            game.EnemyManager = root.AddComponent<EnemyManager>();
            game.EnemyManager.SpawnDefinitions = new() { new SpawnDefinition { SpawnName = "Test monster", FloorMin = 1, FloorMax = 10, EnemyCharacterPrefab = template } };
            Trigger(Trap(FantasyTrapKind.Summoning));
            Assert.That(game.Enemies.Count, Is.EqualTo(2));
            Assert.That(game.Enemies.Select(e => e.TilemapPosition).Distinct().Count(), Is.EqualTo(2));
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset").AsInventoryItem(null);
            var dropped = dungeon.SetDroppedItem(ally.TilemapPosition + Vector3Int.left, item.ItemDefinition); dropped.InventoryItem = item;
            Trigger(Trap(FantasyTrapKind.Transformation));
            Assert.That(game.Enemies.Count, Is.EqualTo(3));
            var carrier = game.Enemies.Last().GetComponent<TrapCarriedItem>();
            Assert.That(carrier.Item, Is.SameAs(item)); Assert.That(dungeon.Interactables.Contains(dropped), Is.False);
            Assert.That(carrier.TryDrop(true), Is.True); Assert.That(carrier.TryDrop(true), Is.False);
            Assert.That(dungeon.Interactables.OfType<DroppedItem>().Single().InventoryItem, Is.SameAs(item));
        }

        [Test] public void MovementEntersOnceStandingDoesNotAndReentryCanTriggerAgain()
        {
            var trap = Trap(FantasyTrapKind.Arrow, ally.TilemapPosition + Vector3Int.right);
            void Resolve(GameAction action)
            {
                var pending = new Queue<GameAction>(); pending.Enqueue(action);
                while (pending.Count > 0) foreach (var child in ally.ExecuteActionImmediate(pending.Dequeue())) pending.Enqueue(child);
            }
            var origin = ally.TilemapPosition;
            Resolve(new MovementAction(ally, origin, trap.Position));
            Assert.That(ally.Vitals.HP, Is.EqualTo(90));
            Resolve(new MovementAction(ally, trap.Position, trap.Position));
            Assert.That(ally.GetTrapSideEffects(), Is.Empty); Assert.That(ally.Vitals.HP, Is.EqualTo(90));
            Resolve(new MovementAction(ally, trap.Position, origin)); Resolve(new MovementAction(ally, origin, trap.Position));
            Assert.That(ally.Vitals.HP, Is.EqualTo(80));
        }

        [TestCase(false)] [TestCase(true)]
        public void EveryKindCanBeEvadedAndRevealsWithoutEffects(bool enemy)
        {
            Character victim = ally;
            if (enemy) { victim = Actor<Enemy>(new Vector3Int(12,12)); game.Enemies.Add(victim); }
            foreach (FantasyTrapKind kind in Enum.GetValues(typeof(FantasyTrapKind)))
            {
                var trap = Trap(kind, victim.TilemapPosition); trap.ActivationChance = 0;
                if (enemy) trap.VisualObject.SetActive(true);
                var action = Trigger(trap, victim);
                var replay = (List<GameAction>)typeof(TrapResolutionAction).GetField("replay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(action);
                Assert.That(replay.OfType<TrapFeedbackAction>().Single().Message, Is.EqualTo("Evaded"));
                Assert.That(victim.Vitals.HP, Is.EqualTo(100)); Assert.That(trap.VisualObject.activeSelf, Is.True);
                Assert.That(dungeon.Interactables.Contains(trap), Is.True);
            }
        }
        [TestCase(FantasyTrapKind.Arrow,90)] [TestCase(FantasyTrapKind.IronArrow,85)]
        [TestCase(FantasyTrapKind.FallingRock,85)] [TestCase(FantasyTrapKind.RollingLog,90)]
        [TestCase(FantasyTrapKind.Bomb,85)] [TestCase(FantasyTrapKind.Pitfall,90)]
        public void DamageAndSkippedPlaybackResolveExactlyOnce(FantasyTrapKind kind, int hp)
        {
            var action = Trigger(Trap(kind)); Assert.That(ally.Vitals.HP, Is.EqualTo(hp));
            action.ExecuteImmediate(ally); Assert.That(ally.Vitals.HP, Is.EqualTo(hp));
            Drain(action.ExecuteRoutine(ally, true)); Assert.That(ally.Vitals.HP, Is.EqualTo(hp));
        }
        [Test] public void EnemyCanTriggerDamageAndBombHitsBothTeams()
        {
            var enemy = Actor<Enemy>(ally.TilemapPosition + Vector3Int.up); game.Enemies.Add(enemy);
            var bomb = Trap(FantasyTrapKind.Bomb); bomb.VisualObject.SetActive(true); Trigger(bomb, enemy);
            Assert.That(enemy.Vitals.HP, Is.EqualTo(85)); Assert.That(ally.Vitals.HP, Is.EqualTo(85));
        }
        [Test] public void SlidingChainsIntoBindAndStopsAtOccupiedCells()
        {
            var origin = ally.TilemapPosition;
            var ice = Trap(FantasyTrapKind.IceSlick);
            Trap(FantasyTrapKind.ShadowBind, origin + Vector3Int.right);
            Trigger(ice);
            Assert.That(ally.TilemapPosition, Is.EqualTo(origin + Vector3Int.right)); Assert.That(ally.IsMovementBlocked, Is.True);
        }
        [TestCase(FantasyTrapKind.Updraft)] [TestCase(FantasyTrapKind.IceSlick)]
        public void ForcedMovementStopsBeforeWalls(FantasyTrapKind kind)
        {
            mask[12,10] = false; Trigger(Trap(kind)); Assert.That(ally.TilemapPosition, Is.EqualTo(new Vector3Int(11,10)));
        }
        [TestCase(FantasyTrapKind.Warp)] [TestCase(FantasyTrapKind.Pitfall)]
        public void TeleportStaysOnFreeReachableFloor(FantasyTrapKind kind)
        {
            var origin = ally.TilemapPosition; Trigger(Trap(kind));
            Assert.That(ally.TilemapPosition, Is.Not.EqualTo(origin)); Assert.That(DungeonPlacement.Fits(dungeon, ally, ally.TilemapPosition), Is.True);
            Assert.That(game.PlayerController.Floor, Is.EqualTo(1));
        }
        [TestCase(FantasyTrapKind.Hallucination,1)] [TestCase(FantasyTrapKind.WitheringHex,2)]
        [TestCase(FantasyTrapKind.ShadowBind,1)]
        public void StatusTrapsApplyAuthoredDurations(FantasyTrapKind kind, int count)
        { Trigger(Trap(kind)); Assert.That(ally.StatusEffects.Count, Is.EqualTo(count)); Assert.That(ally.StatusEffects.All(s => s.TurnsLeft == 3), Is.True); }

        [Test] public void JinxPreservesExactItemsAndEnemyDoesNotTouchPartyBag()
        {
            var bread = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset");
            var items = Enumerable.Range(0,4).Select(_ => bread.AsInventoryItem(null)).ToArray();
            game.PlayerController.Inventory.InventoryItems.AddRange(items);
            var enemy = Actor<Enemy>(new Vector3Int(14,14)); game.Enemies.Add(enemy);
            var trap = Trap(FantasyTrapKind.ClumsyJinx);
            trap.VisualObject.SetActive(true);
            Trigger(trap, enemy); Assert.That(game.PlayerController.Inventory.Count(), Is.EqualTo(4));
            Trigger(trap); Assert.That(game.PlayerController.Inventory.Count(), Is.EqualTo(1));
            Assert.That(dungeon.Interactables.OfType<DroppedItem>().Select(d => d.InventoryItem), Is.EquivalentTo(items.Take(3)));
        }
        [Test] public void DisarmDropsTheActualEquipmentInstance()
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            try
            {
                definition.EquipmentSlot = EquipmentSlot.MainHand; definition.DroppedItemVisual = DroppedItemVisual.Bread;
                var item = (EquipableInventoryItem)definition.AsInventoryItem(null);
                ally.Equipment.Equip(item); game.PlayerController.Inventory.Add(item);
                Trigger(Trap(FantasyTrapKind.Disarm));
                Assert.That(ally.Equipment.EquippedWeapon, Is.Null);
                Assert.That(dungeon.Interactables.OfType<DroppedItem>().Single().InventoryItem, Is.SameAs(item));
                Assert.That(game.PlayerController.Inventory.Count(), Is.Zero);
            }
            finally { Object.DestroyImmediate(definition); }
        }
        [TestCase(FantasyTrapKind.Blight,"Spoiled Bread",13)] [TestCase(FantasyTrapKind.Scorch,"Charred Bread",25)]
        public void FoodChangesOnlyOneUnitAndRemainsConsumable(FantasyTrapKind kind, string name, int hunger)
        {
            var bread = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset");
            game.PlayerController.Inventory.Add(bread.AsInventoryItem(null)); game.PlayerController.Inventory.Add(bread.AsInventoryItem(null));
            Trigger(Trap(kind)); var items = game.PlayerController.Inventory.InventoryItems;
            Assert.That(items.Count, Is.EqualTo(2)); Assert.That(items.Count(i => i.ItemName == "Bread"), Is.EqualTo(1));
            var altered = items.Single(i => i.ItemName == name);
            Assert.That(altered.ShouldRemoveAfterUse(), Is.True);
            Assert.That(((ModifyStatsItemEffectDefinition)altered.ItemDefinition.ItemEffectDefinition).VitalModification.Hunger, Is.EqualTo(hunger));
            var manager = root.AddComponent<ItemManager>(); manager.ItemDefinitions = new() { bread };
            Assert.That(manager.GetAsInventoryItemByName(name).ItemDefinition, Is.SameAs(altered.ItemDefinition));
        }
        [Test] public void WallRecoveryPreservesUnitsItemsAndLargeFootprints()
        {
            var large = Actor<Enemy>(Vector3Int.zero); large.FootPrint = FootPrint.Size3x3; game.Enemies.Add(large);
            ally.TilemapPosition = new Vector3Int(-20,0);
            var bread = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset").AsInventoryItem(null);
            var drop = dungeon.SetDroppedItem(Vector3Int.zero, bread.ItemDefinition); drop.InventoryItem = bread;
            Assert.That(DungeonPlacement.Recover(dungeon), Is.True);
            Assert.That(DungeonPlacement.Fits(dungeon, ally, ally.TilemapPosition), Is.True);
            Assert.That(DungeonPlacement.Fits(dungeon, large, large.TilemapPosition), Is.True);
            Assert.That(dungeon.CanWalk(drop.Position), Is.True); Assert.That(drop.InventoryItem, Is.SameAs(bread));
            Assert.That(drop.transform.position, Is.EqualTo(dungeon.CellToWorld(drop.Position)));
            Assert.That(DungeonPlacement.Recover(dungeon), Is.False);
        }
        [Test] public void NoSpaceDoesNotLoseItemsOrMoveIntoWalls()
        {
            var bread = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset").AsInventoryItem(null);
            game.PlayerController.Inventory.Add(bread);
            for (int x = 0; x < 32; x++) for (int y = 0; y < 32; y++) mask[x,y] = false;
            mask[10,10] = true;
            Trigger(Trap(FantasyTrapKind.ClumsyJinx)); Trigger(Trap(FantasyTrapKind.Warp));
            Assert.That(game.PlayerController.Inventory.InventoryItems.Single(), Is.SameAs(bread));
            Assert.That(ally.TilemapPosition, Is.EqualTo(new Vector3Int(10,10)));
        }

        [Test] public void FatalLogStopsDisplacementAndDoesNotAwardVictimExperience()
        {
            var enemy = Actor<Enemy>(new Vector3Int(15,15)); game.Enemies.Add(enemy);
            enemy.Vitals.HP = 1; enemy.BaseStats.EXPOnKill = 100;
            var origin = enemy.TilemapPosition;
            var log = Trap(FantasyTrapKind.RollingLog, origin); log.VisualObject.SetActive(true); Trigger(log, enemy);
            Assert.That(enemy.Vitals.HP, Is.Zero); Assert.That(enemy.Vitals.Exp, Is.Zero);
            Assert.That(enemy.TilemapPosition, Is.EqualTo(origin)); Assert.That(game.Enemies.Contains(enemy), Is.False);
            Assert.That(game.DeadUnits, Does.Contain(enemy));
        }

        [Test] public void TrapChainsAreBoundedAtEightAttempts()
        {
            var origin = ally.TilemapPosition;
            var traps = new List<FantasyTrap>();
            for (int i = 0; i < 10; i++)
            { var t = Trap(FantasyTrapKind.IceSlick, origin + Vector3Int.right * i); t.Distance = 1; traps.Add(t); }
            Trigger(traps[0]);
            Assert.That(traps.Take(8).All(t => t.VisualObject.activeSelf), Is.True);
            Assert.That(traps[8].VisualObject.activeSelf, Is.False);
            Assert.That(ally.TilemapPosition, Is.EqualTo(origin + Vector3Int.right * 8));
        }

        [Test] public void FullBagFoodStackSplitsOnlyOneUnitOntoFloor()
        {
            var food = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Bread.asset"));
            try
            {
                food.StackMax = 5;
                var stack = food.AsInventoryItem(4); game.PlayerController.Inventory.Add(stack); game.PlayerController.Inventory.MaxItems = 1;
                Trigger(Trap(FantasyTrapKind.Blight));
                Assert.That(stack.StackStock, Is.EqualTo(3));
                Assert.That(game.PlayerController.Inventory.InventoryItems.Single(), Is.SameAs(stack));
                Assert.That(dungeon.Interactables.OfType<DroppedItem>().Single().InventoryItem.ItemName, Is.EqualTo("Spoiled Bread"));
            }
            finally { Object.DestroyImmediate(food); }
        }

        [Test] public void LargeFootprintsCannotCutDiagonalWallCorners()
        {
            var large = Actor<Enemy>(new Vector3Int(10,15)); large.FootPrint = FootPrint.Size3x3; game.Enemies.Add(large);
            mask[12,14] = false;
            Assert.That(DungeonPlacement.Fits(dungeon, large, new Vector3Int(11,16)), Is.True);
            Assert.That(DungeonPlacement.CanStep(dungeon, large, large.TilemapPosition, new Vector3Int(11,16)), Is.False);
        }

        [Test] public void EnemiesIgnoreEveryHiddenTrapWithoutRevealingRollingOrEvents()
        {
            var enemy = Actor<Enemy>(new Vector3Int(14,14)); game.Enemies.Add(enemy);
            foreach (FantasyTrapKind kind in Enum.GetValues(typeof(FantasyTrapKind)))
            {
                var trap = Trap(kind, enemy.TilemapPosition);
                var before = UnityEngine.Random.state;
                var action = Trigger(trap, enemy);
                Assert.That(UnityEngine.Random.state, Is.EqualTo(before), kind.ToString());
                Assert.That(trap.VisualObject.activeSelf, Is.False, kind.ToString());
                Assert.That(enemy.Vitals.HP, Is.EqualTo(100));
                var replay = (List<GameAction>)typeof(TrapResolutionAction).GetField("replay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(action);
                Assert.That(replay, Is.Empty, kind.ToString());
            }
        }

        [Test] public void EnemySlidingOverHiddenTrapOnlyTriggersItAfterRevelation()
        {
            var enemy = Actor<Enemy>(new Vector3Int(14,14)); game.Enemies.Add(enemy);
            var origin = enemy.TilemapPosition;
            var ice = Trap(FantasyTrapKind.IceSlick, origin); ice.VisualObject.SetActive(true);
            var arrow = Trap(FantasyTrapKind.Arrow, origin + Vector3Int.right);
            Trigger(ice, enemy);
            Assert.That(enemy.TilemapPosition, Is.EqualTo(origin + Vector3Int.right * 3));
            Assert.That(enemy.Vitals.HP, Is.EqualTo(100)); Assert.That(arrow.VisualObject.activeSelf, Is.False);
            enemy.TilemapPosition = origin; arrow.VisualObject.SetActive(true);
            Trigger(ice, enemy);
            Assert.That(enemy.Vitals.HP, Is.EqualTo(90));
        }

        [TestCase(FantasyTrapKind.Disarm)] [TestCase(FantasyTrapKind.ClumsyJinx)]
        public void ItemTrapsSafelyHandleEnemiesWithoutEquipmentOrInventory(FantasyTrapKind kind)
        {
            var enemy = Actor<Enemy>(new Vector3Int(14,14)); game.Enemies.Add(enemy);
            Object.DestroyImmediate(enemy.Equipment); enemy.Equipment = null;
            var trap = Trap(kind, enemy.TilemapPosition); trap.VisualObject.SetActive(true);
            var action = Trigger(trap, enemy);
            var replay = (List<GameAction>)typeof(TrapResolutionAction).GetField("replay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(action);
            Assert.That(replay.OfType<TrapFeedbackAction>().Any(a => a.Message == "No effect" && a.HistoryMessage.Contains("dropped nothing")), Is.True);
            Assert.That(dungeon.Interactables.OfType<DroppedItem>(), Is.Empty);
        }

        [Test] public void PlayerCaltropsStillIgnorePartyAndBindOneEnemy()
        {
            var prefab = Resources.Load<StuckStatusEffect>("FantasyTraps/Effects/ShadowBind"); game.StatusEffectPrefabs.Add(prefab);
            var trap = CaltropTrap.Spawn(dungeon, ally.TilemapPosition, ally);
            Assert.That(trap.GetTrapSideEffects(ally), Is.Empty); Assert.That(dungeon.Interactables.Contains(trap), Is.True);
            var enemy = Actor<Enemy>(ally.TilemapPosition + Vector3Int.right); game.Enemies.Add(enemy);
            foreach (var action in trap.GetTrapSideEffects(enemy)) action.ExecuteImmediate(enemy);
            Assert.That(enemy.IsMovementBlocked, Is.True); Assert.That(dungeon.Interactables.Contains(trap), Is.False);
        }
    }
}
