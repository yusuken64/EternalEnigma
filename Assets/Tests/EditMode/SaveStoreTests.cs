using NUnit.Framework;

public class SaveStoreTests
{
    [Test]
    public void LegacyWorldKeysMigrateWithoutLosingProgress()
    {
        var store = new Store { Json = "{\"OverworldSaveData\":{\"OverworldSeed\":123,\"Gold\":731,\"DonationTotal\":1200,\"Inventory\":[\"Test sword\"],\"RecruitedAlliesData\":[{\"AllyName\":\"Rowan\",\"Skills\":[\"Healing\"]}]},\"DungeonSaveData\":{\"StartFloor\":1,\"EndFloor\":5}}" };
        using (SaveSystem.UseStore(store))
        {
            var save = SaveSystem.LoadData();
            Assert.That(save.TownSaveData.TownSeed, Is.EqualTo(123));
            Assert.That(save.TownSaveData.Gold, Is.EqualTo(731));
            Assert.That(save.TownSaveData.DonationTotal, Is.EqualTo(1200));
            Assert.That(save.TownSaveData.Inventory, Is.EqualTo(new[] { "Test sword" }));
            Assert.That(save.TownSaveData.InventoryFormatVersion, Is.Zero);
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].Skills, Does.Contain("Healing"));
            SaveSystem.SaveData(save);
            Assert.That(store.Json, Does.Contain("TownSaveData"));
            Assert.That(store.Json, Does.Not.Contain("OverworldSaveData"));
        }
    }

    private sealed class Store : ISaveStore
    {
        public string Json;
        public string Read() => Json;
        public void Write(string json) => Json = json;
        public void Clear() => Json = null;
    }

    [Test]
    public void SaveRoundTripUsesOnlyScopedStoreAndReturnsIndependentData()
    {
        var outer = new Store();
        using (SaveSystem.UseStore(outer))
        {
            var data = new GameSaveData();
            data.TownSaveData.Gold = 321;
            data.TownSaveData.Inventory.Add("Test sword");
            SaveSystem.SaveData(data);
            var originalJson = outer.Json;
            using (SaveSystem.UseStore(new Store()))
            {
                SaveSystem.SaveData(new GameSaveData());
                SaveSystem.ClearData();
                Assert.That(SaveSystem.LoadData(), Is.Null);
            }
            var loaded = SaveSystem.LoadData();
            Assert.That(loaded.TownSaveData.Gold, Is.EqualTo(321));
            Assert.That(loaded.TownSaveData.Inventory, Is.EqualTo(new[] { "Test sword" }));
            loaded.TownSaveData.Inventory.Clear();
            Assert.That(SaveSystem.LoadData().TownSaveData.Inventory.Count, Is.EqualTo(1));
            Assert.That(outer.Json, Is.EqualTo(originalJson));
        }
    }
}
