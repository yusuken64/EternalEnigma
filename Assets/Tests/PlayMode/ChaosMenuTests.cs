#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

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
