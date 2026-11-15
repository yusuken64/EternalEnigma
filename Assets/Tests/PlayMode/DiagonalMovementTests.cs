#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
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
    public sealed class DiagonalMovementTests
    {
        private GameTestHarness harness;
        private TestInputScope inputs;
        private IDisposable preferences;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad pad;
        private bool? oldControl;
        private DungeonAnimationMode? oldAnimation;
        private PlayerInputHandler Input => PlayerInputHandler.Instance;
        private static readonly string[] Names = { "MoveUpLeft", "MoveUpRight", "MoveDownLeft", "MoveDownRight" };
        private static readonly Key[] Keys = { Key.Numpad7, Key.Numpad9, Key.Numpad1, Key.Numpad3 };
        private static readonly Vector3Int[] Steps = { new(-1, 1), new(1, 1), new(-1, -1), new(1, -1) };

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope();
            preferences = GamePreferences.Isolate();
            ControlBindings.Reload();
            oldControl = DungeonPreferences.FullControlOverride;
            oldAnimation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.FullControlOverride = false;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            pad = InputSystem.AddDevice<Gamepad>();
            Input.PlayerInput.neverAutoSwitchControlSchemes = true;
            Input.PlayerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, mouse, pad };
            for (int i = 0; i < Names.Length; i++)
                ControlBindings.ApplyBinding("Player/" + Names[i], 0, "<Keyboard>/" + Keys[i].ToString().ToLowerInvariant());
            yield return new WaitForSecondsRealtime(.3f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            try
            {
                ControlBindings.IsCapturing = false;
                yield return harness.Cleanup();
            }
            finally
            {
                inputs?.Dispose();
                preferences?.Dispose();
                ControlBindings.Reload();
                DungeonPreferences.FullControlOverride = oldControl;
                DungeonPreferences.AnimationOverride = oldAnimation;
            }
        }

        private IEnumerator Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
        }

        [UnityTest] public IEnumerator DiagonalKeysTurnCombineCancelAndRespectCaptureWithoutMoving()
        {
            var origin = harness.Ally.TilemapPosition;
            for (int i = 0; i < Names.Length; i++)
            {
                yield return Hold(Key.LeftShift, Keys[i]);
                Assert.That(Input.moveInput, Is.EqualTo(new Vector2(Steps[i].x, Steps[i].y).normalized));
                Assert.That(harness.Ally.CurrentFacing, Is.EqualTo(Character.GetFacing(Steps[i])));
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(origin));
            }
            yield return Hold(Key.LeftShift, Key.W, Key.Numpad3);
            Assert.That(Input.moveInput, Is.EqualTo(new Vector2(1, -1).normalized), "A dedicated direction takes priority.");
            yield return Hold(Key.LeftShift, Key.W, Key.Numpad7, Key.Numpad3);
            Assert.That(Input.moveInput, Is.EqualTo(Vector2.zero), "Opposites cancel even while W is held.");
            Assert.That(Input.isMoving, Is.False);
            yield return Hold(Key.LeftShift, Key.Numpad7, Key.Numpad9);
            Assert.That(Input.moveInput, Is.EqualTo(Vector2.up), "Opposite horizontal components cancel.");
            yield return Hold(Key.LeftShift, Key.W);
            Assert.That(Input.moveInput, Is.EqualTo(Vector2.up), "Releasing dedicated directions restores normal input.");
            yield return Hold(Key.LeftShift, Key.W, Key.D);
            Assert.That(Input.moveInput.normalized, Is.EqualTo(new Vector2(1, 1).normalized), "Existing two-key diagonals remain available.");
            ControlBindings.IsCapturing = true;
            yield return Hold(Key.LeftShift, Key.Numpad7, Key.Space);
            Assert.That(Input.moveInput, Is.EqualTo(Vector2.zero));
            Assert.That(Input.attackPressed, Is.False);
            Assert.That(Input.holdPosition, Is.False);
            yield return Hold();
            ControlBindings.IsCapturing = false;
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(origin));
        }

        private Vector3Int FreeRun(Vector3Int step)
        {
            var dungeon = harness.Game.CurrentDungeon;
            return Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .First(p => dungeon.IsWalkable(p) && Enumerable.Range(0, 3).All(i =>
                    dungeon.CanWalkTo(p + step * i, p + step * (i + 1)) &&
                    dungeon.GetCharacterAtPosition(p + step * (i + 1)) == null &&
                    dungeon.GetInteractable(p + step * i) == null));
        }

        [UnityTest] public IEnumerator EachDedicatedDirectionRepeatsThroughRealMovementTurns()
        {
            for (int i = 0; i < Names.Length; i++)
            {
                var origin = FreeRun(Steps[i]);
                harness.PlaceAlly(origin);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Keys[i]));
                yield return harness.WaitUntil(() => Vector3Int.Distance(harness.Ally.TilemapPosition, origin) >= 2.8f,
                    Names[i] + " held movement across two turns");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return harness.WaitForIdle();
                var delta = harness.Ally.TilemapPosition - origin;
                Assert.That(Mathf.Sign(delta.x), Is.EqualTo(Steps[i].x));
                Assert.That(Mathf.Sign(delta.y), Is.EqualTo(Steps[i].y));
                Assert.That(Mathf.Abs(delta.x), Is.EqualTo(Mathf.Abs(delta.y)));
                Assert.That(Mathf.Abs(delta.x), Is.GreaterThanOrEqualTo(2));
                yield return Hold();
            }
        }

        [UnityTest] public IEnumerator ShiftThenSpaceAndControllerButtonsAttackTheAdjacentDiagonal()
        {
            var origin = FreeRun(Steps[1]);
            harness.PlaceAlly(origin);
            yield return harness.SpawnEnemy("Enemy_Slime", origin + Steps[1]);
            var target = (Enemy)harness.Game.Enemies.Single();
            target.Policies.Clear();
            target.BaseStats.HPMax = 5000;
            target.InvalidateCachedStats();
            target.Vitals.HP = 5000;
            harness.Ally.BaseStats.HitBonus = 1;
            harness.Ally.InvalidateCachedStats();
            Assert.That(CombatMath.HitChance(harness.Ally, target), Is.EqualTo(1));
            yield return Hold(Key.LeftShift, Key.Numpad9);
            Assert.That(harness.Ally.CurrentFacing, Is.EqualTo(Facing.UpRight));
            var hp = target.Vitals.HP;
            yield return Hold(Key.LeftShift, Key.Numpad9, Key.Space);
            yield return Hold();
            yield return harness.WaitForIdle();
            Assert.That(target.Vitals.HP, Is.LessThan(hp));
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(origin));

            ControlBindings.ApplyBinding("Player/MoveUpRight", 1, "<Gamepad>/rightTrigger");
            Input.PlayerInput.SwitchCurrentControlScheme("Gamepad", pad);
            yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 }.WithButton(GamepadButton.North));
            yield return null;
            yield return null;
            Assert.That(Input.moveInput, Is.EqualTo(new Vector2(1, 1).normalized));
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(origin));
            hp = target.Vitals.HP;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 }
                .WithButton(GamepadButton.North).WithButton(GamepadButton.South));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return harness.WaitForIdle();
            Assert.That(target.Vitals.HP, Is.LessThan(hp));
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(origin));
        }
    }
}
#endif
