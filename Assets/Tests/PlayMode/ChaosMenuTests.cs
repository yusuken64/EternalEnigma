#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class ChaosMenuTests
    {
        GameTestHarness harness;
        ChaosInputDevices devices;
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness(); devices = new ChaosInputDevices(); yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() { yield return harness.Cleanup(); devices.Dispose(); }
        IEnumerator Input(ChaosInput input)
        {
            if (input.Device == "Mouse") { devices.Send(new ChaosInput { Device = "Mouse", Position = input.Position }); yield return null; yield return null; }
            devices.Send(input); yield return null; yield return null;
            devices.Release(); yield return null; yield return null;
        }
        [UnityTest] public IEnumerator RawFixtureContinuesThroughKeyboardSlotInput() => ContinueFixture("Keyboard");
        [UnityTest] public IEnumerator RawFixtureContinuesThroughControllerSlotInput() => ContinueFixture("Controller");
        [UnityTest] public IEnumerator RawFixtureContinuesThroughMouseSlotInput() => ContinueFixture("Mouse");
        [UnityTest] public IEnumerator TerminalFixtureContinuesThroughKeyboardSlotInput() => ContinueFixture("Keyboard", true);
        [UnityTest] public IEnumerator TerminalFixtureContinuesThroughControllerSlotInput() => ContinueFixture("Controller", true);
        IEnumerator ContinueFixture(string device, bool terminal = false)
        {
            using var preferences = GamePreferences.Isolate();
            yield return harness.LoadMainMenu(null);
            if (terminal)
            {
                yield return Input(ChaosInput.Key(Key.F11));
                yield return harness.WaitUntil(() => TerminalMode.Effective, "terminal mode through input");
                // A chaos fixture reload preserves the chosen presentation mode.
                yield return SceneManager.LoadSceneAsync("MainMenu");
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true &&
                    !Common.Instance.ScreenTransition.BlocksInput && TerminalMode.Effective, "terminal fixture menu reload");
            }
            var fixture = ChaosFixtures.Create(ChaosRandom.Derive(1352076509, "Town"),
                TownSceneLoader.Default, Common.Instance.ItemManager);
            fixture.Validate();
            harness.Store.Write(0, fixture.SaveJson);
            SaveSystem.ActiveSlot = 0;
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(menu.StartButton),
                "Continue must be reached by navigation from the default Start selection.");
            yield return ActivateThroughInput(menu.ContinueButton.GetComponent<Selectable>(), device);
            var slots = menu.CampaignStartMenu.SlotViewer.GetComponent<CampaignSlots>();
            yield return harness.WaitUntil(() => slots.isActiveAndEnabled && MenuUIInputModule.Active?.OwnsFocus(slots) == true,
                "chaos campaign slot browser");
            Assert.That(slots.Slots[0].Data, Is.Not.Null, "Raw chaos fixture must be readable without SaveData restamping it.");
            Assert.That(slots.Slots[0].Data.FormatVersion, Is.EqualTo(SaveSystem.CurrentFormatVersion));
            Assert.That(slots.Slots[0].Data.HasCampaign, Is.True);
            yield return ActivateThroughInput(slots.Slots[0].Button, device);
            yield return harness.WaitUntil(() => SceneManager.GetActiveScene().name == "Town" && ChaosSnapshot.Read().CanControl,
                "chaos fixture Town load through real slot input");
            Assert.That(Common.Instance.GameSaveData.ProtagonistId, Is.EqualTo(fixture.ControlledHeroId));
            Assert.That(Common.Instance.CampaignContext.State.Identity,
                Is.EqualTo(JsonUtility.FromJson<GameSaveData>(fixture.SaveJson).Campaign.Identity));
        }
        IEnumerator ActivateThroughInput(Selectable target, string device)
        {
            Assert.That(target.IsActive() && target.IsInteractable(), Is.True);
            if (device == "Mouse")
            {
                if (EventSystem.current.currentSelectedGameObject != target.gameObject &&
                    target is SelectToActivateButton button && !button.ActivateOnFirstPointerPress)
                    yield return Input(ChaosInput.Click(ChaosUI.Center(target)));
                yield return Input(ChaosInput.Click(ChaosUI.Center(target)));
                yield break;
            }
            yield return NavigateThroughInput(target, device);
            yield return Input(device == "Controller" ? ChaosInput.Pad(GamepadButton.South) : ChaosInput.Key(Key.Enter));
        }
        IEnumerator NavigateThroughInput(Selectable target, string device)
        {
            for (int i = 0; i < 20 && EventSystem.current.currentSelectedGameObject != target.gameObject; i++)
            {
                string direction = ChaosUI.NextDirection(EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>(), target);
                if (device == "Controller")
                {
                    var button = direction switch { "UpArrow" => GamepadButton.DpadUp, "DownArrow" => GamepadButton.DpadDown,
                        "LeftArrow" => GamepadButton.DpadLeft, "RightArrow" => GamepadButton.DpadRight,
                        _ => throw new System.InvalidOperationException("Unexpected navigation direction: " + direction) };
                    yield return Input(ChaosInput.Pad(button));
                }
                else yield return Input(ChaosInput.Key((Key)System.Enum.Parse(typeof(Key), direction)));
            }
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(target.gameObject));
        }
        [UnityTest] public IEnumerator ControllerSubmitAliasesCannotActivateExcludedControls()
        {
            using var preferences = GamePreferences.Isolate();
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var exit = menu.StartButton.transform.parent.GetComponentsInChildren<Button>()
                .Single(button => ChaosUI.Label(button) == "Exit");
            int activations = 0;
            exit.onClick.AddListener(() => activations++);
            yield return NavigateThroughInput(exit, "Keyboard");

            var host = new GameObject("chaos excluded control regression");
            host.SetActive(false);
            var runner = host.AddComponent<ChaosRunner>();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(string name, object value) => typeof(ChaosRunner).GetField(name, fields).SetValue(runner, value);
            using var inputBytes = new MemoryStream();
            using var checkpointBytes = new MemoryStream();
            using var inputWriter = new StreamWriter(inputBytes, Encoding.UTF8, 1024, true) { AutoFlush = true };
            using var checkpointWriter = new StreamWriter(checkpointBytes, Encoding.UTF8, 1024, true) { AutoFlush = true };
            typeof(ChaosRunner).GetProperty("Report").SetValue(runner, new ChaosReport());
            Set("devices", devices); Set("inputs", inputWriter); Set("checkpoints", checkpointWriter); Set("prepared", true);
            var pulse = typeof(ChaosRunner).GetMethod("Pulse", fields);
            try
            {
                foreach (string alias in new[] { "South", "A", "Cross" })
                {
                    var input = new ChaosInput { Device = "Controller", Buttons = new[] { alias } };
                    yield return (IEnumerator)pulse.Invoke(runner, new object[] { input, null });
                    Assert.That(activations, Is.Zero, alias + " must not activate Exit.");
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(exit.gameObject));
                }
                var recorded = Encoding.UTF8.GetString(inputBytes.ToArray()).Trim('\uFEFF', '\r', '\n').Split('\n')
                    .Select(JsonUtility.FromJson<ChaosInput>).ToArray();
                Assert.That(recorded.Length, Is.EqualTo(3));
                Assert.That(recorded.All(input => input.Label == "excluded-control" && input.Buttons.Length == 0), Is.True,
                    "Replay must receive the same neutral replacements that were dispatched.");

                yield return NavigateThroughInput(menu.ContinueButton.GetComponent<Selectable>(), "Keyboard");
                yield return (IEnumerator)pulse.Invoke(runner, new object[] { ChaosInput.Pad(GamepadButton.South), null });
                Assert.That(menu.CampaignStartMenu.SlotViewer.activeInHierarchy, Is.True,
                    "The same serialized controller submit must still activate permitted controls.");
            }
            finally
            {
                Set("disposed", true); // Harness owns devices and save isolation.
                Object.DestroyImmediate(host);
            }
        }
        [UnityTest] public IEnumerator MainMenuSettingsRemainReadyForChaosInput()
        {
            using var preferences = GamePreferences.Isolate();
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var options = menu.StartButton.transform.parent.GetComponentsInChildren<Button>()
                .Single(button => ChaosUI.Label(button) == "Options");
            yield return ActivateThroughInput(options, "Keyboard");
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            Assert.That(menu.gameObject.activeInHierarchy, Is.False, "Options suspends the menu controller.");
            Assert.That(menu.IsReady, Is.True);
            var snapshot = ChaosSnapshot.Read();
            Assert.That(snapshot.Scene, Is.EqualTo("MainMenu"));
            Assert.That(snapshot.Settings, Is.True);
            Assert.That(snapshot.Loading, Is.False);
            Assert.That(snapshot.Ready, Is.True, "An open settings dialog must not be mistaken for a scene load.");
            Assert.That(snapshot.CanControl, Is.True, "Chaos must keep sending input to the live settings dialog.");
            yield return ActivateThroughInput(Common.Instance.GlobalSettings.ResumeButton, "Keyboard");
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            Assert.That(menu.gameObject.activeInHierarchy, Is.True);
            Assert.That(ChaosSnapshot.Read().CanControl, Is.True);
        }
        [UnityTest] public IEnumerator FixtureBoundaryRecordsSettingsCloseBeforeReloadingDungeon()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            yield return Input(ChaosInput.Key(Key.Escape));
            var settings = Common.Instance.GlobalSettings;
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(MenuUIInputModule.Active.OwnsFocus(settings), Is.True);

            // Exercise the real boundary and Pulse recorder without starting a
            // background smoke session. The inactive host never runs Start().
            var host = new GameObject("chaos fixture boundary regression");
            host.SetActive(false);
            var runner = host.AddComponent<ChaosRunner>();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(string name, object value) => typeof(ChaosRunner).GetField(name, fields).SetValue(runner, value);
            using var inputBytes = new MemoryStream();
            using var checkpointBytes = new MemoryStream();
            using var observationBytes = new MemoryStream();
            using var inputWriter = new StreamWriter(inputBytes, Encoding.UTF8, 1024, true) { AutoFlush = true };
            using var checkpointWriter = new StreamWriter(checkpointBytes, Encoding.UTF8, 1024, true) { AutoFlush = true };
            using var observationWriter = new StreamWriter(observationBytes, Encoding.UTF8, 1024, true) { AutoFlush = true };
            var options = new ChaosOptions { ActionTimeoutSeconds = 10, RecoverySeconds = 2 };
            var report = new ChaosReport { Options = options, Coverage = new ChaosCoverage() };
            string artifacts = Path.Combine("Temp/Chaos", "boundary-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(artifacts);
            typeof(ChaosRunner).GetProperty("Options").SetValue(runner, options);
            typeof(ChaosRunner).GetProperty("Report").SetValue(runner, report);
            typeof(ChaosRunner).GetProperty("DirectoryPath").SetValue(runner, Path.GetFullPath(artifacts));
            Set("devices", devices); Set("inputs", inputWriter); Set("checkpoints", checkpointWriter);
            Set("observations", observationWriter); Set("prepared", true); Set("segment", artifacts);
            try
            {
                yield return (IEnumerator)typeof(ChaosRunner).GetMethod("CompleteSegment", fields)
                    .Invoke(runner, new object[] { "Keyboard" });
                Assert.That(settings.IsOpen, Is.False);
                Assert.That(report.Inputs, Is.GreaterThan(0), "Boundary cleanup must travel through recorded real input.");
                Assert.That(report.Issues, Is.Empty);
                Assert.That(typeof(ChaosRunner).GetField("prepared", fields).GetValue(runner), Is.False);
                Assert.That(typeof(ChaosRunner).GetField("inputs", fields).GetValue(runner), Is.Null);
                string[] inputs = Encoding.UTF8.GetString(inputBytes.ToArray()).Trim('\uFEFF', '\r', '\n').Split('\n');
                string[] checkpoints = Encoding.UTF8.GetString(checkpointBytes.ToArray()).Trim('\uFEFF', '\r', '\n').Split('\n');
                Assert.That(inputs.Length, Is.EqualTo(report.Inputs));
                Assert.That(checkpoints.Length, Is.EqualTo(report.Inputs));
                Assert.That(inputs.Any(line => JsonUtility.FromJson<ChaosInput>(line).Keys.Contains("Enter")), Is.True);
            }
            finally
            {
                // This test owns the shared devices and harness save isolation.
                Set("disposed", true);
                Object.DestroyImmediate(host);
            }

            CampaignParty.ClearLiveParty(Common.Instance);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            Common.Instance.CampaignContext = null; Common.Instance.GameSaveData = null;
            Common.Instance.CurrentTownConfiguration = null;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlocksInput, "next fixture menu");
            Assert.That(settings.IsOpen, Is.False);
            var fixture = ChaosFixtures.Create(ChaosRandom.Derive(965499332, "DungeonScene"),
                TownSceneLoader.Default, Common.Instance.ItemManager, "DungeonScene");
            harness.Store.Write(0, fixture.SaveJson); SaveSystem.ActiveSlot = 0;
            var menu = Object.FindFirstObjectByType<MainMenu>();
            yield return ActivateThroughInput(menu.ContinueButton.GetComponent<Selectable>(), "Keyboard");
            var slots = menu.CampaignStartMenu.SlotViewer.GetComponent<CampaignSlots>();
            yield return ActivateThroughInput(slots.Slots[0].Button, "Keyboard");
            yield return harness.WaitUntil(() => SceneManager.GetActiveScene().name == "DungeonScene" && ChaosSnapshot.Read().CanControl,
                "next dungeon fixture control");
            Assert.That(settings.IsOpen, Is.False, "Persistent settings must not leak across fixture scenes.");
            // PlayerController's command debounce advances only after the cover
            // releases. CanControl describes scene/turn readiness, not that .2s timer.
            yield return new WaitForSeconds(.25f);
            var playerInput = JuicyChickenGames.Menu.PlayerInputHandler.Instance.PlayerInput;
            Assert.That(playerInput.currentActionMap.name, Is.EqualTo("Player"));
            Assert.That(playerInput.actions["Options"].enabled, Is.True);
            yield return Input(ChaosInput.Key(Key.Escape));
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(MenuUIInputModule.Active.OwnsFocus(settings), Is.True, "The new scene must own the settings scope.");
            yield return ActivateThroughInput(settings.ResumeButton, "Keyboard");
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(ChaosSnapshot.Read().CanControl, Is.True);
        }
        [UnityTest] public IEnumerator NestedInventoryAndSettingsReturnThroughMixedDeviceInputs()
        {
            yield return harness.LoadTown(new TestScenario { Items = new[] { "Bread" } }.CreateSave());
            yield return Input(ChaosInput.Key(Key.Q));
            Assert.That(MenuUIInputModule.Active.HasDialog, Is.True);
            yield return Input(ChaosInput.Pad(GamepadButton.South));
            Assert.That(Object.FindFirstObjectByType<TownMenuManager>().DialogStack.Count, Is.GreaterThanOrEqualTo(2));
            yield return Input(ChaosInput.Pad(GamepadButton.Start));
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            var resume = Common.Instance.GlobalSettings.ResumeButton;
            yield return Input(ChaosInput.Click(ChaosUI.Center(resume))); // select-to-activate widgets may need two clicks
            if (Common.Instance.GlobalSettings.IsOpen) yield return Input(ChaosInput.Click(ChaosUI.Center(resume)));
            var pointer = new PointerEventData(EventSystem.current) { position = ChaosUI.Center(resume) };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False, $"resume={ChaosUI.Path(resume.transform)} center={ChaosUI.Center(resume)} mouse={devices.Mouse.position.ReadValue()} lock={Cursor.lockState} selected={ChaosUI.Path(EventSystem.current.currentSelectedGameObject?.transform)} hits={string.Join(",", hits.Take(5).Select(h => ChaosUI.Path(h.gameObject.transform)))}");
            var manager = Object.FindFirstObjectByType<TownMenuManager>();
            for (int i = 0; i < 6 && manager.Opened; i++) yield return Input(ChaosInput.Key(Key.Escape));
            Assert.That(manager.Opened, Is.False);
            Assert.That(harness.Store.Writes, Is.GreaterThanOrEqualTo(1));
        }
        [UnityTest] public IEnumerator MissileTargetCancelPreservesCostsAndRestoresControl()
        {
            yield return harness.LoadDungeon(new TestScenario { Skills = new[] { "Fire Bolt" }, Mana = 50 });
            var actor = harness.Ally; int before = actor.Vitals.Mana;
            yield return Input(ChaosInput.Pad(GamepadButton.LeftShoulder));
            var menu = MenuManager.Instance.PartyMenu;
            var target = menu.EntryButtons.Single(b => ChaosUI.Label(b).StartsWith("Fire Bolt", System.StringComparison.Ordinal));
            for (int i = 0; i < 15 && EventSystem.current.currentSelectedGameObject != target.gameObject; i++) yield return Input(ChaosInput.Pad(GamepadButton.DpadDown));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(target.gameObject));
            yield return Input(ChaosInput.Pad(GamepadButton.South));
            Assert.That(harness.Game.PlayerController.CurrentControlMode, Is.EqualTo(PlayerControlMode.TargetSelecting));
            yield return Input(ChaosInput.Key(Key.Escape));
            for (int i = 0; i < 6 && MenuManager.Instance.Opened; i++) yield return Input(ChaosInput.Key(Key.Escape));
            Assert.That(actor.Vitals.Mana, Is.EqualTo(before));
            Assert.That(harness.Game.PlayerController.CurrentControlMode, Is.Not.EqualTo(PlayerControlMode.TargetSelecting));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
        }
    }
}
#endif
