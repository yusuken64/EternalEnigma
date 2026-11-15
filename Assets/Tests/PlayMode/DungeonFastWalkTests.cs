#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.World;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonFastWalkTests
    {
        private GameTestHarness harness;
        private TestInputScope inputs;
        private IDisposable preferences;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad pad;
        private bool? oldControl;
        private DungeonAnimationMode? oldAnimation;
        private DungeonFastWalk Fast => harness.Game.PlayerController.FastWalk;
        private Ally Follower => harness.Game.Allies.First(a => a != harness.Ally);
        private TileWorldDungeon Dungeon => harness.Game.CurrentDungeon;
        private static Vector3Int Cell(int x, int y = 4) => new(x, y, 0);

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope(); preferences = GamePreferences.Isolate(); ControlBindings.Reload();
            oldControl = DungeonPreferences.FullControlOverride; oldAnimation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.FullControlOverride = false; DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Avery" } });
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); pad = InputSystem.AddDevice<Gamepad>();
            var input = PlayerInputHandler.Instance.PlayerInput;
            input.neverAutoSwitchControlSchemes = true;
            input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, mouse, pad };
            Corridor(20);
            yield return new WaitForSecondsRealtime(.3f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { yield return harness.Cleanup(); }
            finally
            {
                inputs?.Dispose(); preferences?.Dispose(); ControlBindings.Reload();
                DungeonPreferences.FullControlOverride = oldControl; DungeonPreferences.AnimationOverride = oldAnimation;
            }
        }

        private static void Field(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        // A deterministic terrain fixture, using the production floor, sight, AI, input and turn pipeline.
        private void Layout(bool[,] mask, Vector3Int start, params GridRect[] rooms)
        {
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            foreach (var item in Dungeon.Interactables.ToArray()) if (item != null) Object.Destroy(item.gameObject);
            Dungeon.Interactables.Clear();
            var walls = new bool[mask.GetLength(0), mask.GetLength(1)];
            for (int x = 0; x < walls.GetLength(0); x++) for (int y = 0; y < walls.GetLength(1); y++) walls[x, y] = !mask[x, y];
            var ctor = typeof(DungeonFloor).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var args = new object[ctor.GetParameters().Length];
            args[0] = 12345; args[1] = false;
            args[2] = new Dictionary<string, GridLayer> { [DungeonLayers.Floor] = new(mask), [DungeonLayers.Dungeon] = new(walls) };
            args[3] = rooms; args[4] = start.ToGridPoint(); args[5] = start.ToGridPoint();
            for (int i = 6; i < 10; i++) args[i] = Array.Empty<Placement>();
            Field(Dungeon, "runtimeFloor", ctor.Invoke(args)); Field(Dungeon, "floorMask", mask);
            Dungeon.InitializeCache(); Dungeon.DoorsChanged();
            Field(harness.Game, "<Encounter>k__BackingField", new EncounterRuntime(harness.Game));
            harness.Ally.SetPosition(start); harness.Ally.currentInteractable = null;
            Follower.SetPosition(start + Vector3Int.left); Follower.currentInteractable = null;
            foreach (var ally in harness.Game.Allies) ally.Awareness.Refresh(harness.Game);
            harness.Game.PlayerController.Minimap.Initialize(Dungeon);
            harness.Game.PlayerController.Minimap.FogOverlay.Initialize(Dungeon);
            harness.Game.UpdateMiniMap();
            harness.Game.PlayerController.CameraController.SnapToFollowTarget();
        }

        private void Corridor(int end)
        {
            var mask = new bool[end + 3, 12];
            for (int x = 1; x <= end; x++) mask[x, 4] = true;
            Layout(mask, Cell(3));
        }

        private IEnumerator Run(Vector3Int direction)
        {
            Assert.That(Fast.Launch(direction), Is.True);
            Fast.Tick(); // PlayerController ticks immediately after reading the launch input.
            yield return harness.WaitUntil(() => !Fast.Running, "fast walk stopped");
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            Assert.That(harness.Ally.transform.position, Is.EqualTo(Dungeon.CellToWorld(harness.Ally.TilemapPosition)));
        }

        private IEnumerator Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator HeldModifierRequiresFreshDirectionAtEveryStop()
        {
            var mask = new bool[16, 16];
            for (int x = 1; x <= 10; x++) mask[x, 4] = true;
            for (int y = 4; y <= 10; y++) mask[10, y] = true;
            Layout(mask, Cell(3));
            yield return Hold(Key.LeftCtrl, Key.D);
            yield return harness.WaitUntil(() => !Fast.Running && Fast.Steps > 0, "keyboard fast walk");
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(10)));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(10)));
            yield return Hold(Key.D); yield return Hold(Key.LeftCtrl, Key.D);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(10)), "Toggling only the modifier cannot rearm a held direction.");
            yield return Hold(Key.LeftCtrl, Key.W);
            yield return harness.WaitUntil(() => !Fast.Running, "second run with modifier still held");
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(10, 10)));
            yield return Hold(Key.LeftCtrl); yield return Hold(Key.LeftCtrl, Key.S);
            yield return harness.WaitUntil(() => !Fast.Running, "released and repressed direction");
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(10)));
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
            yield return Hold(Key.LeftCtrl);
            var branch = new bool[20, 12];
            for (int x = 1; x <= 15; x++) branch[x, 4] = true;
            for (int y = 4; y <= 8; y++) branch[8, y] = true;
            Layout(branch, Cell(3));
            yield return Hold(Key.LeftCtrl, Key.D);
            yield return harness.WaitUntil(() => !Fast.Running, "branch decision");
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(8)));
            yield return Hold(Key.LeftCtrl, Key.D);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(8)));
            yield return Hold(Key.LeftCtrl); yield return Hold(Key.LeftCtrl, Key.D);
            yield return harness.WaitUntil(() => !Fast.Running, "same direction repressed after a branch");
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(15)));
            yield return Hold(Key.LeftCtrl, Key.LeftShift, Key.A);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(15)));
            Assert.That(harness.Ally.CurrentFacing, Is.EqualTo(Facing.Left), "Holding fast must not prevent aiming in place.");
        }

        [UnityTest] public IEnumerator BranchRoomThresholdAndDiagonalWallsAreDecisions()
        {
            var mask = new bool[20, 20];
            for (int x = 1; x <= 16; x++) mask[x, 4] = true;
            for (int y = 4; y <= 9; y++) mask[9, y] = true;
            Layout(mask, Cell(3));
            yield return Run(Vector3Int.right);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(9)));
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.Branch));
            for (int y = 5; y <= 9; y++) mask[9, y] = false;
            for (int x = 10; x <= 16; x++) for (int y = 3; y <= 8; y++) mask[x, y] = true;
            Layout(mask, Cell(3), new GridRect(10, 3, 7, 6));
            yield return Run(Vector3Int.right);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(9)));
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.RoomBoundary));
            Corridor(12);
            yield return Run(new Vector3Int(1, 1));
            Assert.That(Fast.Steps, Is.Zero, "Diagonal movement must never cut a blocked corner.");
        }

        [UnityTest] public IEnumerator AllEightDirectionsAndReboundControllerUseTheSameRules()
        {
            var mask = new bool[20, 20];
            for (int x = 1; x < 19; x++) for (int y = 1; y < 19; y++) mask[x, y] = true;
            foreach (Facing facing in Enum.GetValues(typeof(Facing)))
            {
                Layout(mask, Cell(10, 10), new GridRect(1, 1, 18, 18));
                Follower.AllyStrategy = AllyStrategy.HoldPosition;
                var delta = GridMovement.GetFacingOffset(facing);
                yield return Run(delta);
                Assert.That(Fast.Steps, Is.GreaterThan(1), facing.ToString());
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(10, 10) + delta * Fast.Steps));
            }
            Corridor(15); Follower.AllyStrategy = AllyStrategy.HoldPosition;
            PlayerInputHandler.Instance.PlayerInput.SwitchCurrentControlScheme("Gamepad", pad);
            yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.LeftShoulder));
            yield return null; yield return null;
            yield return harness.WaitUntil(() => !Fast.Running, "default LB fast walk");
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(15)));
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null; yield return null;
            Corridor(15); Follower.AllyStrategy = AllyStrategy.HoldPosition;
            ControlBindings.ApplyBinding("Player/FastWalk", 1, "<Gamepad>/rightTrigger");
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right, rightTrigger = 1 });
            yield return null; yield return null;
            yield return harness.WaitUntil(() => !Fast.Running, "controller fast walk");
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(15)));
        }

        [UnityTest] public IEnumerator VisibleLootStopsAdjacentAndCanBeLeftBehind()
        {
            var gold = Object.Instantiate(Dungeon.GoldPrefab, Dungeon.transform);
            gold.Setup(Cell(9)); gold.transform.position = Dungeon.CellToWorld(gold.Position); Dungeon.Interactables.Add(gold);
            yield return Run(Vector3Int.right);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(8)));
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.PointOfInterest));
            yield return Run(Vector3Int.left);
            Assert.That(Fast.Steps, Is.GreaterThan(1), "A new decision may walk away from nearby loot.");
            Assert.That(Dungeon.Interactables, Does.Contain(gold));
            yield return Run(Vector3Int.right);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(8)));
            int before = harness.Game.PlayerController.Gold;
            yield return Run(Vector3Int.right);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(9)), "Restarting next to loot enters its tile and stops there.");
            Assert.That(Fast.Steps, Is.EqualTo(1));
            Assert.That(harness.Game.PlayerController.Gold, Is.GreaterThan(before));
            Assert.That(Dungeon.Interactables, Has.No.Member(gold));
        }

        [UnityTest] public IEnumerator RestartingBesideStairsStepsOntoThemAndOpensConfirmation()
        {
            var mask = new bool[20, 20];
            for (int x = 1; x < 19; x++) for (int y = 1; y < 19; y++) mask[x, y] = true;
            foreach (var direction in new[] { Vector3Int.right, new Vector3Int(1, 1, 0) })
            {
                Layout(mask, Cell(4), new GridRect(1, 1, 18, 18));
                Follower.AllyStrategy = AllyStrategy.HoldPosition;
                var stairs = Object.Instantiate(Dungeon.StairsPrefab, Dungeon.transform);
                stairs.Setup(Cell(4) + direction * 4);
                stairs.transform.position = Dungeon.CellToWorld(stairs.Position);
                Dungeon.Interactables.Add(stairs);
                int floor = harness.Game.PlayerController.Floor;
                yield return Run(direction);
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(stairs.Position - direction));
                Assert.That(MenuManager.Instance.Opened, Is.False);
                yield return Run(direction);
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(stairs.Position));
                Assert.That(Fast.Steps, Is.EqualTo(1));
                Assert.That(harness.Ally.currentInteractable, Is.SameAs(stairs));
                Assert.That(MenuManager.Instance.StairDialog.gameObject.activeSelf, Is.True);
                Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(floor), "Entering stairs still requires confirmation.");
                MenuManager.Instance.StairDialog.NoClicked();
                yield return null;
            }
        }

        [UnityTest] public IEnumerator HiddenTrapIsNotAnticipatedButItsResolutionStopsTheRun()
        {
            var trap = Object.Instantiate(Dungeon.TrapPrefabs.OfType<DamageTrap>().First(), Dungeon.transform);
            trap.Setup(Cell(9)); trap.transform.position = Dungeon.CellToWorld(trap.Position); trap.VisualObject.SetActive(false);
            Dungeon.Interactables.Add(trap);
            yield return Run(Vector3Int.right);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(Cell(9)), "Hidden information cannot stop travel before the trap is reached.");
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.PartyEvent));
            Assert.That(trap.VisualObject.activeSelf, Is.True);
        }

        [UnityTest] public IEnumerator NewlySeenEnemyAndBlockedFollowerStopTravel()
        {
            yield return harness.SpawnEnemy("Enemy_Slime", Cell(16));
            yield return Run(Vector3Int.right);
            Assert.That(Fast.Steps, Is.GreaterThan(0), "Unseen enemies must not stop the launch.");
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.EnemyVisible).Or.EqualTo(FastWalkStop.PartyEvent));
            Corridor(20);
            var mask = Dungeon.GetFloorMask(); mask[2, 8] = true;
            Layout(mask, Cell(3)); Follower.SetPosition(Cell(2, 8));
            yield return Run(Vector3Int.right);
            Assert.That(Fast.Steps, Is.EqualTo(1));
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.BlockedFollower));
        }

        [UnityTest] public IEnumerator AllyDamageIsLatchedEvenWhenHealedInTheSameTurn()
        {
            var status = new GameObject("Damage and heal status").AddComponent<FastWalkDamageAndHealStatus>();
            status.transform.SetParent(Follower.transform);
            status.TurnsLeft = 10; Follower.StatusEffects.Add(status);
            int hp = Follower.Vitals.HP;
            yield return Run(Vector3Int.right);
            Assert.That(Follower.Vitals.HP, Is.EqualTo(hp));
            Assert.That(Fast.Steps, Is.EqualTo(1));
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.PartyEvent));
        }

        [UnityTest] public IEnumerator StatusCountdownContinuesButExpirationInterrupts()
        {
            var status = new GameObject("Blind status").AddComponent<BlindStatusEffect>(); status.TurnsLeft = 3;
            status.transform.SetParent(Follower.transform);
            Follower.StatusEffects.Add(status); Follower.InvalidateCachedStats();
            yield return Run(Vector3Int.right);
            Assert.That(Fast.Steps, Is.EqualTo(3));
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.PartyEvent));
            Assert.That(Follower.StatusEffects, Is.Empty);
        }

        [UnityTest] public IEnumerator StarvationAndFullControlNeverChooseAnotherCommand()
        {
            harness.Ally.Vitals.Hunger = 0; harness.Ally.SyncDisplayedStats();
            yield return Run(Vector3Int.right);
            Assert.That(Fast.Steps, Is.EqualTo(1));
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.PartyEvent));
            harness.Ally.Vitals.Hunger = harness.Ally.FinalStats.HungerMax; harness.Ally.SyncDisplayedStats();
            DungeonPreferences.FullControlOverride = true;
            yield return harness.WaitUntil(() => harness.Game.TurnManager.AwaitingCommand, "full-control leader command");
            Assert.That(Fast.Launch(Vector3Int.right), Is.True);
            yield return harness.WaitUntil(() => !Fast.Running, "full-control fast walk stops");
            Assert.That(Fast.Steps, Is.EqualTo(1));
            Assert.That(harness.Game.TurnManager.AwaitingCommand, Is.True);
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.InputRequired));
        }

        [UnityTest] public IEnumerator LevelUpInterruptionKeepsItsCelebrationAnimation()
        {
            var recipient = Follower;
            var status = new GameObject("Level up status").AddComponent<FastWalkLevelUpStatus>();
            status.transform.SetParent(recipient.transform); status.TurnsLeft = 10;
            recipient.StatusEffects.Add(status);
            Assert.That(Fast.Launch(Vector3Int.right), Is.True); Fast.Tick();
            yield return harness.WaitUntil(() => recipient.HeroAnimator.LastPlayedAction == AnimatedAction.LevelUp, "level-up feedback during fast-walk interruption");
            Assert.That(Fast.StopReason, Is.EqualTo(FastWalkStop.PartyEvent));
            Assert.That(Fast.Steps, Is.EqualTo(1));
            Assert.That(recipient.CurrentFacing, Is.EqualTo(Facing.Down));
            Assert.That(DungeonFastWalk.DeferPresentation, Is.False);
            yield return harness.WaitUntil(() => !Fast.Running, "level-up turn completes");
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
        }

        [UnityTest] public IEnumerator FastAndOrdinaryTurnsHaveIdenticalVitalsPositionsAndRandomState()
        {
            Corridor(15);
            var actors = harness.Game.Allies.ToArray();
            var saved = actors.Select(a => JsonUtility.ToJson(a.Vitals)).ToArray();
            var random = UnityEngine.Random.state;
            yield return Run(Vector3Int.right);
            int count = Fast.Steps;
            var fastVitals = actors.Select(a => JsonUtility.ToJson(a.Vitals)).ToArray();
            var fastPositions = actors.Select(a => a.TilemapPosition).ToArray();
            string fastRandom = JsonUtility.ToJson(UnityEngine.Random.state);
            Corridor(15);
            for (int i = 0; i < actors.Length; i++) { JsonUtility.FromJsonOverwrite(saved[i], actors[i].Vitals); actors[i].SyncDisplayedStats(); }
            UnityEngine.Random.state = random;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            for (int i = 0; i < count; i++)
                yield return harness.ExecuteAction(new MovementAction(harness.Ally, harness.Ally.TilemapPosition, harness.Ally.TilemapPosition + Vector3Int.right));
            Assert.That(actors.Select(a => JsonUtility.ToJson(a.Vitals)), Is.EqualTo(fastVitals));
            Assert.That(actors.Select(a => a.TilemapPosition), Is.EqualTo(fastPositions));
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(fastRandom));
        }

        [UnityTest] public IEnumerator HundredStepRunBatchesTurnsAndRevealsItsPath()
        {
            Corridor(103);
            FinalePerformance.Begin();
            int startFrame = Time.frameCount;
            FinalePerformance.Sample[] samples;
            try { yield return Run(Vector3Int.right); }
            finally { samples = FinalePerformance.Finish(); }
            Directory.CreateDirectory("Temp/MovementAudit");
            string timing = $"100 steps, 2 allies: {Fast.ElapsedSeconds * 1000:F1} ms, {Time.frameCount - startFrame} frames (Unity Editor).\n";
            File.WriteAllText("Temp/MovementAudit/fast-walk.txt", timing + string.Join("\n", samples.Select(s => $"{s.Name}: {s.Calls} calls, {s.Milliseconds:F1}ms total, {s.MaximumMilliseconds:F1}ms max")));
            Debug.Log("FAST WALK AUDIT: " + timing);
            Assert.That(Fast.Steps, Is.EqualTo(100));
            Assert.That(Fast.ElapsedSeconds, Is.LessThan(.5), "Fast walking must not take one animation/frame per tile.");
            for (int x = 3; x < 103; x++) Assert.That(harness.Game.PlayerController.Minimap.dungeonMap[x, 4].visibility,
                Is.Not.EqualTo(Minimap.MinimapTileVisibility.Unseen));
        }

        [UnityTest] public IEnumerator AuditRealHeldInputAtThirtySixtyAndOneTwentyFrames()
        {
            int frameRate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            var csv = new System.Text.StringBuilder("targetFps,actionsPerTurn,frame,dt,secondsSinceInput,tileX,worldX,cameraX,animationState,animationTime,turnProcessing\n");
            var report = new System.Text.StringBuilder();
            try
            {
                QualitySettings.vSyncCount = 0;
                foreach (int fps in new[] { 30, 60, 120 }) foreach (int actions in new[] { 1, 2 })
                {
                    Application.targetFrameRate = fps;
                    Corridor(45);
                    foreach (var ally in harness.Game.Allies)
                    {
                        ally.BaseStats.ActionsPerTurnMax = actions; ally.InvalidateCachedStats();
                        ally.Vitals.ActionsPerTurnLeft = actions; ally.SyncDisplayedStats();
                    }
                    yield return new WaitForSecondsRealtime(.2f);
                    double began = Time.realtimeSinceStartupAsDouble, firstMotion = -1, lastMotion = began;
                    float lastX = harness.Ally.transform.position.x;
                    int stationary = 0, frames = 0, commands = 0;
                    double maximumGap = 0, stationaryTime = 0, firstCommand = -1;
                    void Observe(string kind, Character actor, Skill skill)
                    {
                        if (kind != "CommandAccepted" || actor != harness.Ally) return;
                        commands++; if (firstCommand < 0) firstCommand = Time.realtimeSinceStartupAsDouble - began;
                    }
                    GameInputDiagnostics.Observed += Observe;
                    try
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                        while (Time.realtimeSinceStartupAsDouble - began < 1.6)
                        {
                            yield return new WaitForEndOfFrame();
                            double now = Time.realtimeSinceStartupAsDouble;
                            float x = harness.Ally.transform.position.x;
                            if (Mathf.Abs(x - lastX) > .0001f)
                            {
                                if (firstMotion < 0) firstMotion = now - began;
                                maximumGap = Math.Max(maximumGap, now - lastMotion); lastMotion = now;
                            }
                            else if (firstMotion >= 0) { stationary++; stationaryTime += Time.unscaledDeltaTime; }
                            lastX = x; frames++;
                            var anim = harness.Ally.HeroAnimator.Animator.GetCurrentAnimatorStateInfo(0);
                            csv.AppendLine(FormattableString.Invariant($"{fps},{actions},{Time.frameCount},{Time.unscaledDeltaTime:F5},{now - began:F5},{harness.Ally.TilemapPosition.x},{x:F5},{Camera.main.transform.position.x:F5},{anim.fullPathHash},{anim.normalizedTime:F5},{harness.Game.TurnManager.IsProcessingTurn}"));
                        }
                    }
                    finally { GameInputDiagnostics.Observed -= Observe; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); }
                    yield return null; yield return harness.WaitForIdle();
                    report.AppendLine($"Target {fps}fps, actions/turn {actions}: measured {frames / 1.6:F1}fps, first command {firstCommand * 1000:F1}ms, first motion {firstMotion * 1000:F1}ms, {commands} commands, stationary {stationary}/{frames} frames ({stationaryTime * 1000:F1}ms), max movement gap {maximumGap * 1000:F1}ms.");
                    Assert.That(commands, Is.GreaterThan(2));
                }
                Directory.CreateDirectory("Temp/MovementAudit");
                File.WriteAllText("Temp/MovementAudit/held-input.csv", csv.ToString());
                File.WriteAllText("Temp/MovementAudit/held-input.txt", report.ToString());
                Debug.Log("HELD WALK AUDIT:\n" + report);
            }
            finally { Application.targetFrameRate = frameRate; QualitySettings.vSyncCount = vsync; }
        }
    }

    public sealed class FastWalkDamageAndHealStatus : StatusEffect
    {
        public override GameAction GetActionOverride(Character character) => null;
        internal override StatModification GetStatModification() => null;
        internal override string GetEffectName() => "Damage/heal test";
        internal override bool PreventsMenu() => false;
        internal override List<GameAction> GetTickEffects(Character character) => new()
            { new TakeDamageAction(character, character, 1, false), new TakeHealAction(character, character, 1, false) };
    }

    public sealed class FastWalkLevelUpStatus : StatusEffect
    {
        public override GameAction GetActionOverride(Character character) => null;
        internal override StatModification GetStatModification() => null;
        internal override string GetEffectName() => "Level-up test";
        internal override bool PreventsMenu() => false;
        internal override List<GameAction> GetTickEffects(Character character) => new()
            { new ModifyStatAction(character, character, (_, vitals) => vitals.Level++, false), new LevelUpAction(character) };
    }
}
#endif
