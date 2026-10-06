using System;

public static class AttributeSpending
{
    public static bool TrySpend(Ally hero, HeroAttribute attribute)
    {
        if (hero == null || hero.IsSummon || hero.Vitals == null || hero.PendingAttributePoints <= 0 ||
            !Enum.IsDefined(typeof(HeroAttribute), attribute)) return false;
        var before = hero.FinalStats;
        int hp = before.HPMax, sp = before.SPMax;
        hero.Attributes = hero.Attributes.Added(attribute);
        hero.BaseStats = HeroStatRules.BaseStats(hero.StartingStats, hero.PrimaryClass, hero.Vitals.Level, hero.Attributes);
        hero.InvalidateCachedStats();
        hero.Vitals.HP += Math.Max(0, hero.FinalStats.HPMax - hp);
        hero.Vitals.SP += Math.Max(0, hero.FinalStats.SPMax - sp);
        hero.SyncDisplayedStats();
        return true;
    }

    public static bool TrySpend(TownAlly hero, HeroAttribute attribute)
    {
        if (hero == null || hero.PendingAttributePoints <= 0 || !Enum.IsDefined(typeof(HeroAttribute), attribute)) return false;
        var before = TownUtilityService.StatsFor(hero);
        hero.Attributes = hero.Attributes.Added(attribute);
        var after = TownUtilityService.StatsFor(hero);
        if (hero.Hp >= 0) hero.Hp += Math.Max(0, after.HPMax - before.HPMax);
        if (hero.Sp >= 0) hero.Sp += Math.Max(0, after.SPMax - before.SPMax);
        return true;
    }
}
