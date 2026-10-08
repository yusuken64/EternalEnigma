#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
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
        public IEnumerator EnteringAndLeavingARoomOnlyChangesItsRoof()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town = World;
            Assert.That(town.Roofs.Count, Is.EqualTo(town.Plan.BuildingSlots.Count));
            Assert.That(town.Roofs.All(r => r.GetComponentsInChildren<Collider>().Length == 0 &&
                r.GetComponentsInChildren<MeshRenderer>().Length > 0), Is.True);
            var roomRoof = town.Roofs.First(r => r.Room != null);
            var otherRoofs = town.Roofs.Where(r => r != roomRoof).ToArray();
            var hero = town.TownPlayer.ControllingTownAlly;
            var door = roomRoof.Door.ToCell();
            var inside = roomRoof.Room.Floor.First().ToCell();
            hero.TilemapPosition = door;
            hero.transform.position = town.WalkableMap.CellToWorld(door);
            town.RefreshRoofs();
            Assert.That(town.Roofs.All(r => r.gameObject.activeSelf), Is.True);
            town.TownPlayer.SetAction(new TownMovement(town.TownPlayer, door, inside));
            yield return harness.WaitUntil(() => !town.TownPlayer.IsBusy, "enter room");
            Assert.That(roomRoof.gameObject.activeSelf, Is.False);
            Assert.That(otherRoofs.All(r => r.gameObject.activeSelf), Is.True);
            town.TownPlayer.SetAction(new TownMovement(town.TownPlayer, inside, door));
            yield return harness.WaitUntil(() => !town.TownPlayer.IsBusy, "leave room");
            Assert.That(town.Roofs.All(r => r.gameObject.activeSelf), Is.True);
            // A spawn or leader change already inside the room uses the same occupancy rule.
            hero.TilemapPosition = inside;
            town.RefreshRoofs();
            Assert.That(roomRoof.gameObject.activeSelf, Is.False);
            Assert.That(otherRoofs.All(r => r.gameObject.activeSelf), Is.True);
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
            custom.ShopCatalog.Clear();
            custom.ServiceInterior = false;
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
            var customBuilding = World.TownBuildings[2];
            var door = customBuilding.TilemapPosition;
            var front = door + Vector3Int.up;
            var footprint = World.Plan.Footprints.Single(f => f.Door.Equals(door.ToGridPoint()));
            var hasBody = footprint.Cells.Count > 0;
            Assert.That(customBuilding.transform.position, Is.EqualTo(World.WalkableMap.CellToWorld(hasBody ? front : door)));
            Assert.That(hasBody, Is.True, "The caller's custom building has an authored footprint.");
            var hero = World.TownPlayer.ControllingTownAlly;
            var approach = door + Vector3Int.down;
            hero.TilemapPosition = approach;
            hero.transform.position = World.WalkableMap.CellToWorld(approach);
            World.TownPlayer.SetAction(new TownMovement(World.TownPlayer, approach, door));
            yield return harness.WaitUntil(() => Manager.CurrentDialog is TestTownDialog, "non-interior door interaction");
            Assert.That(((TestTownDialog)Manager.CurrentDialog).Context.Building, Is.SameAs(custom));
            Manager.CurrentDialog.CloseDialog();
            var offer = first.ShopCatalog[0];
            Assert.That(World.Services.Buy(first, offer.Item.ItemName, out _), Is.True);
            Assert.That(World.Services.Shop(first).Stock[0].Remaining, Is.EqualTo(offer.Quantity - 1));
            Assert.That(World.Services.Shop(second).Stock[0].Remaining, Is.EqualTo(offer.Quantity));
            // Explicit checkpoints retain stock when the caller reloads its configured town.
            World.WriteSaveData();
            SaveSystem.SaveData(Common.Instance.GameSaveData);
            Common.Instance.GlobalSettings.MainMenu_Clicked();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "main menu return");
            Common.Instance.GameSaveData = SaveSystem.LoadData();
            TownSceneLoader.Load(configuration);
            yield return harness.WaitUntil(() => World != null && World.IsReady, "configured town return");
            Assert.That(World.Configuration, Is.SameAs(configuration));
            Assert.That(World.Services.Shop(first).Stock[0].Remaining, Is.EqualTo(offer.Quantity - 1));
        }

        [UnityTest]
        public IEnumerator ShopItemsRestoreFromSavedNames()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var items = Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings")
                .SelectMany(b => b.ShopCatalog).Select(o => o.Item).ToList();
            Assert.That(items.Any(item => item.ItemName == "Wooden Buckler"), Is.True);
            foreach (var item in items)
            {
                var saved = ItemSaveData.From(item.AsInventoryItem(null));
                Assert.That(saved.Restore(Common.Instance.ItemManager).ItemDefinition, Is.SameAs(item), item.ItemName);
            }
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
            Assert.That(SaveSystem.LoadData().TownSaveData.InventoryItems, Is.Empty,
                "Purchases do not overwrite the checkpoint until an explicit save.");
            SaveSystem.SaveData(Common.Instance.GameSaveData);
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
            trainer.SkillGridItems[index].ToggleOn_Clicked();
            yield return null;
            trainer.BallistaPurchaseDialog.Purchase_Clicked();
            Assert.That(Manager.CurrentDialog, Is.SameAs(trainer));
            Assert.That(Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData[0].Skills, Does.Contain(skill.SkillName));
            Assert.That(SaveSystem.LoadData().TownSaveData.RecruitedAlliesData[0].Skills, Does.Not.Contain(skill.SkillName));
            SaveSystem.SaveData(Common.Instance.GameSaveData);
            var save = SaveSystem.LoadData().TownSaveData;
            Assert.That(save.RecruitedAlliesData[0].Skills, Does.Contain(skill.SkillName));
            Assert.That(save.Gold, Is.EqualTo(10000), "Skills cost points, not gold.");
            Assert.That(World.Services.Learn(ally, skill, out _), Is.False, "Rank 2 needs a higher level (or the skill is single-rank).");
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(save.Gold));
        }

        [UnityTest]
        public IEnumerator DungeonTiersAreAvailableWithoutDonations()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 1000 }.CreateSave());
            var tier = World.Configuration.DungeonTiers.First(t => t.RequiredDonation == 1000);
            Assert.That(World.Services.CanEnter(tier), Is.True);
            Assert.That(World.Configuration.Buildings.Any(b => b.DialogId == "statue"), Is.False);
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(1000));
        }

        [UnityTest]
        public IEnumerator TownInventoryUsesSharedActionMenuAndSavesEquipment()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var player = World.TownPlayer;
            var hero = player.ControllingTownAlly;
            var item = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
                .Select(d => (EquipableInventoryItem)d.AsInventoryItem(null))
                .First(i => HeroClass.AllowsItem(hero.PrimaryClass, hero.SecondaryClass, i));
            player.Inventory.Add(item);
            yield return null;
            Assert.That(Manager.CurrentDialog, Is.SameAs(Menus.ItemActionDialog));
            Menus.ItemActionDialog.Use_Clicked();
            yield return null;
            Assert.That(player.ControllingTownAlly.Equipment.IsEquipped(item), Is.True);
            Assert.That(player.Inventory, Has.No.Member(item));
            SaveSystem.SaveData(Common.Instance.GameSaveData);
            Assert.That(SaveSystem.LoadData().TownSaveData.RecruitedAlliesData[0].Equipment[0].ItemName, Is.EqualTo(item.ItemName));
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
            Assert.That(Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData[0].AllyId, Is.EqualTo(recruit.Id));
        }

        [UnityTest] public IEnumerator VictoryReturnsRemainingStacksAndEquipment() => DungeonRoundTrip(true);
        [UnityTest] public IEnumerator DefeatLosesItemsButKeepsGold() => DungeonRoundTrip(false);

        [UnityTest]
        public IEnumerator LeavingDungeonThroughSettingsCommitsDefeatRules()
        {
            yield return harness.LoadDungeon(new TestScenario { Gold = 250 });
            harness.Game.PlayerController.Gold += 70;
            var item = Common.Instance.ItemManager.ItemDefinitions.First().AsInventoryItem(null);
            harness.Game.PlayerController.Inventory.Add(item);
            Common.Instance.GlobalSettings.MainMenu_Clicked();
            yield return null;
            var save = Common.Instance.GameSaveData;
            Assert.That(save.TownSaveData.Gold, Is.EqualTo(320));
            Assert.That(save.TownSaveData.InventoryItems, Is.Empty);
            Assert.That(save.DungeonSaveData.ReturnCommitted, Is.True);
            Assert.That(SaveSystem.LoadData().TownSaveData.Gold, Is.EqualTo(250),
                "Abandoning a run does not overwrite the explicit checkpoint.");
        }

        private IEnumerator DungeonRoundTrip(bool victory)
        {
            yield return harness.LoadTown(new TestScenario { Gold = 250 }.CreateSave());
            var configuration = World.Configuration;
            var hero = World.TownPlayer.ControllingTownAlly;
            var weapon = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
                .Select(d => d.AsInventoryItem(null)).Cast<EquipableInventoryItem>()
                .First(i => HeroClass.AllowsItem(hero.PrimaryClass, hero.SecondaryClass, i));
            var supply = Common.Instance.ItemManager.ItemDefinitions.First(d => d.StackMax >= 3).AsInventoryItem(3);
            World.TownPlayer.Inventory.Add(weapon);
            World.TownPlayer.Inventory.Add(supply);
            Assert.That(World.Services.ToggleEquipment(hero, weapon), Is.True);
            World.TownBuildings.First(b => b.Definition.DialogId == "entrance").Interact(World.TownPlayer, null);
            ((EntranceDialog)Manager.CurrentDialog).DungeonClicked(configuration.DungeonTiers[0]);
            yield return harness.WaitForIdle();
            Assert.That(harness.Ally.Equipment.GetEquippedItems().Single().ItemName, Is.EqualTo(weapon.ItemName));
            var carried = harness.Game.PlayerController.Inventory.InventoryItems.Single();
            Assert.That(carried.StackStock, Is.EqualTo(3));
            carried.Decrement();
            harness.Game.PlayerController.Gold += 70;
            GameOverScreen.GoBackToTown(victory, harness.Game.PlayerController);
            yield return harness.WaitUntil(() => World != null && World.IsReady, "dungeon return to town");
            Assert.That(World.Configuration, Is.SameAs(configuration));
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(320));
            Assert.That(World.TownPlayer.Inventory.Count, Is.EqualTo(victory ? 1 : 0));
            Assert.That(World.TownPlayer.ControllingTownAlly.Equipment.GetEquippedItems().Count(), Is.EqualTo(victory ? 1 : 0));
            if (victory) Assert.That(World.TownPlayer.Inventory[0].StackStock, Is.EqualTo(2));
            var saved = Common.Instance.GameSaveData;
            Assert.That(SaveSystem.LoadData().TownSaveData.Gold, Is.EqualTo(250), "Returns must not overwrite the checkpoint.");
            if (victory)
            {
                Assert.That(saved.TownSaveData.InventoryItems[0].Stock, Is.EqualTo(2));
                Assert.That(saved.TownSaveData.RecruitedAlliesData[0].Equipment[0].ItemName, Is.EqualTo(weapon.ItemName));
            }
            Assert.That(saved.TownSaveData.RestockCycle, Is.EqualTo(victory ? 1 : 0));
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
