#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

[PrebuildSetup(typeof(HarnessSceneBootstrap))]
[PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public class HarnessSmokeTests
{
    private GameTestHarness harness;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        harness = new GameTestHarness();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown() => harness.Cleanup();

    [UnityTest]
    public IEnumerator DungeonScenarioLoadsRealSceneAndPlacesAlly()
    {
        yield return harness.LoadDungeon(new TestScenario { HP = 7, SP = 3 });
        Assert.That(harness.Game.IsReady, Is.True);
        Assert.That(harness.Ally.CharacterName, Is.EqualTo("Rowan"));
        Assert.That(harness.Ally.Vitals.HP, Is.EqualTo(7));
        Assert.That(harness.Game.PlayerController.Inventory.Count(), Is.Zero);
        Assert.That(SaveSystem.LoadData().OverworldSaveData.OverworldSeed, Is.EqualTo(12345));
        harness.PlaceBesideStairs();
        Assert.That(harness.Game.CurrentDungeon.IsWalkable(harness.Ally.TilemapPosition), Is.True);
    }

    [UnityTest]
    public IEnumerator OverworldScenarioLoadsSuppliedGoldAndAlly()
    {
        yield return harness.LoadOverworld(new TestScenario { Gold = 432 }.CreateSave());
        var player = UnityEngine.Object.FindFirstObjectByType<OverworldPlayer>();
        Assert.That(player.Gold, Is.EqualTo(432));
        Assert.That(player.RecruitedAllies.Count, Is.EqualTo(1));
        Assert.That(player.ControllingOverworldAlly.Name, Is.EqualTo("Rowan"));
    }

    [UnityTest]
    public IEnumerator OrdinaryEquipUsesInventoryButtonsAndRealTurnPipeline()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var definition = Common.Instance.ItemManager.ItemDefinitions.Find(d =>
            d is EquipmentItemDefinition e && e.EquipmentSlot == EquipmentSlot.MainHand);
        Assert.That(definition, Is.Not.Null, "Need one main-hand item in ItemManager.");
        var item = harness.AddItem(definition.ItemName) as EquipableInventoryItem;
        yield return harness.UseItemThroughMenu(item);
        Assert.That(harness.Ally.Equipment.EquippedWeapon, Is.SameAs(item));
        Assert.That(harness.Game.PlayerController.Inventory.InventoryItems.Contains(item), Is.False);
        Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
    }
}
#endif
