#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[PrebuildSetup(typeof(HarnessSceneBootstrap))]
[PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public class AuditRegressionTests
{
    private GameTestHarness harness;
    [SetUp] public void SetUp() => harness = new GameTestHarness();
    [UnityTearDown] public IEnumerator TearDown() => harness.Cleanup();

    private EquipableInventoryItem AddEquipment(EquipmentSlot slot)
    {
        var definition = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
            .First(x => x.EquipmentSlot == slot);
        var item = (EquipableInventoryItem)definition.AsInventoryItem(null);
        harness.Game.PlayerController.Inventory.Add(item);
        return item;
    }

    [UnityTest]
    public IEnumerator ReplacementReturnsBothDisplacedItemsExactlyOnce()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var sword = AddEquipment(EquipmentSlot.MainHand);
        var shield = AddEquipment(EquipmentSlot.OffHand);
        var twoHand = AddEquipment(EquipmentSlot.TwoHand);
        yield return harness.UseItemThroughMenu(sword);
        yield return harness.UseItemThroughMenu(shield);
        yield return harness.UseItemThroughMenu(twoHand);
        var bag = harness.Game.PlayerController.Inventory.InventoryItems;
        Assert.That(bag.Count(x => x == sword), Is.EqualTo(1));
        Assert.That(bag.Count(x => x == shield), Is.EqualTo(1));
        Assert.That(bag.Contains(twoHand), Is.False);
        Assert.That(harness.Ally.Equipment.EquippedShield, Is.Null);
        yield return harness.UseItemThroughMenu(shield);
        Assert.That(harness.Ally.Equipment.EquippedWeapon, Is.Null);
        Assert.That(bag.Count(x => x == twoHand), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator TwoHandWeaponCanBeUnequippedThroughMenu()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var item = AddEquipment(EquipmentSlot.TwoHand);
        yield return harness.UseItemThroughMenu(item);
        Assert.That(harness.Ally.Equipment.EquippedWeapon, Is.SameAs(item));
        yield return harness.UseItemThroughMenu(item);
        Assert.That(harness.Ally.Equipment.EquippedWeapon, Is.Null);
        Assert.That(harness.Game.PlayerController.Inventory.InventoryItems.Count(x => x == item), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator SavedInventorySurvivesTownLoadAndSnapshot()
    {
        // Discover an actual catalog item without hard-coding renamed game content.
        var definition = UnityEditor.AssetDatabase.FindAssets("t:EquipmentItemDefinition")
            .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
            .First();
        var save = new TestScenario { Items = new[] { definition.ItemName, definition.ItemName } }.CreateSave();
        yield return harness.LoadTown(save);
        var world = Object.FindFirstObjectByType<Town>();
        Assert.That(world.TownPlayer.Inventory.Select(x => x.ItemName), Is.EqualTo(save.TownSaveData.Inventory));
        world.WriteSaveData();
        Assert.That(Common.Instance.GameSaveData.TownSaveData.Inventory, Is.EqualTo(save.TownSaveData.Inventory));
    }

    [UnityTest]
    public IEnumerator ReturnToMainMenuSavesTownProgress()
    {
        yield return harness.LoadTown(new TestScenario().CreateSave());
        var world = Object.FindFirstObjectByType<Town>();
        world.TownPlayer.Gold = 37;
        Object.FindFirstObjectByType<StatueDialog>(FindObjectsInactive.Include).DonatedAmount = 63;
        var item = Common.Instance.ItemManager.ItemDefinitions.First();
        world.TownPlayer.Inventory.Add(item.AsInventoryItem(null));
        world.TownPlayer.RecruitedAllies[0].Skills.Add("regression-snapshot");
        Common.Instance.GlobalSettings.ShowDialog();
        Common.Instance.GlobalSettings.ReturntoMainButton.onClick.Invoke();
        yield return null;
        var saved = SaveSystem.LoadData().TownSaveData;
        Assert.That(saved.Gold, Is.EqualTo(37));
        Assert.That(saved.DonationTotal, Is.EqualTo(63));
        Assert.That(saved.Inventory, Does.Contain(item.ItemName));
        Assert.That(saved.RecruitedAlliesData[0].Skills, Does.Contain("regression-snapshot"));
    }

    [UnityTest]
    public IEnumerator CurrentFloorMatchesHudAndExitFloor()
    {
        yield return harness.LoadDungeon(new TestScenario { StartFloor = 4, EndFloor = 5 });
        Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(4));
        Assert.That(harness.Game.FloorText.text, Is.EqualTo("4F"));
        Assert.That(harness.Game.NewFloorMessage.FloorMessage.text, Is.EqualTo("Floor 4"));
        Assert.That(harness.Game.CurrentDungeon.IsExitFloor, Is.False);
        harness.Game.AdvanceFloor();
        yield return harness.WaitForIdle();
        Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(5));
        Assert.That(harness.Game.FloorText.text, Is.EqualTo("5F"));
        Assert.That(harness.Game.CurrentDungeon.IsExitFloor, Is.True);
        Assert.That(harness.Game.NewFloorMessage.FloorMessage.text, Is.EqualTo("Floor 5"));
        harness.Game.GameOverScreen.Setup(harness.Game.PlayerController);
        Assert.That(harness.Game.GameOverScreen.MessageText.text, Does.Contain("On floor 5"));
    }

    [UnityTest]
    public IEnumerator AttackOnStairsReopensPromptWithoutMoving()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var stairs = Object.FindFirstObjectByType<Stairs>();
        harness.PlaceAlly(stairs.Position);
        harness.Ally.currentInteractable = stairs;
        harness.Ally.MovedThisTurn = true;
        harness.Game.PlayerController.StartTurn();
        MenuManager.Instance.StairDialog.NoButton.onClick.Invoke();
        Assert.That(MenuManager.Instance.StairDialog.gameObject.activeSelf, Is.False);
        harness.Ally.MovedThisTurn = false;
        // Inject the sampled input edge, then invoke the production input decision.
        typeof(PlayerInputHandler).GetProperty("attackPressed").SetValue(PlayerInputHandler.Instance, true);
        typeof(PlayerController).GetMethod("DeterminePlayerAction", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(harness.Game.PlayerController, null);
        Assert.That(MenuManager.Instance.StairDialog.gameObject.activeSelf, Is.True);
        Assert.That(MenuManager.Instance.StairDialog.PromptText.text, Is.EqualTo("Take Stairs?"));
        MenuManager.Instance.StairDialog.NoButton.onClick.Invoke();
        harness.Game.CurrentDungeon.IsExitFloor = true;
        typeof(PlayerController).GetMethod("DeterminePlayerAction", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(harness.Game.PlayerController, null);
        Assert.That(MenuManager.Instance.StairDialog.PromptText.text, Is.EqualTo("Exit Dungeon?"));
        yield return null;
    }
}
#endif
