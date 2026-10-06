using System;

public enum HeroAttribute { Str, Int, Agi }

[Serializable]
public struct AttributePoints
{
    public int Str, Int, Agi;
    public int Total => Math.Max(0, Str) + Math.Max(0, Int) + Math.Max(0, Agi);
    public int Get(HeroAttribute attribute) => attribute == HeroAttribute.Str ? Str : attribute == HeroAttribute.Int ? Int : Agi;
    public AttributePoints Added(HeroAttribute attribute)
    {
        var result = this;
        switch (attribute)
        {
            case HeroAttribute.Str: result.Str++; break;
            case HeroAttribute.Int: result.Int++; break;
            case HeroAttribute.Agi: result.Agi++; break;
        }
        return result;
    }
}

public static class HeroAttributes
{
    public static int Pending(int level, AttributePoints spent) => Math.Max(0, Math.Max(1, level) - 1 - spent.Total);
    public static StatModification ToModification(AttributePoints points)
    {
        int strength = Math.Max(0, points.Str), intellect = Math.Max(0, points.Int), agility = Math.Max(0, points.Agi);
        return new StatModification {
            Strength = strength, HPMax = strength * 3, Defense = strength / 4,
            SPMax = intellect * 2, MagicPower = intellect,
            SPRegenAcccumlateThreshold = -Math.Min(400, intellect * 15),
            HitBonus = agility * .01f, Evasion = Math.Min(25, agility) * .01f,
            CritChance = Math.Min(25, agility) * .01f, HungerMax = agility * 2
        };
    }
    public static float MagicMultiplier(int power) => Math.Max(0f, 1f + .05f * power);
}

public static class HeroStatRules
{
    public static Stats BaseStats(StartingStats template, ClassDefinition primary, int level, AttributePoints points)
    {
        var stats = new Stats();
        if (template != null) stats.FromStartingStats(template);
        stats += primary?.StartingStatBonus;
        HeroClass.ApplyLevelGrowth(stats, primary, Math.Max(1, level) - 1);
        return stats + HeroAttributes.ToModification(points);
    }
}
