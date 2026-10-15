#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class ScreenTransitionTests
    {
        const string Output = "Temp/UIScreenAudit/Transitions";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameTestHarness harness;
        TestInputScope input;
        IDisposable preferences;
        DungeonAnimationMode? previousAnimation;
        bool? previousControl;
        UnityEditor.EditorWindow view;
        int previousSize;
        readonly List<InputDevice> devices = new();

        [UnitySetUp] public IEnumerator Setup()
        {
            input = new TestInputScope(); preferences = GamePreferences.Isolate();
            previousAnimation = DungeonPreferences.AnimationOverride;
            previousControl = DungeonPreferences.FullControlOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            DungeonPreferences.FullControlOverride = false;
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            Directory.CreateDirectory(Output);
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            previousSize = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { new Vector2(960, 600), "Fade audit" });
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            foreach (var device in devices) InputSystem.RemoveDevice(device);
            devices.Clear();
            try { yield return harness.Cleanup(); }
            finally
            {
                DungeonPreferences.AnimationOverride = previousAnimation;
                DungeonPreferences.FullControlOverride = previousControl;
                if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, previousSize);
                input?.Dispose(); preferences?.Dispose();
            }
        }

        [UnityTest] public IEnumerator PausedFadesCompleteOnceAndInterruptedDriversCannotFlash()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance; var transition = common.ScreenTransition;
            transition.TransitionTimeSeconds = .24f; transition.OpenDelayTimeSeconds = .06f;
            Time.timeScale = 0;
            int calls = 0, duplicates = 0;
            transition.DoTransition(() =>
            {
                calls++;
                Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1).Within(.001f));
                Assert.That(transition.BlocksInput, Is.True);
            });
            transition.DoTransition(() => duplicates++);
            var frames = new List<Frame>();
            float deadline = Time.realtimeSinceStartup + 4;
            while (transition.BlockScreen.activeSelf)
            {
                frames.Add(Sample(false));
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "A paused game must not freeze a fade.");
                yield return null;
            }
            File.WriteAllText(Output + "/paused-fade.json", JsonUtility.ToJson(new Trace { frames = frames.ToArray() }, true));
            Assert.That(calls, Is.EqualTo(1)); Assert.That(duplicates, Is.Zero);
            Assert.That(frames.Any(f => f.alpha > .1f && f.alpha < .9f), Is.True);

            transition.HoldClosed("Interrupted reveal"); transition.DoOpen();
            yield return harness.WaitUntil(() => transition.ShutterScreen.color.a < .999f, "reveal after the destination frame");
            float partial = transition.ShutterScreen.color.a;
            Assert.That(partial, Is.GreaterThan(0).And.LessThan(1));
            calls = 0;
            transition.DoTransition(() => calls++, autoOpen: false);
            Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(partial).Within(.001f), "Reversing a reveal must preserve its current alpha.");
            yield return harness.WaitUntil(() => calls == 1, "reversed fade");
            common.StartCoroutine(transition.FadeTo(0, .25f));
            yield return new WaitForSecondsRealtime(.06f);
            transition.HoldClosed("New loading owner");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1).Within(.001f), "A superseded sleep fade cannot uncover a later load.");
            transition.ReleaseFade();
            transition.DoOpen();
            Assert.That(transition.BlockScreen.activeSelf, Is.False, "Opening an already visible screen must not flash black.");

            // A synchronous generation stall before a fade belongs to the old
            // frame, not to the reveal that begins after generation finishes.
            transition.HoldClosed();
            System.Threading.Thread.Sleep(400);
            double revealStarted = Time.realtimeSinceStartupAsDouble;
            common.StartCoroutine(transition.FadeTo(0, transition.TransitionTimeSeconds));
            yield return null; yield return null;
            float expectedAlpha = 1 - Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - revealStarted) / transition.TransitionTimeSeconds);
            Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(expectedAlpha).Within(.2f),
                "The previous loading frame's delta must not skip the new fade.");
            yield return harness.WaitUntil(() => transition.ShutterScreen.color.a == 0, "fade after a synchronous load");
            transition.ReleaseFade();

            transition.HoldClosed(); transition.DoOpen();
            yield return null;
            Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1), "A ready destination gets an opaque rendered frame before its reveal clock starts.");
            yield return harness.WaitUntil(() => !transition.BlocksInput, "destination frame handoff");

            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            calls = 0; transition.DoTransition(() => calls++);
            Assert.That(calls, Is.Zero, "No animations still presents the cover before loading.");
            Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1));
            yield return harness.WaitUntil(() => !transition.BlockScreen.activeSelf, "instant covered load");
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator OpaqueCoverBlocksKeyboardGamepadAndMouseUntilReleased()
        {
            yield return harness.LoadMainMenu(null);
            var keyboard = InputSystem.AddDevice<Keyboard>(); devices.Add(keyboard);
            var pad = InputSystem.AddDevice<Gamepad>(); devices.Add(pad);
            var mouse = InputSystem.AddDevice<Mouse>(); devices.Add(mouse);
            var module = MenuUIInputModule.Active;
            module.actionsAsset.devices = devices.ToArray();
            var canvas = GameUISkin.Canvas("Fade input probe", Common.Instance.transform, 100);
            int activated = 0;
            var button = GameUISkin.Button(canvas.transform, "Fade input probe", new Vector2(.35f,.4f), new Vector2(.65f,.6f), () => activated++);
            button.Select(); Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            var transition = Common.Instance.ScreenTransition;
            transition.HoldClosed();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            Assert.That(activated, Is.Zero, "The loading cover must block Submit and pointer activation equally.");
            Assert.That(module.InputConsumed, Is.True);
            transition.ReleaseFade(); button.Select(); yield return null; yield return null;
            Assert.That(activated, Is.Zero, "Clicks made beneath the cover must be discarded rather than replayed when it releases.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(activated, Is.EqualTo(1), "Fresh input works after the reveal.");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
            Assert.That(activated, Is.EqualTo(2), "Fresh gamepad input works after the reveal.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            Assert.That(activated, Is.EqualTo(3), "Fresh pointer input works after the reveal.");
            Object.Destroy(canvas.gameObject);
        }

        [UnityTest] public IEnumerator AnimatedCampaignTravelStaysCoveredUntilEachDestinationIsReady()
        {
            yield return StartCampaign("animated");
            var common = Common.Instance; var context = common.CampaignContext;
            harness.PrepareOpening();
            context.Resolved.UnionWith(context.Campaign.Routes.Where(r => r.IsTownExit).Select(r => r.Id));
            yield return Travel("town-overworld", "Overworld", () => Assert.That(common.Travel.ExitTown(Object.FindFirstObjectByType<Town>()), Is.True),
                () => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true);
            context.Position = context.Grid.Locations["repeatable-0"];
            // Stage an earlier clear through the campaign domain so this return
            // has no first-clear rewards awaiting acknowledgement during capture.
            Assert.That(context.BeginDungeon(), Is.True);
            Assert.That(context.CompleteDungeon(true), Is.True);
            yield return Travel("overworld-dungeon", "DungeonScene", () => Assert.That(common.Travel.EnterLocation(), Is.True), () => Game.Instance?.IsReady == true);
            Game.Instance.PlayerController.Floor = common.GameSaveData.DungeonSaveData.EndFloor;
            Assert.That(Game.Instance.CheckFloorExit(false), Is.True);
            yield return Travel("dungeon-overworld", "Overworld", () => Assert.That(common.Travel.FinishDungeon(true, Game.Instance.PlayerController), Is.True),
                () => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true);
            context.Position = context.Grid.Locations[context.Campaign.StartLocationId];
            yield return Travel("overworld-town", "Town", () => Assert.That(common.Travel.EnterLocation(), Is.True), TownReady);
            yield return Travel("town-dungeon", "DungeonScene", () => Assert.That(common.Travel.EnterTownDungeon(Object.FindFirstObjectByType<Town>(), "story-0"), Is.True),
                () => Game.Instance?.IsReady == true);
            yield return Travel("dungeon-town", "Town", () => Assert.That(common.Travel.FinishDungeon(false, Game.Instance.PlayerController), Is.True), TownReady);
            yield return Travel("town-menu", "MainMenu", () => Assert.That(common.Travel.ReturnToMenu(), Is.True),
                () => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true);
        }

        [UnityTest] public IEnumerator NoAnimationTravelStillCoversTownGeneration()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            yield return StartCampaign("no-animation");
        }

        IEnumerator StartCampaign(string name)
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);
            yield return Travel(name + "-menu-town", "Town", () => common.Travel.NewCampaign(42), TownReady);
        }

        static bool TownReady() => Object.FindFirstObjectByType<Town>()?.IsReady == true;

        IEnumerator Travel(string name, string destination, Action start, Func<bool> ready)
        {
            var transition = Common.Instance.ScreenTransition;
            var frames = new List<Frame>(); var captured = new HashSet<string>();
            float deadline = Time.realtimeSinceStartup + 120;
            start();
            bool complete = false;
            try
            {
                while (!complete)
                {
                    bool arrived = SceneManager.GetActiveScene().name == destination;
                    bool destinationReady = arrived && ready();
                    var frame = Sample(destinationReady); frames.Add(frame);
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), name + " timed out.");
                    if (arrived && !destinationReady)
                    {
                        Assert.That(frame.covered, Is.True, name + " exposed generation.");
                        Assert.That(frame.alpha, Is.EqualTo(1).Within(.001f), name + " must remain opaque until ready.");
                    }
                    if (frame.covered) Assert.That(Common.Instance.Travel.IsTransitioning, Is.True, "Scene readiness must not release input during reveal.");
                    if (frame.floorCover && destinationReady && frame.alpha < .999f)
                        Assert.That(frame.sceneTitleVisible, Is.False, "The floor title must own the dungeon entry reveal without overlapping loading text.");
                    if (destinationReady && frame.alpha > .1f && frame.alpha < .8f && destination != "MainMenu")
                    {
                        Assert.That(frame.resourceHudVisible, Is.True, name + " must reveal the HUD with the scene, without a late pop.");
                        if (destination == "Overworld")
                        {
                            Assert.That(frame.travelActionsVisible, Is.True, "Travel controls should fade in with the overworld.");
                            Assert.That(frame.travelActionsEnabled, Is.False, "Visible travel controls must remain disabled until the cover releases.");
                        }
                    }
                    string phase = frame.alpha >= .999f ? "covered" : frame.alpha > .1f && frame.alpha < .9f ? (arrived ? "revealing" : "closing") : null;
                    if (phase != null && captured.Add(phase)) ScreenCapture.CaptureScreenshot(Output + "/" + name + "-" + phase + ".png");
                    complete = destinationReady && !transition.BlocksInput;
                    if (!complete) yield return null;
                }
                Assert.That(captured.Contains("covered"), Is.True, name + " must present at least one opaque frame.");
                ScreenCapture.CaptureScreenshot(Output + "/" + name + "-ready.png");
                yield return null;
            }
            finally { File.WriteAllText(Output + "/" + name + ".json", JsonUtility.ToJson(new Trace { frames = frames.ToArray() }, true)); }
        }

        static Frame Sample(bool ready)
        {
            var transition = Common.Instance.ScreenTransition;
            var title = (TMPro.TMP_Text)typeof(ScreenTransition).GetField("destinationLabel", Flags).GetValue(transition);
            var resourceHud = Object.FindFirstObjectByType<ResourceHUD>();
            var campaignHud = Object.FindFirstObjectByType<CampaignHUD>();
            var actions = campaignHud == null ? null : (Transform)typeof(CampaignHUD).GetField("actions", Flags).GetValue(campaignHud);
            var buttons = actions == null ? Array.Empty<UnityEngine.UI.Button>() : actions.GetComponentsInChildren<UnityEngine.UI.Button>();
            return new Frame { frame = Time.frameCount, time = Time.realtimeSinceStartup, scene = SceneManager.GetActiveScene().name,
                alpha = transition.ShutterScreen.gameObject.activeInHierarchy ? transition.ShutterScreen.color.a : 0,
                covered = transition.BlockScreen.activeInHierarchy, blocksInput = transition.BlocksInput,
                inputConsumed = MenuUIInputModule.Active?.InputConsumed == true, destinationReady = ready,
                sceneTitleVisible = title != null && title.gameObject.activeInHierarchy,
                sceneTitle = title == null ? "" : title.text,
                floorTitle = Game.Instance?.NewFloorMessage?.FloorMessage?.text ?? "",
                resourceHudVisible = resourceHud != null && resourceHud.GetComponent<Canvas>().enabled,
                travelActionsVisible = actions != null && actions.gameObject.activeInHierarchy && buttons.Length > 0,
                travelActionsEnabled = buttons.Any(button => button.IsInteractable()),
                floorCover = Game.Instance?.NewFloorMessage != null && Game.Instance.NewFloorMessage.gameObject.activeInHierarchy };
        }
        [Serializable] sealed class Trace { public Frame[] frames; }
        [Serializable] sealed class Frame
        {
            public int frame; public float time, alpha; public string scene;
            public bool covered, blocksInput, inputConsumed, destinationReady, floorCover;
            public bool resourceHudVisible, travelActionsVisible, travelActionsEnabled;
            public bool sceneTitleVisible; public string sceneTitle, floorTitle;
        }
    }
}
#endif
