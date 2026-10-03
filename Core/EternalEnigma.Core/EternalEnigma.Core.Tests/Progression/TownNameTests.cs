using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;
public sealed class TownNameTests
{
    [Fact]
    public void NamesAreUniqueDeterministicAndDoNotGenerateGridOrChangeFingerprint()
    {
        for (int seed = 0; seed < 30; seed++) {
            var a = new CampaignContext(new(OverworldLaunchMode.Campaign, seed));
            var b = new CampaignContext(new(OverworldLaunchMode.Campaign, seed));
            Assert.False(a.IsGridGenerated);
            Assert.Equal(a.State.TownNames.Select(n => n.Name), b.State.TownNames.Select(n => n.Name));
            Assert.Equal(a.State.TownNames.Length, a.State.TownNames.Select(n => n.Name).Distinct().Count());
            Assert.Equal(CampaignFingerprint.Compute(a.Campaign), a.Capture().Fingerprint);
        }
    }
    [Fact]
    public void OldSavesBackfillAndStoredNamesSurvive()
    {
        var original = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var save = original.Capture();
        save.TownNames = new[] { new TownDisplayName { TownId = "town-0", Name = "My Old Town" } };
        save.NamingVersion = 0;
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), save);
        Assert.Equal("My Old Town", restored.GetTownDisplayName("town-0"));
        Assert.Equal(1, restored.Capture().NamingVersion);
        Assert.Equal(original.Campaign.Locations.Count(l => l.Kind == LocationKind.Town), restored.State.TownNames.Length);
        Assert.Equal(restored.State.TownNames.Select(n => n.Name), new CampaignContext(new(OverworldLaunchMode.Campaign), restored.Capture()).State.TownNames.Select(n => n.Name));
    }
    [Fact]
    public void VocabularyExhaustionStillProducesUniqueNames()
    {
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 200; i++) TownNameGenerator.Generate(42, "same", biome, used);
            Assert.Equal(200, used.Count);
        }
    }
}
