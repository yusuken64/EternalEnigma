using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;
public sealed class LockInteractionTests
{
    private static CampaignRoute Route(params CapabilitySet[] alternatives) => new("gate", "a", "b", new Requirement(alternatives), LockForm.Obstacle);
    private static CapabilityLockAction Use(Capability c) => new(c);

    [Theory]
    [InlineData(Capability.Climb, Capability.Grapple)]
    [InlineData(Capability.Grapple, Capability.Climb)]
    public void PairInEitherOrderRetainsProgressThroughWrongAndRepeatedAttempts(Capability first, Capability second)
    {
        var evaluator = new LockSolutionEvaluator(Route(CapabilitySet.Of(first, second)));
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(Use(Capability.Boat)));
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(new KeyLockAction("wrong")));
        Assert.Equal(LockOutcome.Progress, evaluator.Apply(Use(first)));
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(Use(first)));
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(Use(Capability.Boat)));
        Assert.Equal(LockOutcome.Opened, evaluator.Apply(Use(second)));
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(Use(second)));
    }

    [Theory]
    [InlineData(Capability.Climb)]
    [InlineData(Capability.Boat)]
    public void EitherSingleAlternativeOpens(Capability chosen)
    {
        var evaluator = new LockSolutionEvaluator(Route(CapabilitySet.Of(Capability.Climb), CapabilitySet.Of(Capability.Boat)));
        Assert.Equal(LockOutcome.Opened, evaluator.Apply(Use(chosen)));
    }

    [Fact]
    public void IncompatiblePartialAlternativesCannotCombine()
    {
        var evaluator = new LockSolutionEvaluator(Route(CapabilitySet.Of(Capability.Climb, Capability.Grapple), CapabilitySet.Of(Capability.Boat, Capability.Breach)));
        Assert.Equal(LockOutcome.Progress, evaluator.Apply(Use(Capability.Climb)));
        Assert.Equal(LockOutcome.Progress, evaluator.Apply(Use(Capability.Boat)));
        Assert.Equal(LockOutcome.Opened, evaluator.Apply(Use(Capability.Breach)));
    }

    [Fact]
    public void CorrectKeyOpensWithoutConsumingIt()
    {
        var route = new CampaignRoute("gate", "a", "b", Requirement.Open, LockForm.Interaction, shortcutKind: ShortcutKind.Keyed, keyId: "key");
        var evaluator = new LockSolutionEvaluator(route);
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(Use(Capability.Climb)));
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(new KeyLockAction("wrong")));
        Assert.Equal(LockOutcome.Opened, evaluator.Apply(new KeyLockAction("key")));
        Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(new KeyLockAction("key")));
    }

    [Fact]
    public void ClosingAndReopeningDiscardsProgressAndUnavailableActionsDoNotContribute()
    {
        var route = Route(CapabilitySet.Of(Capability.Climb, Capability.Grapple));
        bool available = false;
        int commits = 0;
        LockInteractionSession Session() => new(route, () => true, _ => available, () => { commits++; return true; });
        using var first = Session();
        Assert.Equal(LockOutcome.NoEffect, first.Attempt(Use(Capability.Climb)));
        available = true;
        Assert.Equal(LockOutcome.Progress, first.Attempt(Use(Capability.Climb)));
        first.Dispose();
        Assert.Equal(LockOutcome.NoEffect, first.Attempt(Use(Capability.Grapple)));
        using var second = Session();
        Assert.Equal(LockOutcome.Progress, second.Attempt(Use(Capability.Grapple)));
        Assert.Equal(LockOutcome.Opened, second.Attempt(Use(Capability.Climb)));
        Assert.Equal(LockOutcome.NoEffect, second.Attempt(Use(Capability.Climb)));
        Assert.Equal(1, commits);
    }

    [Fact]
    public void StaleTargetClosesWithoutCommittingAndReentrantSubmissionIsIgnored()
    {
        bool valid = true;
        int commits = 0;
        var route = Route(CapabilitySet.Of(Capability.Climb));
        using var stale = new LockInteractionSession(route, () => valid, _ => true, () => { commits++; return true; });
        valid = false;
        Assert.Equal(LockOutcome.NoEffect, stale.Attempt(Use(Capability.Climb)));
        Assert.True(stale.Closed);
        LockInteractionSession? session = null;
        session = new(route, () => true, _ => { Assert.Equal(LockOutcome.NoEffect, session!.Attempt(Use(Capability.Climb))); return true; }, () => { commits++; return true; });
        Assert.Equal(LockOutcome.Opened, session.Attempt(Use(Capability.Climb)));
        Assert.Equal(1, commits);
    }
}
