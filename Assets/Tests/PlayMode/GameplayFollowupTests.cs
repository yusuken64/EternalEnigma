#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class GameplayFollowupTests
    {
        GameTestHarness harness;
        TestInputScope inputs;
        Vector3Int center;
        DungeonAnimationMode? oldAnimations;
        bool? oldControl;
        Enemy Last => (Enemy)harness.Game.Enemies.Last();
        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope(); oldAnimations = DungeonPreferences.AnimationOverride;
            oldControl = DungeonPreferences.FullControlOverride; DungeonPreferences.FullControlOverride = false;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            harness = new GameTestHarness(); yield return harness.LoadDungeon(new TestScenario { Mana = 50 });
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            var dungeon = harness.Game.CurrentDungeon;
            center = Enumerable.Range(3, dungeon.dungeonWidth - 6).SelectMany(x => Enumerable.Range(3, dungeon.dungeonHeight - 6).Select(y => new Vector3Int(x, y)))
                .First(p => Enumerable.Range(-2, 5).All(x => Enumerable.Range(-2, 5).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            harness.PlaceAlly(center); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { yield return harness.Cleanup(); inputs.Dispose(); DungeonPreferences.AnimationOverride = oldAnimations; DungeonPreferences.FullControlOverride = oldControl; }

        [UnityTest] public IEnumerator DragonRollsOnceOnlyForValidShotsAndDealsFire()
        {
            yield return harness.SpawnEnemy("Enemy_Dragon", center + Vector3Int.right * 2);
            var dragon = Last; dragon.StartTurn(); dragon.IsDormant = false; dragon.PursuitTarget = harness.Ally;
            var behavior = dragon.GetComponent<EnemyBehavior>();
            Assert.That(behavior.FireBreath && behavior.RangedAttack, Is.True);
            Assert.That(behavior.RangedAttackChance, Is.EqualTo(.5f));
            Random.InitState(791); var expected = Enumerable.Range(0, 512).Select(_ => Random.value < .5f).ToArray();
            var afterRolls = Random.state; Random.InitState(791);
            Assert.That(Enumerable.Range(0, 512).Select(_ => behavior.ChooseAction() != null), Is.EqualTo(expected));
            Assert.That(Random.state, Is.EqualTo(afterRolls));
            harness.Ally.SetPosition(center + Vector3Int.up);
            var before = Random.state;
            Assert.That(behavior.ChooseAction(), Is.Null); Assert.That(Random.state, Is.EqualTo(before));
            harness.Ally.SetPosition(center);
            var silence = dragon.gameObject.AddComponent<SilenceStatusEffect>(); silence.TurnsLeft = 2; dragon.StatusEffects.Add(silence);
            Assert.That(behavior.ChooseAction(), Is.Null); Assert.That(Random.state, Is.EqualTo(before));
            dragon.StatusEffects.Remove(silence); Object.Destroy(silence);
            GameAction shot = null;
            for (int i = 0; shot == null && i < 32; i++) shot = behavior.ChooseAction();
            Assert.That(shot, Is.Not.Null);
            var damage = dragon.ExecuteActionImmediate(shot).OfType<TakeDamageAction>().Single();
            Assert.That(damage.Element, Is.EqualTo(DamageElement.Fire));
            Assert.That(shot.Visuals.Sequence.Profile, Is.SameAs(CombatVisualCatalog.Instance.FireBreath));
            Assert.That(shot.Visuals.Sequence.Profile.CastSound, Is.Not.Null);
            Assert.That(shot.Visuals.Sequence.Profile.Projectile.Prefab, Is.Not.Null);
            dragon.Vitals.AttacksPerTurnLeft = 1; before = Random.state;
            Assert.That(behavior.ChooseAction(), Is.Null, "Cooldown prevents another attempt.");
            Assert.That(Random.state, Is.EqualTo(before));
            Assert.That(Object.FindFirstObjectByType<GameMessages>().History.Any(s => s.Contains("breathes a fireball")), Is.True);
        }

        [UnityTest] public IEnumerator WerewolfPackMembersMoveTwiceButHaveOneAttack()
        {
            harness.PlaceAlly(center + Vector3Int.left * 2);
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Werewolf.prefab");
            var pack = harness.Game.EnemyManager.SpawnPack(prefab, center + Vector3Int.right * 2, new System.Collections.Generic.HashSet<Vector3Int>());
            Assert.That(pack.Count, Is.EqualTo(3));
            foreach (var wolf in pack)
            {
                wolf.StartTurn(); Assert.That(wolf.Vitals.ActionsPerTurnLeft, Is.EqualTo(2));
                Assert.That(wolf.Vitals.AttacksPerTurnLeft, Is.EqualTo(1));
            }
            var leader = pack[0]; var start = leader.TilemapPosition;
            leader.IsDormant = false; leader.PursuitTarget = harness.Ally;
            for (int i = 0; i < 2; i++)
            {
                leader.DetermineAction(); var move = leader.GetDeterminedAction().Single();
                Assert.That(move, Is.TypeOf<MovementAction>()); leader.ExecuteActionImmediate(move);
                yield return move.ExecuteRoutine(leader, true);
            }
            Assert.That(leader.Vitals.ActionsPerTurnLeft, Is.Zero);
            Assert.That(TileWorldDungeon.ChevDistance(start, leader.TilemapPosition), Is.EqualTo(2));
            Assert.That(leader.Vitals.AttacksPerTurnLeft, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MimicRevealAndSpiderSplitReplayEffectsSoundAndHistory()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            yield return harness.SpawnEnemy("Enemy_ChestMonster", center + Vector3Int.right);
            var mimic = Last;
            var hit = new TakeDamageAction(harness.Ally, mimic, 0, false, true);
            harness.Ally.ExecuteActionImmediate(hit);
            Assert.That(EnemyBehavior.IsDisguised(mimic), Is.False);
            var effects = CombatEffectPlayer.Get(); effects.Clear();
            yield return hit.Visuals.Play(hit, harness.Ally, false);
            Assert.That(effects.ActiveCount, Is.GreaterThan(0), "The reveal replay spawns its authored cue.");
            System.IO.Directory.CreateDirectory("Temp/TodoVerification");
            ScreenCapture.CaptureScreenshot("Temp/TodoVerification/mimic-reveal.png"); yield return new WaitForEndOfFrame();
            Assert.That(CombatVisualCatalog.Instance.Reveal.CastSound, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<GameMessages>().History.Any(s => s.Contains("was a mimic")), Is.True);
            effects.Clear();
            yield return harness.SpawnEnemy("Enemy_Spider", center + Vector3Int.up);
            var spider = Last;
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Spider.prefab");
            var split = new SpawnEnemyAction(prefab, center + Vector3Int.left, spider.VisualParent.transform.position, spider);
            spider.ExecuteActionImmediate(split);
            Assert.That(split.SpawnedEnemy, Is.Not.Null);
            foreach (var audio in AudioManager.Instance.EffectAudioSources) audio.Stop();
            var replay = spider.StartCoroutine(split.Visuals.Play(split, spider, false));
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(AudioManager.Instance.EffectAudioSources.Any(s => s.isPlaying), Is.True);
            yield return replay;
            Assert.That(split.SpawnedEnemy.VisualParent.transform.localScale, Is.EqualTo(spider.VisualParent.transform.localScale));
            Assert.That(Vector3.Distance(split.SpawnedEnemy.VisualParent.transform.localPosition, spider.VisualParent.transform.localPosition), Is.LessThan(.001f));
            Assert.That(CombatVisualCatalog.Instance.Duplication.CastSound, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<GameMessages>().History.Any(s => s.Contains("split into two")), Is.True);
        }

        [UnityTest] public IEnumerator ThrowUsesAuthoredGestureAndCellCentersRegardlessOfFacingVisualOffset()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right * 2);
            var target = Last;
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Potion_HealPotion.asset").AsInventoryItem(null);
            harness.Game.PlayerController.Inventory.Add(item);
            harness.Ally.SetFacing(Facing.Right);
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere); projectile.name = "Throw verification"; projectile.SetActive(false);
            var action = new ThrowItemAction(harness.Game.PlayerController.Inventory, harness.Ally, item, projectile) { LookAt = true };
            harness.Ally.ExecuteActionImmediate(action);
            var dungeon = harness.Game.CurrentDungeon;
            var halfCell = (dungeon.CellToWorld(Vector3Int.right) - dungeon.CellToWorld(Vector3Int.zero)).x / 2;
            Assert.That(action.ProjectileStart, Is.EqualTo(dungeon.CellToWorld(center) + new Vector3(halfCell, halfCell)));
            Assert.That(action.ProjectileEnd, Is.EqualTo(dungeon.CellToWorld(target.TilemapPosition) + new Vector3(halfCell, halfCell)));
            var animator = harness.Ally.HeroAnimator; var stance = animator.CurrentStance;
            var routine = action.ExecuteRoutine(harness.Ally);
            Assert.That(routine.MoveNext(), Is.True); animator.Animator.Update(0);
            Assert.That(animator.LastPlayedAction, Is.EqualTo(AnimatedAction.Throw));
            Assert.That(animator.Animator.GetCurrentAnimatorClipInfo(0).Single().clip.name, Does.StartWith("CarryThrow_"));
            while (routine.MoveNext()) yield return routine.Current;
            Assert.That(animator.CurrentStance, Is.EqualTo(stance)); Assert.That(animator.LastPlayedAction, Is.EqualTo(AnimatedAction.Idle));
            Assert.That(GameObject.Find("Throw verification(Clone)"), Is.Null);
            Object.Destroy(projectile);
        }

        Skill PrepareFindExit(int gold = 1000, int mana = 20)
        {
            var skill = Resources.Load<Skill>("Classes/Skills/Scout/Find Exit");
            harness.Ally.BaseStats.ManaMax = 100;
            harness.Ally.BaseStats.ManaRegenThreshold = 10000;
            harness.Ally.InvalidateCachedStats();
            harness.Ally.Skills.Add(skill); harness.Ally.Vitals.Mana = mana; harness.Ally.SyncDisplayedStats();
            harness.Game.PlayerController.Gold = gold;
            return skill;
        }

        [UnityTest] public IEnumerator FindExitPlacesBoundedTrailWithoutMovingOrTakingAutomaticTurns()
        {
            var skill = PrepareFindExit();
            var game = harness.Game; var floor = game.CurrentDungeon;
            int number = game.PlayerController.Floor;
            int mana = harness.Ally.Vitals.Mana;
            var route = FindExitAction.Route(harness.Ally).ToArray();
            Assert.That(route.Length, Is.InRange(1, 15));
            Assert.That(TrailCoin.Prefab, Is.Not.Null);
            Assert.That(skill.CostText(harness.Ally), Is.EqualTo($"2 Mana, {route.Length * 2} gold"));
            game.FloorReveal.TreasureRevealed = true;
            game.UpdateMiniMap();
            var map = game.PlayerController.Minimap;
            var beforeMap = map.minimapTexture.GetPixels32();
            yield return harness.ExecuteAction(new SkillAction(harness.Ally, skill, harness.Ally));
            var coins = floor.Interactables.OfType<TrailCoin>().ToArray();
            Assert.That(coins.Select(c => c.Position), Is.EquivalentTo(route));
            Assert.That(coins.All(c => c.SeededAmount == 1), Is.True);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(center));
            Assert.That(game.PlayerController.Gold, Is.EqualTo(1000 - 2 * route.Length));
            Assert.That(harness.Ally.Vitals.Mana, Is.EqualTo(mana - 2));
            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
                Assert.That(game.TurnManager.IsProcessingTurn, Is.False, "The trail must wait for the next player command.");
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(center));
            }
            game.UpdateMiniMap();
            CollectionAssert.AreEqual(beforeMap, map.minimapTexture.GetPixels32(), "Trail coins must not add minimap markers.");
            foreach (var coin in coins)
            {
                var renderers = coin.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, "Trail coins need an authored visible model.");
                bool visible = FogOverlay.Instance.IsCurrentlyVisible(coin.transform.position, FootPrint.Size1x1);
                Assert.That(renderers.All(r => r.forceRenderingOff == !visible), Is.True, "Coins obey ordinary dungeon fog.");
            }
            Assert.That(game.CurrentDungeon, Is.SameAs(floor));
            Assert.That(game.PlayerController.Floor, Is.EqualTo(number));
            System.IO.Directory.CreateDirectory("Temp/FindExitTrail");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Temp/FindExitTrail/trail.png");
            yield return null;
        }

        [UnityTest] public IEnumerator FindExitRejectsInsufficientFundsOrManaWithoutManaendingEither()
        {
            var skill = PrepareFindExit();
            int price = FindExitAction.GoldCost(harness.Ally);
            Assert.That(price, Is.GreaterThan(0));
            foreach (var resources in new[] { (gold: price - 1, mana: 20), (gold: price, mana: 1) })
            {
                harness.Game.PlayerController.Gold = resources.gold;
                harness.Ally.Vitals.Mana = resources.mana;
                var cast = new SkillAction(harness.Ally, skill, harness.Ally);
                Assert.That(cast.IsValid(harness.Ally), Is.False);
                Assert.That(cast.ExecuteImmediate(harness.Ally), Is.Empty);
                Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(resources.gold));
                Assert.That(harness.Ally.Vitals.Mana, Is.EqualTo(resources.mana));
                Assert.That(harness.Game.CurrentDungeon.Interactables.OfType<TrailCoin>(), Is.Empty);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator FindExitRepeatCastsStackCoinsAndAlwaysLoseGoldAfterCollection()
        {
            var skill = PrepareFindExit();
            var floor = harness.Game.CurrentDungeon;
            int count = FindExitAction.Route(harness.Ally).Count;
            int mana = harness.Ally.Vitals.Mana;
            Assert.That(count, Is.GreaterThan(0));
            Assert.That(mana, Is.GreaterThanOrEqualTo(12));
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                var report = new AutoplayReport();
                var ledger = new AutoplayLootLedger(report, _ => { });
                yield return harness.ExecuteAction(new SkillAction(harness.Ally, skill, harness.Ally));
                foreach (var coin in floor.Interactables.OfType<TrailCoin>()) ledger.Observe(coin);
                yield return harness.ExecuteAction(new SkillAction(harness.Ally, skill, harness.Ally));
                var coins = floor.Interactables.OfType<TrailCoin>().ToArray();
                foreach (var coin in coins) ledger.Observe(coin);
                Assert.That(coins.Length, Is.EqualTo(count), "Overlapping casts reuse one pile per tile.");
                Assert.That(coins.All(c => c.SeededAmount == 2), Is.True);
                Assert.That(report.Loot.Count, Is.EqualTo(count));
                Assert.That(report.Loot.All(r => r.ExpectedAmount == 2), Is.True, "Loot tracking updates the value of an existing trail pile.");
                Assert.That(report.PlacementIssues, Is.Empty, "Coin art stays inside its logical pickup tile.");
                foreach (var coin in coins)
                {
                    using (ledger.Pickup(coin, harness.Ally, 2)) coin.GetInteractionSideEffects(harness.Ally);
                    coin.GetInteractionSideEffects(harness.Ally); // Deferred destruction must never award twice.
                }
                ledger.Audit();
                Assert.That(ledger.Failed, Is.False);
                Assert.That(report.CollectedLoot, Is.EqualTo(count));
                Assert.That(floor.Interactables.OfType<TrailCoin>(), Is.Empty);
                Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(1000 - cycle * count * 2));
            }
            Assert.That(harness.Ally.Vitals.Mana, Is.EqualTo(mana - 12));
            int beforeReplay = harness.Game.PlayerController.Gold;
            var effect = new FindExitAction();
            effect.ExecuteImmediate(harness.Ally);
            effect.ExecuteImmediate(harness.Ally);
            Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(beforeReplay - count * 2));
            var replayCoins = floor.Interactables.OfType<TrailCoin>().ToArray();
            Assert.That(replayCoins.Sum(c => c.SeededAmount.Value), Is.EqualTo(count), "Replaying one resolved effect cannot mint another trail.");
            foreach (var coin in replayCoins) coin.GetInteractionSideEffects(harness.Ally);
            Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(beforeReplay - count));
        }

        [UnityTest] public IEnumerator FindExitStopsAtStairsAndCollectsTheirCoinWithoutChangingFloors()
        {
            var skill = PrepareFindExit();
            harness.PlaceBesideStairs();
            var floor = harness.Game.CurrentDungeon;
            var stairs = floor.Interactables.OfType<Stairs>().Single();
            int number = harness.Game.PlayerController.Floor;
            var start = harness.Ally.TilemapPosition;
            Assert.That(FindExitAction.Route(harness.Ally), Is.EqualTo(new[] { stairs.Position }));
            yield return harness.ExecuteAction(new SkillAction(harness.Ally, skill, harness.Ally));
            Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(998));
            var stairsCoin = floor.Interactables.OfType<TrailCoin>().Single();
            Assert.That(stairsCoin.Position, Is.EqualTo(stairs.Position));
            var report = new AutoplayReport();
            new AutoplayLootLedger(report, _ => { }).Observe(stairsCoin);
            Assert.That(report.PlacementIssues, Is.Empty, "Moving the coin art aside for the stairs preserves its tile bounds.");
            yield return harness.ExecuteAction(new MovementAction(harness.Ally, start, stairs.Position));
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(stairs.Position));
            Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(999));
            Assert.That(floor.Interactables.OfType<TrailCoin>(), Is.Empty);
            Assert.That(floor.GetInteractable(stairs.Position), Is.SameAs(stairs));
            Assert.That(harness.Game.CurrentDungeon, Is.SameAs(floor));
            Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(number));
            int mana = harness.Ally.Vitals.Mana;
            var emptyCast = new SkillAction(harness.Ally, skill, harness.Ally);
            Assert.That(emptyCast.IsValid(harness.Ally), Is.False);
            Assert.That(emptyCast.ExecuteImmediate(harness.Ally), Is.Empty);
            Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(999));
            Assert.That(harness.Ally.Vitals.Mana, Is.EqualTo(mana));
        }

        [UnityTest] public IEnumerator FindExitAvoidsRevealedTrapsAndCoinsPreserveOtherPickups()
        {
            var skill = PrepareFindExit();
            var floor = harness.Game.CurrentDungeon;
            var firstStep = FindExitAction.Route(harness.Ally).First();
            floor.SetTrap(firstStep, 0);
            var trap = floor.Interactables.OfType<Trap>().Last();
            trap.VisualObject.SetActive(true);
            var safeRoute = FindExitAction.Route(harness.Ally).ToArray();
            Assert.That(safeRoute, Is.Not.Empty);
            Assert.That(safeRoute, Has.No.Member(firstStep));
            Assert.That(safeRoute.All(cell => !floor.IsHazard(cell)), Is.True);
            var pickupCell = safeRoute[0];
            foreach (var existing in floor.Interactables.Where(i => i.Position == pickupCell && i is Gold or DroppedItem).ToArray())
                floor.RemoveInteractable(existing);
            floor.SetGoldAmount(pickupCell, 7);
            yield return harness.ExecuteAction(new SkillAction(harness.Ally, skill, harness.Ally));
            int paid = harness.Game.PlayerController.Gold;
            yield return harness.ExecuteAction(new MovementAction(harness.Ally, harness.Ally.TilemapPosition, pickupCell));
            Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(paid + 8), "The ordinary pickup and trail coin both collect.");
            Assert.That(floor.Interactables.OfType<Gold>().Any(g => g.Position == pickupCell), Is.False);
            Assert.That(floor.Interactables, Has.Member(trap));
        }
    }
}
#endif
