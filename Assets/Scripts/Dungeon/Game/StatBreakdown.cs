using System;
using System.Collections.Generic;
using System.Linq;

public static class StatBreakdown
{
    private static float Value(Stats stats,string key) => key switch {
        "HP"=>stats.HPMax,"Mana"=>stats.ManaMax,"Food"=>stats.HungerMax,
        "Attack"=>stats.Strength,"Magic Power"=>stats.MagicPower,"Defense"=>stats.Defense,
        "Hit"=>stats.HitBonus,"Evasion"=>stats.Evasion,"Crit"=>stats.CritChance,
        "Actions"=>stats.ActionsPerTurnMax,"Attacks"=>stats.AttacksPerTurnMax,
        "Fire"=>stats.FireResistance,"Ice"=>stats.IceResistance,"Lightning"=>stats.LightningResistance,
        _=>0
    };
    private static float Value(StartingStats stats,string key) => stats==null?0:key switch {
        "HP"=>stats.HPMax,"Mana"=>stats.ManaMax,"Food"=>stats.HungerMax,
        "Attack"=>stats.Strength,"Magic Power"=>stats.MagicPower,"Defense"=>stats.Defense,
        "Hit"=>stats.HitBonus,"Evasion"=>stats.Evasion,"Crit"=>stats.CritChance,
        "Actions"=>stats.ActionsPerTurnMax,"Attacks"=>stats.AttacksPerTurnMax,
        "Fire"=>stats.FireResistance,"Ice"=>stats.IceResistance,"Lightning"=>stats.LightningResistance,
        _=>0
    };
    public static string Describe(Ally hero,string key) => For(hero)(key);

    internal static Func<string,string> For(Ally hero)
    {
        var template=HeroStatRules.BaseStats(hero.StartingStats,null,1,default);
        var classStart=hero.PrimaryClass?.StartingStatBonus;
        var growth=HeroClass.Growth(hero.PrimaryClass);
        var attribute=HeroAttributes.ToModification(hero.Attributes);
        int levels=Math.Max(0,hero.Vitals.Level-1);
        var parts=StatPreview.Parts(hero,hero.Equipment.Current()).ToArray();
        var final=hero.FinalStats;
        var baseStats=hero.BaseStats;
        return key =>
        {
            float built=Value(template,key)+Value(classStart,key)+Value(growth,key)*levels+Value(attribute,key);
            var contributions=new List<(string,float)> {
                ("Base",Value(template,key)),("Class",Value(classStart,key)),
                ("Level growth",Value(growth,key)*levels),("Attributes",Value(attribute,key))};
            float extra=Value(baseStats,key)-built;
            if(Math.Abs(extra)>.0001f)contributions.Add(("Other bonuses",extra));
            contributions.AddRange(parts.Select(p=>(p.Source,Value(p.Value,key))));
            return Format(key,Value(final,key),contributions);
        };
    }
    public static string Describe(TownAlly hero,string key) => For(hero,TownUtilityService.StatsFor(hero))(key);

    internal static Func<string,string> For(TownAlly hero,Stats final)
    {
        var template=GamePresentationProfile.Current?.AllyTemplate?.StartingStats;
        var baseStats=HeroStatRules.BaseStats(template,hero.PrimaryClass,hero.Level,hero.Attributes);
        var starting=HeroStatRules.BaseStats(template,null,1,default);
        var classStart=hero.PrimaryClass?.StartingStatBonus;
        var growth=HeroClass.Growth(hero.PrimaryClass);
        int levels=Math.Max(0,hero.Level-1);
        var attribute=HeroAttributes.ToModification(hero.Attributes);
        var gear=hero.Equipment.Current().Modification;
        return key => Format(key,Value(final,key),new (string,float)[] {
            ("Base",Value(starting,key)),("Class",Value(classStart,key)),
            ("Level growth",Value(growth,key)*levels),("Attributes",Value(attribute,key)),
            ("Equipment",Value(gear,key)),("Passives and conditions",Value(final,key)-Value(baseStats,key)-Value(gear,key))});
    }
    private static string Format(string key,float total,IEnumerable<(string Source,float Amount)> sources)
    {
        bool percent=key=="Hit" || key=="Evasion" || key=="Crit";
        string F(float value)=>percent?$"{value*100:0.#}%":$"{value:0.#}";
        string prefix=key=="Hit"?"Base hit 80%, ":"";
        string parts=string.Join(", ",sources.Where(s=>Math.Abs(s.Amount)>.0001f).Select(s=>s.Source+" "+(s.Amount>0?"+":"")+F(s.Amount)));
        float shown=key=="Hit"?Math.Max(.05f,Math.Min(1f,total+.8f)):total;
        return $"{key} {F(shown)}\n{prefix}{parts}";
    }
}
