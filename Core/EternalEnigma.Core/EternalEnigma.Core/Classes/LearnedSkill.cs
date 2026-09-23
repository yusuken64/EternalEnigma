namespace EternalEnigma.Core.Classes;

public readonly struct LearnedSkill : IEquatable<LearnedSkill>
{
    public LearnedSkill(string skillId, int rank)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            throw new ArgumentException("Skill id is required.", nameof(skillId));

        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank));

        SkillId = skillId;
        Rank = rank;
    }

    public string SkillId { get; }
    public int Rank { get; }

    public bool Equals(LearnedSkill other)
    {
        return string.Equals(SkillId, other.SkillId, StringComparison.Ordinal) && Rank == other.Rank;
    }

    public override bool Equals(object? obj) => obj is LearnedSkill other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            SkillId == null ? 0 : StringComparer.Ordinal.GetHashCode(SkillId),
            Rank);
    }

    public override string ToString() => $"{SkillId} {Rank}";
}
