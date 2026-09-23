using System;
using System.Collections.Generic;

[Serializable]
public class StatModification : StartingStats
{
    public StatModification() { }

    public StatModification(Stats other)
    {
        HPMax = other.HPMax;
        SPMax = other.SPMax;
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
        FireResistance = other.FireResistance;
        IceResistance = other.IceResistance;
        LightningResistance = other.LightningResistance;
        CritChance = other.CritChance;
        Evasion = other.Evasion;
        HitBonus = other.HitBonus;
    }

    public StatModification(StatModification stats)
    {
        HPMax = stats.HPMax;
        HungerMax = stats.HungerMax;
        SPMax = stats.SPMax;
        Strength = stats.Strength;
        Defense = stats.Defense;
        EXPOnKill = stats.EXPOnKill;
        HungerAccumulateThreshold = stats.HungerAccumulateThreshold;
        HPRegenAcccumlateThreshold = stats.HPRegenAcccumlateThreshold;
        SPRegenAcccumlateThreshold = stats.SPRegenAcccumlateThreshold;
        DropRate = stats.DropRate;
        ActionsPerTurnMax = stats.ActionsPerTurnMax;
        AttacksPerTurnMax = stats.AttacksPerTurnMax;
        FireResistance = stats.FireResistance;
        IceResistance = stats.IceResistance;
        LightningResistance = stats.LightningResistance;
        CritChance = stats.CritChance;
        Evasion = stats.Evasion;
        HitBonus = stats.HitBonus;
    }

    public static StatModification operator +(StatModification a, StatModification b)
    {
        a ??= new StatModification();
        b ??= new StatModification();

        var result = new StatModification(a);

        result.HPMax += b.HPMax;
        result.HungerMax += b.HungerMax;
        result.SPMax += b.SPMax;
        result.Strength += b.Strength;
        result.Defense += b.Defense;
        result.EXPOnKill += b.EXPOnKill;
        result.HungerAccumulateThreshold += b.HungerAccumulateThreshold;
        result.HPRegenAcccumlateThreshold += b.HPRegenAcccumlateThreshold;
        result.SPRegenAcccumlateThreshold += b.SPRegenAcccumlateThreshold;
        result.DropRate += b.DropRate;
        result.ActionsPerTurnMax += b.ActionsPerTurnMax;
        result.AttacksPerTurnMax += b.AttacksPerTurnMax;
        result.FireResistance += b.FireResistance;
        result.IceResistance += b.IceResistance;
        result.LightningResistance += b.LightningResistance;
        result.CritChance += b.CritChance;
        result.Evasion += b.Evasion;
        result.HitBonus += b.HitBonus;

        return result;
    }

    public List<string> DescribeEffect()
    {
        var parts = new List<string>();

        if (HPMax != 0) parts.Add($"{(HPMax > 0 ? "+" : "")}{HPMax} HP Max");
        if (SPMax != 0) parts.Add($"{(SPMax > 0 ? "+" : "")}{SPMax} SP Max");
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
        if (FireResistance != 0) parts.Add($"{(FireResistance > 0 ? "+" : "")}{FireResistance} Fire Resistance");
        if (IceResistance != 0) parts.Add($"{(IceResistance > 0 ? "+" : "")}{IceResistance} Ice Resistance");
        if (LightningResistance != 0) parts.Add($"{(LightningResistance > 0 ? "+" : "")}{LightningResistance} Lightning Resistance");
        if (CritChance != 0) parts.Add($"{(CritChance > 0 ? "+" : "")}{CritChance * 100:0}% Crit");
        if (Evasion != 0) parts.Add($"{(Evasion > 0 ? "+" : "")}{Evasion * 100:0}% Evasion");
        if (HitBonus != 0) parts.Add($"{(HitBonus > 0 ? "+" : "")}{HitBonus * 100:0}% Hit");

        return parts;
    }
}