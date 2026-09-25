using System;
using UnityEngine;

// Default per-rank growth for skill effects. Rank 1 always returns the authored value.
[Serializable]
public class SkillRankScaling
{
	public float PowerPercentPerRank = 15f;
	public float ChancePointsPerRank = 5f;
	public int BuffStepPerRank = 1;
	public bool DurationBonusAtRanks3And5 = true;

	public SkillRankScaling() { }

	public SkillRankScaling(SkillRankScaling other)
	{
		PowerPercentPerRank = other.PowerPercentPerRank;
		ChancePointsPerRank = other.ChancePointsPerRank;
		BuffStepPerRank = other.BuffStepPerRank;
		DurationBonusAtRanks3And5 = other.DurationBonusAtRanks3And5;
	}

	public int ScalePower(int baseValue, int rank)
	{
		int extra = Math.Max(0, rank - 1);
		return (int)Math.Round(baseValue * (1f + PowerPercentPerRank / 100f * extra), MidpointRounding.AwayFromZero);
	}

	public float ScaleChance(float baseChance, int rank)
	{
		int extra = Math.Max(0, rank - 1);
		return Mathf.Clamp01(baseChance + ChancePointsPerRank / 100f * extra);
	}

	public int ScaleBuff(int baseValue, int rank)
	{
		if (baseValue == 0) return 0;
		int extra = Math.Max(0, rank - 1);
		return baseValue + Math.Sign(baseValue) * BuffStepPerRank * extra;
	}

	public int ScaleDuration(int baseTurns, int rank)
	{
		if (!DurationBonusAtRanks3And5) return baseTurns;
		return baseTurns + (rank >= 3 ? 1 : 0) + (rank >= 5 ? 1 : 0);
	}
}
