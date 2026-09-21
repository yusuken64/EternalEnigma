using EternalEnigma.Core.Progression;
using NUnit.Framework;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class CampaignSaveTests
    {
        private sealed class Store : ISaveStore
        {
            public string Json;
            public int Writes;
            public string Read() => Json;
            public void Write(string json) { Json = json; Writes++; }
            public void Clear() => Json = null;
        }
        [Test]
        public void LegacySaveIsNotMistakenForAMaterializedEmptyCampaign()
        {
            var store = new Store { Json = "{\"TownSaveData\":{\"TownSeed\":123},\"DungeonSaveData\":{}}" };
            using (SaveSystem.UseStore(store))
            {
                var save = SaveSystem.LoadData();
                Assert.That(save.CampaignFormatVersion, Is.Zero);
                Assert.That(save.Campaign, Is.Null);
                Assert.That(save.TownSaveData.TownSeed, Is.EqualTo(123));
                Assert.That(store.Writes, Is.Zero);
            }
        }
        [Test]
        public void VersionedCampaignSurvivesUnityJsonAndRestoresPhysicalGateState()
        {
            var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 42));
            context.Position = context.Grid.Locations["story-0"];
            context.BeginTownDungeon("story-0"); context.CompleteDungeon(true);
            var store = new Store();
            using (SaveSystem.UseStore(store))
            {
                SaveSystem.SaveData(new GameSaveData { Campaign = context.Capture() });
                var save = SaveSystem.LoadData();
                Assert.That(save.CampaignFormatVersion, Is.EqualTo(1));
                var restored = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign), save.Campaign);
                Assert.That(restored.Keys, Contains.Item("Town gate key"));
                Assert.That(restored.Resolved, Does.Not.Contain("starter-exit"));
                Assert.That(restored.State.Identity, Is.EqualTo(context.State.Identity));
                Assert.That(restored.Position, Is.EqualTo(context.Position));
            }
        }
        [Test]
        public void SandboxSaveCannotWriteEvenWithoutALiveCommon()
        {
            var store = new Store { Json = "player save" };
            using (SaveSystem.UseStore(store)) SaveSystem.SaveData(new GameSaveData { IsSandbox = true });
            Assert.That(store.Json, Is.EqualTo("player save"));
            Assert.That(store.Writes, Is.Zero);
        }
    }
}
