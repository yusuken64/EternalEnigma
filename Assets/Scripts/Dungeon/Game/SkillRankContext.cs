// The caster's rank for one skill cast, passed to each effect when a skill is targeted.
public readonly struct SkillRankContext
{
	public SkillRankContext(int rank, SkillRankScaling scaling)
	{
		Rank = rank < 1 ? 1 : rank;
		Scaling = scaling ?? new SkillRankScaling();
	}

	public int Rank { get; }
	public SkillRankScaling Scaling { get; }

	public static SkillRankContext Unranked => new(1, new SkillRankScaling());
}
