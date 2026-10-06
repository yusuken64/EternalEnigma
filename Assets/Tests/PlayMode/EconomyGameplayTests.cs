#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class EconomyGameplayTests
    {
        private GameTestHarness harness;
        private TownConfiguration configuration;
        private Town Town => Object.FindFirstObjectByType<Town>();
        private TownBuildingDefinition Bakery => Resources.Load<TownBuildingDefinition>("Towns/Buildings/Bakery");
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness { TimeoutSeconds = 120 }; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() { yield return harness.Cleanup(); if (configuration != null) Object.DestroyImmediate(configuration); }

        [UnityTest]
        public IEnumerator EveryWeaponTypeCanBeBoughtEquippedAndReportsClassRestriction()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);
            common.Travel.NewCampaign(42);
            yield return harness.WaitUntil(() => Town != null && Town.IsReady && !common.ScreenTransition.BlockScreen.activeSelf, "campaign town");
            var town = Town;
            var originalId = town.Configuration.Id;
            var hero = town.TownPlayer.ControllingTownAlly;
            var originalClass = hero.PrimaryClass;
            var shop = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
            var classes = ClassCatalog.Load().Classes;
            try
            {
                town.Configuration.Id = common.CampaignContext.Campaign.Locations
                    .Where(l => l.Kind == EternalEnigma.Core.Progression.LocationKind.Town)
                    .OrderByDescending(l => l.Tier).First().Id;
                Assert.That(TownShopCatalog.Tier(town.Configuration.Id), Is.EqualTo(4));
                town.TownPlayer.Gold = 100000;
                foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
                {
                    var offer = TownShopCatalog.Resolve(shop, 4).First(o => o.Item is EquipmentItemDefinition e &&
                        !e.IsAmmunition && e.WeaponType == type);
                    hero.PrimaryClass = classes.First(c => c.AllowsWeapon(type));
                    hero.SecondaryClass = null;
                    Assert.That(town.Services.Buy(shop, offer.Item.ItemName, out var buyReason), Is.True, buyReason);
                    var item = town.TownPlayer.Inventory.Last(i => i.ItemDefinition == offer.Item);
                    Assert.That(town.Services.ToggleEquipment(hero, item, out var equipReason), Is.True, equipReason);
                    var weapon = (EquipmentItemDefinition)offer.Item;
                    var objects = weapon.WeaponModelName == "Bows" ? hero.HeroAnimator.LeftHandObjects :
                        weapon.EquipmentSlot == EquipmentSlot.OffHand ? hero.HeroAnimator.LeftHandObjects : hero.HeroAnimator.RightHandObjects;
                    Assert.That(objects.Any(o => o.activeSelf && o.name == weapon.WeaponModelName), Is.True, type.ToString());
                    Assert.That(town.Services.ToggleEquipment(hero, item, out _), Is.True);
                    var denied = classes.FirstOrDefault(c => !c.AllowsWeapon(type));
                    if (denied == null) continue;
                    hero.PrimaryClass = denied;
                    Assert.That(town.Services.ToggleEquipment(hero, item, out var reason), Is.False);
                    Assert.That(reason, Does.Contain("proficiency"));
                }
                hero.PrimaryClass = classes.First(c => c.AllowsWeapon(WeaponType.BowAndArrow));
                foreach (var name in new[] { "Bow05", "Royal Arrows" })
                {
                    Assert.That(town.Services.Buy(shop, name, out var buyReason), Is.True, buyReason);
                    var item = town.TownPlayer.Inventory.Last(i => i.ItemName == name);
                    Assert.That(town.Services.ToggleEquipment(hero, item, out var equipReason), Is.True, equipReason);
                }
                Assert.That(hero.HeroAnimator.CurrentStance, Is.EqualTo(Stance.BowAndArrowStance));
                town.SaveProgress();
                var restoredSave = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(common.GameSaveData));
                var savedGear = restoredSave.TownSaveData.RecruitedAlliesData
                    .Single(a => a.AllyId == hero.Id).Equipment;
                foreach (var name in new[] { "Bow05", "Royal Arrows" })
                {
                    var savedItem = savedGear.Single(i => i.ItemName == name);
                    var restored = savedItem.Restore(common.ItemManager);
                    Assert.That(restored.ItemDefinition.ItemName, Is.EqualTo(name));
                    Assert.That(restored.ItemDefinition, Is.SameAs(shop.ShopCatalog.Single(o => o.Item.ItemName == name).Item));
                }
            }
            finally { town.Configuration.Id = originalId; hero.PrimaryClass = originalClass; }
        }

        [UnityTest]
        public IEnumerator SalesConfirmExactEntriesCancelAndCannotPayTwice()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var shop = (ShopMenuDialog)Object.FindFirstObjectByType<TownMenu>().OpenBuilding(Bakery, Town.TownPlayer, null);
            yield return null;
            shop.ShopItems[0].BuyButton.onClick.Invoke();
            shop.BuyConfirmationDialog.Cancel_Clicked();
            shop.BuyConfirmationDialog.Ok_Clicked();
            Assert.That(Town.TownPlayer.Gold, Is.EqualTo(10000));
            shop.ShopItems[0].BuyButton.onClick.Invoke();
            shop.BuyConfirmationDialog.Ok_Clicked(); shop.BuyConfirmationDialog.Ok_Clicked();
            Assert.That(Town.TownPlayer.Gold, Is.EqualTo(9900));
            var bread = Town.TownPlayer.Inventory.Single();
            var duplicate = bread.ItemDefinition.AsInventoryItem(null); Town.TownPlayer.Inventory.Add(duplicate);
            shop.SellModeButton.onClick.Invoke();
            Assert.That(shop.ShopItemDatas.Count, Is.EqualTo(2));
            shop.ShopItems[1].BuyButton.onClick.Invoke();
            shop.BuyConfirmationDialog.Cancel_Clicked();
            Assert.That(Town.TownPlayer.Inventory.Contains(duplicate), Is.True);
            shop.ShopItems[1].BuyButton.onClick.Invoke();
            shop.BuyConfirmationDialog.Ok_Clicked(); shop.BuyConfirmationDialog.Ok_Clicked();
            Assert.That(Town.TownPlayer.Inventory, Is.EqualTo(new[] { bread }));
            Assert.That(Town.TownPlayer.Gold, Is.EqualTo(9925));
            Assert.That(Town.Services.Sell(duplicate, out _), Is.False);
            Assert.That(Town.Services.Shop(Bakery).Stock[0].Remaining, Is.EqualTo(5));
            Assert.That(shop.ShopItems[0].BuyButton.navigation.selectOnUp, Is.SameAs(shop.SellModeButton));
            Assert.That(shop.SellModeButton.navigation.selectOnLeft, Is.SameAs(shop.BuyModeButton));
            Assert.That(shop.scrollView.content, Is.SameAs(shop.Container));
            System.IO.Directory.CreateDirectory("Temp/Economy");
            ScreenCapture.CaptureScreenshot("Temp/Economy/shop-sell.png");
            yield return null;
            var equipment = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>().First().AsInventoryItem(null);
            Town.TownPlayer.Inventory.Add(equipment);
            Town.TownPlayer.ControllingTownAlly.Equipment.Equip((EquipableInventoryItem)equipment);
            Assert.That(Town.Services.Sell(equipment, out _), Is.False);
            var stack = Common.Instance.ItemManager.ItemDefinitions.First(d => d.StackMax >= 3).AsInventoryItem(3);
            Town.TownPlayer.Inventory.Add(stack);
            int price = TownServices.SellPrice(stack), balance = Town.TownPlayer.Gold;
            Assert.That(Town.Services.Sell(stack, out _), Is.True);
            Assert.That(Town.TownPlayer.Gold, Is.EqualTo(balance + price));
            Assert.That(Town.Services.Sell(stack, out _), Is.False);
            var offer = Town.Services.Catalog(Bakery)[1];
            int stockBefore = Town.Services.Shop(Bakery).Stock[1].Remaining;
            Town.TownPlayer.Gold = offer.Price - 1;
            Assert.That(Town.Services.Buy(Bakery, offer.Item.ItemName, out _), Is.False);
            Assert.That(Town.TownPlayer.Gold, Is.EqualTo(offer.Price - 1));
            Assert.That(Town.Services.Shop(Bakery).Stock[1].Remaining, Is.EqualTo(stockBefore));
        }

        [UnityTest]
        public IEnumerator ClearedRepeatAndRetreatedRunsRestockAllTownsExactlyOnce()
        {
            configuration = Object.Instantiate(TownSceneLoader.Default);
            yield return harness.LoadTown(new TestScenario { Gold = 20000 }.CreateSave(), configuration);
            var common = Common.Instance;
            string originalId = Town.Configuration.Id;
            var general = Resources.Load<TownBuildingDefinition>("Towns/Buildings/Shop");
            for (int run = 0; run < 3; run++)
            {
                foreach (string id in new[] { originalId, "economy-other-town" })
                {
                    Town.Configuration.Id = id;
                    foreach (var building in new[] { Bakery, general })
                    {
                        var offer = Town.Services.Catalog(building)[0];
                        var stock = Town.Services.Shop(building).Stock[0];
                        Assert.That(stock.Remaining, Is.EqualTo(offer.Quantity));
                        while (stock.Remaining > 0) Assert.That(Town.Services.Buy(building, offer.Item.ItemName, out _), Is.True);
                        int gold = Town.TownPlayer.Gold;
                        Assert.That(Town.Services.Buy(building, offer.Item.ItemName, out _), Is.False);
                        Assert.That(Town.TownPlayer.Gold, Is.EqualTo(gold));
                        Town.Services.Rest(out _); Town.SaveProgress();
                        Assert.That(Town.Services.Shop(building).Stock[0].Remaining, Is.Zero);
                    }
                }
                Town.Configuration.Id = originalId;
                // Serialize depleted stock before the return, as an inn checkpoint would.
                var checkpoint = JsonUtility.ToJson(common.GameSaveData);
                common.GameSaveData = JsonUtility.FromJson<GameSaveData>(checkpoint);
                Assert.That(Town.Services.Shop(Bakery).Stock[0].Remaining, Is.Zero);
                common.GameSaveData.DungeonSaveData.ReturnCommitted = false;
                DungeonReturnService.Commit(common.GameSaveData, Town.Configuration, run < 2, Town.TownPlayer.Gold,
                    Town.TownPlayer.Inventory, new Ally[0], keepLoot: run == 2);
                DungeonReturnService.Commit(common.GameSaveData, Town.Configuration, true, 0, new InventoryItem[0], new Ally[0]);
                Assert.That(common.GameSaveData.TownSaveData.RestockCycle, Is.EqualTo(run+1));
                Assert.That(JsonUtility.FromJson<GameSaveData>(checkpoint).TownSaveData.Shops.All(s => s.Stock[0].Remaining == 0), Is.True);
            }
            foreach (string id in new[] { originalId, "economy-other-town" })
            {
                Town.Configuration.Id = id;
                Assert.That(Town.Services.Shop(Bakery).Stock[0].Remaining, Is.EqualTo(6));
                Assert.That(Town.Services.Shop(general).Stock[0].Remaining, Is.EqualTo(2));
            }
            Town.Configuration.Id = originalId;
        }

        [UnityTest]
        public IEnumerator PurchasedDrinksRestoreAndConsumeInDungeonAndHungerSurvivesReturn()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            Assert.That(Town.Services.Buy(Bakery, "Cactus Pulp Caramel Cloud Latte", out _), Is.True);
            var hero = Town.TownPlayer.ControllingTownAlly;
            hero.HasHunger = true; hero.Hunger = 0; hero.HungerAccumulate = 3;
            Town.SaveProgress();
            Common.Instance.GameSaveData = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(Common.Instance.GameSaveData));
            var entrance = Town.TownBuildings.First(b => b.Definition.DialogId == "entrance");
            var dialog = (EntranceDialog)Object.FindFirstObjectByType<TownMenu>().OpenBuilding(entrance.Definition, Town.TownPlayer, null);
            dialog.DungeonClicked(Town.Configuration.DungeonTiers[0]);
            yield return harness.WaitForIdle();
            var ally = harness.Ally;
            Assert.That(ally.Vitals.Hunger, Is.Zero); Assert.That(ally.Vitals.HungerAccumulate, Is.EqualTo(3));
            var drink = harness.Game.PlayerController.Inventory.InventoryItems.Single();
            ally.Vitals.HP = ally.FinalStats.HPMax - 1; ally.Vitals.SP = ally.FinalStats.SPMax - 1;
            ally.Vitals.Hunger = ally.FinalStats.HungerMax;
            yield return harness.ExecuteAction(new UseInventoryItemAction(harness.Game.PlayerController.Inventory, ally, drink));
            Assert.That(harness.Game.PlayerController.Inventory.InventoryItems.Contains(drink), Is.False);
            Assert.That(ally.Vitals.HP, Is.EqualTo(ally.FinalStats.HPMax));
            Assert.That(ally.Vitals.SP, Is.EqualTo(ally.FinalStats.SPMax));
            ally.Vitals.Hunger = 12; ally.Vitals.HungerAccumulate = 4;
            GameOverScreen.Retreat(harness.Game.PlayerController);
            yield return harness.WaitUntil(() => Town != null && Town.IsReady, "retreat");
            hero = Town.TownPlayer.ControllingTownAlly;
            Assert.That(hero.HasHunger, Is.True); Assert.That(hero.Hunger, Is.EqualTo(12)); Assert.That(hero.HungerAccumulate, Is.EqualTo(4));
            Town.Services.Rest(out _);
            Assert.That(hero.HasHunger, Is.False); Assert.That(hero.HungerAccumulate, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InitializedShopDoesNotGrantNewOffersUntilReturn()
        {
            var save = new TestScenario().CreateSave();
            save.TownSaveData.Shops.Add(new TownShopSaveData { Key = TownSceneLoader.Default.Id + "/bakery", RestockCycle = 0,
                Stock = new() { new TownStockSaveData { ItemName = "Bread", Remaining = 0 } } });
            yield return harness.LoadTown(save);
            Assert.That(Town.Services.Shop(Bakery).Stock.Count, Is.EqualTo(1));
            Assert.That(Town.Services.Buy(Bakery, "Cactus Pulp Caramel Coffee", out _), Is.False);
            Common.Instance.GameSaveData.DungeonSaveData.ReturnCommitted = false;
            DungeonReturnService.Commit(Common.Instance.GameSaveData, Town.Configuration, true, 1000, new InventoryItem[0], new Ally[0]);
            Assert.That(Town.Services.Shop(Bakery).Stock.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator CampaignRepeatReturnsAndCheckpointRollbackPreserveStockAndHunger()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);
            common.Travel.NewCampaign(42);
            yield return harness.WaitUntil(() => Town != null && Town.IsReady && !common.ScreenTransition.BlockScreen.activeSelf, "campaign town");
            Town.TownPlayer.Gold = 10000;
            foreach (var location in common.CampaignContext.Campaign.Locations.Where(l => l.Kind == EternalEnigma.Core.Progression.LocationKind.Town))
                Assert.That(TownShopCatalog.Tier(location.Id), Is.EqualTo(location.Tier));
            string allyId = Town.Configuration.AllyCatalog.First(a => a.Id != common.GameSaveData.ProtagonistId).Id;
            common.CampaignContext.Roster.Add(allyId); common.CampaignContext.SetParty(new[] { allyId });
            Town.WriteSaveData(); Town.RefreshCampaignParty();
            var companion = Town.TownPlayer.RecruitedAllies.Single(a => a.Id == allyId);
            companion.HasHunger = true; companion.Hunger = 17; companion.HungerAccumulate = 2;
            Town.WriteSaveData(); common.CampaignContext.SetParty(new string[0]); Town.RefreshCampaignParty();
            common.CampaignContext.SetParty(new[] { allyId }); Town.RefreshCampaignParty();
            companion = Town.TownPlayer.RecruitedAllies.Single(a => a.Id == allyId);
            Assert.That(companion.Hunger, Is.EqualTo(17)); Assert.That(companion.HungerAccumulate, Is.EqualTo(2));
            Town.TownPlayer.ControllingTownAlly.HasHunger = true;
            Town.TownPlayer.ControllingTownAlly.Hunger = 0;
            Town.TownPlayer.ControllingTownAlly.HungerAccumulate = 3;
            var stock = Town.Services.Shop(Bakery).Stock[0];
            while (stock.Remaining > 0) Town.Services.Buy(Bakery, "Bread", out _);
            Assert.That(Town.Services.SaveGame(out var error), Is.True, error);
            for (int run = 0; run < 3; run++)
            {
                if (run > 0)
                {
                    stock = Town.Services.Shop(Bakery).Stock[0];
                    while (stock.Remaining > 0) Town.Services.Buy(Bakery, "Bread", out _);
                }
                Assert.That(common.Travel.EnterTownDungeon(Town, "story-0"), Is.True);
                yield return harness.WaitForIdle();
                Assert.That(common.GameSaveData.DungeonSaveData.ReturnCommitted, Is.False);
                var hero = harness.Ally; hero.Vitals.Hunger = 23 + run; hero.Vitals.HungerAccumulate = 4;
                Assert.That(common.Travel.FinishDungeon(run < 2, harness.Game.PlayerController, keepLoot: run == 2), Is.True);
                Assert.That(common.Travel.FinishDungeon(true, harness.Game.PlayerController), Is.False);
                if (run == 0)
                {
                    yield return harness.WaitUntil(() => common.MessageDialog.gameObject.activeSelf, "clear rewards");
                    common.MessageDialog.Ok_Clicked();
                }
                yield return harness.WaitUntil(() => Town != null && Town.IsReady && !common.ScreenTransition.BlockScreen.activeSelf, "campaign return");
                Assert.That(common.GameSaveData.TownSaveData.RestockCycle, Is.EqualTo(run+1));
                Assert.That(Town.Services.Shop(Bakery).Stock[0].Remaining, Is.EqualTo(6));
                Assert.That(Town.TownPlayer.ControllingTownAlly.Hunger, Is.EqualTo(23+run));
                Assert.That(Town.TownPlayer.ControllingTownAlly.HungerAccumulate, Is.EqualTo(4));
            }
            Assert.That(common.Travel.ExitTown(Town), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true, "travel out of town");
            Assert.That(common.Travel.EnterLocation(), Is.True);
            yield return harness.WaitUntil(() => Town != null && Town.IsReady && !common.ScreenTransition.BlockScreen.activeSelf, "travel back to town");
            Assert.That(Town.TownPlayer.ControllingTownAlly.Hunger, Is.EqualTo(25));
            Assert.That(Town.TownPlayer.ControllingTownAlly.HungerAccumulate, Is.EqualTo(4));
            Assert.That(common.GameSaveData.TownSaveData.RestockCycle, Is.EqualTo(3));
            string townId = Town.Configuration.Id;
            Town.Configuration.Id = common.CampaignContext.Campaign.Locations.First(l => l.Kind == EternalEnigma.Core.Progression.LocationKind.Town && l.Tier == 4).Id;
            var shopView = (ShopMenuDialog)Object.FindFirstObjectByType<TownMenu>().OpenBuilding(Bakery, Town.TownPlayer, null);
            Assert.That(shopView.ShopItems.Count, Is.EqualTo(15));
            shopView.ShopItems[^1].BuyButton.Select(); shopView.ScrollToSelected(shopView.ShopItems[^1].gameObject);
            yield return new WaitForSecondsRealtime(.2f);
            System.IO.Directory.CreateDirectory("Temp/Economy"); ScreenCapture.CaptureScreenshot("Temp/Economy/bakery-tier4.png"); yield return null;
            shopView.CloseDialog(); Town.Configuration.Id = townId;
            var before = Town;
            Assert.That(InnCheckpoint.TryRestore(common), Is.True);
            yield return harness.WaitUntil(() => Town != null && Town != before && Town.IsReady, "checkpoint rollback");
            Assert.That(common.GameSaveData.TownSaveData.RestockCycle, Is.Zero);
            Assert.That(common.CampaignContext.Completed, Is.Empty);
            Assert.That(Town.Services.Shop(Bakery).Stock[0].Remaining, Is.Zero);
            Assert.That(Town.TownPlayer.ControllingTownAlly.HasHunger, Is.True);
            Assert.That(Town.TownPlayer.ControllingTownAlly.Hunger, Is.Zero);
            Assert.That(Town.TownPlayer.ControllingTownAlly.HungerAccumulate, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator StandaloneDefeatFullyRecoversWithoutRestocking()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var ally = harness.Ally;
            Assert.That(ally.Vitals.Hunger, Is.EqualTo(ally.FinalStats.HungerMax), "Old saves start full.");
            ally.Vitals.HP = 1; ally.Vitals.SP = 0; ally.Vitals.Hunger = 0; ally.Vitals.HungerAccumulate = 3;
            GameOverScreen.GoBackToTown(false, harness.Game.PlayerController);
            yield return harness.WaitUntil(() => Town != null && Town.IsReady, "defeat return");
            var hero = Town.TownPlayer.ControllingTownAlly;
            Assert.That(hero.Hp, Is.EqualTo(-1)); Assert.That(hero.Sp, Is.EqualTo(-1));
            Assert.That(hero.HasHunger, Is.False); Assert.That(hero.HungerAccumulate, Is.Zero);
            Assert.That(Common.Instance.GameSaveData.TownSaveData.RestockCycle, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShopSupportsKeyboardGamepadAndMouseSelection()
        {
            using var inputScope = new TestInputScope();
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, pad, mouse };
                var shop = (ShopMenuDialog)Object.FindFirstObjectByType<TownMenu>().OpenBuilding(Bakery, Town.TownPlayer, null);
                yield return null;
                shop.BuyModeButton.Select();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(shop.SellModeButton.gameObject));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(shop.Selling, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadLeft)); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(shop.BuyModeButton.gameObject));
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                Assert.That(shop.Selling, Is.False);
                var button = shop.ShopItems[1].BuyButton;
                var point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
                Assert.That(shop.BuyConfirmationDialog.gameObject.activeSelf, Is.True);
                Assert.That(shop.BuyConfirmationDialog.PromptText.text, Does.Contain("Cactus"));
                shop.BuyConfirmationDialog.Cancel_Clicked();
                Assert.That(Town.TownPlayer.Gold, Is.EqualTo(10000));
            }
            finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(mouse); }
        }
    }
}
#endif
