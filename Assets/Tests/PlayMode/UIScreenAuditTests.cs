#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
        bool terminalAudit;
        readonly List<string> textIssues = new();

        [UnitySetUp] public IEnumerator Setup()
        {
            terminalAudit = false;
            textIssues.Clear();
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
            try
            {
                if (AutoplayRunner.Active != null)
                {
                    if (AutoplayRunner.Active.Report != null)
                    {
                        AutoplayRunner.Active.Report.ValidationOnly = true;
                        AutoplayRunner.Active.ExitDemo();
                    }
                    else Object.Destroy(AutoplayRunner.Active.gameObject);
                    yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "audit demo cleanup");
                }
                TerminalMode.SetRequested(false);
                yield return harness.Cleanup();
            }
            finally
            {
                DungeonPreferences.AnimationOverride = animation;
                DungeonPreferences.FullControlOverride = fullControl;
                if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, size);
                input?.Dispose();
                preferences?.Dispose();
            }
            Assert.That(textIssues.Count, Is.Zero, string.Join("\n", textIssues.Take(12)));
        }

        [UnityTest] public IEnumerator CaptureTerminalMainMenuSlotsHeroAndSettingsAcrossAspectRatios()
        {
            terminalAudit = true;
            yield return CaptureMainMenuSlotsHeroAndSettingsAcrossAspectRatios();
        }

        [UnityTest] public IEnumerator CaptureTerminalTownServicesPartyAndOverworldAcrossAspectRatios()
        {
            terminalAudit = true;
            yield return CaptureTownServicesPartyAndOverworldAcrossAspectRatios();
        }

        [UnityTest] public IEnumerator CaptureTerminalDungeonLevelAndResultDialogsAcrossAspectRatios()
        {
            terminalAudit = true;
            yield return CaptureDungeonLevelAndResultDialogsAcrossAspectRatios();
        }

        [UnityTest] public IEnumerator CaptureTerminalAutoplayPanelsAcrossAspectRatios()
        {
            terminalAudit = true;
            yield return CaptureAutoplayPanelsAcrossAspectRatios();
        }

        [UnityTest] public IEnumerator CaptureAutoplayPanelsAcrossAspectRatios()
        {
            yield return harness.LoadMainMenu(null);
            AutoplaySetup.Show(Object.FindFirstObjectByType<MainMenu>()); yield return null;
            var setup = Object.FindFirstObjectByType<AutoplaySetup>();
            yield return CaptureSizes("autoplay-setup");
            setup.Seed.text = "invalid"; yield return null;
            yield return CaptureSizes("autoplay-seed-validation");
            setup.Seed.text = int.MaxValue.ToString(); setup.RunCount.text = "2"; yield return null;
            yield return CaptureSizes("autoplay-range-validation");
            setup.Seed.text = "42"; setup.RunCount.text = "1";
            setup.FastMode.onClick.Invoke(); yield return null;
            yield return CaptureSizes("autoplay-fast-mode");
            setup.Close(); yield return null;
            AutoplayRunner.WatchDemo(new AutoplayOptions { Speed = 1, ReportRoot = "Temp/TextContainmentAudit/Autoplay" });
            var run = AutoplayRunner.Active;
            Assert.That(run, Is.Not.Null);
            Assert.That(run.Report, Is.Not.Null, run.Status);
            Assert.That(run.IsBalance, Is.False, "Copying normal demo options must preserve the absence of a balance experiment.");
            run.Report.ValidationOnly = true;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlocksInput, "audit demo town");
            run.SetPaused(true);
            run.Report.Actions = 100000; run.Report.DungeonTurns = 100000;
            yield return CaptureSizes("autoplay-paused");
            run.SetPanelVisible(false); yield return null;
            yield return CaptureSizes("autoplay-collapsed");
            run.SetPanelVisible(true);
            run.RequestReturn(); yield return null;
            yield return CaptureSizes("autoplay-return-prompt");
            run.ExitDemo();
            yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "audit demo exit");
        }

        [UnityTest] public IEnumerator CaptureMainMenuSlotsHeroAndSettingsAcrossAspectRatios()
        {
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            int writes = harness.Store.Writes;
            yield return CaptureSizes("main-menu");
            var developer = menu.GetComponent<MainMenuDeveloperControls>();
            developer.Toggle.onClick.Invoke(); yield return null;
            yield return CaptureSizes("main-menu-developer");
            developer.Close();
            menu.StartGame_Clicked(); yield return null;
            yield return CaptureSizes("campaign-slots");
            Object.FindFirstObjectByType<CampaignSlots>().BackButton.onClick.Invoke();
            menu.ChooseHero(); yield return null;
            yield return CaptureSizes("hero-picker");
            var heroes = Object.FindFirstObjectByType<ProtagonistHeroPicker>();
            yield return null;
            int heroIndex = 0;
            foreach (var choice in heroes.ChoiceButtons.Where(button => button.gameObject.activeInHierarchy))
            {
                choice.Select(); yield return null;
                yield return CaptureSizes($"hero-{heroIndex++}-" + choice.GetComponentInChildren<TMP_Text>().text.ToLowerInvariant());
            }
            heroes.BackButton.onClick.Invoke();
            var settings = Common.Instance.GlobalSettings;
            menu.Options_Clicked(); yield return null;
            foreach (var tab in settings.TabGroup.TabContents)
            {
                tab.TabButton.Select(); yield return null;
                yield return CaptureSizes("settings-" + tab.TabButton.GetComponentInChildren<TMP_Text>().text.ToLowerInvariant());
                var display = tab.Content.GetComponent<DisplayOptions>();
                if (display != null)
                    foreach (var dropdown in new[] { display.WindowMode, display.Resolution })
                    {
                        dropdown.Show(); yield return null;
                        yield return CaptureSizes("settings-dropdown-" + dropdown.name.Replace(" ", "-").ToLowerInvariant());
                        dropdown.Hide(); yield return new WaitForSecondsRealtime(.2f);
                    }
                var controls = tab.Content.GetComponent<ControlsRebindingScreen>();
                if (controls != null)
                {
                    for (int device = 0; device < 2; device++)
                    {
                        for (int context = 0; context < 2; context++)
                        {
                            yield return CaptureSizes($"settings-bindings-{device}-{context}");
                            controls.BindingButtons.Last(button => button.gameObject.activeInHierarchy).Select(); yield return null;
                            yield return CaptureSizes($"settings-bindings-bottom-{device}-{context}");
                            controls.ContextButton.onClick.Invoke(); yield return null;
                        }
                        var action = ControlBindings.DisplayAsset.FindAction("Player/Wait", true);
                        string path = device == 0 ? "<Keyboard>/period" : "<Gamepad>/leftStickPress";
                        int binding = Enumerable.Range(0, action.bindings.Count).Single(index => action.bindings[index].path == path);
                        controls.BeginRebind("Player/Wait", binding); yield return new WaitForSecondsRealtime(.2f);
                        yield return CaptureSizes("settings-capture-" + device);
                        controls.CancelRebind();
                        controls.DeviceButton.onClick.Invoke(); yield return null;
                    }
                }
                if (tab.Content.name == "Gameplay options")
                    for (int mode = 0; mode < 4; mode++)
                    {
                        DungeonPreferences.AnimationOverride = (DungeonAnimationMode)mode;
                        settings.GetComponent<DungeonOptions>().SendMessage("Refresh"); yield return null;
                        yield return CaptureSizes("settings-animation-" + mode);
                    }
            }
            settings.Exit_Clicked();
            Assert.That(harness.Store.Writes, Is.EqualTo(writes), "Browsing screens must not create or replace a campaign.");
            var save = menu.CreateNewSave(42);
            SaveSystem.SaveData(0, save);
            harness.Store.Write(1, "unreadable audit fixture");
            menu.StartGame_Clicked(); yield return null;
            yield return CaptureSizes("campaign-slots-populated");
            var slots = Object.FindFirstObjectByType<CampaignSlots>();
            slots.Slots[1].Button.onClick.Invoke(); yield return null;
            yield return CaptureSizes("campaign-slot-replacement");
            Object.FindFirstObjectByType<CampaignChoice>().Back.onClick.Invoke();
            slots.BackButton.onClick.Invoke(); yield return null;
            CampaignChoice.Show("Return to the main menu? Unsaved progress will be lost.", "Quit without saving", () => { });
            yield return null;
            yield return CaptureSizes("quit-confirmation");
            Object.FindFirstObjectByType<CampaignChoice>().Back.onClick.Invoke();
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
                var recruit = services.AllyRecruitDialog;
                var board = recruit.NameText.GetComponentInParent<Image>(true);
                var sheet = recruit.DescriptionText.GetComponentInParent<Image>(true);
                var boardSprite = board.sprite;
                var sheetSprite = sheet.sprite;
                services.AllyRecruitDialog.Show(ally, mode);
                menus.Open(services.AllyRecruitDialog); yield return null;
                yield return CaptureSizes("town-" + mode.ToString().ToLowerInvariant(), () => {
                    AssertCompactPointer(services.AllyRecruitDialog.GetComponent<NavigationHandler>(),
                        mode == AllyRecruitDialogMode.Recruit ? services.AllyRecruitDialog.CancelButton : services.AllyRecruitDialog.CancelButton2);
                    if (TerminalMode.Effective)
                        foreach (var surface in new[] { board, sheet })
                        {
                            Assert.That(surface.sprite, Is.Null, "Recruitment text must sit on a terminal surface, including aspect-preserving sheets.");
                            Assert.That(surface.color.r + surface.color.g + surface.color.b, Is.LessThan(.15f));
                            Assert.That(surface.color.a, Is.EqualTo(1));
                        }
                });
                services.AllyRecruitDialog.CloseDialog(); yield return null;
                Assert.That(board.sprite, Is.SameAs(boardSprite), "Closing recruitment restores its authored board.");
                Assert.That(sheet.sprite, Is.SameAs(sheetSprite));
            }
            foreach (var building in town.Configuration.Buildings)
            {
                string id = building.Id;
                var dialog = services.OpenBuilding(building, town.TownPlayer, null);
                if (dialog == null) continue;
                yield return null;
                yield return CaptureSizes("town-service-" + id, () =>
                {
                    AssertServiceLayout(dialog);
                    if (dialog is EntranceDialog entrance)
                        AssertCompactPointer(entrance.GetComponent<NavigationHandler>(),
                            entrance.Container.GetComponentsInChildren<DungeonTierItem>().First().Button);
                });
                if (dialog is EntranceDialog lockedEntrance)
                {
                    var rows = lockedEntrance.Container.GetComponentsInChildren<DungeonTierItem>();
                    Assert.That(rows, Is.Not.Empty);
                    Assert.That(rows.All(row => row.Button.interactable), Is.True, "Locked dungeons remain selectable so an attempt explains the requirement.");
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(rows[0].Button.gameObject));
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
                    var skill = trainer.SkillGridItems.FirstOrDefault(row => row.Offer?.CanLearn == true && !row.Offer.IsMaxed);
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
            int npcIndex = 0;
            foreach (var npc in town.GetComponentsInChildren<TownNpc>())
            {
                npc.Greet(town.TownPlayer.ControllingTownAlly.TilemapPosition); yield return null;
                yield return CaptureSizes("town-npc-" + npcIndex++);
                Common.Instance.MessageDialog.CloseDialog(); yield return null;
            }
            var longestItem = Common.Instance.ItemManager.ItemDefinitions
                .OrderByDescending(item => item.Description?.Length ?? 0).First();
            TownMenu.ShowMessage(longestItem.ItemName + "\n" + longestItem.Description + "\nUse this item in the dungeon.");
            yield return null;
            yield return CaptureSizes("town-item-message");
            Common.Instance.MessageDialog.CloseDialog(); yield return null;
            var book = town.GetComponentInChildren<TownDungeonBook>(true);
            if (book != null)
            {
                BookDungeonPrompt.Show(town, book.Definition, () => { }, () => { }); yield return null;
                yield return CaptureSizes("town-book-locked");
                town.GetComponentInChildren<BookDungeonPrompt>(true).Close(); yield return null;
            }
            harness.PrepareOpening();
            foreach (var building in town.Configuration.Buildings)
            {
                var dialog = services.OpenBuilding(building, town.TownPlayer, null);
                if (dialog is ShopMenuDialog shop)
                {
                    Assert.That(shop.GetComponentsInChildren<MenuControlHints>(), Is.Empty, "Shop footers must stay hidden.");
                    for (int item = 0; item < shop.ShopItems.Count; item++)
                    {
                        shop.ShopItems[item].BuyButton.Select(); yield return null;
                        yield return CaptureSizes($"town-shop-{building.Id}-item-{item}");
                    }
                    shop.SetMode(true); yield return null;
                    yield return CaptureSizes("town-shop-stocked-sell-" + building.Id);
                    if (shop.ShopItems.Count > 0)
                    {
                        shop.ShopItems[0].BuyButton.onClick.Invoke(); yield return null;
                        yield return CaptureSizes("town-sale-confirmation-" + building.Id);
                        shop.BuyConfirmationDialog.CloseDialog(); yield return null;
                    }
                }
                dialog?.CloseDialog(); yield return null;
            }
            var readyEntrance = (EntranceDialog)services.OpenBuilding(town.Configuration.Buildings.First(b => b.DialogId == "entrance"), town.TownPlayer, null);
            yield return null;
            var availableEntry = readyEntrance.Container.GetComponentsInChildren<DungeonTierItem>().First(row => row.Button.interactable);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(availableEntry.Button.gameObject));
            yield return CaptureSizes("town-entrance-ready", () =>
            {
                AssertServiceLayout(readyEntrance);
                AssertCompactPointer(readyEntrance.GetComponent<NavigationHandler>(), availableEntry.Button);
            });
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
                        MaxHP = 9999, MaxMana = 999, Weapon = weapon
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
            if (book != null)
            {
                BookDungeonPrompt.Show(town, book.Definition, () => { }, () => { }); yield return null;
                yield return CaptureSizes("town-book-unlocked");
                town.GetComponentInChildren<BookDungeonPrompt>(true).Close(); yield return null;
            }
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

        [UnityTest] public IEnumerator CaptureLevelUpPopupAcrossAspectRatios()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var hero = harness.Ally;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            hero.SetFacing(Facing.Down); hero.PlayIdleAnimation();
            yield return new WaitForSecondsRealtime(.15f);
            ScreenCapture.CaptureScreenshot("Temp/UIScreenAudit/level-up-south-idle.png");
            yield return null;
            hero.SetFacing(Facing.UpRight);
            var celebration = hero.StartCoroutine(new LevelUpAction(hero).ExecuteRoutine(hero));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(hero.CurrentFacing, Is.EqualTo(Facing.Down));
            ScreenCapture.CaptureScreenshot("Temp/UIScreenAudit/level-up-celebration.png");
            File.WriteAllText("Temp/UIScreenAudit/level-up-pose.txt", "Facing: " + hero.CurrentFacing + "\nVisual: " + hero.VisualParent.transform.eulerAngles +
                "\nAnimator: " + hero.HeroAnimator.Animator.transform.eulerAngles + "\nState: " + hero.HeroAnimator.LastPlayedAction);
            yield return new WaitForSecondsRealtime(.4f);
            ScreenCapture.CaptureScreenshot("Temp/UIScreenAudit/level-up-celebration-later.png");
            yield return celebration;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            hero.Vitals.Level = 2; hero.Attributes = default; hero.SyncDisplayedStats();
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            var choice = LevelUpChoiceDialog.Open(hero, null);
            yield return new WaitForSecondsRealtime(.15f);
            ScreenCapture.CaptureScreenshot("Temp/UIScreenAudit/level-up-popup-celebration.png");
            yield return CaptureSizes("dungeon-level-up-popup", () =>
            {
                var panel = (RectTransform)choice.Heading.transform.parent;
                var surface = (RectTransform)choice.transform;
                Assert.That(panel.rect.width * panel.rect.height, Is.LessThan(surface.rect.width * surface.rect.height * .5f));
                var buttons = new[] { choice.StrButton, choice.IntButton, choice.AgiButton };
                for (int i = 1; i < buttons.Length; i++)
                {
                    Assert.That(buttons[i].transform.position.x, Is.GreaterThan(buttons[i - 1].transform.position.x));
                    Assert.That(buttons[i].transform.position.y, Is.EqualTo(buttons[0].transform.position.y).Within(.1f));
                }
                Assert.That(choice.transform.Find("Input shield").GetComponent<Image>().color.a, Is.Zero);
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                var camera = harness.Game.PlayerController.CameraController.Camera;
                var bounds = CombatEffectPlayer.TargetBounds(hero, hero.TilemapPosition).Value;
                float top = float.NegativeInfinity;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    top = Mathf.Max(top, camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))).y);
                Assert.That(top + 12, Is.LessThan(corners[0].y), "The entire hero must remain below the popup.");
                Assert.That(hero.CurrentFacing, Is.EqualTo(Facing.Down));
            });
            choice.StrButton.onClick.Invoke();
            yield return null;
        }

        [UnityTest] public IEnumerator CaptureDungeonLevelAndResultDialogsAcrossAspectRatios()
        {
            yield return harness.LoadDungeon(new TestScenario { IncludeStartingItems = true });
            yield return CaptureSizes("dungeon-hud");
            foreach (var tab in new[] { PartyMenuTab.Inventory, PartyMenuTab.Equipment, PartyMenuTab.Skills, PartyMenuTab.Stats })
            {
                MenuManager.Instance.OpenPartyMenu(tab); yield return null;
                yield return CaptureSizes("dungeon-" + tab.ToString().ToLowerInvariant());
                MenuManager.Instance.PartyMenu.CloseDialog(); yield return null;
            }
            var stairs = MenuManager.Instance.StairDialog;
            stairs.Setup("Leave this floor and descend to the next one?", () => { }, () => { });
            MenuManager.Open(stairs); yield return null;
            yield return CaptureSizes("dungeon-stairs-confirmation");
            stairs.CloseDialog(); yield return null;
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

        static void AssertServiceLayout(JuicyChickenGames.Menu.Dialog dialog)
        {
            if (dialog is EntranceDialog entrance)
            {
                var title = entrance.transform.Find("Prompt").GetComponent<TMP_Text>();
                AssertLabelFits(title);
                Assert.That(title.textInfo.lineCount, Is.EqualTo(1), "The entrance title stays on its own line.");
                var titleBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(entrance.transform, title.transform);
                var rows = RectTransformUtility.CalculateRelativeRectTransformBounds(entrance.transform, entrance.Container);
                var cancel = RectTransformUtility.CalculateRelativeRectTransformBounds(entrance.transform, entrance.CancelButton.transform);
                Assert.That(titleBounds.min.y, Is.GreaterThan(rows.max.y), "Dungeon choices cannot cover the title.");
                Assert.That(rows.min.y, Is.GreaterThan(cancel.max.y), "Dungeon choices and Cancel need separate rows.");
                var background = RectTransformUtility.CalculateRelativeRectTransformBounds(entrance.transform, entrance.transform.Find("Background"));
                var dialogRect = ((RectTransform)entrance.transform).rect;
                Assert.That(background.size.x, Is.EqualTo(dialogRect.width).Within(.1f), "The authored backdrop must still cover the dialog.");
                Assert.That(background.size.y, Is.EqualTo(dialogRect.height).Within(.1f));
                Assert.That(Vector2.Distance(background.center, dialogRect.center), Is.LessThan(.1f));
                var illustration = RectTransformUtility.CalculateRelativeRectTransformBounds(entrance.transform, entrance.transform.Find("Illustration"));
                Assert.That(illustration.max.x, Is.LessThan(rows.min.x), "The illustration belongs beside the dungeon choices.");
                Assert.That(illustration.min.y, Is.GreaterThan(cancel.max.y), "The illustration must not obscure Cancel's selection feedback.");
                foreach (var item in entrance.Container.GetComponentsInChildren<DungeonTierItem>()) AssertLabelFits(item.TierText);
            }
            else if (dialog is HomeBedDialog home)
            {
                var description = home.transform.Find("Preparation description").GetComponent<TMP_Text>();
                AssertLabelFits(description);
                var save = home.transform.Find("Sleep and Save");
                var descriptionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(home.transform, description.transform);
                var saveBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(home.transform, save);
                Assert.That(descriptionBounds.min.y, Is.GreaterThan(saveBounds.max.y), "Home instructions cannot overlap Save.");
            }

            static void AssertLabelFits(TMP_Text label)
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, $"{label.name} must fit at {Screen.width}x{Screen.height}: {label.text}");
            }
        }


        [UnityTest] public IEnumerator CaptureLearnSkillConfirmationsAcrossAspectRatios()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var town = Object.FindFirstObjectByType<Town>();
            town.TownBuildings.First(building => building.Definition.DialogId == "trainer").Interact(town.TownPlayer, null);
            yield return null;
            var trainer = (BallistaDialog)Object.FindFirstObjectByType<TownMenuManager>().CurrentDialog;
            var confirmation = trainer.BallistaPurchaseDialog;
            var skills = Resources.LoadAll<Skill>("Classes/Skills").OrderBy(skill => skill.DisplayName).ToArray();
            Assert.That(skills.Length, Is.GreaterThan(100), "Exercise the complete authored skill catalog.");
            var samples = skills.OrderByDescending(skill => (skill.Description + SkillPreviewText.Usage(skill)).Length).Take(2)
                .Concat(skills.OrderByDescending(skill => skill.DisplayName.Length).Take(1))
                .Concat(skills.Where(skill => skill.SkillName == "Double Strike")).ToHashSet();
            string directory = "Temp/LearnSkillDialogAudit/" + (terminalAudit ? "Terminal" : "Graphical");
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/summary.txt", "Running");
            TerminalMode.SetRequested(terminalAudit);
            yield return harness.WaitUntil(() => TerminalMode.Effective == terminalAudit, "learn dialog presentation");
            foreach (var resolution in new[] { new Vector2(960, 600), new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) })
            {
                view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { resolution, "Learn skill audit " + resolution });
                view.Repaint();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Screen.width, Is.EqualTo((int)resolution.x));
                Assert.That(Screen.height, Is.EqualTo((int)resolution.y));
                for (int index = 0; index < skills.Length; index++)
                {
                    var skill = skills[index];
                    confirmation.Setup(skill, skill.SkillName == "Double Strike" ? 1 : Mathf.Max(1, skill.MaxRank), 3, usesPoints: true);
                    if (terminalAudit) yield return new WaitForSecondsRealtime(.12f);
                    else yield return null;
                    Canvas.ForceUpdateCanvases();
                    string path = $"{directory}/{index:D3}-{resolution.x}x{resolution.y}";
                    textIssues.AddRange(TextLayoutAudit.Capture(path).Select(issue => skill.DisplayName + ": " + issue));
                    foreach (var label in new[] { confirmation.TitleText, confirmation.DescriptionText })
                    {
                        label.ForceMeshUpdate();
                        if (label.isTextOverflowing) textIssues.Add(skill.DisplayName + ": truncated confirmation: " + label.text);
                        Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(22), "Keep the confirmation readable.");
                    }
                    foreach (var button in new[] { confirmation.CancelButton, confirmation.PurchaseButton })
                    {
                        var label = button.GetComponentInChildren<TMP_Text>();
                        var face = (Image)button.targetGraphic;
                        label.ForceMeshUpdate();
                        Assert.That(face.isActiveAndEnabled, Is.True, "Actions need visible button faces.");
                        Assert.That(face.rectTransform.rect.width, Is.GreaterThan(120), "Actions must retain their width.");
                        Assert.That(label.isTextOverflowing, Is.False, label.text);
                        Assert.That(label.textInfo.lineCount, Is.EqualTo(1), label.text);
                    }
                    if (samples.Contains(skill))
                    {
                        ScreenCapture.CaptureScreenshot(path + ".png");
                        File.WriteAllText(path + ".txt", skill.DisplayName + "\n" + confirmation.TitleText.text + "\n\n" + confirmation.DescriptionText.text);
                        yield return new WaitForSecondsRealtime(.12f);
                    }
                }
            }
            Assert.That(textIssues, Is.Empty, string.Join("\n", textIssues.Take(12)));
            File.WriteAllText(directory + "/summary.txt", $"PASS: {skills.Length} skills at four resolutions; {skills.Length * 4} confirmations checked.");
            confirmation.Cancel_ClickeD();
        }

        [UnityTest] public IEnumerator CaptureTerminalLearnSkillConfirmationsAcrossAspectRatios()
        {
            terminalAudit = true;
            yield return CaptureLearnSkillConfirmationsAcrossAspectRatios();
        }

        [UnityTest] public IEnumerator CaptureHeaderButtonsAcrossScenesAndInputs()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            string previousDevice = ControlDeviceState.BindingGroup;
            try
            {
                yield return harness.LoadMainMenu(null);
                var main = Object.FindFirstObjectByType<MainMenu>();
                Common.Instance.GameSaveData = main.CreateNewSave(42);
                Common.Instance.Travel.NewCampaign(42);
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                    !Common.Instance.ScreenTransition.BlockScreen.activeSelf, "header audit town");
                yield return CaptureHeader("town");
                harness.PrepareOpening();
                var town = Object.FindFirstObjectByType<Town>();
                var context = Common.Instance.CampaignContext;
                Assert.That(context.BeginTownDungeon("story-0"), Is.True);
                Assert.That(context.CompleteDungeon(true), Is.True);
                Assert.That(Common.Instance.Travel.ExitTown(town), Is.True);
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true &&
                    !Common.Instance.ScreenTransition.BlockScreen.activeSelf, "header audit overworld");
                yield return CaptureHeader("overworld");
                yield return harness.Cleanup();
                harness = new GameTestHarness { TimeoutSeconds = 120 };
                yield return harness.LoadDungeon(new TestScenario());
                yield return CaptureHeader("dungeon");
            }
            finally { ControlDeviceState.Set(previousDevice); }
        }

        IEnumerator CaptureHeader(string scene)
        {
            var launcher = Object.FindFirstObjectByType<PartyMenuLauncher>();
            Assert.That(launcher, Is.Not.Null);
            foreach (bool gamepad in new[] { false, true })
            {
                ControlDeviceState.Set(gamepad); launcher.Refresh(); yield return null;
                yield return CaptureSizes("header-" + scene + (gamepad ? "-gamepad" : "-keyboard"), () => {
                    var buttons = new[] { PartyMenuTab.Inventory, PartyMenuTab.Equipment, PartyMenuTab.Skills,
                        PartyMenuTab.Stats, PartyMenuTab.Capabilities }.Select(launcher.ButtonFor)
                        .Where(button => button != null).Append(launcher.OptionsButton).ToArray();
                    foreach (var button in buttons)
                    {
                        var label = button.GetComponentInChildren<TMP_Text>();
                        var face = (Image)button.targetGraphic;
                        label.ForceMeshUpdate();
                        Assert.That(face.enabled && face.rectTransform.rect.width > 80, Is.True, button.name);
                        Assert.That(label.isTextOverflowing, Is.False, label.text);
                        Assert.That(label.textInfo.lineCount, Is.EqualTo(1), label.text);
                        var box = RectTransformUtility.CalculateRelativeRectTransformBounds(face.transform, label.rectTransform);
                        var rect = face.rectTransform.rect;
                        Assert.That(box.min.x - rect.xMin, Is.GreaterThanOrEqualTo(16), label.text + " left border");
                        Assert.That(rect.xMax - box.max.x, Is.GreaterThanOrEqualTo(16), label.text + " right border");
                        Assert.That(box.min.y - rect.yMin, Is.GreaterThanOrEqualTo(9), label.text + " lower border");
                        Assert.That(rect.yMax - box.max.y, Is.GreaterThanOrEqualTo(8), label.text + " upper border");
                    }
                    var resources = Object.FindFirstObjectByType<ResourceHUD>();
                    var parent = launcher.OptionsButton.transform.parent;
                    var strip = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, resources.GoldText.transform.parent);
                    var options = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, launcher.OptionsButton.transform);
                    Assert.That(strip.max.y, Is.LessThan(options.min.y), "Resources must clear the top buttons.");
                });
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
            string directory = terminalAudit ? "Temp/TerminalAudit" : "Temp/UIScreenAudit";
            Directory.CreateDirectory(directory);
            if (terminalAudit)
            {
                TerminalMode.SetRequested(true);
                yield return harness.WaitUntil(() => TerminalMode.Effective, "terminal presentation");
                yield return new WaitForSecondsRealtime(.15f);
                if (screen == "main-menu") yield return new WaitForSecondsRealtime(1.4f);
            }
            foreach (var resolution in new[] { new Vector2(960, 600), new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) })
            {
                view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { resolution, "UI audit " + resolution });
                view.Repaint();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Screen.width, Is.EqualTo((int)resolution.x), "Capture width must match its filename.");
                Assert.That(Screen.height, Is.EqualTo((int)resolution.y), "Capture height must match its filename.");
                Canvas.ForceUpdateCanvases();
                verify?.Invoke();
                string path = $"{directory}/{screen}-{resolution.x}x{resolution.y}";
                textIssues.AddRange(TextLayoutAudit.Capture(path).Select(issue => path + ": " + issue));
                if (terminalAudit)
                {
                    var terminal = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
                    Assert.That(terminal.GetComponent<Canvas>().enabled, Is.True);
                    File.WriteAllText(path + ".txt", terminal.GetComponentInChildren<TextMeshProUGUI>().GetParsedText());
                }
                ScreenCapture.CaptureScreenshot(path + ".png");
                yield return new WaitForSecondsRealtime(.2f);
            }
        }

    }
}
#endif
