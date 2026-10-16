#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
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
    }
}
#endif
