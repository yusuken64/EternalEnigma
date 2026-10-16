#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TerminalDungeonAuditTests
    {
        GameTestHarness harness;
        TestInputScope inputs;
        System.IDisposable preferences;
        DungeonAnimationMode? animation;
        bool? fullControl;
        Keyboard keyboard;
        Gamepad pad;
        Mouse mouse;
        UnityEditor.EditorWindow view;
        int originalSize;
        Skill testSpell;
        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        TerminalScreen ScreenView => Common.Instance.GetComponentInChildren<TerminalScreen>(true);
        TMP_Text Ascii => ScreenView.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "ASCII");

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope(); preferences = GamePreferences.Isolate();
            animation = DungeonPreferences.AnimationOverride; fullControl = DungeonPreferences.FullControlOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            DungeonPreferences.FullControlOverride = false;
            TerminalMode.SetRequested(false);
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            originalSize = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { Seed = 12345, AdditionalAllies = new[] { "Avery", "Reese", "Sage" }, IncludeStartingItems = true, Skills = new[] { "Damage", "Fire Bolt" } });
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            foreach (var ally in harness.Game.Allies) ally.AllyStrategy = AllyStrategy.HoldPosition;
            keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>(); mouse = InputSystem.AddDevice<Mouse>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, pad, mouse };
            Directory.CreateDirectory("Temp/TerminalAudit/Dungeon");
            yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            TerminalMode.SetRequested(false);
            yield return harness.Cleanup();
            if (testSpell != null) Object.DestroyImmediate(testSpell);
            inputs.Dispose(); preferences.Dispose();
            DungeonPreferences.AnimationOverride = animation; DungeonPreferences.FullControlOverride = fullControl;
            if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, originalSize);
        }

        [UnityTest] public IEnumerator DungeonDockTargetAndHistoryFitAllSizesAndRestorePresentation()
        {
            var manager = MenuManager.Instance; var actor = harness.Ally;
            manager.OpenPartyMenu(PartyMenuTab.Inventory); yield return null;
            var menu = manager.PartyMenu;
            var nativeFont = menu.HeroText.font;
            var nativePanel = menu.Panel.GetComponent<Image>().sprite;
            var options = Object.FindFirstObjectByType<DungeonHud>().OptionsButton;
            var optionsLabel = options.GetComponentInChildren<TMP_Text>();
            var nativeOptionsFont = optionsLabel.font;
            var targetPrompt = manager.TargetDialog.SelectTargetPrompt;
            var promptLabel = targetPrompt.GetComponentInChildren<TMP_Text>(true);
            var promptPanel = targetPrompt.GetComponent<Image>();
            var nativePromptFont = promptLabel.font;
            var nativePromptPanel = promptPanel.sprite;
            manager.CloseAllMenus(); yield return null;
            int hunger = actor.Vitals.HungerAccumulate, sp = actor.Vitals.SP, writes = harness.Store.Writes;
            TerminalMode.SetRequested(true); yield return new WaitForSecondsRealtime(.25f);
            foreach (var size in new[] { new Vector2(960, 600), new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) })
            {
                view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { size, "Terminal dungeon " + size }); view.Repaint();
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(TerminalMode.Effective, Is.True);
                Assert.That(Ascii.text, Does.Contain("@"));
                Assert.That(optionsLabel.font, Is.SameAs(ScreenView.FontAsset), "Persistent controls use the terminal font too.");
                yield return Capture("hud", size);
                manager.OpenPartyMenu(PartyMenuTab.Inventory); yield return new WaitForSecondsRealtime(.2f);
                foreach (var tab in new[] { PartyMenuTab.Inventory, PartyMenuTab.Equipment, PartyMenuTab.Skills, PartyMenuTab.Stats })
                {
                    menu.SwitchTab(tab); yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(menu.HeroText.text, Is.EqualTo(GameplayText.Format(actor.CharacterName)));
                    Assert.That(menu.HeroText.font, Is.SameAs(ScreenView.FontAsset));
                    Assert.That(menu.GetComponent<Canvas>().enabled, Is.True);
                    Assert.That(menu.Panel.GetComponentsInChildren<DungeonDialogClose>().Length, Is.EqualTo(1));
                    var mapBounds = Bounds(ScreenView.ContentRect);
                    var dockBounds = Bounds(menu.Panel);
                    Assert.That(mapBounds.xMax, Is.LessThanOrEqualTo(dockBounds.xMin + 1), "Map text must end before the party dock.");
                    Assert.That(mapBounds.xMin, Is.GreaterThanOrEqualTo(Screen.width * .29f), "Retained party cards have their own column.");
                    yield return Capture(tab.ToString().ToLowerInvariant(), size);
                }
                menu.SwitchTab(PartyMenuTab.Inventory); yield return null;
                var first = menu.EntryButtons[0]; first.Select(); yield return null;
                yield return KeyPress(Key.Enter);
                Assert.That(manager.CurrentDialog, Is.SameAs(manager.ActionDialog), "Terminal submit opens the production item commands.");
                yield return Capture("item-actions", size);
                yield return KeyPress(Key.Escape);
                Assert.That(manager.CurrentDialog, Is.SameAs(menu));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(first.gameObject));

                menu.SwitchTab(PartyMenuTab.Skills); yield return null;
                var openDirection = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down }
                    .First(direction => harness.Game.CurrentDungeon.IsWalkable(actor.TilemapPosition + direction));
                actor.SetFacingByTargetPosition(actor.TilemapPosition + openDirection);
                manager.OpenTargetingMenu(actor, actor.Skills.Single(skill => skill.SkillName == "Fire Bolt"));
                yield return new WaitForSecondsRealtime(.25f);
                var target = manager.TargetDialog;
                Assert.That(target.TerminalMissilePath.Any(), Is.True);
                Assert.That(promptLabel.font, Is.SameAs(ScreenView.FontAsset), "The production targeting prompt shares the terminal presentation.");
                Assert.That(promptPanel.sprite, Is.Null, "Targeting cannot retain a parchment panel over the terminal HUD.");
                promptLabel.ForceMeshUpdate();
                Assert.That(promptLabel.isTextOverflowing, Is.False, "Target controls remain visible at every captured window size.");
                Ascii.ForceMeshUpdate();
                Assert.That(Ascii.GetParsedText(), Does.Contain("Fire Bolt"), "The live runtime must discover the initially inactive target dialog.");
                Assert.That(Ascii.GetParsedText(), Does.Contain("cancel"), "A narrow target panel retains the final cancel instruction.");
                yield return Capture("targeting", size);
                yield return KeyPress(Key.Escape);
                Assert.That(manager.CurrentDialog, Is.SameAs(menu));
                manager.CloseAllMenus(); yield return null;

                GameMessages.Post("Terminal audit history remains accessible from gameplay.");
                yield return KeyPress(Key.H);
                Assert.That(manager.CurrentDialog, Is.TypeOf<EventHistoryDialog>(), "H opens the same production history reader as Options.");
                var history = (EventHistoryDialog)manager.CurrentDialog;
                history.Setup(Enumerable.Range(0, 60).Select(i => "Event " + i + ": the party found a path through the dungeon.").ToArray());
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(history.Entries.font, Is.SameAs(ScreenView.FontAsset));
                history.Reader.Select(); yield return null;
                yield return KeyPress(Key.UpArrow);
                Assert.That(history.scrollView.verticalNormalizedPosition, Is.GreaterThan(0), "Keyboard navigation scrolls the actual history.");
                yield return Capture("history", size);
                yield return Click(history.Back);
                Assert.That(manager.Opened, Is.False);
            }
            Assert.That(actor.Vitals.HungerAccumulate, Is.EqualTo(hunger));
            Assert.That(actor.Vitals.SP, Is.EqualTo(sp));
            Assert.That(harness.Store.Writes, Is.EqualTo(writes), "Browsing and mode changes never save implicitly.");
            manager.OpenPartyMenu(PartyMenuTab.Inventory); yield return null;
            TerminalMode.SetRequested(false); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(menu.HeroText.font, Is.SameAs(nativeFont));
            Assert.That(menu.Panel.GetComponent<Image>().sprite, Is.SameAs(nativePanel));
            Assert.That(optionsLabel.font, Is.SameAs(nativeOptionsFont));
            Assert.That(promptLabel.font, Is.SameAs(nativePromptFont));
            Assert.That(promptPanel.sprite, Is.SameAs(nativePromptPanel));
        }

        [UnityTest] public IEnumerator KeyboardGamepadAndTerminalMouseTargetWithoutLeakingHudClicks()
        {
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { new Vector2(1280, 720), "Terminal input 1280x720" });
            view.Repaint();
            yield return new WaitForSecondsRealtime(.25f);
            var manager = MenuManager.Instance; var actor = harness.Ally;
            // A zero-effect instant spell isolates real command/SP accounting from combat RNG.
            testSpell = Object.Instantiate(actor.Skills.Single(skill => skill.SkillName == "Fire Bolt"));
            testSpell.CastTime = 0; testSpell.SPCost = 1; testSpell.ActionEffects.Clear(); testSpell.AreaRadius = 0;
            actor.Skills.Add(testSpell);
            actor.BaseStats.SPMax = 30; actor.InvalidateCachedStats(); actor.Vitals.SP = 30; actor.SyncDisplayedStats();
            TerminalMode.SetRequested(true); yield return new WaitForSecondsRealtime(.25f);
            int sp = actor.Vitals.SP, hunger = actor.Vitals.HungerAccumulate;
            manager.OpenPartyMenu(PartyMenuTab.Skills); yield return null;
            var menu = manager.PartyMenu;
            var target = manager.TargetDialog;
            manager.OpenTargetingMenu(actor, testSpell); yield return new WaitForSecondsRealtime(.2f);
            yield return KeyPress(Key.LeftArrow);
            Assert.That(target.Direction, Is.EqualTo(Vector3Int.left));
            yield return KeyPress(Key.Escape);
            Assert.That(manager.CurrentDialog, Is.SameAs(menu));
            manager.OpenTargetingMenu(actor, testSpell); yield return new WaitForSecondsRealtime(.2f);
            yield return PadPress(GamepadButton.DpadRight);
            Assert.That(target.Direction, Is.EqualTo(Vector3Int.right));
            yield return PadPress(GamepadButton.East);
            Assert.That(manager.CurrentDialog, Is.SameAs(menu));
            Assert.That(actor.Vitals.SP, Is.EqualTo(sp));
            Assert.That(actor.Vitals.HungerAccumulate, Is.EqualTo(hunger));

            manager.OpenTargetingMenu(actor, testSpell);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(400, 200), buttons = 1 });
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(manager.CurrentDialog, Is.SameAs(target), "An opening click held into targeting cannot cast.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(400, 200) }); yield return null; yield return null;
            Assert.That(TerminalRuntime.TryGetCellScreenPosition(actor.TilemapPosition + Vector3Int.left, out var left), Is.True);
            Assert.That(TerminalRuntime.TryGetMapCell(left, out var leftCell), Is.True, "The center of a terrain cell accepts the pointer even when its glyph is a dot or blank.");
            Assert.That(leftCell, Is.EqualTo(actor.TilemapPosition + Vector3Int.left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = left }); yield return null; yield return null;
            Assert.That(target.Direction, Is.EqualTo(Vector3Int.left), "Mouse hover uses the displayed ASCII cell, not the hidden 3D camera.");
            var options = Object.FindFirstObjectByType<DungeonHud>().OptionsButton;
            var point = Center((RectTransform)options.transform);
            yield return Pointer(point, 1); yield return Pointer(point, 0);
            Assert.That(manager.CurrentDialog, Is.SameAs(target));
            Assert.That(target.Direction, Is.EqualTo(Vector3Int.left));
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False, "A HUD hit cannot cast or activate Options underneath targeting.");
            yield return Pointer(point, 2); yield return Pointer(point, 0);
            Assert.That(manager.CurrentDialog, Is.SameAs(menu));
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False, "Cancellation cannot replay the earlier Options click.");
            Assert.That(actor.Vitals.SP, Is.EqualTo(sp));
            var selected = EventSystem.current.currentSelectedGameObject;
            yield return Click(options);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            yield return Click(Common.Instance.GlobalSettings.ResumeButton);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            Assert.That(manager.CurrentDialog, Is.SameAs(menu));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected));

            // Each control scheme confirms through the ordinary validated SkillAction.
            for (int scheme = 0; scheme < 3; scheme++)
            {
                manager.OpenTargetingMenu(actor, testSpell); yield return new WaitForSecondsRealtime(.2f);
                if (scheme == 0) { yield return KeyPress(Key.RightArrow); yield return KeyPress(Key.Enter); }
                else if (scheme == 1) { yield return PadPress(GamepadButton.DpadRight); yield return PadPress(GamepadButton.South); }
                else
                {
                    Assert.That(TerminalRuntime.TryGetCellScreenPosition(actor.TilemapPosition + Vector3Int.right, out var right), Is.True);
                    yield return Pointer(right, 0); yield return Pointer(right, 1);
                    yield return new WaitForSecondsRealtime(.2f);
                    yield return Pointer(right, 0);
                }
                yield return harness.WaitForIdle(); yield return new WaitForSecondsRealtime(.25f);
                Assert.That(manager.Opened, Is.False);
                Assert.That(actor.Vitals.SP, Is.EqualTo(sp - scheme - 1), "Each confirmation spends the spell's SP exactly once.");
                Assert.That(actor.Vitals.HungerAccumulate, Is.EqualTo(hunger + scheme + 1), "Each confirmation spends exactly one action.");
            }
        }

        IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        IEnumerator PadPress(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; yield return null;
        }
        IEnumerator Pointer(Vector2 point, ushort buttons)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = buttons }); yield return null; yield return null;
        }
        IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases(); var point = Center((RectTransform)button.transform);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(button.gameObject), "The visible control must own the actual pointer hit.");
            yield return Pointer(point, 0); yield return Pointer(point, 1); yield return Pointer(point, 0);
        }
        static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]); var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        IEnumerator Capture(string name, Vector2 size)
        {
            yield return new WaitForSecondsRealtime(.15f); Canvas.ForceUpdateCanvases(); Ascii.ForceMeshUpdate();
            string path = $"Temp/TerminalAudit/Dungeon/{name}-{size.x}x{size.y}";
            File.WriteAllText(path + ".txt", Ascii.GetParsedText());
            ScreenCapture.CaptureScreenshot(path + ".png"); yield return new WaitForEndOfFrame(); yield return new WaitForSecondsRealtime(.1f);
        }
    }
}
#endif
