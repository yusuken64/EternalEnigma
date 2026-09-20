using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;

public sealed class RequirementTests
{
    [Fact]
    public void AlternativesAreOrAndEachSolutionIsAnd()
    {
        var requirement = new Requirement(CapabilitySet.Of(Capability.Breach), CapabilitySet.Of(Capability.Boat, Capability.HazardWard));
        Assert.True(requirement.IsSatisfiedBy(CapabilitySet.Of(Capability.Breach)));
        Assert.True(requirement.IsSatisfiedBy(CapabilitySet.Of(Capability.Boat, Capability.HazardWard)));
        Assert.False(requirement.IsSatisfiedBy(CapabilitySet.Of(Capability.Boat)));
        Assert.False(requirement.IsSatisfiedBy(CapabilitySet.Empty));
    }

    [Fact]
    public void DuplicateAndDominatedSolutionsNormalizeToOne()
    {
        var requirement = new Requirement(CapabilitySet.Of(Capability.Breach), CapabilitySet.Of(Capability.Breach),
            CapabilitySet.Of(Capability.Breach, Capability.Engineering));
        Assert.Single(requirement.Alternatives);
        Assert.Equal("Breach", requirement.ToString());
        Assert.True(new Requirement(CapabilitySet.Empty, CapabilitySet.Of(Capability.Breach)).IsOpen);
    }

    [Fact]
    public void InvalidVocabularyAndOversizedSolutionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CapabilitySet.Of((Capability)25));
        Assert.Throws<ArgumentException>(() => new Requirement());
        Assert.Throws<ArgumentException>(() => new Requirement(CapabilitySet.Of(Capability.Climb, Capability.Boat, Capability.Engineering)));
    }
}
