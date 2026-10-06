#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public sealed class CoreBugfixTests
{
    private GameTestHarness harness;
    private TestInputScope inputScope;
    private Keyboard keyboard;
    [UnitySetUp] public IEnumerator Setup() { inputScope = new TestInputScope(); harness = new GameTestHarness(); yield return null; }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        yield return harness.Cleanup(); inputScope.Dispose();
    }

    [UnityTest] public IEnumerator FourMemberSightProfileAndCarriedGold()
    {
        yield return harness.LoadDungeon(new TestScenario { Gold = 731, AdditionalAllies = new[] { "Avery", "Morgan", "Alex" } });
        var game = harness.Game;
        Assert.That(game.Allies.Count, Is.EqualTo(4));
        Assert.That(game.PlayerController.Gold, Is.EqualTo(731));
        for (int i = 0; i < 5; i++) game.UpdateMiniMap();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long bytes = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) game.UpdateMiniMap();
        watch.Stop(); bytes = System.GC.GetAllocatedBytesForCurrentThread() - bytes;
        string report = $"Four-member sight: {watch.Elapsed.TotalMilliseconds / 100:0.000} ms/update, {bytes / 100} bytes/update";
        System.IO.Directory.CreateDirectory("Temp/CoreBugfix");
        System.IO.File.AppendAllText("Temp/CoreBugfix/profile.txt", report + "\n");
        Debug.Log(report);
        var map = game.PlayerController.Minimap;
        var playerCell = game.CurrentDungeon.WorldToCell(harness.Ally.transform.position);
        Assert.That(map.FogOverlay.VisibilityTexture.GetPixel(playerCell.x, playerCell.y).a, Is.EqualTo(1f).Within(.01f));
        game.FloorReveal.LayoutRevealed = true;
        game.UpdateMiniMap();
        var stairs = game.CurrentDungeon.Interactables.OfType<Stairs>().First();
        Assert.That(map.minimapTexture.GetPixel(stairs.Position.x * 3 + 1, stairs.Position.y * 3 + 1), Is.EqualTo(Color.white));
        game.PlayerController.Gold += 69;
        var save = Common.Instance.GameSaveData;
        DungeonReturnService.Commit(save, TownSceneLoader.ResolveSaved(), true, game.PlayerController.Gold,
            game.PlayerController.Inventory.InventoryItems, game.Allies);
        DungeonReturnService.Commit(save, TownSceneLoader.ResolveSaved(), true, game.PlayerController.Gold,
            game.PlayerController.Inventory.InventoryItems, game.Allies);
        Assert.That(save.TownSaveData.Gold, Is.EqualTo(800));
    }

    [UnityTest] public IEnumerator WeaponWithoutConsumableEffectCanEquipAndExplainsProficiency()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var actor = harness.Ally;
        var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        try
        {
            definition.ItemName = "Regression blade";
            definition.EquipmentSlot = EquipmentSlot.MainHand;
            definition.WeaponType = actor.PrimaryClass.AllowedWeapons.First();
            var item = definition.AsInventoryItem(null);
            harness.Game.PlayerController.Inventory.Add(item);
            var action = new UseInventoryItemAction(harness.Game.PlayerController.Inventory, actor, item);
            Assert.That(action.CanBegin(actor), Is.True);
            foreach (var effect in action.ExecuteImmediate(actor)) effect.ExecuteImmediate(actor);
            Assert.That(actor.Equipment.IsEquipped(item), Is.True);
            Assert.That(harness.Game.PlayerController.Inventory.InventoryItems, Has.No.Member(item));
            foreach (var effect in action.ExecuteImmediate(actor)) effect.ExecuteImmediate(actor);
            Assert.That(actor.Equipment.IsEquipped(item), Is.False);
            Assert.That(harness.Game.PlayerController.Inventory.InventoryItems.Count(i => ReferenceEquals(i, item)), Is.EqualTo(1));
            var forbidden = System.Enum.GetValues(typeof(WeaponType)).Cast<WeaponType>()
                .First(type => !HeroClass.AllowsWeapon(actor.PrimaryClass, actor.SecondaryClass, type));
            definition.WeaponType = forbidden;
            Assert.That(action.CanBegin(actor), Is.False);
            Assert.That(HeroClass.EquipmentRestriction(actor.PrimaryClass, actor.SecondaryClass, (EquipableInventoryItem)item), Does.Contain("proficiency"));
        }
        finally { Object.Destroy(definition); }
    }

    [UnityTest] public IEnumerator RecruitmentDoesNotCopyLeaderEquipmentAndClearAbilitiesSurviveReload()
    {
        yield return harness.LoadMainMenu(null);
        var menu = Object.FindFirstObjectByType<MainMenu>();
        yield return harness.WaitUntil(() => menu.IsReady, "menu ready");
        menu.StartGame();
        yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "campaign town");
        var common = Common.Instance;
        var town = Object.FindFirstObjectByType<Town>();
        town.TownPlayer.Gold = 10000;
        var leader = town.TownPlayer.ControllingTownAlly;
        var definition = common.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
            .First(d => HeroClass.AllowsWeapon(leader.PrimaryClass, leader.SecondaryClass, d.WeaponType));
        var weapon = definition.AsInventoryItem(null);
        town.TownPlayer.Inventory.Add(weapon);
        Assert.That(town.Services.ToggleEquipment(leader, weapon, out _), Is.True);
        var recruit = town.TownAllies.First();
        Assert.That(town.Services.Recruit(recruit, out _), Is.True);
        Assert.That(recruit.Equipment.GetEquippedItems(), Is.Empty);
        Assert.That(recruit.HeroAnimator.RightHandObjects.Concat(recruit.HeroAnimator.LeftHandObjects).Any(o => o.activeSelf), Is.False,
            "Prefab weapon models must be synchronized when a recruit joins.");
        Assert.That(leader.Equipment.GetEquippedItems().Count(), Is.EqualTo(1));
        Assert.That(town.TownPlayer.Inventory, Has.No.Member(weapon));
        town.WriteSaveData();
        var context = common.CampaignContext;
        Assert.That(context.BeginTownDungeon("story-0"), Is.True);
        Assert.That(context.CompleteDungeon(false), Is.True);
        Assert.That(DungeonClearAbilities.Unlocked, Is.Empty);
        Assert.That(context.BeginTownDungeon("story-0"), Is.True);
        Assert.That(context.CompleteDungeon(true), Is.True);
        var ability = DungeonClearAbilities.ForDungeon("story-0");
        leader.EnsureStartingSkills(); leader.EnsureStartingSkills();
        Assert.That(leader.Skills.Count(s => s == ability.SkillName), Is.EqualTo(1));
        int hp = TownUtilityService.StatsFor(leader).HPMax;
        leader.Skills.Remove(ability.SkillName);
        Assert.That(hp - TownUtilityService.StatsFor(leader).HPMax, Is.EqualTo(6));
        leader.EnsureStartingSkills();
        Assert.That(context.BeginTownDungeon("story-0"), Is.True);
        Assert.That(context.CompleteDungeon(true), Is.True);
        Assert.That(DungeonClearAbilities.Unlocked.Count(), Is.EqualTo(1));
        town.WriteSaveData(); SaveSystem.Capture(common); SaveSystem.SaveData(common.GameSaveData);
        common.GameSaveData = SaveSystem.LoadData();
        common.Travel.Continue();
        yield return null;
        yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>() is Town loaded && loaded != town && loaded.IsReady, "saved town");
        var restored = Object.FindFirstObjectByType<Town>().TownPlayer.RecruitedAllies;
        Assert.That(restored.Sum(h => h.Equipment.GetEquippedItems().Count()), Is.EqualTo(1));
        Assert.That(restored.All(h => h.GetRank(ability.SkillName) == 1), Is.True);
        Assert.That(restored.All(h => h.Skills.Count(s => s == ability.SkillName) == 1), Is.True);
        var dungeonIds = context.Campaign.Locations.Where(l => l.Kind == EternalEnigma.Core.Progression.LocationKind.StoryDungeon ||
            l.Kind == EternalEnigma.Core.Progression.LocationKind.RepeatableDungeon || l.Kind == EternalEnigma.Core.Progression.LocationKind.FinalDungeon).Select(l => l.Id).ToArray();
        Assert.That(dungeonIds.Select(id => DungeonClearAbilities.ForDungeon(id).SkillName).Distinct().Count(), Is.EqualTo(dungeonIds.Length));
    }

    [UnityTest] public IEnumerator SaveSlotsSupportKeyboardSelectionAndSubmit()
    {
        yield return harness.LoadMainMenu(null);
        var menu = Object.FindFirstObjectByType<MainMenu>();
        yield return harness.WaitUntil(() => menu.IsReady, "menu ready");
        keyboard = InputSystem.AddDevice<Keyboard>();
        MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard };
        CampaignSlots.Show(menu, true);
        var slots = Object.FindFirstObjectByType<CampaignSlots>();
        int initial = SaveSystem.ActiveSlot;
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(slots.Slots[initial].Button.gameObject));
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        int next = (initial + 1) % SaveSystem.SlotCount;
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(slots.Slots[next].Button.gameObject));
        Assert.That(slots.Slots[next].Label.color, Is.Not.EqualTo(slots.Slots[initial].Label.color));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
        yield return null; yield return null;
        Assert.That(slots.gameObject.activeSelf, Is.False);
        Assert.That(SaveSystem.ActiveSlot, Is.EqualTo(next));
    }

    [UnityTest] public IEnumerator VictoryCarriesBalanceEquipmentAndAbilityThroughSaveLoad() => CampaignReturn(true);
    [UnityTest] public IEnumerator DefeatHonorsLootPolicyThroughSaveLoad() => CampaignReturn(false);

    private IEnumerator CampaignReturn(bool victory)
    {
        yield return harness.LoadMainMenu(null);
        var menu = Object.FindFirstObjectByType<MainMenu>();
        yield return harness.WaitUntil(() => menu.IsReady, "menu ready");
        menu.StartGame();
        yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "campaign town");
        var common = Common.Instance;
        var town = Object.FindFirstObjectByType<Town>();
        town.TownPlayer.Gold = 731;
        town.TownPlayer.Inventory.Clear();
        var supply = common.ItemManager.ItemDefinitions.First(d => d.StackMax >= 3).AsInventoryItem(3);
        town.TownPlayer.Inventory.Add(supply);
        var hero = town.TownPlayer.ControllingTownAlly;
        var definition = common.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
            .First(d => HeroClass.AllowsWeapon(hero.PrimaryClass, hero.SecondaryClass, d.WeaponType));
        var weapon = definition.AsInventoryItem(null);
        town.TownPlayer.Inventory.Add(weapon);
        Assert.That(town.Services.ToggleEquipment(hero, weapon, out _), Is.True);
        bool keepGold = town.Configuration.KeepGoldOnDefeat;
        bool keepItems = victory || !town.Configuration.LoseItemsOnDefeat;
        Assert.That(common.Travel.EnterTownDungeon(town, "story-0"), Is.True);
        yield return harness.WaitForIdle();
        var game = harness.Game;
        Assert.That(game.PlayerController.Gold, Is.EqualTo(731));
        Assert.That(game.Allies.Sum(a => a.Equipment.GetEquippedItems().Count()), Is.EqualTo(1));
        var carriedSupply = game.PlayerController.Inventory.InventoryItems.Single();
        Assert.That(carriedSupply.StackStock, Is.EqualTo(3));
        carriedSupply.Decrement();
        game.PlayerController.Gold += 69;
        Assert.That(common.Travel.FinishDungeon(victory, game.PlayerController), Is.True);
        Assert.That(common.Travel.FinishDungeon(victory, game.PlayerController), Is.False);
        if (victory)
        {
            yield return harness.WaitUntil(() => common.MessageDialog.gameObject.activeSelf, "clear reward");
            Assert.That(common.MessageDialog.PromptText.text, Does.Contain("Gatekeeper's Resolve"));
            common.MessageDialog.Ok_Clicked();
        }
        yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "returned town");
        town = Object.FindFirstObjectByType<Town>();
        Assert.That(town.TownPlayer.Gold, Is.EqualTo(victory || keepGold ? 800 : 731));
        Assert.That(town.TownPlayer.RecruitedAllies.Sum(a => a.Equipment.GetEquippedItems().Count()), Is.EqualTo(keepItems ? 1 : 0));
        Assert.That(town.TownPlayer.Inventory.Count, Is.EqualTo(keepItems ? 1 : 0));
        if (keepItems) Assert.That(town.TownPlayer.Inventory.Single().StackStock, Is.EqualTo(2));
        Assert.That(common.CampaignContext.Completed.Contains("story-0"), Is.EqualTo(victory));
        town.WriteSaveData(); SaveSystem.Capture(common); SaveSystem.SaveData(common.GameSaveData);
        common.GameSaveData = SaveSystem.LoadData(); common.Travel.Continue();
        yield return null;
        yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>() is Town loaded && loaded != town && loaded.IsReady, "reloaded return");
        var restored = Object.FindFirstObjectByType<Town>().TownPlayer;
        Assert.That(restored.Gold, Is.EqualTo(victory || keepGold ? 800 : 731));
        Assert.That(restored.RecruitedAllies.Sum(a => a.Equipment.GetEquippedItems().Count()), Is.EqualTo(keepItems ? 1 : 0));
        Assert.That(restored.Inventory.Count, Is.EqualTo(keepItems ? 1 : 0));
        if (keepItems) Assert.That(restored.Inventory.Single().StackStock, Is.EqualTo(2));
        Assert.That(restored.RecruitedAllies.All(a => a.GetRank("Gatekeeper's Resolve") == 1), Is.EqualTo(victory));
    }
}
#endif

