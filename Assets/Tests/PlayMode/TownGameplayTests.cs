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
        public IEnumerator RoomAndTransactionAudioPlaysOnceOnTheCorrectChannelWithoutAnimations()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            var town = World;
            var player = town.TownPlayer;
            Assert.That(town.Services.Recruit(town.TownAllies.First(), out _), Is.True);
            player.Gold = 10000;
            var audio = AudioManager.Instance;
            var heard = new List<(SoundCue cue, UnityEngine.Audio.AudioMixerGroup group)>();
            void Heard(SoundCue cue, AudioClip _, UnityEngine.Audio.AudioMixerGroup group) => heard.Add((cue, group));
            var previousAnimation = DungeonPreferences.AnimationOverride;
            audio.CuePlayed += Heard;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            try
            {
                var door = town.Plan.ShopRooms.First().Door.ToCell();
                var threshold = door + Vector3Int.up;
                var inside = door + Vector3Int.up * 2;
                var hero = player.ControllingTownAlly;
                hero.TilemapPosition = door;
                hero.transform.position = town.WalkableMap.CellToWorld(door);
                town.RefreshRoofs();
                Assert.That(heard, Is.Empty, "A roof refresh or spawn must not sound like a door opening.");
                player.SetAction(new TownMovement(player, door, threshold));
                yield return harness.WaitUntil(() => !player.IsBusy, "audio doorway threshold");
                player.SetAction(new TownMovement(player, threshold, inside));
                yield return harness.WaitUntil(() => !player.IsBusy, "audio room entry");
                Assert.That(heard.Count(e => e.cue == SoundCue.DoorOpen), Is.EqualTo(1));
                Assert.That(heard.Single(e => e.cue == SoundCue.DoorOpen).group, Is.SameAs(audio.EffectAudioMixerGroup));
                player.SetAction(new TownMovement(player, inside, threshold));
                yield return harness.WaitUntil(() => !player.IsBusy, "audio room exit threshold");
                player.SetAction(new TownMovement(player, threshold, door));
                yield return harness.WaitUntil(() => !player.IsBusy, "audio room exit");
                town.RefreshRoofs();
                Assert.That(heard.Count(e => e.cue == SoundCue.DoorOpen), Is.EqualTo(1), "Followers, exit and refresh must not replay entry audio.");

                var building = town.Configuration.Buildings.First(b => town.Services.Catalog(b).Any(o => o.Price > 0));
                var offer = town.Services.Catalog(building).First(o => o.Price > 0);
                heard.Clear();
                Assert.That(town.Services.Buy(building, offer.Item.ItemName, out _), Is.True);
                var bought = player.Inventory.Last();
                Assert.That(heard.Count(e => e.cue == SoundCue.Purchase), Is.EqualTo(1));
                Assert.That(heard.Single(e => e.cue == SoundCue.Purchase).group, Is.SameAs(audio.EffectAudioMixerGroup));
                Assert.That(town.Services.Sell(bought, out _), Is.True);
                Assert.That(heard.Count(e => e.cue == SoundCue.Sell), Is.EqualTo(1));
                Assert.That(heard.Single(e => e.cue == SoundCue.Sell).group, Is.SameAs(audio.EffectAudioMixerGroup));

                player.Gold = 0;
                heard.Clear();
                Assert.That(town.Services.Buy(building, offer.Item.ItemName, out _), Is.False);
                Assert.That(heard.Select(e => e.cue), Is.EqualTo(new[] { SoundCue.Denied }));
                Assert.That(heard.Single().group, Is.SameAs(audio.UIAudioMixerGroup));
            }
            finally
            {
                audio.CuePlayed -= Heard;
                DungeonPreferences.AnimationOverride = previousAnimation;
            }
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
            var threshold = door + Vector3Int.up;
            var inside = door + Vector3Int.up * 2;
            hero.TilemapPosition = door;
            hero.transform.position = town.WalkableMap.CellToWorld(door);
            town.RefreshRoofs();
            Assert.That(town.Roofs.All(r => r.gameObject.activeSelf), Is.True);
            town.TownPlayer.SetAction(new TownMovement(town.TownPlayer, door, threshold));
            yield return harness.WaitUntil(() => !town.TownPlayer.IsBusy, "cross doorway");
            town.TownPlayer.SetAction(new TownMovement(town.TownPlayer, threshold, inside));
            yield return harness.WaitUntil(() => !town.TownPlayer.IsBusy, "enter room");
            Assert.That(roomRoof.gameObject.activeSelf, Is.False);
            Assert.That(otherRoofs.All(r => r.gameObject.activeSelf), Is.True);
            town.TownPlayer.SetAction(new TownMovement(town.TownPlayer, inside, threshold));
            yield return harness.WaitUntil(() => !town.TownPlayer.IsBusy, "cross doorway outward");
            town.TownPlayer.SetAction(new TownMovement(town.TownPlayer, threshold, door));
            yield return harness.WaitUntil(() => !town.TownPlayer.IsBusy, "leave room");
            Assert.That(town.Roofs.All(r => r.gameObject.activeSelf), Is.True);
            // A spawn or leader change already inside the room uses the same occupancy rule.
            hero.TilemapPosition = inside;
            town.RefreshRoofs();
            Assert.That(roomRoof.gameObject.activeSelf, Is.False);
            Assert.That(otherRoofs.All(r => r.gameObject.activeSelf), Is.True);
        }

        [UnityTest]
        public IEnumerator DoorwayMovementAndAutoplayUseStraightApproaches()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town = World; var player = town.TownPlayer;
            var door = town.Plan.BuildingSlots.First().ToCell();
            var diagonal = door + new Vector3Int(-1,-1);
            var approach = door + Vector3Int.down;
            player.ControllingTownAlly.TilemapPosition = diagonal;
            Assert.That(town.WalkableMap.CanWalkTo(diagonal,door), Is.False);
            new TownMovement(player,diagonal,door).ExecuteImmediate();
            Assert.That(player.ControllingTownAlly.TilemapPosition, Is.EqualTo(diagonal));
            var grid = town.WalkableMap.GetAStarGrid();
            var path = AStar.FindPath(grid,grid[diagonal.x,diagonal.y],grid[door.x,door.y],DiagonalMovement.AllowCornerCutting);
            Assert.That(path, Is.Not.Null);
            var from = diagonal;
            foreach (var node in path)
            {
                var to = new Vector3Int(node.X,node.Y);
                Assert.That(town.WalkableMap.CanWalkTo(from,to),Is.True);
                from = to;
            }
            player.ControllingTownAlly.TilemapPosition = approach;
            new TownMovement(player,approach,door).ExecuteImmediate();
            Assert.That(player.ControllingTownAlly.TilemapPosition, Is.EqualTo(door));
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
        public IEnumerator NumberEntryCanReachCancelAndPartyShortcutsRespectModalDialogs()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 1000 }.CreateSave());
            var statue = Menus.BuildingDialogs.Select(b => b.Dialog).OfType<StatueDialog>().Single();
            statue.PrepareTown(null);
            Manager.Open(statue);
            yield return null;
            var es = UnityEngine.EventSystems.EventSystem.current;
            var digit = statue.NumberInput.SelectableDigits[0];
            Assert.That(es.currentSelectedGameObject, Is.SameAs(digit.gameObject));
            UnityEngine.EventSystems.ExecuteEvents.Execute(digit.gameObject,
                new UnityEngine.EventSystems.AxisEventData(es) { moveDir = UnityEngine.EventSystems.MoveDirection.Right },
                UnityEngine.EventSystems.ExecuteEvents.moveHandler);
            Assert.That(es.currentSelectedGameObject, Is.SameAs(statue.OkButton.gameObject));
            UnityEngine.EventSystems.ExecuteEvents.Execute(statue.OkButton.gameObject,
                new UnityEngine.EventSystems.AxisEventData(es) { moveDir = UnityEngine.EventSystems.MoveDirection.Down },
                UnityEngine.EventSystems.ExecuteEvents.moveHandler);
            Assert.That(es.currentSelectedGameObject, Is.SameAs(statue.CancelButton.gameObject));
            Manager.OpenPartyMenu(PartyMenuTab.Inventory);
            Assert.That(Manager.CurrentDialog, Is.SameAs(statue));
            statue.Cancel_Clicked();
            Assert.That(World.TownPlayer.Gold, Is.EqualTo(1000));
            Common.Instance.GlobalSettings.ShowDialog();
            Manager.OpenPartyMenu(PartyMenuTab.Inventory);
            Assert.That(Manager.Opened, Is.False, "Settings must retain exclusive focus.");
            Common.Instance.GlobalSettings.Exit_Clicked();
            yield return null;
            Manager.OpenPartyMenu(PartyMenuTab.Inventory);
            Assert.That(Manager.CurrentDialog, Is.SameAs(Manager.PartyMenu));
        }

        [UnityTest]
        public IEnumerator TownServicesHideGameplayHudAndRestorePartyDocks()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 1000 }.CreateSave());
            var resource = Object.FindFirstObjectByType<ResourceHUD>().GetComponent<Canvas>();
            var resourcePanel = resource.GetComponent<ResourceHUD>().GoldText.transform.parent;
            var launcher = Object.FindFirstObjectByType<PartyMenuLauncher>().GetComponent<Canvas>();
            var feed = Object.FindFirstObjectByType<GameMessages>().GetComponentInChildren<CanvasGroup>();
            GameMessages.Post("The town is ready.");
            var shop = (ShopMenuDialog)Menus.OpenBuilding(World.Configuration.Buildings.First(b => b.DialogId == "shop"), World.TownPlayer, null);
            yield return null;
            Assert.That(resource.enabled, Is.False, "Resource text must not cover service content.");
            Assert.That(launcher.enabled, Is.False, "Gameplay shortcuts must not cover full service dialogs.");
            Assert.That(feed.alpha, Is.Zero);
            Assert.That(feed.blocksRaycasts, Is.False, "The hidden event feed must not intercept service buttons.");
            shop.ShopItems[0].BuyButton.onClick.Invoke(); yield return null;
            Assert.That(resource.enabled || launcher.enabled || feed.blocksRaycasts, Is.False, "Nested confirmations keep the full service HUD hidden.");
            shop.BuyConfirmationDialog.Cancel_Clicked();
            shop.CloseDialog(); yield return null;
            var talk = Menus.AllyRecruitDialog;
            talk.Show(World.TownPlayer.RecruitedAllies[0], AllyRecruitDialogMode.Talk);
            Manager.Open(talk); yield return null;
            Assert.That(resource.enabled || launcher.enabled || feed.blocksRaycasts, Is.False);
            Assert.That(talk.RemoveButton.interactable, Is.False, "The sole party member cannot be removed.");
            Assert.That(talk.DescriptionText.text, Does.Contain("Keep at least one party member"));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(talk.CancelButton2.gameObject));
            Assert.That(talk.transform.Find("CharacterBG_Board/FeatherObj").gameObject.activeSelf, Is.False);
            talk.CloseDialog(); yield return null;
            World.TownPlayer.Gold = 0;
            var recruit = World.TownAllies.First(a => a.RecruitCost > 0);
            talk.Show(recruit, AllyRecruitDialogMode.Recruit);
            Manager.Open(talk); yield return null;
            Assert.That(talk.RecruitButton.interactable, Is.False);
            Assert.That(talk.DescriptionText.text, Does.Contain("Not enough gold"));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(talk.CancelButton.gameObject));
            talk.CloseDialog(); yield return null;
            World.TownPlayer.Gold = 1000;
            Manager.OpenPartyMenu(PartyMenuTab.Inventory);
            GameMessages.Post("Party controls are available.");
            yield return null;
            Assert.That(resource.enabled && launcher.enabled && feed.blocksRaycasts, Is.True, "The party dock preserves shared HUD controls.");
            Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(resource.transform, resourcePanel).min.y,
                Is.GreaterThan(RectTransformUtility.CalculateRelativeRectTransformBounds(resource.transform, Manager.PartyMenu.Panel).max.y),
                "The complete resource strip must remain above the party panel.");
            var picker = PartyMenuPicker.Build(Manager.PartyMenu.transform.parent, "Choose", new List<(string, System.Action)> { ("Example", () => { }) });
            Manager.Open(picker); yield return null;
            Assert.That(resource.enabled && launcher.enabled && feed.blocksRaycasts, Is.True, "Nested dock pickers preserve shared HUD controls.");
            Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(resource.transform, resourcePanel).min.y,
                Is.GreaterThan(RectTransformUtility.CalculateRelativeRectTransformBounds(resource.transform, picker.Title.transform.parent).max.y),
                "The complete resource strip must remain above picker panels.");
            picker.CloseDialog(); Manager.PartyMenu.CloseDialog();
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
            Manager.OpenPartyMenu(PartyMenuTab.Inventory);
            yield return null;
            var menu = Manager.PartyMenu;
            menu.EntryButtons.Single().onClick.Invoke();
            yield return null;
            Assert.That(Manager.CurrentDialog, Is.TypeOf<PartyMenuPicker>());
            ((PartyMenuPicker)Manager.CurrentDialog).Rows.GetComponentsInChildren<AuthoredButton>()
                .Single(b => b.Label.text == "Equip").Button.onClick.Invoke();
            yield return null;
            Assert.That(player.ControllingTownAlly.Equipment.IsEquipped(item), Is.True);
            Assert.That(player.Inventory, Has.No.Member(item));
            SaveSystem.SaveData(Common.Instance.GameSaveData);
            Assert.That(SaveSystem.LoadData().TownSaveData.RecruitedAlliesData[0].Equipment[0].ItemName, Is.EqualTo(item.ItemName));
            menu.SwitchTab(PartyMenuTab.Equipment);
            yield return null;
            menu.EntryButtons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text.Contains(item.DisplayName)).onClick.Invoke();
            yield return null;
            ((PartyMenuPicker)Manager.CurrentDialog).Rows.GetComponentsInChildren<AuthoredButton>()
                .Single(b => b.Label.text == "Unequip").Button.onClick.Invoke();
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
