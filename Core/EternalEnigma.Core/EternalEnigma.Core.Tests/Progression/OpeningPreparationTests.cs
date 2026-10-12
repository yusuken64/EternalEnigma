using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;

public sealed class OpeningPreparationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OnlyOpeningKeyUnlocksPreparedNewJourney(bool bread, bool skill)
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        context.State.RequiresOpeningPreparation = true;
        context.State.BoughtOpeningBread = bread;
        context.State.LearnedOpeningSkill = skill;
        context.Keys.Add("Town gate key");
        Assert.Equal(bread && skill, context.ReadyForOpeningDay);
        Assert.False(context.CanEnterDungeon("story-0"));
        Assert.False(context.BeginTownDungeon("story-0"));
        Assert.False(context.BeginDungeon());
        Assert.False(context.IsGridGenerated);

        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.Equal(bread && skill, restored.ReadyForOpeningDay);
        Assert.False(restored.BeginTownDungeon("story-0"));
        restored.Keys.Add(CampaignContext.OpeningDayKey);
        Assert.True(restored.BeginTownDungeon("story-0"));
        Assert.True(restored.CompleteDungeon(false));
        Assert.True(restored.BeginTownDungeon("story-0"));
        Assert.False(restored.IsGridGenerated);
        Assert.Empty(restored.Completed);
    }

    [Fact]
    public void OpeningDayDoesNotUnlockTownExitOrRequireOwningBread()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        context.State.RequiresOpeningPreparation = true;
        context.Keys.Add(CampaignContext.OpeningDayKey);
        Assert.False(context.CanLeaveTown("town-0"));
        Assert.True(context.BeginTownDungeon("story-0"));
        Assert.True(context.CompleteDungeon(true));
        Assert.True(context.CanLeaveTown("town-0"));
        Assert.Contains("Town gate key", context.Keys);
        Assert.Contains(CampaignContext.OpeningDayKey, context.Keys);
    }

    [Fact]
    public void ExistingCampaignsAndSandboxKeepTheirExistingEntryRules()
    {
        var legacy = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        Assert.False(legacy.IsPreparingForOpening);
        Assert.True(legacy.BeginTownDungeon("story-0"));
        var sandbox = new CampaignContext(new(OverworldLaunchMode.Sandbox, 42));
        sandbox.State.RequiresOpeningPreparation = true;
        Assert.False(sandbox.IsPreparingForOpening);
        Assert.True(sandbox.BeginTownDungeon("story-0"));
    }
}
