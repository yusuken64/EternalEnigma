using EternalEnigma.Core.Classes;
using Xunit;
using static EternalEnigma.Core.Tests.Classes.ClassTestData;

namespace EternalEnigma.Core.Tests.Classes;

public sealed class SkillTreeFormatTests
{
    private const string Sample = @"
# a comment
skilltree 1
class warrior
name Warrior

tier 1
  mastery   | Novice Training
  gathering | Mining
  skill     | Double Strike | 5
tier 2
  mastery | Adept Training
  single  | Initiative
tier 3
  mastery | Master Training
";

    [Fact]
    public void ParsesEveryEntryKind()
    {
        var doc = SkillTreeFormat.Parse(Sample);

        Assert.Equal("warrior", doc.ClassId);
        Assert.Equal("Warrior", doc.DisplayName);
        Assert.Equal(6, doc.Entries.Count);
        Assert.Equal(SkillKind.Normal, doc.Find("Double Strike")!.Kind);
        Assert.Equal(5, doc.Find("Double Strike")!.MaxRank);
        Assert.Equal(SkillKind.SingleRank, doc.Find("Initiative")!.Kind);
        Assert.Equal(2, doc.Find("Initiative")!.Tier);
        Assert.Empty(doc.Validate());
    }

    [Fact]
    public void WriteIsCanonicalAndRoundTrips()
    {
        var doc = SkillTreeFormat.Parse(Sample);
        string text = SkillTreeFormat.Write(doc);
        var again = SkillTreeFormat.Parse(text);

        Assert.Equal(text, SkillTreeFormat.Write(again));
        Assert.Equal(doc.Entries.Select(e => e.ToString()), again.Entries.Select(e => e.ToString()));
    }

    [Fact]
    public void RoundTripsAClassTable()
    {
        foreach (var table in new[] { Warrior(), Scout(), Guardian() })
        {
            var doc = SkillTreeDocument.FromTable(table);
            var back = SkillTreeFormat.Parse(SkillTreeFormat.Write(doc)).ToTable();

            Assert.Equal(table.ClassId, back.ClassId);
            Assert.Equal(
                table.Skills.OrderBy(s => s.Tier).Select(s => s.ToString()),
                back.Skills.Select(s => s.ToString()));
        }
    }

    [Fact]
    public void NamesMayContainSpacesHashesAndPunctuation()
    {
        var doc = new SkillTreeDocument("x");
        doc.Set("Follow-up #2 (Mastery)", 1, SkillKind.Normal, 3);

        var back = SkillTreeFormat.Parse(SkillTreeFormat.Write(doc));

        Assert.Equal(3, back.Find("Follow-up #2 (Mastery)")!.MaxRank);
    }

    [Theory]
    [InlineData("class x\n", "skilltree")]
    [InlineData("skilltree 2\nclass x\n", "version")]
    [InlineData("skilltree 1\n", "missing 'class'")]
    [InlineData("skilltree 1\nclass x\nskill | A | 1\n", "before any 'tier'")]
    [InlineData("skilltree 1\nclass x\ntier 4\n", "tier must be")]
    [InlineData("skilltree 1\nclass x\ntier 1\nskill | A\n", "need a rank")]
    [InlineData("skilltree 1\nclass x\ntier 1\nskill | A | 9\n", "line 4")]
    [InlineData("skilltree 1\nclass x\ntier 1\nmastery | A | 3\n", "line 4")]
    [InlineData("skilltree 1\nclass x\ntier 1\nspell | A | 1\n", "unknown entry kind")]
    [InlineData("skilltree 1\nclass x\ntier 1\nskill | A | 1\nskill | A | 2\n", "duplicate skill")]
    [InlineData("skilltree 1\nclass x\nbogus\n", "unrecognized")]
    public void RejectsBadInputWithAMessage(string text, string expected)
    {
        var ex = Assert.Throws<SkillTreeFormatException>(() => SkillTreeFormat.Parse(text));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void EditsSetAndUnsetSkillsInPlace()
    {
        var doc = SkillTreeFormat.Parse(Sample);

        Assert.True(doc.Set("Cleave", 2, SkillKind.Normal, 5));
        Assert.False(doc.Set("Cleave", 2, SkillKind.Normal, 4));
        Assert.Equal(4, doc.Find("Cleave")!.MaxRank);

        doc.MoveToTier("Cleave", 3);
        Assert.Equal(3, doc.Find("Cleave")!.Tier);

        doc.SetKind("Cleave", SkillKind.SingleRank);
        Assert.Equal(1, doc.Find("Cleave")!.MaxRank);

        Assert.True(doc.Remove("Cleave"));
        Assert.False(doc.Remove("Cleave"));
        Assert.Null(doc.Find("Cleave"));
    }

    [Fact]
    public void ReplacingASkillKeepsItsPosition()
    {
        var doc = SkillTreeFormat.Parse(Sample);
        int before = doc.Entries.ToList().FindIndex(e => e.SkillId == "Mining");

        doc.Set("Mining", 1, SkillKind.Gathering, 1);

        Assert.Equal(before, doc.Entries.ToList().FindIndex(e => e.SkillId == "Mining"));
    }

    [Fact]
    public void InvalidEditsThrowAndLeaveTheTreeUnchanged()
    {
        var doc = SkillTreeFormat.Parse(Sample);
        string before = SkillTreeFormat.Write(doc);

        Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetMaxRank("Double Strike", 6));
        Assert.Throws<ArgumentException>(() => doc.SetMaxRank("Mining", 3));
        Assert.Throws<ArgumentException>(() => doc.MoveToTier("Nope", 2));
        Assert.Throws<ArgumentException>(() => doc.Set("a|b", 1, SkillKind.Normal, 1));

        Assert.Equal(before, SkillTreeFormat.Write(doc));
    }

    [Fact]
    public void ValidateReportsMissingMasteries()
    {
        var doc = new SkillTreeDocument("x");
        doc.Set("Strike", 1, SkillKind.Normal, 5);

        var errors = doc.Validate();

        Assert.Contains(errors, e => e.Contains("missing tier 1 mastery"));
    }

    [Fact]
    public void CloneIsIndependent()
    {
        var doc = SkillTreeFormat.Parse(Sample);
        var copy = doc.Clone();

        copy.Remove("Mining");

        Assert.NotNull(doc.Find("Mining"));
    }
}
