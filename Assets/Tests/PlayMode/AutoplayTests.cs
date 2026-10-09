#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    // Explicit: these session checks are opt-in, just like the full playthrough. No CI launch.
    [Explicit("Run manually with Tools > Eternal Enigma > Tests > Run Autoplay")]
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class AutoplayTests
    {
        private GameTestHarness harness;
        private bool hadTerminalPreference;
        private int terminalPreference;
        private static void CheckControlPreference(bool blocked)
        {
            bool hadKey = PlayerPrefs.HasKey("Dungeon.FullControl");
            int saved = PlayerPrefs.GetInt("Dungeon.FullControl");
            bool? previousOverride = DungeonPreferences.FullControlOverride;
            try
            {
                foreach (int value in new[] { 0, 1 })
                foreach (bool? controlOverride in new bool?[] { null, false, true })
                {
                    PlayerPrefs.SetInt("Dungeon.FullControl", value);
                    DungeonPreferences.FullControlOverride = controlOverride;
                    Assert.That(DungeonPreferences.FullControl, Is.EqualTo(!blocked && (controlOverride ?? value != 0)));
                    Assert.That(PlayerPrefs.GetInt("Dungeon.FullControl"), Is.EqualTo(value));
                    Assert.That(DungeonPreferences.FullControlOverride, Is.EqualTo(controlOverride));
                }
            }
            finally
            {
                DungeonPreferences.FullControlOverride = previousOverride;
                if (hadKey) PlayerPrefs.SetInt("Dungeon.FullControl", saved);
                else PlayerPrefs.DeleteKey("Dungeon.FullControl");
            }
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            if (!UnityEditor.SessionState.GetBool("EternalEnigma.Autoplay.ManualChecks",false))
                Assert.Ignore("Autoplay session checks require the manual Run Autoplay command.");
            hadTerminalPreference = PlayerPrefs.HasKey(TerminalMode.PreferenceKey);
            terminalPreference = PlayerPrefs.GetInt(TerminalMode.PreferenceKey);
            TerminalMode.SetRequested(false); // These tests click the 3D menu's developer controls.
            harness = new GameTestHarness(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (harness == null) yield break;
            try
            {
                if (AutoplayRunner.Active != null)
                {
                    var run = AutoplayRunner.Active; var report = run.Report; var path = Path.Combine(run.DirectoryPath,"report.json");
                    report.ValidationOnly = true; report.EligibleForBalance = false;
                    run.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null,"demo exit");
                    File.WriteAllText(path,JsonUtility.ToJson(report,true));
                }
                yield return harness.Cleanup();
            }
            finally
            {
                if (hadTerminalPreference) PlayerPrefs.SetInt(TerminalMode.PreferenceKey, terminalPreference);
                else PlayerPrefs.DeleteKey(TerminalMode.PreferenceKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator DebugMenuLaunchCanResumeWhileLoading()
        {
            using var input = new TestInputScope();
            var animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            try
            {
                yield return harness.LoadMainMenuDirect();
                var original = Common.Instance.GameSaveData;
                string saved = harness.Store.Json;
                var transition = Common.Instance.ScreenTransition;
                transition.TransitionTimeSeconds = 2;
                var mouse = InputSystem.AddDevice<Mouse>();
                var keyboard = InputSystem.AddDevice<Keyboard>();
                var developer = Object.FindFirstObjectByType<MainMenuDeveloperControls>();
                // The shipped scene hides the developer canvas; this opt-in fixture opens it at runtime.
                developer.Toggle.GetComponentInParent<Canvas>(true).gameObject.SetActive(true);
                developer.Toggle.onClick.Invoke();
                yield return null;
                var launch = developer.Controls.Select(control => control.GetComponent<Button>()).Where(button => button != null)
                    .Single(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                        .Any(i => button.onClick.GetPersistentMethodName(i) == nameof(MainMenu.DebugAutoplay_Clicked)));
                Assert.That(launch.IsActive() && launch.IsInteractable(), Is.True, ButtonState(launch));
                EventSystem.current.SetSelectedGameObject(launch.gameObject);
                yield return Click(mouse, launch);
                var run = AutoplayRunner.Active;
                Assert.That(run, Is.Not.Null, "The authored debug button must start autoplay.");
                run.Report.ValidationOnly = true;
                Assert.That(run.Options.Speed, Is.EqualTo(1));

                // Input during the initial fade used to pause time with the resume UI hidden underneath it.
                yield return new WaitForSecondsRealtime(.85f);
                Assert.That(transition.BlockScreen.activeSelf, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(run.ReturnPromptOpen, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                var panel = Object.FindFirstObjectByType<AutoplayPanel>();
                var backgroundHits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(5, 5) }, backgroundHits);
                Assert.That(backgroundHits[0].gameObject, Is.EqualTo(panel.gameObject),
                    "Autoplay must shield game UI while still accepting playback controls.");
                Directory.CreateDirectory("Temp/AutoplayValidation");
                ScreenCapture.CaptureScreenshot("Temp/AutoplayValidation/loading-prompt.png");
                yield return Click(mouse, panel.KeepWatching);
                Assert.That(run.ReturnPromptOpen, Is.False, "Keep watching must accept a real pointer click.");
                Assert.That(run.Paused, Is.False);

                transition.TransitionTimeSeconds = .1f;
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                    !transition.BlockScreen.activeSelf && run.Report.Actions > 1, "autoplay leaving the loading screen");
                yield return Click(mouse, panel.Pause);
                Assert.That(run.Paused, Is.True);
                yield return Click(mouse, panel.Hide);
                Assert.That(run.PanelVisible, Is.False);
                yield return Click(mouse, panel.Show);
                Assert.That(run.PanelVisible, Is.True);
                yield return Click(mouse, panel.Pause);
                Assert.That(run.Paused, Is.False);
                Assert.That(run.ReturnPromptOpen, Is.False, "Playback clicks must not open stop options.");
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Game>()?.IsReady == true &&
                    run.Report.DungeonTurns > 0 && !transition.BlockScreen.activeSelf, "debug autoplay taking dungeon turns");
                ScreenCapture.CaptureScreenshot("Temp/AutoplayValidation/debug-started.png");
                yield return null;
                run.ExitDemo();
                yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "debug launch cleanup");
                Assert.That(Common.Instance.GameSaveData, Is.SameAs(original));
                Assert.That(harness.Store.Json, Is.EqualTo(saved));
            }
            finally { DungeonPreferences.AnimationOverride = animation; }
        }

        private static string ButtonState(Button button) =>
            $"enabled={button.enabled}, interactable={button.interactable}/{button.IsInteractable()}, terminal={TerminalMode.Requested}/{TerminalMode.Effective}; " +
            string.Join(" > ", button.GetComponentsInParent<Transform>(true).Select(t => t.name + "=" + t.gameObject.activeSelf));

        private static IEnumerator Click(Mouse mouse, Button button)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject),
                button.name + " is covered by " + hits[0].gameObject.name);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator InputPromptCancelAndReturnPreserveOriginalSave()
        {
            yield return harness.LoadMainMenu(new TestScenario { Gold = 321 }.CreateSave());
            var original = Common.Instance.GameSaveData; string json = harness.Store.Json;
            Directory.CreateDirectory("Temp/AutoplayValidation");
            ScreenCapture.CaptureScreenshot("Temp/AutoplayValidation/main-menu.png");
            yield return null;
            AutoplayRunner.WatchDemo(new AutoplayOptions { Speed = 1 });
            var run = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true,"demo's separate campaign");
            run.Report.ValidationOnly = true;
            Assert.That(Common.Instance.GameSaveData,Is.Not.SameAs(original));
            Assert.That(harness.Store.Json,Is.EqualTo(json));
            run.SetPaused(true);
            yield return DioramaUICapture.Save(Object.FindFirstObjectByType<AutoplayPanel>().GetComponent<Canvas>(), "autoplay-panel");
            run.SetSpeed(4);
            CheckControlPreference(true);
            var settings = Common.Instance.GlobalSettings;
            settings.ShowDialog();
            yield return null;
            var control = settings.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(b => b.GetComponentInChildren<TMPro.TMP_Text>(true)?.text == "Full Control: Off (Autoplay)");
            Assert.That(control.interactable, Is.False);
            int savedControl = PlayerPrefs.GetInt("Dungeon.FullControl", 0);
            control.onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt("Dungeon.FullControl", 0), Is.EqualTo(savedControl));
            settings.Exit_Clicked();
            Assert.That(Time.timeScale, Is.Zero, "Changing speed keeps the demo paused.");
            run.SetSpeed(16);
            Assert.That(run.Options.Speed, Is.EqualTo(16));
            Assert.That(Time.timeScale, Is.Zero);
            run.SetPaused(false);
            Assert.That(Time.timeScale, Is.EqualTo(16));
            run.SetSpeed(.5f);
            Assert.That(Time.timeScale, Is.EqualTo(.5f));
            run.SetPaused(true);
            using (var input = new TestInputScope())
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                var mouse = InputSystem.AddDevice<Mouse>();
                var pad = InputSystem.AddDevice<Gamepad>();
                var touch = InputSystem.AddDevice<Touchscreen>();
                try
                {
                    yield return new WaitForSecondsRealtime(.85f);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True);
                    CheckControlPreference(true);
                    yield return DioramaUICapture.Save(Object.FindFirstObjectByType<AutoplayPanel>().GetComponent<Canvas>(), "autoplay-return-prompt");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    yield return null;
                    run.ConfirmReturn(false);
                    Assert.That(run.ReturnPromptOpen,Is.False);
                    Assert.That(run.Paused,Is.True,"Cancel restores the previous pause state.");
                    yield return new WaitForSecondsRealtime(.55f);
                    InputSystem.QueueStateEvent(mouse,new MouseState { delta = new Vector2(20,0) });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.False,"Pointer movement can reach playback controls.");
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = new Vector2(Screen.width-30, Screen.height-90), buttons = 1 });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.False,"Clicking the playback panel does not interrupt playback.");
                    var outsidePanel = new Vector2(5, Screen.height - 5);
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = outsidePanel });
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = outsidePanel, buttons = 1 });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True,"Clicking outside controls opens the return prompt.");
                    InputSystem.QueueStateEvent(mouse,new MouseState());
                    run.ConfirmReturn(false);
                    yield return new WaitForSecondsRealtime(.55f);
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True,"Controller input opens the return prompt.");
                    InputSystem.QueueStateEvent(pad,new GamepadState());
                    run.ConfirmReturn(false);
                    yield return new WaitForSecondsRealtime(.55f);
                    InputSystem.QueueStateEvent(touch,new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = outsidePanel });
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen,Is.True,"Touch opens the return prompt.");
                    InputSystem.QueueStateEvent(touch,new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended });
                    run.ConfirmReturn(false);
                    run.SetPaused(false); run.RequestReturn(); run.ConfirmReturn(false);
                    Assert.That(run.Paused,Is.False,"Cancel resumes a running demo.");
                    run.RequestReturn(); run.ConfirmReturn(true);
                    yield return harness.WaitUntil(() => AutoplayRunner.Active == null,"restore original session");
                    Assert.That(Common.Instance.GameSaveData,Is.SameAs(original));
                    Assert.That(harness.Store.Json,Is.EqualTo(json));
                    Assert.That(Common.Instance.GameSaveData.TownSaveData.Gold,Is.EqualTo(321));
                    yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true,"main menu restored");
                    var developer = Object.FindFirstObjectByType<MainMenuDeveloperControls>();
                    developer.Toggle.GetComponentInParent<Canvas>(true).gameObject.SetActive(true);
                    developer.Toggle.onClick.Invoke();
                    yield return null;
                    var debugButton = developer.Controls.Select(control => control.GetComponent<Button>()).Where(button => button != null)
                        .Single(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                            .Any(i => button.onClick.GetPersistentMethodName(i) == nameof(MainMenu.DebugAutoplay_Clicked)));
                    Assert.That(debugButton.IsActive() && debugButton.IsInteractable(), Is.True, ButtonState(debugButton));
                    debugButton.onClick.Invoke();
                    Assert.That(AutoplayRunner.Active, Is.Not.Null);
                    Assert.That(AutoplayRunner.Active.Options.DebugPlaythrough, Is.True);
                    Assert.That(AutoplayRunner.Active.Options.Godmode, Is.True);
                    Assert.That(AutoplayRunner.Active.Options.InfiniteResources, Is.True);
                }
                finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(touch); }
            }
        }

        [UnityTest]
        public IEnumerator HidePanelAndTakeControlPreserveSessionAndSave()
        {
            yield return harness.LoadMainMenu(new TestScenario { Gold = 321 }.CreateSave());
            var original = Common.Instance.GameSaveData;
            string json = harness.Store.Json;
            AutoplayRunner.WatchDemo(new AutoplayOptions { DebugPlaythrough = true, Speed = 8 });
            var run = AutoplayRunner.Active;
            run.Report.ValidationOnly = true;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Game>()?.IsReady == true, "demo dungeon");
            run.SetPaused(true);
            using (var input = new TestInputScope())
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F8));
                    yield return null; yield return null;
                    Assert.That(run.PanelVisible, Is.False);
                    Assert.That(run.ReturnPromptOpen, Is.False);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F8));
                    yield return null; yield return null;
                    Assert.That(run.PanelVisible, Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    var game = Game.Instance;
                    var party = game.Allies.ToArray();
                    int floor = game.PlayerController.Floor;
                    int actions = run.Report.Actions;
                    run.RequestReturn();
                    Assert.That(run.ReturnPromptOpen, Is.True);
                    yield return new WaitForSecondsRealtime(.25f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                    yield return null; yield return null;
                    Assert.That(run.ReturnPromptOpen, Is.False);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return harness.WaitUntil(() => run.PlayerControlled, "manual control at action boundary");
                    Assert.That(Game.Instance, Is.SameAs(game));
                    CollectionAssert.AreEquivalent(party, game.Allies);
                    Assert.That(game.PlayerController.Floor, Is.EqualTo(floor));
                    Assert.That(Time.timeScale, Is.EqualTo(1));
                    Assert.That(AutoplayRunner.BlocksPlayerInput, Is.False);
                    CheckControlPreference(false);
                    Assert.That(run.PanelVisible, Is.False);
                    Assert.That(run.Report.Outcome, Is.EqualTo("PlayerControl"));
                    Assert.That(AutoplayRunner.GodmodeFor(game.PlayerController.ControlledAlly), Is.False);
                    Assert.That(AutoplayRunner.InfiniteResourcesFor(game.PlayerController.ControlledAlly), Is.False);
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(run.Report.Actions, Is.EqualTo(actions));
                    yield return harness.WaitUntil(() => game.PlayerController.ControlledAlly.IsWaitingForPlayerInput &&
                        !game.NewFloorMessage.gameObject.activeSelf, "player command prompt");
                    yield return new WaitForSecondsRealtime(.3f);
                    var hero = game.PlayerController.ControlledAlly;
                    int hunger = hero.Vitals.HungerAccumulate;
                    var playerInput = Common.Instance.MenuInputHandler.PlayerInput;
                    playerInput.SwitchCurrentControlScheme(keyboard);
                    MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard };
                    Assert.That(playerInput.currentActionMap.name, Is.EqualTo("Player"));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Period));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return harness.WaitUntil(() => hero.Vitals.HungerAccumulate != hunger, "manual keyboard wait action");
                    yield return harness.WaitForIdle();
                    Assert.That(run.Report.Actions, Is.EqualTo(actions), "Manual commands do not restart the bot.");
                    SaveSystem.SaveData(Common.Instance.GameSaveData);
                    Assert.That(harness.Store.Json, Is.EqualTo(json), "Manual demo play remains isolated.");
                    Directory.CreateDirectory("Temp/AutoplayValidation");
                    ScreenCapture.CaptureScreenshot("Temp/AutoplayValidation/manual-control.png");
                    yield return null; yield return null;
                    Assert.That(Common.Instance.Travel.ReturnToMenu(), Is.True);
                    yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "manual session exit");
                    CheckControlPreference(false);
                    Assert.That(Common.Instance.GameSaveData, Is.SameAs(original));
                    Assert.That(harness.Store.Json, Is.EqualTo(json));
                }
                finally { InputSystem.RemoveDevice(keyboard); }
            }
        }

        [UnityTest]
        public IEnumerator NormalModeAllowsDefeatAndWritesTuningReport()
        {
            yield return harness.LoadMainMenu(null);
            AutoplayRunner.WatchDemo(new AutoplayOptions { DebugPlaythrough = false, Godmode = true, InfiniteResources = true });
            var run = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Game>()?.IsReady == true,"normal demo dungeon");
            Assert.That(run.Report.EligibleForBalance,Is.True,"Unmodified normal runs are eligible for tuning.");
            run.Report.ValidationOnly = true;
            run.SetPaused(true);
            int before = harness.Ally.Vitals.HP;
            harness.Ally.Vitals.HP = 0;
            Assert.That(harness.Ally.Vitals.HP,Is.Zero);
            Assert.That(run.Report.DamageTaken,Is.EqualTo(before));
            Assert.That(AutoplayRunner.InfiniteResourcesFor(harness.Ally),Is.False);
            run.SetPaused(false);
            yield return harness.WaitUntil(() => !run.Running,"automatic defeat detection");
            CheckControlPreference(true);
            var report = JsonUtility.FromJson<AutoplayReport>(File.ReadAllText(Path.Combine(run.DirectoryPath,"report.json")));
            Assert.That(report.Outcome,Is.EqualTo("Defeat"));
            Assert.That(report.EligibleForBalance,Is.False,"Synthetic lethal damage must be excluded from tuning data.");
            Assert.That(report.Party[0].HP,Is.Zero);
        }

        [UnityTest]
        public IEnumerator DebugProtectsOnlyPartyAndRestoresResourceRulesOnExit()
        {
            yield return harness.LoadDungeon(new TestScenario());
            string original = harness.Store.Json;
            AutoplayRunner.WatchDemo(new AutoplayOptions { DebugPlaythrough = true });
            var run = AutoplayRunner.Active; run.SetPaused(true);
            run.Report.ValidationOnly = true;
            harness.Ally.Vitals.HP = 0; harness.Ally.Vitals.SP = 0; harness.Ally.Vitals.Hunger = 0;
            Assert.That(harness.Ally.Vitals.HP,Is.EqualTo(harness.Ally.FinalStats.HPMax));
            Assert.That(harness.Ally.Vitals.SP,Is.EqualTo(harness.Ally.FinalStats.SPMax));
            Assert.That(harness.Ally.Vitals.Hunger,Is.EqualTo(harness.Ally.FinalStats.HungerMax));
            Assert.That(run.Report.EligibleForBalance,Is.False);
            int strength = harness.Ally.FinalStats.Strength;
            var dungeon = harness.Game.CurrentDungeon;
            // Ranged attacks trace a straight line; use an adjacent clear cell.
            var free = new[]{Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down}.Select(offset=>harness.Ally.TilemapPosition+offset)
                .First(p => dungeon.IsWalkable(p) && !harness.Game.AllCharacters.Any(c => c.ToBounds().Contains(p)));
            yield return harness.SpawnEnemy("Enemy_Slime",free);
            var enemy = harness.Game.Enemies.Last();
            AttackAction.GetAttackDamage(harness.Ally,enemy,out bool hit,out int damage);
            Assert.That(hit,Is.True); Assert.That(damage,Is.EqualTo(enemy.Vitals.HP));
            var ranged = new RangedAttackAction(harness.Ally,enemy,1,null).ExecuteImmediate(harness.Ally).OfType<TakeDamageAction>().Single();
            Assert.That(ranged.damage,Is.EqualTo(enemy.Vitals.HP));
            Assert.That(harness.Ally.FinalStats.Strength,Is.EqualTo(strength),"Infinite strength must not alter saved/base stats.");
            var stats = new Stats { HPMax = 10, SPMax = 4, HungerMax = 10 };
            var unrelated = new Vitals { LinkedStats = () => stats }; unrelated.HP = 0;
            Assert.That(unrelated.HP,Is.Zero,"Godmode cannot protect enemy/unrelated vitals.");
            run.Options.Godmode = false; run.Options.InfiniteResources = false;
            harness.Ally.Vitals.HP = 3; harness.Ally.Vitals.SP = 0;
            Assert.That(harness.Ally.Vitals.HP,Is.EqualTo(3));
            Assert.That(harness.Ally.Vitals.SP,Is.Zero);
            run.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null,"debug cleanup");
            Assert.That(harness.Store.Json,Is.EqualTo(original));
        }
    }
}
#endif
