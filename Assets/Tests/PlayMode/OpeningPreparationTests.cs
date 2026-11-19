#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class OpeningPreparationTests
    {
        GameTestHarness harness;
        TestInputScope inputs;
        DungeonAnimationMode? animation;
        UnityEditor.EditorWindow view;
        int size;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Town Town => Object.FindFirstObjectByType<Town>();
        Common Common => global::Common.Instance;
        CampaignContext Context => Common.CampaignContext;
        TownAlly Hero => Town.TownPlayer.RecruitedAllies.Single(a => a.Id == Common.GameSaveData.ProtagonistId);
        TownBuildingDefinition Bakery => Town.Configuration.Buildings.Single(b => b.Id == "bakery");
        HomeBed Bed => Object.FindFirstObjectByType<HomeBed>();

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope();
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            size = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { new Vector2(960, 600), "Opening preparation 960x600" });
            Directory.CreateDirectory("Temp/OpeningPreparation");
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (AutoplayRunner.Active != null)
            {
                AutoplayRunner.Active.Report.ValidationOnly = true;
                AutoplayRunner.Active.ExitDemo();
                yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "opening autoplay exit");
            }
            yield return harness.Cleanup();
            DungeonPreferences.AnimationOverride = animation;
            view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, size);
            inputs.Dispose();
        }
        IEnumerator StartCampaign()
        {
            yield return harness.LoadMainMenu(null);
            Common.GameSaveData = Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);
            Common.Travel.NewCampaign(42);
            yield return WaitTown();
        }
        IEnumerator WaitTown() => harness.WaitUntil(() => Town?.IsReady == true &&
            !Common.ScreenTransition.BlockScreen.activeSelf && !Common.Travel.IsTransitioning, "opening town");
        IEnumerator Capture(string name)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot("Temp/OpeningPreparation/" + name + ".png");
            yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator CaptureGate(string name)
        {
            var hero = Hero; var position = hero.transform.position; var tile = hero.TilemapPosition;
            var entrance = Town.TownBuildings.Single(b => b.Definition.DialogId == "entrance");
            hero.TilemapPosition = entrance.TilemapPosition - Vector3Int.up;
            hero.transform.position = Town.WalkableMap.CellToWorld(hero.TilemapPosition);
            Town.TownPlayer.CameraController.SnapToFollowTarget();
            yield return Capture(name);
            hero.TilemapPosition = tile; hero.transform.position = position;
            Town.TownPlayer.CameraController.SnapToFollowTarget();
        }
        Button BedAction() => Object.FindFirstObjectByType<HomeBedDialog>().GetComponentsInChildren<Button>()
            .Single(b => b.GetComponentInChildren<TMP_Text>().text != "Back");

        [UnityTest] public IEnumerator BookPromptUsesCurrentBindingAndOnlyAppearsForAnAvailableUnreadBook()
        {
            var preferences = GamePreferences.Isolate();
            ControlBindings.Reload();
            try
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                var pad = InputSystem.AddDevice<Gamepad>();
                yield return StartCampaign();
                var devices = new InputDevice[] { keyboard, pad };
                Common.MenuInputHandler.PlayerInput.actions.devices = devices;
                MenuUIInputModule.Active.actionsAsset.devices = devices;
                IEnumerator KeyPress(Key key)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null; yield return null;
                }
                yield return KeyPress(Key.F8);
                var canvas = Town.GetComponentsInChildren<Canvas>(true).Single(c => c.name == "Current objective");
                var label = canvas.GetComponentInChildren<TMP_Text>(true);
                Assert.That(canvas.enabled, Is.False, "The opening movement banner is gone.");
                Assert.That(Context.OpeningObjective, Is.EqualTo("Read the book in your home."));
                yield return Capture("home-quiet-opening");

                var home = Town.HomeLesson;
                Assert.That(home.TryGetInteractionStand(home.BookCell, out var stand), Is.True);
                Hero.TilemapPosition = stand;
                Hero.transform.position = Town.WalkableMap.CellToWorld(stand);
                var facing = new TownMovement(Town.TownPlayer, stand, home.BookCell).GetFacing(home.BookCell - stand);
                Hero.SetFacing(facing);
                Town.TownPlayer.StopHeldWalk(); Town.RefreshRoofs(); Town.TownPlayer.CameraController.SnapToFollowTarget();
                yield return null; yield return null;
                Assert.That(canvas.enabled, Is.True);
                Assert.That(label.text, Is.EqualTo("[Space] Read"));
                foreach (var resolution in new[] { new Vector2(960, 600), new Vector2(1280, 720), new Vector2(2560, 1080) })
                {
                    view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { resolution, "Book prompt" });
                    yield return null; yield return null;
                    Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False);
                    Assert.That(home.TryGetBookPromptAnchor(out var anchor), Is.True);
                    var screen = Town.TownPlayer.CameraController.Camera.WorldToScreenPoint(anchor);
                    var corners = new Vector3[4]; ((RectTransform)label.transform.parent).GetWorldCorners(corners);
                    Assert.That((corners[0].x + corners[2].x) / 2, Is.EqualTo(screen.x).Within(2));
                    Assert.That(corners[0].y, Is.GreaterThan(screen.y));
                    Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(0));
                    Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width));
                    yield return Capture($"book-prompt-{(int)resolution.x}x{(int)resolution.y}");
                }

                Hero.SetFacing(facing == Facing.Down ? Facing.Up : Facing.Down);
                yield return null; yield return null;
                Assert.That(canvas.enabled, Is.False, "Facing away hides the prompt.");
                Hero.SetFacing(facing);
                var position = Hero.TilemapPosition;
                Hero.TilemapPosition -= Dungeon.GetFacingOffset(facing);
                yield return null; yield return null;
                Assert.That(canvas.enabled, Is.False, "The book must be in interaction range.");
                Hero.TilemapPosition = position;
                Common.GlobalSettings.ShowDialog();
                yield return null; yield return null;
                Assert.That(canvas.enabled, Is.False, "Settings cover the interaction.");
                Common.GlobalSettings.Exit_Clicked();
                yield return null; yield return null;
                Assert.That(canvas.enabled, Is.True);

                var attack = ControlBindings.DisplayAsset.FindAction("Player/Attack", true);
                int keyBinding = Enumerable.Range(0, attack.bindings.Count).Single(i => attack.bindings[i].path == "<Keyboard>/space");
                int padBinding = Enumerable.Range(0, attack.bindings.Count).Single(i => attack.bindings[i].path == "<Gamepad>/buttonSouth");
                ControlBindings.ApplyBinding("Player/Attack", keyBinding, "<Keyboard>/k");
                ControlBindings.ApplyBinding("Player/Attack", padBinding, "<Gamepad>/rightShoulder");
                yield return null; yield return null; yield return null;
                Assert.That(label.text, Is.EqualTo("[K] Read"));
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftStick));
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null; yield return null;
                Assert.That(label.text, Is.EqualTo("[RB] Read"));
                yield return KeyPress(Key.F8);
                Assert.That(label.text, Is.EqualTo("[K] Read"));
                yield return Capture("book-prompt-rebound");
                yield return KeyPress(Key.Space);
                Assert.That(Context.State.ReadHomeBook, Is.False, "The old key no longer reads the book.");
                yield return KeyPress(Key.K);
                Assert.That(Context.State.ReadHomeBook, Is.True, "The advertised rebound key reads the book.");
                Assert.That(canvas.enabled, Is.False);
                var menus = Object.FindFirstObjectByType<TownMenuManager>();
                menus.DialogStack.Peek().CloseDialog();
                yield return null; yield return null;
                Assert.That(canvas.enabled, Is.False, "The hint stays dismissed after reading.");
                Assert.That(Context.OpeningObjective, Is.EqualTo("Open the chest to find your Home Key."));
            }
            finally
            {
                preferences.Dispose();
                ControlBindings.Reload();
            }
        }

        [UnityTest] public IEnumerator FirstTownHasOnlyUsefulHousesAndDungeonSmartTileEntrance()
        {
            yield return StartCampaign();
            Assert.That(Town.Plan.BuildingSlots.Count, Is.EqualTo(8));
            Assert.That(Town.Plan.Interiors.Count(i => i.Spec.Kind == EternalEnigma.Core.World.TownInteriorKind.Residential), Is.EqualTo(1));
            Assert.That(Town.DungeonBook, Is.Not.Null, "The remaining residence contains the adventure book.");
            Assert.That(Town.GetComponentInChildren<HomeBed>(), Is.Not.Null);
            var entrance = Town.TownBuildings.Single(b => b.Definition.DialogId == "entrance");
            var creator = Town.WalkableMap.TileWorldCreator;
            var layer = creator.twcAsset.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().Single(l => l.Buildings);
            var doors = TownDungeonEntranceTiles.Doors(Town.Plan, layer.BuildingServices);
            Assert.That(doors, Does.Contain(entrance.TilemapPosition.ToGridPoint()));
            Assert.That(creator.worldObject.GetComponentsInChildren<TownRoofVisual>(true)
                .Any(roof => roof.Door.Equals(entrance.TilemapPosition.ToGridPoint())), Is.False,
                "The dungeon masonry must remain visible instead of being covered by a house roof.");
            Assert.That(entrance.transform.Find("Dungeon gate"), Is.Not.Null);
            var theme = Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Get(new DungeonVisualSelection {
                Biome = creator.GetComponent<TownBiomeStyle>().Current, Environment = DungeonEnvironmentKind.Interior
            });
            if (theme.RegularSmartBoundary.MaterialOverride != null)
                Assert.That(creator.worldObject.GetComponentsInChildren<MeshRenderer>(true)
                    .SelectMany(r => r.sharedMaterials), Does.Contain(theme.RegularSmartBoundary.MaterialOverride));
            yield return CaptureGate("dungeon-smart-tile-entrance");
        }

        void BuyBread() => Assert.That(Town.Services.Buy(Bakery, "Bread", out var reason), Is.True, reason);
        string LearnSkill()
        {
            var offer = TrainerOffers.Build(Hero, Town.Configuration).First(o => o.CanLearn && o.Skill.ActivationType == ActivationType.Active);
            Assert.That(Town.Services.Learn(Hero, offer.Skill, out var reason), Is.True, reason);
            return offer.Skill.SkillName;
        }

        [UnityTest] public IEnumerator TransactionsMorningFailureReloadAndDefeatPreserveOpening()
        {
            yield return StartCampaign();
            Assert.That(Context.IsPreparingForOpening, Is.True);
            Assert.That(Town.TownPlayer.Inventory.Any(i => i.ItemName == "Bread"), Is.False);
            Assert.That(SaveSystem.LoadData().Campaign.Keys, Does.Not.Contain(CampaignContext.OpeningDayKey), "Initial save is still preparation day.");
            Assert.That(Common.Travel.EnterTownDungeon(Town, "story-0"), Is.False);
            Assert.That(Object.FindFirstObjectByType<OpeningDungeonGate>().IsClosed, Is.True);
            yield return CaptureGate("entrance-closed");
            Bed.Interact(); yield return null;
            Assert.That(BedAction().GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Save and quit"));
            yield return Capture("home-before-errands");
            Object.FindFirstObjectByType<HomeBedDialog>().CloseDialog();
            int writes = harness.Store.Writes;
            Bed.SleepAndSave();
            Assert.That(harness.Store.Writes, Is.EqualTo(writes));
            Common.MessageDialog.Ok_Clicked();

            Town.TownPlayer.Gold = 0;
            var bakery = Town.TownBuildings.Single(b => b.Definition.Id == "bakery");
            bakery.Interact(Town.TownPlayer, null); yield return null;
            var shop = Object.FindFirstObjectByType<ShopMenuDialog>();
            var breadIndex = shop.ShopItemDatas.FindIndex(i => i.ItemName == "Bread");
            Assert.That(shop.ShopItemDatas[breadIndex].Cost, Is.Zero);
            yield return Capture("free-bread");
            shop.ShopItems[breadIndex].BuyButton.onClick.Invoke(); yield return null;
            shop.BuyConfirmationDialog.Cancel_Clicked();
            Assert.That(Context.State.BoughtOpeningBread, Is.False);
            shop.ShopItems[breadIndex].BuyButton.onClick.Invoke(); yield return null;
            shop.BuyConfirmationDialog.Ok_Clicked(); yield return null;
            Assert.That(Context.State.BoughtOpeningBread, Is.True);
            Assert.That(Town.TownPlayer.Gold, Is.Zero);
            Assert.That(Town.Services.Catalog(Bakery).Single(o => o.Item.ItemName == "Bread").Price, Is.GreaterThan(0));
            shop.Cancel_Clicked();

            var trainer = Town.TownBuildings.Single(b => b.Definition.Id == "trainer");
            trainer.Interact(Town.TownPlayer, null); yield return null;
            var lesson = Object.FindFirstObjectByType<BallistaDialog>();
            Assert.That(lesson.SkillGridItems, Is.Not.Empty);
            Assert.That(lesson.SkillGridItems.Any(i => i.Offer.CanLearn && i.Offer.Skill.ActivationType == ActivationType.Active), Is.True);
            Assert.That(lesson.SkillGridItems.All(i => i.GridButton.interactable), Is.True);
            Assert.That(lesson.SkillGridItems.Where(i => i.Offer.Skill.ActivationType == ActivationType.Passive).All(i => !i.Offer.CanLearn), Is.True);
            yield return Capture("first-lesson");
            var row = lesson.SkillGridItems.First(i => i.Offer.CanLearn); string skillName = row.Offer.Skill.SkillName;
            int points = TrainerOffers.AvailableSkillPoints(Hero), cost = row.Offer.NextCost;
            row.ToggleOn_Clicked(); yield return null;
            lesson.BallistaPurchaseDialog.Cancel_ClickeD();
            Assert.That(Context.State.LearnedOpeningSkill, Is.False);
            yield return new WaitForSeconds(.5f);
            row.ToggleOn_Clicked(); yield return null;
            lesson.BallistaPurchaseDialog.Purchase_Clicked(); yield return null;
            Assert.That(Context.State.LearnedOpeningSkill, Is.True);
            Assert.That(TrainerOffers.AvailableSkillPoints(Hero), Is.EqualTo(points - cost));
            Assert.That(lesson.SkillGridItems.Any(i => i.Offer.Skill.ActivationType == ActivationType.Passive), Is.True);
            lesson.Close_Clicked();
            Assert.That(Town.Services.SaveGame(out var innError), Is.True, innError);
            Assert.That(Context.IsPreparingForOpening, Is.True, "Inn saves cannot advance the opening.");
            Assert.That(Common.Travel.EnterTownDungeon(Town, "story-0"), Is.False);

            Bed.Interact(); yield return null;
            Assert.That(BedAction().GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Sleep and save until morning"));
            foreach (var text in Object.FindFirstObjectByType<HomeBedDialog>().GetComponentsInChildren<TMP_Text>())
            { text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.text); }
            yield return Capture("ready-for-morning");
            string before = harness.Store.Json;
            harness.Store.FailWrites = true;
            BedAction().onClick.Invoke(); yield return null;
            Assert.That(harness.Store.Json, Is.EqualTo(before));
            Assert.That(Context.IsPreparingForOpening, Is.True);
            Assert.That(Common.MessageDialog.PromptText.text, Does.Contain("Saving failed"));
            harness.Store.FailWrites = false; Common.MessageDialog.Ok_Clicked(); yield return null;
            Bed.Interact(); yield return null;
            BedAction().onClick.Invoke(); yield return null;
            Assert.That(Context.IsPreparingForOpening, Is.False);
            Assert.That(Object.FindFirstObjectByType<OpeningDungeonGate>().IsClosed, Is.False);
            Assert.That(SaveSystem.LoadData().Campaign.Keys, Contains.Item(CampaignContext.OpeningDayKey));
            Assert.That(Context.CanLeaveTown("town-0"), Is.False, "Opening day is separate from the dungeon victory key.");
            Common.MessageDialog.Ok_Clicked();
            yield return CaptureGate("entrance-open");
            Assert.That(Common.Travel.EnterTownDungeon(Town, "story-0"), Is.True);
            yield return harness.WaitForIdle();
            GameOverScreen.Retreat(Game.Instance.PlayerController);
            yield return WaitTown();
            Assert.That(Context.IsPreparingForOpening, Is.False);
            Assert.That(Hero.GetRank(skillName), Is.EqualTo(1));
            Assert.That(Town.TownPlayer.Inventory.Any(i => i.ItemName == "Bread"), Is.True);
            Assert.That(InnCheckpoint.TryRestore(Common), Is.True);
            yield return WaitTown();
            Assert.That(Context.IsPreparingForOpening, Is.False);
            Assert.That(Hero.GetRank(skillName), Is.EqualTo(1));
            Assert.That(Context.IsGridGenerated, Is.False);
        }

        [UnityTest] public IEnumerator SkillFirstEarlySaveAndConsumedBreadStillOpenTomorrow()
        {
            yield return StartCampaign();
            string skill = LearnSkill();
            Assert.That(Context.ReadyForOpeningDay, Is.False);
            Bed.Interact(); yield return null;
            BedAction().onClick.Invoke();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "early save and quit");
            Common.GameSaveData = SaveSystem.LoadData(); Common.Travel.Continue();
            yield return WaitTown();
            Assert.That(Hero.GetRank(skill), Is.EqualTo(1));
            Assert.That(Context.State.LearnedOpeningSkill, Is.True);
            Assert.That(Context.State.BoughtOpeningBread, Is.False);
            Assert.That(Context.IsPreparingForOpening, Is.True);
            BuyBread();
            var bread = Town.TownPlayer.Inventory.Single(i => i.ItemName == "Bread");
            Assert.That(Town.Services.Sell(bread, out var reason), Is.True, reason);
            Assert.That(Context.ReadyForOpeningDay, Is.True, "Completion tracks the transaction, not possession.");
            Assert.That(Town.Services.OffersOpeningBread(Bakery), Is.False);
            Bed.SleepAndSave(); yield return null;
            Assert.That(Context.IsPreparingForOpening, Is.False);
            Assert.That(SaveSystem.LoadData().Campaign.BoughtOpeningBread, Is.True);
            Assert.That(Town.TownPlayer.Inventory.Any(i => i.ItemName == "Bread"), Is.False);
        }

        [UnityTest] public IEnumerator EverySelectableHeroHasAnAffordableActiveFirstLesson()
        {
            yield return StartCampaign();
            string originalId = Common.GameSaveData.ProtagonistId;
            int checkedHeroes = 0;
            foreach (var prefab in TownSceneLoader.Default.AllyCatalog)
            {
                var hero = Object.Instantiate(prefab, Common.TownAllyParent);
                try
                {
                    Common.GameSaveData.ProtagonistId = hero.Id;
                    hero.EnsureStartingSkills();
                    var offers = TrainerOffers.Build(hero, Town.Configuration);
                    Assert.That(offers, Is.Not.Empty, hero.Name);
                    Assert.That(offers.Any(o => o.CanLearn && o.CurrentRank == 0 && o.Skill.ActivationType == ActivationType.Active &&
                        o.UsesPoints && o.NextCost <= TrainerOffers.AvailableSkillPoints(hero)), Is.True, hero.Name);
                    Assert.That(offers.Where(o => o.CanLearn).All(o => o.CurrentRank == 0 && o.Skill.ActivationType == ActivationType.Active), Is.True, hero.Name);
                    checkedHeroes++;
                }
                finally { Object.Destroy(hero.gameObject); }
            }
            Common.GameSaveData.ProtagonistId = originalId;
            Assert.That(checkedHeroes, Is.GreaterThanOrEqualTo(24));
            Assert.That(Context.State.LearnedOpeningSkill, Is.False);
            yield return null;
            var companion = Town.TownAllies.First(a => a.PrimaryClass != null);
            Town.TownPlayer.Gold = companion.RecruitCost;
            Assert.That(Town.Services.Recruit(companion, out var recruitReason), Is.True, recruitReason);
            var companionLesson = TrainerOffers.Build(companion, Town.Configuration)
                .First(o => o.CanLearn && o.Skill.ActivationType == ActivationType.Active);
            Assert.That(Town.Services.Learn(companion, companionLesson.Skill, out var learnReason), Is.True, learnReason);
            Assert.That(Context.State.LearnedOpeningSkill, Is.False, "The required lesson belongs to the protagonist.");
            Town.TownPlayer.ControllingTownAlly = companion;
            Town.TownBuildings.Single(b => b.Definition.Id == "trainer").Interact(Town.TownPlayer, null);
            yield return null;
            var trainer = Object.FindFirstObjectByType<BallistaDialog>();
            Assert.That(trainer.Character, Is.SameAs(Hero), "The first lesson selects the protagonist even when a companion leads.");
            Assert.That(trainer.SkillGridItems.Count, Is.EqualTo(1));
            Assert.That(trainer.SkillGridContainer.GetComponentsInChildren<TMP_Text>()
                .Any(label => label.transform.parent == trainer.SkillGridContainer), Is.False, "The first lesson needs no heading above its only skill.");
            Assert.That(trainer.Layout.GetComponentsInChildren<MenuControlHints>(), Is.Empty);
            trainer.Layout.Preview.ForceMeshUpdate();
            Assert.That(trainer.Layout.Preview.isTextOverflowing, Is.False, trainer.Layout.Preview.text);
            yield return Capture("first-lesson");
            trainer.SkillGridItems[0].ToggleOn_Clicked(); yield return null;
            trainer.BallistaPurchaseDialog.Purchase_Clicked(); yield return null;
            Assert.That(Context.State.LearnedOpeningSkill, Is.True);
            trainer.Layout.Preview.ForceMeshUpdate();
            Assert.That(trainer.Layout.Preview.isTextOverflowing, Is.False, trainer.Layout.Preview.text);
            yield return Capture("first-lesson-learned");
            trainer.Close_Clicked();
            Town.TownPlayer.ControllingTownAlly = Hero;
        }

        [UnityTest] public IEnumerator AutoplayCompletesTheSameOpeningWithoutBypassingTheLock()
        {
            yield return harness.LoadMainMenu(null);
            AutoplayRunner.WatchDemo(new AutoplayOptions { Seed = 42, Speed = 16, NoAnimations = true, TimeLimitMinutes = 2 });
            var run = AutoplayRunner.Active; run.Report.ValidationOnly = true;
            yield return harness.WaitUntil(() => !run.Running || Game.Instance?.IsReady == true, "autoplay opening dungeon");
            Assert.That(Game.Instance?.IsReady, Is.True, run.Status);
            Assert.That(Context.State.BoughtOpeningBread && Context.State.LearnedOpeningSkill, Is.True);
            Assert.That(SaveSystem.LoadData().Campaign.Keys, Contains.Item(CampaignContext.OpeningDayKey));
            Assert.That(run.Report.Purchases.Any(p => p.Name == "Bread" && p.Cost == 0 && p.Success), Is.True);
            Assert.That(run.Report.Purchases.All(p => p.Success), Is.True, "The changed price after free bread must be re-evaluated.");
            run.Stop();
        }

        [UnityTest] public IEnumerator AutoplayRestsAtHomeAfterTheOpeningClearBeforeLeavingTown()
        {
            yield return harness.LoadMainMenu(null);
            AutoplayRunner.WatchDemo(new AutoplayOptions { Seed = 42, Speed = 32, NoAnimations = true, TimeLimitMinutes = 6,
                DebugPlaythrough = true, Godmode = true, InfiniteStrength = true, InfiniteResources = true });
            var run = AutoplayRunner.Active; run.Report.ValidationOnly = true;
            float timeout = harness.TimeoutSeconds; harness.TimeoutSeconds = 360;
            try
            {
                yield return harness.WaitUntil(() => !run.Running || Context?.Completed.Contains("story-0") == true, "autoplay clearing the opening dungeon");
                Assert.That(run.Running, Is.True, run.Status);
                yield return harness.WaitUntil(() => !run.Running || Context.State.Scene == "Overworld", "autoplay leaving the first town");
            }
            finally { harness.TimeoutSeconds = timeout; }
            Assert.That(Context.State.Scene, Is.EqualTo("Overworld"), run.Status);
            Assert.That(Context.State.OpeningClearNeedsSave, Is.False,
                "\"The town gate is open! Rest at home\" must not follow the run out of town.");
            Assert.That(Context.OpeningObjective, Does.Not.Contain("Rest at home"));
            run.Stop();
        }
    }
}
#endif
