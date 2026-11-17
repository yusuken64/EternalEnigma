using NUnit.Framework;

public sealed class PartyExperienceShareTests
{
	[TestCase(10, 1, 10)]
	[TestCase(10, 2, 5)]
	[TestCase(10, 3, 4)]
	[TestCase(7, 4, 2)]
	[TestCase(1, 4, 1)]
	public void SplitsFlatAndRoundsUp(int pool, int witnesses, int expected) =>
		Assert.AreEqual(expected, PartyRules.ExperienceShare(pool, witnesses));

	[TestCase(0, 3)]
	[TestCase(-5, 3)]
	[TestCase(10, 0)]
	public void NoPoolOrNoWitnessesAwardsNothing(int pool, int witnesses) =>
		Assert.AreEqual(0, PartyRules.ExperienceShare(pool, witnesses));
}
