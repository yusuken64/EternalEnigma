using NUnit.Framework;
using UnityEngine;

public sealed class CoreStateRegressionTests
{
    [TestCase(true, false, false, 800)]
    [TestCase(true, true, false, 800)]
    [TestCase(false, true, false, 800)]
    [TestCase(false, false, false, 731)]
    [TestCase(false, false, true, 800)]
    public void ReturnCommitsCarriedBalanceOnce(bool victory, bool keepGold, bool retreat, int expected)
    {
        var config = ScriptableObject.CreateInstance<TownConfiguration>();
        try
        {
            config.Id = "test-town";
            config.KeepGoldOnDefeat = keepGold;
            var save = new GameSaveData(); save.TownSaveData.Gold = 731;
            DungeonReturnService.Commit(save, config, victory, 800, new InventoryItem[0], new Ally[0], retreat);
            DungeonReturnService.Commit(save, config, victory, 999, new InventoryItem[0], new Ally[0], retreat);
            Assert.That(save.TownSaveData.Gold, Is.EqualTo(expected));
            Assert.That(save.TownSaveData.RestockCycle, Is.EqualTo(victory||retreat?1:0));
        }
        finally { Object.DestroyImmediate(config); }
    }
}
