namespace EternalEnigma.Core.Classes;

public sealed class ClassSkillEntry
{
    public ClassSkillEntry(string skillId, int tier, int maxRank, SkillKind kind)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            throw new ArgumentException("Skill id is required.", nameof(skillId));

        if (tier < 1 || tier > 3)
            throw new ArgumentOutOfRangeException(nameof(tier));

        if (maxRank < 1 || maxRank > 5)
            throw new ArgumentOutOfRangeException(nameof(maxRank));

        if (!Enum.IsDefined(typeof(SkillKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (kind != SkillKind.Normal && maxRank != 1)
            throw new ArgumentException("Only normal skills can have more than one rank.", nameof(maxRank));

        SkillId = skillId;
        Tier = tier;
        MaxRank = maxRank;
        Kind = kind;
    }

    public string SkillId { get; }
    public int Tier { get; }
    public int MaxRank { get; }
    public SkillKind Kind { get; }

    public override string ToString() => $"{SkillId} (tier {Tier}, max rank {MaxRank}, {Kind})";
}
