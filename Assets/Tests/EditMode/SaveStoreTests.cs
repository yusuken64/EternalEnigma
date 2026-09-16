using NUnit.Framework;

public class SaveStoreTests
{
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
            data.OverworldSaveData.Gold = 321;
            data.OverworldSaveData.Inventory.Add("Test sword");
            SaveSystem.SaveData(data);
            var originalJson = outer.Json;
            using (SaveSystem.UseStore(new Store()))
            {
                SaveSystem.SaveData(new GameSaveData());
                SaveSystem.ClearData();
                Assert.That(SaveSystem.LoadData(), Is.Null);
            }
            var loaded = SaveSystem.LoadData();
            Assert.That(loaded.OverworldSaveData.Gold, Is.EqualTo(321));
            Assert.That(loaded.OverworldSaveData.Inventory, Is.EqualTo(new[] { "Test sword" }));
            loaded.OverworldSaveData.Inventory.Clear();
            Assert.That(SaveSystem.LoadData().OverworldSaveData.Inventory.Count, Is.EqualTo(1));
            Assert.That(outer.Json, Is.EqualTo(originalJson));
        }
    }
}
