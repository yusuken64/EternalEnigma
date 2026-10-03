using NUnit.Framework;
using System.Linq;

public class SaveStoreTests
{
    [Test]
    public void CurrentSchemaPreservesStockAndDoesNotWriteFormatMetadata()
    {
        var store = new Store();
        using (SaveSystem.UseStore(store))
        {
            var data = new GameSaveData();
            data.TownSaveData.TownSeed = 123;
            data.TownSaveData.Gold = 731;
            data.TownSaveData.DonationTotal = 1200;
            data.TownSaveData.InventoryItems.Add(new ItemSaveData { ItemName = "Wooden Arrow", HasStock = true, Stock = 7 });
            data.TownSaveData.RecruitedAlliesData.Add(new TownAllyData { AllyName = "Rowan", Skills = new() { "Healing" } });
            SaveSystem.SaveData(data);
            var save = SaveSystem.LoadData();
            Assert.That(save.TownSaveData.TownSeed, Is.EqualTo(123));
            Assert.That(save.TownSaveData.Gold, Is.EqualTo(731));
            Assert.That(save.TownSaveData.DonationTotal, Is.EqualTo(1200));
            Assert.That(save.TownSaveData.InventoryItems.Single().ItemName, Is.EqualTo("Wooden Arrow"));
            Assert.That(save.TownSaveData.InventoryItems.Single().HasStock, Is.True);
            Assert.That(save.TownSaveData.InventoryItems.Single().Stock, Is.EqualTo(7));
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].Skills, Does.Contain("Healing"));
            SaveSystem.SaveData(save);
            Assert.That(store.Json, Does.Contain("TownSaveData"));
            Assert.That(store.Json, Does.Not.Contain("OverworldSaveData"));
            Assert.That(store.Json, Does.Not.Contain("FormatVersion"));
            Assert.That(store.Json, Does.Not.Contain("LayoutVersion"));
            Assert.That(store.Json, Does.Not.Contain("VisualSelectionVersion"));
            Assert.That(store.Json, Does.Not.Contain("\"Inventory\":"));
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
            data.TownSaveData.InventoryItems.Add(new ItemSaveData { ItemName = "Test sword" });
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
            Assert.That(loaded.TownSaveData.InventoryItems.Single().ItemName, Is.EqualTo("Test sword"));
            loaded.TownSaveData.InventoryItems.Clear();
            Assert.That(SaveSystem.LoadData().TownSaveData.InventoryItems.Count, Is.EqualTo(1));
            Assert.That(outer.Json, Is.EqualTo(originalJson));
        }
    }
}
