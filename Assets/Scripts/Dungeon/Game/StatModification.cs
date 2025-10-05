using System;
using System.Collections.Generic;

[Serializable]
public class StatModification : StartingStats
{
    public StatModification() { }

    public StatModification(Stats other)
    {
        HPMax = other.HPMax;
        HungerMax = other.HungerMax;
        Strength = other.Strength;
        Defense = other.Defense;
        EXPOnKill = other.EXPOnKill;
        HungerAccumulateThreshold = other.HungerAccumulateThreshold;
        HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
        SPRegenAcccumlateThreshold = other.SPRegenAcccumlateThreshold;
        DropRate = other.DropRate;
        ActionsPerTurnMax = other.ActionsPerTurnMax;
        AttacksPerTurnMax = other.AttacksPerTurnMax;
    }

    public StatModification(StatModification stats)
    {
        HPMax = stats.HPMax;
        HungerMax = stats.HungerMax;
        Strength = stats.Strength;
        Defense = stats.Defense;
        EXPOnKill = stats.EXPOnKill;
        HungerAccumulateThreshold = stats.HungerAccumulateThreshold;
        HPRegenAcccumlateThreshold = stats.HPRegenAcccumlateThreshold;
        SPRegenAcccumlateThreshold = stats.SPRegenAcccumlateThreshold;
        DropRate = stats.DropRate;
        ActionsPerTurnMax = stats.ActionsPerTurnMax;
        AttacksPerTurnMax = stats.AttacksPerTurnMax;
    }

    public static StatModification operator +(StatModification a, StatModification b)
    {
        a ??= new StatModification();
        b ??= new StatModification();

        var result = new StatModification(a);

        result.HPMax += b.HPMax;
        result.HungerMax += b.HungerMax;
        result.Strength += b.Strength;
        result.Defense += b.Defense;
        result.EXPOnKill += b.EXPOnKill;
        result.HungerAccumulateThreshold += b.HungerAccumulateThreshold;
        result.HPRegenAcccumlateThreshold += b.HPRegenAcccumlateThreshold;
        result.SPRegenAcccumlateThreshold += b.SPRegenAcccumlateThreshold;
        result.DropRate += b.DropRate;
        result.ActionsPerTurnMax += b.ActionsPerTurnMax;
        result.AttacksPerTurnMax += b.AttacksPerTurnMax;

        return result;
    }

    public List<string> DescribeEffect()
    {
        var parts = new List<string>();

        if (HPMax != 0) parts.Add($"{(HPMax > 0 ? "+" : "")}{HPMax} HP Max");
        if (HungerMax != 0) parts.Add($"{(HungerMax > 0 ? "+" : "")}{HungerMax} Hunger Max");
        if (Strength != 0) parts.Add($"{(Strength > 0 ? "+" : "")}{Strength} Strength");
        if (Defense != 0) parts.Add($"{(Defense > 0 ? "+" : "")}{Defense} Defense");
        if (EXPOnKill != 0) parts.Add($"{(EXPOnKill > 0 ? "+" : "")}{EXPOnKill} EXP on Kill");
        if (HungerAccumulateThreshold != 0) parts.Add($"{(HungerAccumulateThreshold > 0 ? "+" : "")}{HungerAccumulateThreshold} Hunger Threshold");
        if (HPRegenAcccumlateThreshold != 0) parts.Add($"{(HPRegenAcccumlateThreshold > 0 ? "+" : "")}{HPRegenAcccumlateThreshold} HP Regen Threshold");
        if (SPRegenAcccumlateThreshold != 0) parts.Add($"{(SPRegenAcccumlateThreshold > 0 ? "+" : "")}{SPRegenAcccumlateThreshold} SP Regen Threshold");
        if (DropRate != 0) parts.Add($"{(DropRate > 0 ? "+" : "")}{DropRate} Drop Rate");
        if (ActionsPerTurnMax != 0) parts.Add($"{(ActionsPerTurnMax > 0 ? "+" : "")}{ActionsPerTurnMax} Actions / Turn");
        if (AttacksPerTurnMax != 0) parts.Add($"{(AttacksPerTurnMax > 0 ? "+" : "")}{AttacksPerTurnMax} Attacks / Turn");

        return parts;
    }
}