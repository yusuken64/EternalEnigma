#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class StatusMenuTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameTestHarness harness;
        TestInputScope input;
        IDisposable preferences;
        EditorWindow view;
        int oldSize;
        bool? fullControl;
        DungeonAnimationMode? animation;

        [UnitySetUp] public IEnumerator Setup()
        {
            input = new TestInputScope(); preferences = GamePreferences.Isolate();
            fullControl = DungeonPreferences.FullControlOverride; animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.FullControlOverride = false; DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            Directory.CreateDirectory("Temp/StatusAudit");
            yield return harness.LoadDungeon(new TestScenario { AllyName = "Avery", AdditionalAllies = new[] { "Sage" } });
            MenuManager.Instance.OpenPartyMenu(PartyMenuTab.Equipment);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { TerminalMode.SetRequested(false); yield return harness.Cleanup(); }
            finally
            {
                DungeonPreferences.FullControlOverride = fullControl; DungeonPreferences.AnimationOverride = animation;
                if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, oldSize);
                input?.Dispose(); preferences?.Dispose();
            }
        }
        StatusEffect Add(Type type)
        {
            var go = new GameObject(type.Name); go.transform.SetParent(harness.Ally.transform, false);
            var status = (StatusEffect)go.AddComponent(type); status.TurnsLeft = 7; status.Apply();
            harness.Ally.StatusEffects.Add(status);
            return status;
        }
        T Add<T>() where T : StatusEffect => (T)Add(typeof(T));
        static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        static IEnumerator Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; yield return null;
        }
        IEnumerator Resolution(Vector2 resolution)
        {
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { resolution, "Status audit " + resolution });
            view.Repaint(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases(); yield return null;
        }

        [UnityTest] public IEnumerator BuffsAndDebuffsUseEquipmentRowsAndSelectionPopupWithoutSpendingActions()
        {
            yield return Resolution(new Vector2(960, 600));
            var actor = harness.Ally;
            var frailty = Add<FrailtyStatusEffect>(); frailty.ReApply(frailty); frailty.ReApply(frailty);
            var strength = Add<StrengthStatusEffect>();
            var expired = Add<WeakenStatusEffect>(); expired.TurnsLeft = 0;
            var keyboard = InputSystem.AddDevice<Keyboard>(); var pad = InputSystem.AddDevice<Gamepad>(); var mouse = InputSystem.AddDevice<Mouse>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, pad, mouse };
            var manager = MenuManager.Instance; var menu = manager.PartyMenu;
            int actions = actor.Vitals.ActionsPerTurnLeft;
            using (var context = new DungeonPartyMenuContext(harness.Game))
            {
                var hero = context.Heroes.Single(h => h.DungeonActor == actor);
                var statuses = context.Entries(hero, PartyMenuTab.Equipment).Where(entry => entry.Status != null).ToArray();
                Assert.That(statuses.Select(entry => entry.Status), Is.EqualTo(new StatusEffect[] { frailty, strength }));
                Assert.That(statuses.All(entry => entry.Section == "Statuses" && entry.Icon != null), Is.True);
                foreach (var entry in statuses) Assert.That(context.Actions(hero, entry), Is.Empty);
            }
            menu.Refresh(); menu.SetFirstSelect(); yield return null;
            Assert.That(menu.RowsRoot.GetComponentsInChildren<TMP_Text>().Count(label => label.text == "Statuses"), Is.EqualTo(1));
            var frailtyRow = menu.EntryButtons.Single(button => button.GetComponent<AuthoredButton>().Label.text == "Frail");
            var strengthRow = menu.EntryButtons.Single(button => button.GetComponent<AuthoredButton>().Label.text == "Strength");
            var row = frailtyRow.GetComponent<AuthoredButton>();
            Assert.That(row.Icon.gameObject.activeSelf, Is.True);
            Assert.That(row.Icon.sprite, Is.SameAs(CombatVisualCatalog.Instance.ForStatus(frailty).Icon));
            Assert.That(row.GetComponent<PartyMenuRow>(), Is.Not.Null);
            var first = menu.EntryButtons.ToList().IndexOf(frailtyRow);
            for (int i = 0; i < first; i++) yield return Press(keyboard, Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(frailtyRow.gameObject));
            Assert.That(menu.Details.isActiveAndEnabled, Is.True);
            Assert.That(menu.Details.GetParsedText(), Does.Contain("Frail").And.Contain("Attack -3").And.Contain("Until cleansed"));
            yield return Press(keyboard, Key.Enter);
            Assert.That(manager.CurrentDialog, Is.SameAs(menu), "Inspecting a status does not open item actions or a second dialog.");
            yield return Press(pad, GamepadButton.DpadDown);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(strengthRow.gameObject));
            Assert.That(menu.Details.GetParsedText(), Does.Contain("Attack +5").And.Contain("7 turns left"));
            yield return Press(pad, GamepadButton.South);
            Assert.That(manager.CurrentDialog, Is.SameAs(menu));
            yield return Press(pad, GamepadButton.DpadUp);
            Assert.That(menu.Details.GetParsedText(), Does.Contain("Attack -3"));
            strengthRow.Select(); yield return new WaitForSecondsRealtime(.25f);
            var rect = (RectTransform)frailtyRow.transform;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(frailtyRow.gameObject), "Mouse hover uses the same selection preview.");
            Assert.That(actor.Vitals.ActionsPerTurnLeft, Is.EqualTo(actions));
            Assert.That(frailty.TurnsLeft, Is.EqualTo(7));
            Assert.That(DungeonHud.StatusSummary(actor), Does.Contain("Frail x3, Until cleansed").And.Contain("Strength, 7 turns left"));
            Assert.That(Object.FindObjectsByType<DungeonPartyCard>(FindObjectsSortMode.None).Any(card =>
                card.StatusButtons.Any(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>().text == "x3")), Is.True);
            yield return Capture("selection-960x600");
            menu.BrowseHero(1); yield return null;
            Assert.That(menu.EntryButtons.Any(button => button.GetComponent<AuthoredButton>().Label.text == "Frail"), Is.False, "Statuses belong to the selected hero.");
            menu.BrowseHero(-1); yield return null;
            Assert.That(menu.Details.GetParsedText(), Does.Contain("Attack -3"), "Returning restores the same selected status.");
            actor.RemoveStatusEffect(frailty); menu.Refresh(); menu.SetFirstSelect(); yield return null;
            Assert.That(menu.EntryButtons.Any(button => button.GetComponent<AuthoredButton>().Label.text == "Frail"), Is.False);
        }

        [UnityTest] public IEnumerator SelectedStatusRefitsAfterDisplayModeAndWindowChanges()
        {
            Add<FollowUpMarkStatusEffect>();
            var menu = MenuManager.Instance.PartyMenu; menu.Refresh();
            var selected = menu.EntryButtons.Last(); selected.Select(); yield return null;
            foreach (bool terminal in new[] { false, true, false })
            {
                TerminalMode.SetRequested(terminal);
                yield return harness.WaitUntil(() => TerminalMode.Effective == terminal, "status display transition");
                foreach (var resolution in new[] { new Vector2(1280,720), new Vector2(960,600) })
                {
                    yield return Resolution(resolution);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected.gameObject));
                    Assert.That(menu.Details.preferredHeight, Is.LessThanOrEqualTo(menu.DetailsControl.Scroll.viewport.rect.height + 1));
                    Assert.That(menu.Details.GetParsedText(), Does.Contain("7 turns left"));
                    yield return Capture($"transition-{terminal}-{resolution.x}x{resolution.y}");
                }
            }
        }

        [UnityTest] public IEnumerator EveryStatusPopupFitsNormalAndTerminalLayouts()
        {
            var statuses = TypeCache.GetTypesDerivedFrom<StatusEffect>()
                .Where(type => !type.IsAbstract && type.Assembly == typeof(StatusEffect).Assembly)
                .OrderBy(type => type.Name).Select(Add).ToList();
            foreach (var status in statuses)
            {
                if (status is SongStatusEffect song) { song.SongName = "Restorative Melody"; song.HealPerTurn = 8; song.Modification.Strength = 4; }
                if (status is CommandStatusEffect command) { command.CommandName = "Blazing Arms"; command.BonusElement = DamageElement.Fire; command.BonusElementPercent = 20; }
                if (status is BossEffectStatus boss) boss.Kind = "Guard";
                if (status is TimedBuffStatusEffect buff) { buff.BuffName = "Protective Blessing"; buff.Modification.Defense = 6; }
            }
            var menu = MenuManager.Instance.PartyMenu; menu.Refresh(); menu.SetFirstSelect(); yield return null;
            var entries = (System.Collections.Generic.List<PartyMenuEntry>)typeof(PartyMenu).GetField("entries", Flags).GetValue(menu);
            Assert.That(entries.Count(entry => entry.Status != null), Is.EqualTo(statuses.Count));
            foreach (bool terminal in new[] { false, true })
            {
                TerminalMode.SetRequested(terminal); yield return harness.WaitUntil(() => TerminalMode.Effective == terminal, "status presentation mode");
                foreach (var resolution in new[] { new Vector2(960,600), new Vector2(1280,720), new Vector2(2560,1080) })
                {
                    yield return Resolution(resolution);
                    for (int i = 0; i < statuses.Count; i++)
                    {
                        var status = statuses[i]; var button = menu.EntryButtons[entries.FindIndex(entry => entry.Status == status)]; button.Select();
                        yield return new WaitForSecondsRealtime(.08f); Canvas.ForceUpdateCanvases();
                        Assert.That(button.GetComponent<AuthoredButton>().Icon.sprite, Is.Not.Null, status.GetType().Name);
                        Assert.That(menu.Details.GetParsedText(), Does.Contain(StatusPresentation.Duration(status)));
                        Assert.That(menu.Details.preferredHeight, Is.LessThanOrEqualTo(menu.DetailsControl.Scroll.viewport.rect.height + 1),
                            status.GetType().Name + " must show its entire explanation and duration without scrolling the popup.");
                        Assert.That(menu.DetailsControl.Scroll.transform.IsChildOf(menu.Panel), Is.False);
                        Assert.That(MenuManager.Instance.CurrentDialog, Is.SameAs(menu));
                        string path = $"{(terminal ? "terminal" : "normal")}-{status.GetType().Name}-{resolution.x}x{resolution.y}";
                        Assert.That(TextLayoutAudit.Capture("Temp/StatusAudit/" + path), Is.Empty, path);
                        if (status is FrailtyStatusEffect or FollowUpMarkStatusEffect or SongStatusEffect)
                            yield return Capture(path);
                    }
                }
            }
        }
        static IEnumerator Capture(string name)
        {
            var issues = TextLayoutAudit.Capture("Temp/StatusAudit/" + name);
            ScreenCapture.CaptureScreenshot("Temp/StatusAudit/" + name + ".png");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(issues, Is.Empty, name);
        }
    }
}
#endif
