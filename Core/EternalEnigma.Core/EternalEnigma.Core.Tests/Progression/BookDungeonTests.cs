using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;

public sealed class BookDungeonTests
{
    [Theory]
    [InlineData(0)] [InlineData(42)] [InlineData(-1)]
    [InlineData(int.MinValue)] [InlineData(int.MaxValue)]
    public void BookIsDeterministicSeparateContentWithFiveValidFloors(int seed)
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, seed));
        var book = context.BookDungeon;
        var town = context.Campaign.Locations.Single(l => l.Id == context.Campaign.StartLocationId);
        Assert.Equal("book-0", book.Id);
        Assert.Equal("The Other Country", book.Title);
        Assert.Equal(book.Title, CampaignGuidance.LocationName(context, book.Id));
        Assert.Equal(town.Id, book.ParentTownId);
        Assert.Equal("story-0", book.RequiredCompletionId);
        Assert.Equal(0, book.Location.Tier);
        Assert.False(book.Location.Required);
        Assert.NotEqual(OverworldGridGenerator.BiomeForRegion(context.Campaign, town.RegionId), book.Biome);
        Assert.Same(book.Location, context.ResolveDungeonLocation(book.Id));
        Assert.Null(context.ResolveDungeonLocation(town.Id));
        Assert.Null(context.ResolveDungeonLocation("missing"));
        Assert.DoesNotContain(context.Campaign.Locations, l => l.Id == book.Id);
        Assert.DoesNotContain(context.Campaign.Routes, r => r.Other(book.Id) != null || r.KeyLocationId == book.Id);
        Assert.DoesNotContain(context.Campaign.Sources, s => s.LocationId == book.Id);
        Assert.Equal(CampaignFingerprint.Compute(CampaignGenerator.Generate(seed)), context.State.Fingerprint);
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.Equal(book.Biome, restored.BookDungeon.Biome);
        var plan = context.RunPlan(book.Id);
        Assert.Equal(Enumerable.Range(1, 5), plan.Floors.Select(f => f.Number));
        Assert.False(plan.IsEncounter);
        Assert.Equal(DungeonVariant.Standard, plan.Variant);
        foreach (var runFloor in plan.Floors)
        {
            Assert.Equal(book.Biome, runFloor.Options.Biome);
            Assert.Equal(runFloor.Number, runFloor.CombatDifficulty);
            var floor = context.DungeonFloor(book.Id, runFloor.Number);
            var validation = DungeonFloorValidator.Validate(floor, runFloor.Options);
            Assert.True(validation.IsValid, string.Join("\n", validation.Errors));
            Assert.Equal(DungeonFloorFingerprint.Compute(floor),
                DungeonFloorFingerprint.Compute(restored.DungeonFloor(book.Id, runFloor.Number)));
            if (runFloor.Role == DungeonFloorRole.Regular)
            {
                Assert.NotEmpty(floor.Items);
                Assert.NotEmpty(floor.Gold);
                Assert.NotEmpty(floor.Enemies);
            }
        }
        Assert.False(context.IsGridGenerated);
        Assert.False(restored.IsGridGenerated);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BookUnlocksAfterOpeningClearAndCanReplayWithoutProgressionRewards(bool finished)
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var book = context.BookDungeon;
        Assert.False(context.CanEnterDungeon(book.Id));
        Assert.False(context.BeginTownDungeon(book.Id));
        Assert.True(context.BeginTownDungeon(book.RequiredCompletionId));
        Assert.True(context.CompleteDungeon(true));
        context.State.Finished = finished;
        var keys = context.Keys.OrderBy(x => x).ToArray();
        var opened = context.Opened.OrderBy(x => x).ToArray();
        var resolved = context.Resolved.OrderBy(x => x).ToArray();
        var permanent = context.Permanent;
        var claimed = context.Claimed.OrderBy(x => x).ToArray();
        Assert.True(context.CanEnterDungeon(book.Id));
        for (int attempt = 0; attempt < 2; attempt++)
        {
            Assert.True(context.BeginTownDungeon(book.Id));
            Assert.Same(book.Location, context.Location);
            Assert.False(context.BeginTownDungeon(book.Id));
            Assert.False(context.SetParty());
            Assert.True(context.CompleteDungeon(true));
            Assert.Equal(book.ParentTownId, context.Location!.Id);
            Assert.Equal("Town", context.State.Scene);
            Assert.Equal("", context.State.PendingDungeon);
        }
        Assert.Equal(2, context.Completed.Count);
        Assert.Contains(book.Id, context.Completed);
        Assert.Equal(keys, context.Keys.OrderBy(x => x));
        Assert.Equal(opened, context.Opened.OrderBy(x => x));
        Assert.Equal(resolved, context.Resolved.OrderBy(x => x));
        Assert.Equal(permanent, context.Permanent);
        Assert.Equal(claimed, context.Claimed.OrderBy(x => x));
        Assert.Equal(finished, context.State.Finished);
        Assert.False(context.ResetFloorUnlocked);
        Assert.False(context.IsGridGenerated);
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.Contains(book.Id, restored.Completed);
        Assert.True(restored.BeginTownDungeon(book.Id));
        Assert.True(restored.CompleteDungeon(false));
        Assert.Contains(book.Id, restored.Completed);
    }

    [Fact]
    public void BookCannotBeEnteredOutsideItsHouseTownOrBeforeHomePreparation()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign));
        context.Completed.Add("story-0");
        context.State.Scene = "Overworld";
        Assert.False(context.BeginTownDungeon(context.BookDungeon.Id));
        context.State.Scene = "Town";
        context.State.LocationId = "town-1";
        Assert.False(context.BeginTownDungeon(context.BookDungeon.Id));
        context.State.LocationId = "town-0";
        context.State.RequiresOpeningPreparation = true;
        context.State.HomeLessonVersion = 1;
        Assert.False(context.CanEnterDungeon(context.BookDungeon.Id));
        context.State.HomeDoorUnlocked = true;
        Assert.True(context.BeginTownDungeon(context.BookDungeon.Id));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RetreatAndInterruptedBookReturnWithoutAwardingClear(bool generateGrid)
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign));
        context.Completed.Add("story-0");
        Assert.True(context.BeginTownDungeon(context.BookDungeon.Id));
        if (generateGrid) Assert.Equal(context.Grid.Locations["town-0"], context.Position);
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.Equal("book-0", restored.Location!.Id);
        Assert.False(restored.IsGridGenerated);
        Assert.True(restored.RecoverInterruptedRun());
        Assert.False(restored.RecoverInterruptedRun());
        Assert.Equal("town-0", restored.State.LocationId);
        Assert.Equal("Town", restored.State.Scene);
        Assert.False(restored.IsGridGenerated);
        Assert.True(restored.BeginTownDungeon(restored.BookDungeon.Id));
        Assert.True(restored.CompleteDungeon(false));
        Assert.Equal("town-0", restored.State.LocationId);
        Assert.DoesNotContain("book-0", restored.Completed);
        Assert.Empty(restored.Keys);
    }

    [Theory]
    [InlineData(11, false)] [InlineData(12, true)] [InlineData(13, false)] [InlineData(13, true)]
    public void ExistingSnapshotsUpgradeWithoutChangingBaseWorldOrProgression(int version, bool finished)
    {
        var campaign = CampaignGenerator.GenerateVersion(42, version);
        var fingerprint = CampaignFingerprint.Compute(campaign);
        var snapshot = new CampaignSnapshot
        {
            Seed = 42, GeneratorVersion = version, Fingerprint = fingerprint, Identity = "existing-save",
            TownLayoutVersion = 0, Scene = "Town", LocationId = "town-0", LastTownId = "town-0", X = 17, Y = 23,
            Finished = finished, Completed = finished ? new[] { "story-0", campaign.FinalLocationId } : Array.Empty<string>(),
            Permanent = new[] { Capability.HomeKey }, Keys = new[] { "existing-key" },
            Roster = new[] { "ordinary-1" }, Active = new[] { "ordinary-1" }, Towns = new[] { "town-0" }
        };
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign), snapshot);
        Assert.Equal(1, snapshot.BookContentVersion);
        Assert.Equal(fingerprint, snapshot.Fingerprint);
        Assert.Equal(version, snapshot.GeneratorVersion);
        Assert.Equal(0, snapshot.TownLayoutVersion);
        Assert.Equal("existing-save", snapshot.Identity);
        Assert.Equal((17, 23), (snapshot.X, snapshot.Y));
        Assert.Equal(finished, snapshot.Finished);
        Assert.Equal(finished, context.CanEnterDungeon(context.BookDungeon.Id));
        Assert.Equal(new[] { "existing-key" }, context.Keys);
        Assert.Contains(Capability.HomeKey, context.Permanent.Values);
        Assert.Equal(new[] { "ordinary-1" }, context.Active);
        Assert.Equal(new[] { "ordinary-1" }, context.Roster);
        var townBefore = context.Town("town-0");
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.Equal(1, restored.State.BookContentVersion);
        Assert.Equal(context.BookDungeon.Biome, restored.BookDungeon.Biome);
        Assert.Equal(context.Completed.OrderBy(x => x), restored.Completed.OrderBy(x => x));
        var townAfter = restored.Town("town-0");
        Assert.Equal(townBefore.BuildingSlots, townAfter.BuildingSlots);
        Assert.All(townBefore.Layers, layer => Assert.Equal(layer.Value.ToArray().Cast<bool>(), townAfter.Layers[layer.Key].ToArray().Cast<bool>()));
        var originalGrid = OverworldGridGenerator.Generate(campaign);
        Assert.Equal(originalGrid.Locations.OrderBy(l => l.Key), restored.Grid.Locations.OrderBy(l => l.Key));
        Assert.All(originalGrid.Layers, layer => Assert.Equal(layer.Value.ToArray().Cast<bool>(), restored.Grid.Layers[layer.Key].ToArray().Cast<bool>()));
        Assert.DoesNotContain("book-0", restored.Grid.Locations.Keys);
    }

    [Fact]
    public void InvalidFingerprintIsRejectedBeforeOptionalContentUpgrade()
    {
        var snapshot = new CampaignSnapshot { Seed = 42, GeneratorVersion = CampaignGenerator.Version, Fingerprint = "invalid" };
        var error = Assert.Throws<InvalidOperationException>(() => new CampaignContext(new(OverworldLaunchMode.Campaign), snapshot));
        Assert.Contains("fingerprint", error.Message);
        Assert.Equal(0, snapshot.BookContentVersion);
        Assert.Equal("invalid", snapshot.Fingerprint);
    }

    [Theory]
    [InlineData(-1)] [InlineData(2)]
    public void UnsupportedOptionalContentVersionIsRejectedWithoutDowngrade(int contentVersion)
    {
        var snapshot = new CampaignSnapshot { Seed = 42, GeneratorVersion = CampaignGenerator.Version,
            Fingerprint = CampaignFingerprint.Compute(CampaignGenerator.Generate(42)), BookContentVersion = contentVersion };
        var error = Assert.Throws<InvalidOperationException>(() => new CampaignContext(new(OverworldLaunchMode.Campaign), snapshot));
        Assert.Contains("book content version", error.Message);
        Assert.Equal(contentVersion, snapshot.BookContentVersion);
    }
}
