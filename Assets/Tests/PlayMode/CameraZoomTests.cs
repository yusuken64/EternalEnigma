#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class CameraZoomTests
    {
        private GameTestHarness harness;
        private TestInputScope inputs;
        private IDisposable preferences;
        private Keyboard keyboard;
        private Gamepad pad;
        private Mouse mouse;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope();
            preferences = GamePreferences.Isolate();
            ControlBindings.Reload();
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            ControlBindings.IsCapturing = false;
            try { yield return harness.Cleanup(); }
            finally
            {
                inputs?.Dispose();
                preferences?.Dispose();
                ControlBindings.Reload();
            }
        }

        private void BindDevices() => MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, pad, mouse };

        private IEnumerator Hold(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(.25f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        private IEnumerator Stick(float y)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0, y) });
            yield return new WaitForSecondsRealtime(.25f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null; yield return null;
        }

        private IEnumerator Scroll(float ticks)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, ticks) });
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator DungeonZoomUsesAllDefaultsWithoutSpendingTurnsAndRespectsInputOwners()
        {
            yield return harness.LoadDungeon(new TestScenario());
            BindDevices();
            var controller = harness.Game.PlayerController.CameraController;
            var camera = controller.Camera;
            float initial = camera.orthographicSize;
            Assert.That(initial, Is.EqualTo(CameraZoom.DungeonSize).Within(.001f));
            var hero = harness.Ally;
            var cell = hero.TilemapPosition;
            int actions = hero.Vitals.ActionsPerTurnLeft, hunger = hero.Vitals.HungerAccumulate;
            yield return Hold(Key.LeftBracket);
            float wider = camera.orthographicSize;
            Assert.That(wider, Is.GreaterThan(initial));
            yield return Hold(Key.RightBracket);
            Assert.That(camera.orthographicSize, Is.LessThan(wider));
            yield return Stick(-.8f);
            wider = camera.orthographicSize;
            Assert.That(wider, Is.GreaterThan(initial));
            yield return Stick(.8f);
            Assert.That(camera.orthographicSize, Is.LessThan(wider));
            yield return Scroll(-2);
            wider = camera.orthographicSize;
            yield return Scroll(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(wider * Mathf.Exp(-.12f)).Within(.002f),
                "A wheel step is a single impulse, independent of the frame duration.");
            float stable = camera.orthographicSize;
            yield return new WaitForSecondsRealtime(.2f);
            yield return Stick(.05f);
            Assert.That(camera.orthographicSize, Is.EqualTo(stable), "Released input and stick drift must not zoom.");

            foreach (string owner in new[] { "settings", "capture", "transition" })
            {
                if (owner == "settings") Common.Instance.GlobalSettings.ShowDialog();
                if (owner == "capture") ControlBindings.IsCapturing = true;
                if (owner == "transition") Common.Instance.ScreenTransition.HoldClosed("Zoom test");
                yield return Hold(Key.LeftBracket);
                yield return Stick(-1);
                yield return Scroll(-2);
                Assert.That(camera.orthographicSize, Is.EqualTo(stable), owner + " owns input.");
                if (owner == "settings") Common.Instance.GlobalSettings.Exit_Clicked();
                if (owner == "capture") ControlBindings.IsCapturing = false;
                if (owner == "transition") Common.Instance.ScreenTransition.ReleaseFade();
                yield return null; yield return null;
            }

            ControlBindings.ApplyBinding("Player/ZoomOut", 0, "<Keyboard>/numpadMinus");
            yield return null; yield return null;
            yield return Hold(Key.LeftBracket);
            Assert.That(camera.orthographicSize, Is.EqualTo(stable), "The old binding must stop zooming.");
            yield return Hold(Key.NumpadMinus);
            Assert.That(camera.orthographicSize, Is.GreaterThan(stable));
            yield return Scroll(-100);
            Assert.That(camera.orthographicSize, Is.EqualTo(CameraZoom.OverworldSize));
            yield return Scroll(100);
            Assert.That(camera.orthographicSize, Is.EqualTo(initial).Within(.001f));
            Assert.That(hero.TilemapPosition, Is.EqualTo(cell));
            Assert.That(hero.Vitals.ActionsPerTurnLeft, Is.EqualTo(actions));
            Assert.That(hero.Vitals.HungerAccumulate, Is.EqualTo(hunger));
            var target = camera.WorldToViewportPoint(controller._followTarget.position);
            Assert.That(target.x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(target.y, Is.EqualTo(.5f).Within(.001f));

            yield return Scroll(-2);
            yield return SceneManager.LoadSceneAsync("DungeonScene");
            yield return harness.WaitForIdle();
            Assert.That(harness.Game.PlayerController.CameraController.Camera.orthographicSize,
                Is.EqualTo(initial).Within(.001f), "Entering the dungeon resets zoom.");
        }

        [UnityTest] public IEnumerator TownZoomUsesTheSameLimitsAndResetsOnEntry()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            BindDevices();
            var town = Object.FindFirstObjectByType<Town>();
            float initial = town.TownPlayer.CameraController.Camera.orthographicSize;
            Assert.That(initial, Is.EqualTo(CameraZoom.DungeonSize).Within(.001f));
            yield return Hold(Key.LeftBracket);
            Assert.That(town.TownPlayer.CameraController.Camera.orthographicSize, Is.GreaterThan(initial));
            yield return Scroll(-100);
            Assert.That(town.TownPlayer.CameraController.Camera.orthographicSize, Is.EqualTo(CameraZoom.OverworldSize));
            yield return SceneManager.LoadSceneAsync("Town");
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlocksInput, "town reentry");
            Assert.That(Object.FindFirstObjectByType<Town>().TownPlayer.CameraController.Camera.orthographicSize,
                Is.EqualTo(initial).Within(.001f), "Entering town resets zoom.");
        }

        [UnityTest] public IEnumerator OverworldZoomUsesTheSameLimitsAndResetsOnEntry()
        {
            yield return harness.LoadCommon();
            Common.Instance.BeginSandbox(42);
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlocksInput, "overworld ready");
            BindDevices();
            var world = Object.FindFirstObjectByType<OverworldScene>();
            Assert.That(world.ViewCamera.orthographicSize, Is.EqualTo(CameraZoom.OverworldSize));
            yield return Hold(Key.RightBracket);
            Assert.That(world.ViewCamera.orthographicSize, Is.LessThan(CameraZoom.OverworldSize));
            yield return Scroll(100);
            Assert.That(world.ViewCamera.orthographicSize, Is.EqualTo(CameraZoom.DungeonSize).Within(.001f));
            yield return Stick(-1);
            Assert.That(world.ViewCamera.orthographicSize, Is.GreaterThan(CameraZoom.DungeonSize));
            yield return SceneManager.UnloadSceneAsync(world.gameObject.scene);
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlocksInput, "overworld reentry");
            Assert.That(Object.FindFirstObjectByType<OverworldScene>().ViewCamera.orthographicSize,
                Is.EqualTo(CameraZoom.OverworldSize), "Entering the overworld resets zoom.");
        }
    }
}
#endif
