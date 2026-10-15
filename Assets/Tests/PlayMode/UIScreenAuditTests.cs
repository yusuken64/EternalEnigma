#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class UIScreenAuditTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameTestHarness harness;
        TestInputScope input;
        IDisposable preferences;
        UnityEditor.EditorWindow view;
        int size;
        DungeonAnimationMode? animation;
        bool? fullControl;

        [UnitySetUp] public IEnumerator Setup()
        {
            input = new TestInputScope();
            preferences = GamePreferences.Isolate();
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            animation = DungeonPreferences.AnimationOverride;
            fullControl = DungeonPreferences.FullControlOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            DungeonPreferences.FullControlOverride = false;
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            size = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            Directory.CreateDirectory("Temp/UIScreenAudit");
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { yield return harness.Cleanup(); }
            finally
            {
                DungeonPreferences.AnimationOverride = animation;
                DungeonPreferences.FullControlOverride = fullControl;
                if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, size);
                input?.Dispose();
                preferences?.Dispose();
            }
        }

        [UnityTest] public IEnumerator CaptureMainMenuSlotsHeroAndSettingsAcrossAspectRatios()
        {
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            int writes = harness.Store.Writes;
            yield return CaptureSizes("main-menu");
            menu.StartGame_Clicked(); yield return null;
            yield return CaptureSizes("campaign-slots");
            Object.FindFirstObjectByType<CampaignSlots>().BackButton.onClick.Invoke();
            menu.ChooseHero(); yield return null;
            yield return CaptureSizes("hero-picker");
            Object.FindFirstObjectByType<ProtagonistHeroPicker>().BackButton.onClick.Invoke();
            var settings = Common.Instance.GlobalSettings;
            menu.Options_Clicked(); yield return null;
            foreach (var tab in settings.TabGroup.TabContents)
            {
                tab.TabButton.Select(); yield return null;
                yield return CaptureSizes("settings-" + tab.TabButton.GetComponentInChildren<TMP_Text>().text.ToLowerInvariant());
            }
            settings.Exit_Clicked();
            Assert.That(harness.Store.Writes, Is.EqualTo(writes), "Browsing screens must not create or replace a campaign.");
        }

        [UnityTest] public IEnumerator CaptureTownServicesPartyAndOverworldAcrossAspectRatios()
        {
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Common.Instance.GameSaveData = menu.CreateNewSave(42);
            Common.Instance.Travel.NewCampaign(42);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlockScreen.activeSelf, "campaign town");
            var town = Object.FindFirstObjectByType<Town>();
            var menus = Object.FindFirstObjectByType<TownMenuManager>();
            yield return CaptureSizes("town-hud");
            foreach (var tab in new[] { PartyMenuTab.Inventory, PartyMenuTab.Equipment, PartyMenuTab.Skills, PartyMenuTab.Stats })
            {
                menus.OpenPartyMenu(tab); yield return null;
                yield return CaptureSizes("town-" + tab.ToString().ToLowerInvariant());
                menus.PartyMenu.CloseDialog(); yield return null;
            }
            var services = Object.FindFirstObjectByType<TownMenu>();
            foreach (var mode in new[] { AllyRecruitDialogMode.Recruit, AllyRecruitDialogMode.Talk })
            {
                var ally = mode == AllyRecruitDialogMode.Recruit ? town.TownAllies.FirstOrDefault() : town.TownPlayer.RecruitedAllies.FirstOrDefault();
                if (ally == null) continue;
                services.AllyRecruitDialog.Show(ally, mode);
                menus.Open(services.AllyRecruitDialog); yield return null;
                yield return CaptureSizes("town-" + mode.ToString().ToLowerInvariant(), () =>
                    AssertCompactPointer(services.AllyRecruitDialog.GetComponent<NavigationHandler>(),
                        mode == AllyRecruitDialogMode.Recruit ? services.AllyRecruitDialog.CancelButton : services.AllyRecruitDialog.CancelButton2));
                services.AllyRecruitDialog.CloseDialog(); yield return null;
            }
            foreach (var building in town.Configuration.Buildings)
            {
                string id = building.Id;
                var dialog = services.OpenBuilding(building, town.TownPlayer, null);
                if (dialog == null) continue;
                yield return null;
                yield return CaptureSizes("town-service-" + id, dialog is EntranceDialog entrance
                    ? () => AssertCompactPointer(entrance.GetComponent<NavigationHandler>(), entrance.CancelButton) : null);
                if (dialog is EntranceDialog lockedEntrance)
                {
                    var rows = lockedEntrance.Container.GetComponentsInChildren<DungeonTierItem>();
                    Assert.That(rows, Is.Not.Empty);
                    Assert.That(rows.All(row => !row.Button.interactable), Is.True, "Tomorrow's dungeon must look unavailable.");
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(lockedEntrance.CancelButton.gameObject));
                    rows[0].OnClick(); yield return null;
                    Assert.That(menus.CurrentDialog, Is.SameAs(lockedEntrance), "A stale callback cannot enter a locked dungeon.");
                    Assert.That(Common.Instance.Travel.IsTransitioning, Is.False);
                }
                if (dialog is ShopMenuDialog shop)
                {
                    if (shop.ShopItems.Count > 0)
                    {
                        shop.ShopItems[0].BuyButton.onClick.Invoke(); yield return null;
                        yield return CaptureSizes("town-purchase-" + id);
                        shop.BuyConfirmationDialog.CloseDialog(); yield return null;
                    }
                    shop.SetMode(true); yield return null;
                    yield return CaptureSizes("town-sell-" + id);
                }
                else if (dialog is BallistaDialog trainer)
                {
                    var skill = trainer.SkillGridItems.FirstOrDefault(row => row.GridButton.IsInteractable());
                    if (skill != null)
                    {
                        skill.GridButton.onClick.Invoke(); yield return null;
                        if (menus.CurrentDialog != trainer)
                        {
                            yield return CaptureSizes("town-lesson-confirmation");
                            menus.CurrentDialog.CloseDialog(); yield return null;
                        }
                    }
                }
                dialog.CloseDialog(); yield return null;
            }
            harness.PrepareOpening();
            var readyEntrance = (EntranceDialog)services.OpenBuilding(town.Configuration.Buildings.First(b => b.DialogId == "entrance"), town.TownPlayer, null);
            yield return null;
            var availableEntry = readyEntrance.Container.GetComponentsInChildren<DungeonTierItem>().First(row => row.Button.interactable);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(availableEntry.Button.gameObject));
            yield return CaptureSizes("town-entrance-ready", () => AssertCompactPointer(readyEntrance.GetComponent<NavigationHandler>(), availableEntry.Button));
            readyEntrance.CloseDialog(); yield return null;
            var champions = TownSceneLoader.Default.AllyCatalog.Where(hero => hero.Portrait != null)
                .OrderByDescending(hero => hero.Name.Length).Take(4).ToArray();
            Assert.That(champions.Length, Is.EqualTo(4));
            string secondaryClass = ClassCatalog.Load().Classes.OrderByDescending(c => c.DisplayName.Length).First().DisplayName;
            string weapon = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
                .OrderByDescending(item => item.ItemName.Length).First().ItemName;
            int writesBeforeSummary = harness.Store.Writes;
            foreach (int count in new[] { 1, 4 })
            {
                // Representative display data only; actual completion/save behavior is covered by the homecoming fixture.
                var record = new CampaignCompletionRecord {
                    Seed = 42, DungeonsCleared = 24, TownsVisited = 8, CompanionsRecruited = 12,
                    KeysFound = 8, Gold = 1234567, PlaytimeSeconds = 360000,
                    Party = champions.Take(count).Select(hero => new CampaignCompletionRecord.Hero {
                        Id = hero.Id, Name = hero.Name, Level = 99,
                        PrimaryClass = hero.PrimaryClass?.DisplayName ?? "Adventurer", SecondaryClass = secondaryClass,
                        MaxHP = 9999, MaxSP = 999, Weapon = weapon
                    }).ToArray()
                };
                var summary = CampaignSummaryDialog.Create(town.transform, record);
                menus.Open(summary); yield return null;
                yield return CaptureSizes("town-hall-of-fame-" + count + "-heroes");
                summary.ContinueButton.onClick.Invoke(); yield return null;
            }
            Assert.That(harness.Store.Writes, Is.EqualTo(writesBeforeSummary), "Capturing Hall of Fame must not save fixture data.");
            var trainerBuilding = town.Configuration.Buildings.Single(b => b.Id == "trainer");
            var fullTrainer = services.OpenBuilding(trainerBuilding, town.TownPlayer, null);
            yield return null;
            yield return CaptureSizes("town-trainer-tree");
            fullTrainer.CloseDialog(); yield return null;
            var context = Common.Instance.CampaignContext;
            Assert.That(context.BeginTownDungeon("story-0"), Is.True);
            Assert.That(context.CompleteDungeon(true), Is.True);
            Assert.That(Common.Instance.Travel.ExitTown(town), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlockScreen.activeSelf, "overworld");
            yield return CaptureSizes("overworld-hud");
            var overworldMenus = Object.FindFirstObjectByType<OverworldMenuManager>();
            foreach (var tab in new[] { PartyMenuTab.Inventory, PartyMenuTab.Equipment, PartyMenuTab.Skills, PartyMenuTab.Stats, PartyMenuTab.Capabilities })
            {
                overworldMenus.OpenPartyMenu(tab); yield return null;
                yield return CaptureSizes("overworld-" + tab.ToString().ToLowerInvariant());
                overworldMenus.PartyMenu.CloseDialog(); yield return null;
            }
        }

        [UnityTest] public IEnumerator CaptureDungeonLevelAndResultDialogsAcrossAspectRatios()
        {
            yield return harness.LoadDungeon(new TestScenario { IncludeStartingItems = true });
            harness.Ally.Vitals.Level = 2;
            harness.Ally.SyncDisplayedStats();
            var level = LevelUpChoiceDialog.Open(harness.Ally, null);
            yield return null;
            yield return CaptureSizes("dungeon-level-up");
            level.CloseDialog(); yield return null;
            foreach (bool victory in new[] { false, true })
            {
                var result = harness.Game.GameOverScreen;
                result.Setup(harness.Game.PlayerController, victory);
                MenuManager.Open(result); yield return null;
                yield return CaptureSizes(victory ? "dungeon-victory" : "dungeon-defeat");
                result.CloseAction = null;
                result.CloseDialog(); yield return null;
            }
        }

        [UnityTest] public IEnumerator ResultBackKeepsExplicitSaveOrRecoveryActionFocused()
        {
            yield return harness.LoadDungeon(new TestScenario());
            yield return harness.WaitUntil(() => !Common.Instance.ScreenTransition.BlockScreen.activeSelf, "dungeon reveal");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, pad };
            var result = harness.Game.GameOverScreen;
            foreach (bool victory in new[] { false, true })
            {
                result.Setup(harness.Game.PlayerController, victory);
                MenuManager.Open(result);
                yield return null; yield return null;
                int writes = harness.Store.Writes;
                foreach (bool clearSelection in new[] { false, true })
                {
                    if (clearSelection) EventSystem.current.SetSelectedGameObject(null);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    AssertResultRemainsOpen();

                    if (clearSelection) EventSystem.current.SetSelectedGameObject(null);
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    yield return null;
                    AssertResultRemainsOpen();
                }
                result.CloseDialog();
                yield return null;

                void AssertResultRemainsOpen()
                {
                    Assert.That(result.gameObject.activeInHierarchy, Is.True, "Back must keep the result visible.");
                    Assert.That(MenuManager.Instance.CurrentDialog, Is.SameAs(result));
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(result.OkButton.gameObject));
                    Assert.That(Common.Instance.MenuInputHandler.PlayerInput.currentActionMap.name, Is.EqualTo("UI"));
                    Assert.That(Common.Instance.Travel.IsTransitioning, Is.False, "Back must not leave an unsaved result.");
                    Assert.That(harness.Store.Writes, Is.EqualTo(writes), "Back must not implicitly save or load.");
                }
            }
        }

        static void AssertCompactPointer(NavigationHandler navigation, Button selected)
        {
            Assert.That(navigation, Is.Not.Null);
            var pointer = navigation.selectionArrow;
            Assert.That(pointer.gameObject.activeInHierarchy, Is.True, "The focused action needs a visible selection pointer.");
            var target = (RectTransform)selected.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(target, pointer);
            Assert.That(bounds.max.x, Is.LessThan(target.rect.xMin), "The pointer must stay outside the button and its label.");
            Assert.That(Mathf.Abs(bounds.center.y - target.rect.center.y), Is.LessThan(1), "Pointer and action should align vertically at every aspect ratio.");
            Assert.That(bounds.size.y, Is.LessThanOrEqualTo(target.rect.height), "The pointer must not dwarf the action.");
            Assert.That(pointer.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
        }

        IEnumerator CaptureSizes(string screen, Action verify = null)
        {
            foreach (var resolution in new[] { new Vector2(960, 600), new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) })
            {
                view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { resolution, "UI audit " + resolution });
                view.Repaint();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Screen.width, Is.EqualTo((int)resolution.x), "Capture width must match its filename.");
                Assert.That(Screen.height, Is.EqualTo((int)resolution.y), "Capture height must match its filename.");
                Canvas.ForceUpdateCanvases();
                verify?.Invoke();
                var labels = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Where(t => t is TextMeshProUGUI && t.enabled && !string.IsNullOrWhiteSpace(t.text) &&
                        t.GetComponentsInParent<Canvas>().All(c => c.enabled))
                    .Select(t =>
                    {
                        t.ForceMeshUpdate();
                        var corners = new Vector3[4]; t.rectTransform.GetWorldCorners(corners);
                        return new LabelReport { path = PathOf(t.transform), text = t.text,
                            font = t.font != null ? t.font.name : "MISSING", fontPixels = t.fontSize * t.rectTransform.lossyScale.y,
                            overflow = t.isTextOverflowing, x = corners[0].x, y = corners[0].y,
                            width = corners[2].x - corners[0].x, height = corners[2].y - corners[0].y };
                    }).ToArray();
                string path = $"Temp/UIScreenAudit/{screen}-{resolution.x}x{resolution.y}";
                File.WriteAllText(path + ".json", JsonUtility.ToJson(new ScreenReport { labels = labels }, true));
                ScreenCapture.CaptureScreenshot(path + ".png");
                yield return new WaitForSecondsRealtime(.2f);
            }
        }

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        [Serializable] sealed class ScreenReport { public LabelReport[] labels; }
        [Serializable] sealed class LabelReport
        {
            public string path, text, font;
            public float fontPixels, x, y, width, height;
            public bool overflow;
        }
    }
}
#endif
