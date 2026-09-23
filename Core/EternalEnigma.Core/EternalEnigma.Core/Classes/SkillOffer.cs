namespace EternalEnigma.Core.Classes;

public sealed class SkillOffer
{
    public SkillOffer(string skillId, int tier, int maxRank, SkillKind kind, ClassSource source)
    {
        SkillId = skillId;
        Tier = tier;
        MaxRank = maxRank;
        Kind = kind;
        Source = source;
    }

    public string SkillId { get; }
    public int Tier { get; }
    public int MaxRank { get; }
    public SkillKind Kind { get; }
    public ClassSource Source { get; }
}
