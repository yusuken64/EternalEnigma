#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class TownGameplayTests
    {
        private GameTestHarness harness;
        private readonly List<Object> assets = new();
        private Town World => Object.FindFirstObjectByType<Town>();
        private TownMenu Menus => Object.FindFirstObjectByType<TownMenu>();
        private TownMenuManager Manager => Object.FindFirstObjectByType<TownMenuManager>();

        [UnitySetUp]
        public IEnumerator SetUp() { harness = new GameTestHarness(); yield return null; }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return harness.Cleanup();
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [UnityTest]
        public IEnumerator PartyWithoutAnimationComponentCanWalk()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var player = World.TownPlayer;
            var ally = player.ControllingTownAlly;
            ally.HeroAnimator = null; // Some campaign companion prefabs have static visuals.
            var from = ally.TilemapPosition;
            var target = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left }
                .Select(offset => from + offset).First(cell => player.WalkableMap.CanWalkTo(from, cell));
            player.SetAction(new TownMovement(player, from, target));
            yield return harness.WaitUntil(() => !player.IsBusy, "static companion movement");
            Assert.That(ally.TilemapPosition, Is.EqualTo(target));
            Assert.That(Vector3.Distance(ally.transform.position, player.WalkableMap.CellToWorld(target)), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator CallerControlsBuildingsAndRecruitsAndShopsHaveIndependentStock()
        {
            var configuration = Object.Instantiate(TownSceneLoader.Default);
            assets.Add(configuration);
            configuration.Id = "TestTown";
            var first = configuration.Buildings.First(b => b.DialogId == "shop");
            var second = Object.Instantiate(first);
            assets.Add(second);
            second.Id = "second-shop";
            var custom = Object.Instantiate(first);
            assets.Add(custom);
            custom.Id = "custom-building";
            custom.DialogId = "";
            var template = new GameObject("Custom town dialog");
            Object.DontDestroyOnLoad(template);
            assets.Add(template);
            template.SetActive(false);
            custom.DialogPrefab = template.AddComponent<TestTownDialog>();
            configuration.Buildings = new() { first, second, custom };
            var recruit = configuration.Recruits.First(r => r.Ally.Name != "Rowan");
            configuration.Recruits = new() { recruit };
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave(), configuration);
            Assert.That(World.Configuration, Is.SameAs(configuration));
            Assert.That(World.TownBuildings.Select(b => b.Definition), Is.EqualTo(configuration.Buildings));
            Assert.That(World.TownAllies.Select(a => a.Id), Is.EqualTo(new[] { recruit.Ally.Id }));
            World.TownBuildings[2].Interact(World.TownPlayer, null);
            Assert.That(((TestTownDialog)Manager.CurrentDialog).Context.Building, Is.SameAs(custom));
            Manager.CurrentDialog.CloseDialog();
            var offer = first.ShopCatalog[0];
            Assert.That(World.Services.Buy(first, offer.Item.ItemName, out _), Is.True);
            Assert.That(World.Services.Shop(first).Stock[0].Remaining, Is.EqualTo(offer.Quantity - 1));
            Assert.That(World.Services.Shop(second).Stock[0].Remaining, Is.EqualTo(offer.Quantity));
            // Exit/Continue must retain caller configuration and the purchased stock.
            Common.Instance.GlobalSettings.MainMenu_Clicked();
            yield return null;
            Object.FindFirstObjectByType<MainMenu>().Continue_Clicked();
            yield return harness.WaitUntil(() => World != null && World.IsReady, "configured town return");
            Assert.That(World.Configuration, Is.SameAs(configuration));
            Assert.That(World.Services.Shop(first).Stock[0].Remaining, Is.EqualTo(offer.Quantity - 1));
        }

        [UnityTest]
        public IEnumerator ShopPurchasesPersistAndSoldOutOrUnaffordablePurchasesDoNotCharge()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var shop = World.Configuration.Buildings.First(b => b.DialogId == "shop");
            var offer = shop.ShopCatalog[0];
            for (int i = 0; i < offer.Quantity; i++)
                Assert.That(World.Services.Buy(shop, offer.Item.ItemName, out _), Is.True);
            int gold = World.TownPlayer.Gold;
            Assert.That(World.Services.Buy(shop, offer.Item.ItemName, out _), Is.False);
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(gold));
            var saved = SaveSystem.LoadData().TownSaveData;
            Assert.That(saved.InventoryItems.Count, Is.EqualTo(offer.Quantity));
            Assert.That(saved.Shops.Single().Stock[0].Remaining, Is.Zero);
            World.TownPlayer.Gold = 0;
            Assert.That(World.Services.Buy(shop, shop.ShopCatalog[1].Item.ItemName, out _), Is.False);
            Assert.That(World.TownPlayer.Gold, Is.Zero);
            World.TownPlayer.Gold = 10000;
            World.TownBuildings.First(b => b.Definition == shop).Interact(World.TownPlayer, null);
            yield return null;
            var view = (ShopMenuDialog)Manager.CurrentDialog;
            view.ShopItems[1].BuyButton.onClick.Invoke();
            yield return null;
            var confirmation = (BuyConfirmationDialog)Manager.CurrentDialog;
            confirmation.Ok_Clicked();
            int afterPurchase = World.TownPlayer.Gold;
            confirmation.Ok_Clicked();
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(afterPurchase), "A confirmation can commit only once.");
            Assert.That(afterPurchase, Is.EqualTo(10000 - shop.ShopCatalog[1].Price));
        }

        [UnityTest]
        public IEnumerator TrainingSavesBeforeDialogClosesAndCannotChargeTwice()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var building = World.TownBuildings.First(b => b.Definition.DialogId == "trainer");
            building.Interact(World.TownPlayer, null);
            yield return null;
            var trainer = (BallistaDialog)Manager.CurrentDialog;
            var ally = World.TownPlayer.ControllingTownAlly;
            var offers = TrainerOffers.Build(ally, World.Configuration);
            int index = offers.FindIndex(o => o.CanLearn && o.CurrentRank == 0);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "The starting hero's class offers a learnable skill.");
            var skill = offers[index].Skill;
            int cost = offers[index].NextCost;
            trainer.SkillGridItems[index].ToggleOn_Clicked();
            yield return null;
            trainer.BallistaPurchaseDialog.Purchase_Clicked();
            Assert.That(Manager.CurrentDialog, Is.SameAs(trainer));
            var save = SaveSystem.LoadData().TownSaveData;
            Assert.That(save.RecruitedAlliesData[0].Skills, Does.Contain(skill.SkillName));
            Assert.That(save.Gold, Is.EqualTo(10000 - cost));
            Assert.That(World.Services.Learn(ally, skill, out _), Is.False, "Rank 2 needs a higher level (or the skill is single-rank).");
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(save.Gold));
        }

        [UnityTest]
        public IEnumerator DonationsUnlockConfiguredTiersAndCannotSpendMoreThanGold()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 1000 }.CreateSave());
            var tier = World.Configuration.DungeonTiers.First(t => t.RequiredDonation == 1000);
            Assert.That(World.Services.CanEnter(tier), Is.False);
            Assert.That(World.Services.Donate(-20), Is.Zero);
            Assert.That(World.Services.Donate(999), Is.EqualTo(999));
            Assert.That(World.Services.CanEnter(tier), Is.False);
            Assert.That(World.Services.Donate(int.MaxValue), Is.EqualTo(1));
            Assert.That(World.Services.CanEnter(tier), Is.True);
            Assert.That(SaveSystem.LoadData().TownSaveData.DonationTotal, Is.EqualTo(1000));
            Assert.That(World.TownPlayer.Gold, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TownInventoryUsesSharedActionMenuAndSavesEquipment()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var item = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>().First().AsInventoryItem(null);
            var player = World.TownPlayer;
            player.Inventory.Add(item);
            Manager.Open(Menus.InventoryMenu);
            Menus.InventoryMenu.SetupTown(player.Inventory, player.ControllingTownAlly);
            Menus.InventoryMenu.SetNavigation();
            yield return null;
            Menus.InventoryMenu.InventoryMenuItems[0].onClick.Invoke();
            yield return null;
            Assert.That(Manager.CurrentDialog, Is.SameAs(Menus.ItemActionDialog));
            Menus.ItemActionDialog.Use_Clicked();
            yield return null;
            Assert.That(player.ControllingTownAlly.Equipment.IsEquipped(item), Is.True);
            Assert.That(player.Inventory, Has.No.Member(item));
            Assert.That(SaveSystem.LoadData().TownSaveData.RecruitedAlliesData[0].Equipment[0].ItemName, Is.EqualTo(item.ItemName));
            Menus.InventoryMenu.InventoryMenuItems[0].onClick.Invoke();
            yield return null;
            Menus.ItemActionDialog.Use_Clicked();
            Assert.That(player.Inventory.Count(i => i == item), Is.EqualTo(1));
            Assert.That(player.ControllingTownAlly.Equipment.IsEquipped(item), Is.False);
        }

        [UnityTest]
        public IEnumerator RecruitmentAndDismissalProtectPartyAndControlledAlly()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var player = World.TownPlayer;
            var original = player.ControllingTownAlly;
            Assert.That(World.Services.Dismiss(original, out _), Is.False);
            var recruit = World.TownAllies.First();
            Assert.That(World.Services.Recruit(recruit, out _), Is.True);
            Assert.That(World.Services.Dismiss(original, out _), Is.True);
            Assert.That(player.ControllingTownAlly, Is.SameAs(recruit));
            Assert.That(player.RecruitedAllies, Has.Count.EqualTo(1));
            Assert.That(SaveSystem.LoadData().TownSaveData.RecruitedAlliesData[0].AllyId, Is.EqualTo(recruit.Id));
        }

        [UnityTest] public IEnumerator VictoryReturnsRemainingStacksAndEquipment() => DungeonRoundTrip(true);
        [UnityTest] public IEnumerator DefeatLosesItemsButKeepsGold() => DungeonRoundTrip(false);

        [UnityTest]
        public IEnumerator LeavingDungeonThroughSettingsCommitsDefeatRules()
        {
            yield return harness.LoadDungeon(new TestScenario { Gold = 250 });
            harness.Game.PlayerController.Gold = 70;
            var item = Common.Instance.ItemManager.ItemDefinitions.First().AsInventoryItem(null);
            harness.Game.PlayerController.Inventory.Add(item);
            Common.Instance.GlobalSettings.MainMenu_Clicked();
            yield return null;
            var save = SaveSystem.LoadData();
            Assert.That(save.TownSaveData.Gold, Is.EqualTo(320));
            Assert.That(save.TownSaveData.InventoryItems, Is.Empty);
            Assert.That(save.DungeonSaveData.ReturnCommitted, Is.True);
        }

        private IEnumerator DungeonRoundTrip(bool victory)
        {
            yield return harness.LoadTown(new TestScenario { Gold = 250 }.CreateSave());
            var configuration = World.Configuration;
            var weapon = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>().First().AsInventoryItem(null);
            var supply = Common.Instance.ItemManager.ItemDefinitions.First(d => d.StackMax >= 3).AsInventoryItem(3);
            World.TownPlayer.Inventory.Add(weapon);
            World.TownPlayer.Inventory.Add(supply);
            World.Services.ToggleEquipment(World.TownPlayer.ControllingTownAlly, weapon);
            World.TownBuildings.First(b => b.Definition.DialogId == "entrance").Interact(World.TownPlayer, null);
            ((EntranceDialog)Manager.CurrentDialog).DungeonClicked(configuration.DungeonTiers[0]);
            yield return harness.WaitForIdle();
            Assert.That(harness.Ally.Equipment.GetEquippedItems().Single().ItemName, Is.EqualTo(weapon.ItemName));
            var carried = harness.Game.PlayerController.Inventory.InventoryItems.Single();
            Assert.That(carried.StackStock, Is.EqualTo(3));
            carried.Decrement();
            harness.Game.PlayerController.Gold = 70;
            GameOverScreen.GoBackToTown(victory, harness.Game.PlayerController);
            yield return harness.WaitUntil(() => World != null && World.IsReady, "dungeon return to town");
            Assert.That(World.Configuration, Is.SameAs(configuration));
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(320));
            Assert.That(World.TownPlayer.Inventory.Count, Is.EqualTo(victory ? 1 : 0));
            Assert.That(World.TownPlayer.ControllingTownAlly.Equipment.GetEquippedItems().Count(), Is.EqualTo(victory ? 1 : 0));
            if (victory) Assert.That(World.TownPlayer.Inventory[0].StackStock, Is.EqualTo(2));
            var saved = SaveSystem.LoadData();
            if (victory)
            {
                Assert.That(saved.TownSaveData.InventoryItems[0].Stock, Is.EqualTo(2));
                Assert.That(saved.TownSaveData.RecruitedAlliesData[0].Equipment[0].ItemName, Is.EqualTo(weapon.ItemName));
            }
            Assert.That(saved.TownSaveData.RestockVersion, Is.EqualTo(1));
            Assert.That(saved.TownSaveData.CompletedTiers.Count, Is.EqualTo(victory ? 1 : 0));
            DungeonReturnService.Commit(saved, configuration, victory, 70, new InventoryItem[0], new Ally[0]);
            Assert.That(saved.TownSaveData.Gold, Is.EqualTo(320), "Return must commit only once.");
        }
    }

    public class TestTownDialog : Dialog
    {
        public TownInteractionContext Context;
        public override void PrepareTown(TownInteractionContext context) => Context = context;
        internal override void SetFirstSelect() { }
    }
}
#endif
