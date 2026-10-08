#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Classes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class EndgameDebugStartTests
    {
        private GameTestHarness harness;
        private TestInputScope inputs;
        private DungeonAnimationMode? previousAnimation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputs = new TestInputScope();
            previousAnimation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return harness.Cleanup();
            DungeonPreferences.AnimationOverride = previousAnimation;
            inputs.Dispose();
        }

        [Test]
        public void LevelCapFortyPreservesExistingThresholdsAndStopsAtForty()
        {
            var owner = new GameObject("Level cap test");
            try
            {
                var levels = owner.AddComponent<LevelSystem>();
                Assert.That(LevelSystem.MaxLevel, Is.EqualTo(40));
                Assert.That(LevelSystem.ExperienceAtLevel(37), Is.EqualTo(999999));
                Assert.That(levels.GetLevelUps(36, 999999).Select(level => level.Level), Is.EqualTo(new[] { 37 }));
                Assert.That(levels.GetLevelUps(37, 1099999), Is.Empty);
                Assert.That(levels.GetLevelUps(37, 1100000).Select(level => level.Level), Is.EqualTo(new[] { 38 }));
                Assert.That(levels.GetLevelUps(37, int.MaxValue).Select(level => level.Level), Is.EqualTo(new[] { 38, 39, 40 }));
                Assert.That(levels.GetLevelUps(40, int.MaxValue), Is.Empty);
                Assert.That(LevelSystem.Progress(39, 1250000), Is.EqualTo(.5f));
                Assert.That(LevelSystem.ExperienceToNext(39, 1250000), Is.EqualTo(50000));
                Assert.That(LevelSystem.ExperienceToNext(40, 1300000), Is.Zero);
                Assert.That(LevelSystem.Progress(40, 1300000), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [UnityTest] public IEnumerator TownButtonStartsFullEndgamePartyAndPreservesSaves() => CheckStart(EndgameDebugDestination.Town, true);
        [UnityTest] public IEnumerator DungeonButtonStartsFullEndgamePartyWithoutASave() => CheckStart(EndgameDebugDestination.Dungeon, false);
        [UnityTest] public IEnumerator OverworldButtonStartsFullEndgamePartyAndPreservesSaves() => CheckStart(EndgameDebugDestination.Overworld, true);

        private IEnumerator CheckStart(EndgameDebugDestination destination, bool existingSave)
        {
            yield return harness.LoadMainMenu(existingSave ? new TestScenario { Gold = 731 }.CreateSave() : null);
            var common = Common.Instance;
            foreach (var prefab in TownSceneLoader.Default.AllyCatalog)
                Assert.That(prefab.Equipment, Is.SameAs(prefab.GetComponent<Equipment>()), prefab.Name + " owns its equipment");
            for (int slot = 1; slot < SaveSystem.SlotCount; slot++)
                SaveSystem.SaveData(slot, new TestScenario { Gold = 731 + slot }.CreateSave());
            var before = Enumerable.Range(0, SaveSystem.SlotCount).Select(slot => harness.Store.Read(slot)).ToArray();
            var menu = Object.FindFirstObjectByType<MainMenu>();
            yield return harness.WaitUntil(() => menu.IsReady, "main menu ready");
            var developer = menu.GetComponent<MainMenuDeveloperControls>();
            developer.Toggle.onClick.Invoke();
            yield return null;
            Directory.CreateDirectory("Temp/EndgameDebug");
            if (destination == EndgameDebugDestination.Town)
            {
                ScreenCapture.CaptureScreenshot("Temp/EndgameDebug/main-menu.png");
                yield return new WaitForSecondsRealtime(.3f);
            }
            string callback = destination == EndgameDebugDestination.Town ? nameof(MainMenu.EndgameTown_Clicked) :
                destination == EndgameDebugDestination.Dungeon ? nameof(MainMenu.EndgameDungeon_Clicked) : nameof(MainMenu.EndgameOverworld_Clicked);
            var button = developer.Controls.Select(control => control.GetComponent<Button>()).Where(candidate => candidate != null).Single(candidate =>
                Enumerable.Range(0, candidate.onClick.GetPersistentEventCount()).Any(i => candidate.onClick.GetPersistentMethodName(i) == callback));
            Assert.That(button.IsActive() && button.IsInteractable(), Is.True);
            button.onClick.Invoke();
            yield return harness.WaitUntil(() => destination == EndgameDebugDestination.Town ? Object.FindFirstObjectByType<Town>()?.IsReady == true :
                destination == EndgameDebugDestination.Overworld ? Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true : Object.FindFirstObjectByType<Game>()?.IsReady == true,
                destination + " debug start");
            yield return harness.WaitUntil(() => !common.ScreenTransition.BlockScreen.activeSelf, "debug scene revealed");
            var save = common.GameSaveData;
            var context = common.CampaignContext;
            Assert.That(save.IsSandbox, Is.True);
            Assert.That(context.IsSandbox, Is.False, "Debug party uses the complete campaign menus and travel.");
            Assert.That(context.State.Finished, Is.False);
            Assert.That(context.Completed, Does.Not.Contain(context.Campaign.FinalLocationId));
            Assert.That(save.TownSaveData.RecruitedAlliesData, Has.Count.EqualTo(4));
            Assert.That(context.Active, Has.Count.EqualTo(3));
            Assert.That(save.TownSaveData.Gold, Is.EqualTo(99999));
            foreach (var hero in save.TownSaveData.RecruitedAlliesData)
            {
                Assert.That(hero.Level, Is.EqualTo(40));
                Assert.That(hero.HighestLevel, Is.EqualTo(40));
                Assert.That(hero.Experience, Is.EqualTo(1300000));
                Assert.That(hero.Attributes.Total, Is.EqualTo(39));
                var primary = ClassCatalog.Load().Get(hero.PrimaryClassId);
                var secondary = ClassCatalog.Load().Get(hero.SecondaryClassId);
                foreach (var skill in SkillLearningRules.Offers(HeroClass.ToKit(primary, secondary)))
                    Assert.That(hero.SkillRanks.Single(rank => rank.SkillName == skill.SkillId).Rank, Is.EqualTo(skill.MaxRank));
                var equipped = hero.Equipment.Select(item => (EquipableInventoryItem)item.Restore(common.ItemManager)).ToArray();
                Assert.That(equipped, Is.Not.Empty);
                Assert.That(equipped.All(item => HeroClass.AllowsItem(primary, secondary, item)), Is.True);
                var weapon = equipped.First(item => !item.EquipmentItemDefinition.IsAmmunition);
                Assert.That(weapon.EquipmentItemDefinition.WeaponType, Is.EqualTo(primary.AllowedWeapons[0]));
                Assert.That(weapon.GetEquipmentStatModification().Strength, Is.EqualTo(common.ItemManager.ItemDefinitions
                    .OfType<EquipmentItemDefinition>().Where(item => item.WeaponType == weapon.EquipmentItemDefinition.WeaponType &&
                        !item.IsAmmunition).Max(item => item.StatModification.Strength)), hero.AllyName + " has the strongest weapon of its type");
                if (primary.Id == "archer")
                {
                    var arrows = equipped.Single(item => item.EquipmentItemDefinition.IsAmmunition);
                    Assert.That(arrows.ItemName, Is.EqualTo("Royal Arrows"));
                    Assert.That(arrows.StackStock, Is.EqualTo(arrows.ItemDefinition.StackMax));
                }
            }
            if (destination == EndgameDebugDestination.Dungeon)
            {
                yield return harness.WaitForIdle();
                Assert.That(Game.Instance.Allies, Has.Count.EqualTo(4));
                Assert.That(Game.Instance.PlayerController.Floor, Is.EqualTo(30));
                Assert.That(context.State.PendingDungeon, Is.EqualTo(context.Campaign.FinalLocationId));
                foreach (var hero in Game.Instance.Allies)
                {
                    Assert.That(hero.Vitals.Level, Is.EqualTo(40));
                    Assert.That(hero.Vitals.HP, Is.EqualTo(hero.FinalStats.HPMax));
                    Assert.That(hero.Vitals.SP, Is.EqualTo(hero.FinalStats.SPMax));
                    Assert.That(hero.Vitals.Hunger, Is.EqualTo(hero.FinalStats.HungerMax));
                    Assert.That(hero.Equipment.GetEquippedItems().Select(item => item.ItemName),
                        Is.EquivalentTo(save.Roster.Single(member => member.AllyId == hero.TownAllyId).Equipment.Select(item => item.ItemName)));
                }
            }
            if (destination == EndgameDebugDestination.Town)
            {
                var town = Object.FindFirstObjectByType<Town>();
                Assert.That(town.TownPlayer.RecruitedAllies, Has.Count.EqualTo(4));
                Assert.That(town.TownPlayer.RecruitedAllies.All(hero => hero.Level == 40), Is.True);
                foreach (var hero in town.TownPlayer.RecruitedAllies)
                {
                    Assert.That(hero.Equipment.gameObject, Is.SameAs(hero.gameObject), "Each hero owns its equipment.");
                    Assert.That(hero.Equipment.GetEquippedItems().Select(item => item.ItemName),
                        Is.EquivalentTo(save.Roster.Single(member => member.AllyId == hero.Id).Equipment.Select(item => item.ItemName)));
                }
                Assert.That(context.CanLeaveTown(context.State.LocationId), Is.True);
                town.WriteSaveData();
            }
            var launcher = Object.FindFirstObjectByType<PartyMenuLauncher>();
            launcher.ButtonFor(PartyMenuTab.Skills).onClick.Invoke();
            yield return null;
            var partyMenu = Object.FindObjectsByType<PartyMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(menu => menu.IsRoot);
            Assert.That(partyMenu.HeroesRoot.gameObject.activeSelf, Is.False);
            Assert.That(partyMenu.HeroText.gameObject.activeSelf, Is.False);
            Assert.That(partyMenu.DetailsControl.Scroll.gameObject.activeSelf, Is.False);
            Assert.That(partyMenu.Hints.gameObject.activeSelf, Is.False);
            var list = (RectTransform)partyMenu.scrollView.transform;
            Assert.That(list.anchorMax.y - list.anchorMin.y, Is.GreaterThan(.85f));
            ScreenCapture.CaptureScreenshot($"Temp/EndgameDebug/{destination}.png");
            yield return new WaitForSecondsRealtime(.3f);
            partyMenu.CloseDialog();
            SaveSystem.SaveData(save);
            Assert.That(CampaignSaving.Commit(common, "debug-save-check", null, Facing.Down, out _), Is.False);
            Assert.That(Enumerable.Range(0, SaveSystem.SlotCount).Select(slot => harness.Store.Read(slot)), Is.EqualTo(before));
            Assert.That(common.Travel.ReturnToMenu(), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "return from debug start");
            Assert.That(common.CampaignContext, Is.Null);
            Assert.That(common.GameSaveData?.TownSaveData.Gold, Is.EqualTo(existingSave ? (int?)731 : null));
            Assert.That(Enumerable.Range(0, SaveSystem.SlotCount).Select(slot => harness.Store.Read(slot)), Is.EqualTo(before));
        }
    }
}
#endif
