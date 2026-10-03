using System;
using UnityEngine;

public static class RecoveryMath
{
    public static int Heal(int baseHeal, float perLevel, int level, SkillRankContext rank, float multiplier)
    {
        int value = Mathf.FloorToInt(baseHeal + perLevel * Math.Max(1,level));
        if (rank.Scaling != null) value = rank.Scaling.ScalePower(value,rank.Rank);
        return Math.Max(1,Mathf.RoundToInt(value * multiplier));
    }
    public static int SavedVital(int value, int maximum) => value < 0 ? maximum : Mathf.Clamp(value,0,maximum);
    public static int Restore(int value, int amount, int maximum) => Math.Min(maximum, value + Math.Max(0,amount));
}
