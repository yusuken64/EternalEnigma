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
            Directory.CreateDirectory("Temp/BookTravel");
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
            var loadingTitle = (TMPro.TMP_Text)typeof(ScreenTransition).GetField("destinationLabel", Flags).GetValue(transition);
            Assert.That(loadingTitle.gameObject.activeInHierarchy && loadingTitle.alpha >= .999f, Is.True,
                "An instant opaque cover must display its title immediately, before LateUpdate.");
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
            var town = Object.FindFirstObjectByType<Town>();
            PositionTownPartyNear(town, town.Plan.Exit.ToCell());
            yield return Travel("town-overworld", "Overworld", () => Assert.That(common.Travel.ExitTown(Object.FindFirstObjectByType<Town>()), Is.True),
                () => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true, expectedZoom: true);
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
            var world = Object.FindFirstObjectByType<OverworldScene>();
            world.Player.transform.position = world.CellToWorld(context.Position);
            world.Player.TilemapPosition = context.Position.ToCell();
            yield return null; // Let the real follow camera settle on the miniature.
            yield return Travel("overworld-town", "Town", () => Assert.That(common.Travel.EnterLocation(), Is.True), TownReady, expectedZoom: true);
            town = Object.FindFirstObjectByType<Town>();
            PositionTownPartyNear(town, town.TownBuildings.Single(b => b.Definition.DialogId == "entrance").TilemapPosition);
            yield return Travel("town-dungeon", "DungeonScene", () => Assert.That(common.Travel.EnterTownDungeon(Object.FindFirstObjectByType<Town>(), "story-0"), Is.True),
                () => Game.Instance?.IsReady == true, expectedZoom: true);
            yield return Travel("dungeon-town", "Town", () => Assert.That(common.Travel.FinishDungeon(false, Game.Instance.PlayerController, keepLoot: true), Is.True),
                TownReady, expectedZoom: true);
            yield return Travel("town-menu", "MainMenu", () => Assert.That(common.Travel.ReturnToMenu(), Is.True),
                () => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true);
        }

        [UnityTest] public IEnumerator NoAnimationTravelStillCoversTownGeneration()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            yield return StartCampaign("no-animation");
        }

        [UnityTest] public IEnumerator TravelZoomRetainsReadinessOwnershipAndRestoresCameraAndHud()
        {
            yield return harness.LoadMainMenu(null);
            var transition = Common.Instance.ScreenTransition;
            var cameraObject = new GameObject("Travel camera probe", typeof(Camera), typeof(CameraController));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5;
            var target = new GameObject("Travel target probe");
            var follow = cameraObject.GetComponent<CameraController>();
            follow.Camera = camera; follow.SetFollowTarget(target.transform);
            var hud = GameUISkin.Canvas("Travel HUD probe", Common.Instance.transform, 100);
            var hiddenHud = GameUISkin.Canvas("Disabled HUD probe", Common.Instance.transform, 101);
            hiddenHud.enabled = false;
            yield return null;
            follow.SnapToFollowTarget();
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            int loaded = 0, duplicate = 0, resolved = 0;
            var travel = new SceneTravelPresentation(SceneTravelKind.TownToDungeon, target.transform.position, "probe", () =>
            {
                resolved++;
                return new SceneTravelArrival(camera, target.transform.position);
            }) { SourceCamera = camera };
            try
            {
                transition.DoTransition(() =>
                {
                    loaded++;
                    Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1));
                    transition.HoldClosed("Destination building");
                }, autoOpen: false, travel: travel);
                transition.DoTransition(() => duplicate++);
                yield return harness.WaitUntil(() => camera.orthographicSize < size * .95f, "zoom departure");
                Assert.That(hud.enabled, Is.False);
                Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.001f));
                yield return harness.WaitUntil(() => loaded == 1, "opaque travel callback");
                Assert.That(duplicate, Is.Zero);
                Assert.That(resolved, Is.Zero, "Arrival must wait for the destination's ready signal.");
                Assert.That(transition.HasTravelPresentation, Is.True, "Scene startup must retain the travel request.");
                System.Threading.Thread.Sleep(400);
                transition.DoOpen(); transition.DoOpen();
                yield return harness.WaitUntil(() => resolved == 1, "arrival anchor resolution");
                Assert.That(camera.orthographicSize, Is.GreaterThan(size * 1.5f), "The generation stall must not consume the arrival zoom.");
                Assert.That(transition.BlocksInput, Is.True);
                yield return harness.WaitUntil(() => !transition.BlocksInput, "zoom arrival completion");
                Assert.That(resolved, Is.EqualTo(1));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.0001f));
                Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.001f));
                Assert.That(hud.enabled, Is.True);
                Assert.That(hiddenHud.enabled, Is.False);
                Assert.That(transition.HasTravelPresentation, Is.False);

                transition.DoTransition(() => loaded++, autoOpen: false, travel: travel);
                yield return harness.WaitUntil(() => camera.orthographicSize < size * .95f, "cancelled departure");
                transition.ReleaseFade();
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.0001f));
                Assert.That(hud.enabled, Is.True);
                Assert.That(transition.CameraMotionActive, Is.False);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(loaded, Is.EqualTo(1), "Cancelled travel must not execute its scene load.");

                DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
                transition.DoTransition(() => loaded++, travel: travel);
                Assert.That(transition.HasTravelPresentation, Is.False);
                yield return harness.WaitUntil(() => !transition.BlocksInput, "nonanimated travel fallback");
                Assert.That(loaded, Is.EqualTo(2));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.0001f));
            }
            finally
            {
                transition.ReleaseFade();
                Object.Destroy(cameraObject); Object.Destroy(target);
                Object.Destroy(hud.gameObject); Object.Destroy(hiddenHud.gameObject);
            }
        }

        [Test] public void TravelEasesAccelerateAndDecelerateWithoutOvershooting()
        {
            Assert.That(SceneTravelPresentation.DepartureEase(0), Is.Zero);
            Assert.That(SceneTravelPresentation.DepartureEase(1), Is.EqualTo(1));
            Assert.That(SceneTravelPresentation.ArrivalEase(0), Is.Zero);
            Assert.That(SceneTravelPresentation.ArrivalEase(1), Is.EqualTo(1));
            Assert.That(SceneTravelPresentation.DepartureEase(.5f), Is.LessThan(.05f));
            Assert.That(SceneTravelPresentation.ArrivalEase(.5f), Is.GreaterThan(.95f));
            float departure = 0, arrival = 0;
            for (int i = 1; i <= 100; i++)
            {
                float nextDeparture = SceneTravelPresentation.DepartureEase(i / 100f);
                float nextArrival = SceneTravelPresentation.ArrivalEase(i / 100f);
                Assert.That(nextDeparture, Is.InRange(departure, 1));
                Assert.That(nextArrival, Is.InRange(arrival, 1));
                departure = nextDeparture; arrival = nextArrival;
            }
        }

        IEnumerator StartCampaign(string name)
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);
            yield return Travel(name + "-menu-town", "Town", () => common.Travel.NewCampaign(42), TownReady);
        }

        static bool TownReady() => Object.FindFirstObjectByType<Town>()?.IsReady == true;

        static void PositionTownPartyNear(Town town, Vector3Int anchor)
        {
            var stand = new[] { Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.up }
                .Select(direction => anchor + direction).First(town.CanEnter);
            var leader = town.TownPlayer.ControllingTownAlly;
            leader.TilemapPosition = stand; leader.transform.position = town.WalkableMap.CellToWorld(stand);
            town.TownPlayer.StopHeldWalk(); town.RefreshRoofs(); town.TownPlayer.CameraController.SnapToFollowTarget();
        }

        IEnumerator Travel(string name, string destination, Action start, Func<bool> ready, bool expectedZoom = false)
        {
            var transition = Common.Instance.ScreenTransition;
            var frames = new List<Frame>(); var captured = new HashSet<string>();
            float deadline = Time.realtimeSinceStartup + 120;
            bool firstWorldDeparture = expectedZoom && destination == "Overworld" &&
                !Common.Instance.CampaignContext.IsGridGenerated;
            double departureStarted = Time.realtimeSinceStartupAsDouble, arrivalStarted = -1;
            start();
            if (firstWorldDeparture)
            {
                Assert.That(Common.Instance.CampaignContext.IsGridGenerated, Is.False,
                    "ExitTown must start its zoom before generating the first overworld.");
                Assert.That(Common.Instance.CampaignContext.State.Scene, Is.EqualTo("Town"),
                    "Town presentation must keep its cheap location lookup until covered.");
                Assert.That(transition.CameraMotionActive, Is.True);
            }
            var request = (SceneTravelPresentation)typeof(ScreenTransition).GetField("travelPresentation", Flags).GetValue(transition);
            if (expectedZoom) Assert.That(request, Is.Not.Null, name + " must use the dramatic travel presentation.");
            var sourceCamera = request?.SourceCamera;
            float sourceSize = sourceCamera != null ? sourceCamera.orthographicSize : 0;
            var sourceRotation = sourceCamera != null ? sourceCamera.transform.rotation : Quaternion.identity;
            bool verifiedAnchor = false;
            bool complete = false;
            try
            {
                while (!complete)
                {
                    bool arrived = SceneManager.GetActiveScene().name == destination;
                    bool destinationReady = arrived && ready();
                    var frame = Sample(destinationReady); frames.Add(frame);
                    if (firstWorldDeparture && !arrived && frame.alpha < .999f)
                        Assert.That(frame.overworldGenerated, Is.False, "World generation must wait for an opaque rendered frame.");
                    if (expectedZoom && !arrived && sourceCamera != null)
                    {
                        Assert.That(Quaternion.Angle(sourceCamera.transform.rotation, sourceRotation), Is.LessThan(.01f),
                            name + " must preserve viewing orientation.");
                        if (frame.alpha >= .999f && !verifiedAnchor)
                        {
                            var center = sourceCamera.WorldToViewportPoint(request.SourceAnchor);
                            Assert.That(center.x, Is.EqualTo(.5f).Within(.001f), name + " source anchor must be centered.");
                            Assert.That(center.y, Is.EqualTo(.5f).Within(.001f), name + " source anchor must be centered.");
                            Assert.That(sourceCamera.orthographicSize / sourceSize, Is.EqualTo(request.DepartureScale).Within(.001f));
                            verifiedAnchor = true;
                        }
                    }
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
                        Assert.That(frame.resourceHudVisible, Is.EqualTo(!frame.travelZoom),
                            name + " hides gameplay HUDs while the travel camera is moving and reveals them with ordinary fades.");
                        if (destination == "Overworld" && !frame.travelZoom)
                        {
                            Assert.That(frame.travelActionsVisible, Is.True, "Travel controls should fade in with the overworld.");
                            Assert.That(frame.travelActionsEnabled, Is.False, "Visible travel controls must remain disabled until the cover releases.");
                        }
                    }
                    string phase = frame.alpha >= .999f ? "covered" : frame.alpha > .1f && frame.alpha < .9f ? (arrived ? "revealing" : "closing") : null;
                    if (phase != null && captured.Add(phase)) ScreenCapture.CaptureScreenshot(Output + "/" + name + "-" + phase + ".png");
                    if (expectedZoom && frame.cameraMotion)
                    {
                        if (arrived && arrivalStarted < 0) arrivalStarted = Time.realtimeSinceStartupAsDouble;
                        double elapsed = Time.realtimeSinceStartupAsDouble - (arrived ? arrivalStarted : departureStarted);
                        string zoomPhase = (arrived ? "arrival-" : "departure-") + Mathf.FloorToInt((float)elapsed * 10).ToString("00");
                        if (captured.Add(zoomPhase)) ScreenCapture.CaptureScreenshot("Temp/BookTravel/" + name + "-" + zoomPhase + ".png");
                    }
                    complete = destinationReady && !transition.BlocksInput;
                    if (!complete) yield return null;
                }
                Assert.That(captured.Contains("covered"), Is.True, name + " must present at least one opaque frame.");
                if (expectedZoom)
                {
                    Assert.That(verifiedAnchor, Is.True, name + " must reach its exact anchor before scene loading.");
                    Assert.That(captured.Any(p => p.StartsWith("arrival-")), Is.True, name + " must animate its destination camera.");
                    ScreenCapture.CaptureScreenshot("Temp/BookTravel/" + name + "-ready.png");
                }
                else ScreenCapture.CaptureScreenshot(Output + "/" + name + "-ready.png");
                yield return null;
            }
            finally
            {
                var trace = JsonUtility.ToJson(new Trace { frames = frames.ToArray() }, true);
                File.WriteAllText(Output + "/" + name + ".json", trace);
                if (expectedZoom) File.WriteAllText("Temp/BookTravel/" + name + ".json", trace);
            }
        }

        static Frame Sample(bool ready)
        {
            var transition = Common.Instance.ScreenTransition;
            var title = (TMPro.TMP_Text)typeof(ScreenTransition).GetField("destinationLabel", Flags).GetValue(transition);
            var resourceHud = Object.FindFirstObjectByType<ResourceHUD>();
            var campaignHud = Object.FindFirstObjectByType<CampaignHUD>();
            var actions = campaignHud == null ? null : (Transform)typeof(CampaignHUD).GetField("actions", Flags).GetValue(campaignHud);
            var buttons = actions == null ? Array.Empty<UnityEngine.UI.Button>() : actions.GetComponentsInChildren<UnityEngine.UI.Button>();
            var camera = Camera.main;
            return new Frame { frame = Time.frameCount, time = Time.realtimeSinceStartup, scene = SceneManager.GetActiveScene().name,
                alpha = transition.ShutterScreen.gameObject.activeInHierarchy ? transition.ShutterScreen.color.a : 0,
                covered = transition.BlockScreen.activeInHierarchy, blocksInput = transition.BlocksInput,
                inputConsumed = MenuUIInputModule.Active?.InputConsumed == true, destinationReady = ready,
                travelZoom = transition.HasTravelPresentation,
                cameraMotion = transition.CameraMotionActive,
                cameraSize = camera != null ? camera.orthographicSize : 0,
                cameraPosition = camera != null ? camera.transform.position : Vector3.zero,
                overworldGenerated = Common.Instance.CampaignContext?.IsGridGenerated == true,
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
            public int frame; public float time, alpha, cameraSize; public string scene;
            public Vector3 cameraPosition;
            public bool covered, blocksInput, inputConsumed, destinationReady, floorCover, travelZoom, cameraMotion, overworldGenerated;
            public bool resourceHudVisible, travelActionsVisible, travelActionsEnabled;
            public bool sceneTitleVisible; public string sceneTitle, floorTitle;
        }
    }
}
#endif
