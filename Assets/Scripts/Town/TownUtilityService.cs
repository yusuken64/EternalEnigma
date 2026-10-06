using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Town-only whitelist. Validation and target resolution precede every mutation.</summary>
public sealed class TownUtilityService
{
    private readonly IReadOnlyList<TownAlly> party;
    private readonly List<InventoryItem> bag;
    private readonly Action persist;
    private bool committing;
    public TownUtilityService(IReadOnlyList<TownAlly> party, List<InventoryItem> bag, Action persist)
    { this.party=party; this.bag=bag; this.persist=persist; }

    public static Stats StatsFor(TownAlly hero) => StatsFor(hero,hero.Equipment.Current());
    public static Stats StatsFor(TownAlly hero, Loadout loadout)
    {
        var template = GamePresentationProfile.Current?.AllyTemplate;
        var stats = HeroStatRules.BaseStats(template != null ? template.StartingStats : hero.BaseStats == null ? null : new StatModification(hero.BaseStats), hero.PrimaryClass, hero.Level, hero.Attributes);
        int baseMaximum=stats.HPMax;
        stats += loadout.Modification;
        foreach (var name in hero.Skills ?? new())
        {
            var skill=Common.Instance.SkillManager.GetSkillByName(name);
            if(skill?.ActivationType == ActivationType.Passive)
            {
                stats += StatScaling.Scale(skill.PassiveStatModification,skill.RankScaling,Math.Max(1,hero.GetRank(name)));
                foreach(var conditional in skill.PassiveResponses?.OfType<ConditionalStatPassive>() ?? Enumerable.Empty<ConditionalStatPassive>())
                {
                    bool active=conditional.Condition switch {
                        StatCondition.ShieldEquipped => loadout.OffHand?.EquipmentItemDefinition?.WeaponType==WeaponType.OffhandShield,
                        StatCondition.BowEquipped => loadout.Weapon?.EquipmentItemDefinition?.WeaponType==WeaponType.BowAndArrow,
                        StatCondition.HpBelowFraction => RecoveryMath.SavedVital(hero.Hp,baseMaximum)<=conditional.HpFraction*baseMaximum,
                        _ => false
                    };
                    if(active)stats+=StatScaling.Scale(conditional.Bonus,skill.RankScaling,Math.Max(1,hero.GetRank(name)));
                }
            }
        }
        return stats;
    }

    public sealed class Utility
    {
        public Skill Skill;
        public InventoryItem Item;
        public SkillTargeting Targeting;
        public TargetSelector Selector;
        public InventoryTargetSelector InventorySelector;
        public List<GameAction> Effects;
        public int Hp, Sp, Cost;
        public SkillRankContext Rank;
    }
    public bool Describe(TownAlly caster, Skill skill, InventoryItem item, out Utility utility, out string reason)
    {
        utility=null;
        reason="Only HP/SP recovery and inventory inspection can be used in town.";
        if(caster==null || !party.Contains(caster)) return false;
        var u=new Utility {Skill=skill,Item=item,Rank=SkillRankContext.Unranked};
        if(skill!=null)
        {
            if(skill.ActivationType!=ActivationType.Active) {reason="Passive skill: always applied; no Use action.";return false;}
            if(caster.GetRank(skill.SkillName)==0) {reason="Skill not learned.";return false;}
            if(skill.SPCost<0 || skill.UsesArrows) {reason="This skill requires dungeon combat resources.";return false;}
            u.Cost=skill.SPCost;u.Targeting=skill.Targeting;u.Selector=skill.TargetSelector;u.InventorySelector=skill.InventoryTargetSelector;
            u.Effects=skill.ActionEffects;u.Rank=new SkillRankContext(caster.GetRank(skill.SkillName),skill.RankScaling);
        }
        else if(item?.ItemDefinition!=null)
        {
            if(!bag.Contains(item) || item.HasStacks && item.StackIsEmpty()) {reason="This item is no longer available.";return false;}
            if(item.ItemDefinition.IsFood) return false;
            var usable=item.ItemDefinition as UsableItemDefinition;
            u.Targeting=usable?.Targeting ?? SkillTargeting.Self;
            u.Selector=usable?.TargetSelector ?? new TargetSelector { Team=TargetTeam.Self,Area=TargetArea.Self };
            u.InventorySelector=usable?.InventoryTargetSelector;
            if(u.Targeting==SkillTargeting.InventoryItem)u.Effects=usable.InventoryEffects;
            else if(item.ItemDefinition.ItemEffectDefinition is ModifyStatsItemEffectDefinition restore)
            {
                var v=restore.VitalModification;
                // Explicitly reject every non-recovery serialized field, including future fields.
                if(restore.DoDamageAnimation || !IsZero(restore.StatModification) || v==null || v.Hp<0 || v.Sp<0 ||
                    v.Hp+v.Sp<=0 || !IsZero(v,"Hp","Sp"))return false;
                u.Hp=v.Hp;u.Sp=v.Sp;
            }
            else if(item.ItemDefinition.ItemEffectDefinition is SkillItemEffectDefinition effect && effect.EffectSkill!=null)
            {
                u.Effects=effect.EffectSkill.ActionEffects;
                u.Rank=effect.EffectSkill.RankContext;
            }
            else return false;
        }
        else return false;
        if(u.Targeting==SkillTargeting.Missile || u.Selector?.Team==TargetTeam.Enemies || u.Selector?.Area==TargetArea.Custom)return false;
        if(u.Targeting==SkillTargeting.InventoryItem)
        {
            if(u.InventorySelector==null || u.Effects==null || u.Effects.Count==0 || u.Effects.Any(e=>e is not InspectInventoryItemEffect))return false;
        }
        else if(u.Effects!=null && (u.Effects.Count==0 || u.Effects.Any(e=>e is not ScaledHealAction && e is not TakeHealAction && e is not RestoreSPAction) ||
            u.Effects.Any(e=>e is ScaledHealAction h && (h.BaseHeal<0 || h.PerLevel<0 || h.BaseHeal+h.PerLevel<=0) ||
                e is TakeHealAction direct && direct.healing<=0 || e is RestoreSPAction s && s.Amount<=0)))return false;
        if(u.Targeting!=SkillTargeting.InventoryItem && u.Selector==null)return false;
        utility=u;
        var stats=StatsFor(caster);
        if(RecoveryMath.SavedVital(caster.Hp,stats.HPMax)<=0) {reason="A downed hero cannot use this. Revival is unavailable in town.";return false;}
        if(RecoveryMath.SavedVital(caster.Sp,stats.SPMax)<u.Cost) {reason="Not enough SP.";return false;}
        if(u.Targeting==SkillTargeting.InventoryItem ? InventoryTargets(caster,u).Count==0 : Targets(caster,u).Count==0)
        {reason="No eligible target needs recovery.";return false;}
        reason=null;return true;
    }
    private static bool IsZero(object value, params string[] ignore) => value==null || value.GetType().GetFields()
        .Where(f=>!f.IsStatic && !ignore.Contains(f.Name)).All(f=>Convert.ToDouble(f.GetValue(value))==0);

    public List<InventoryItem> InventoryTargets(TownAlly caster, Utility u)
    {
        IEnumerable<InventoryItem> items=bag;
        if(u.InventorySelector?.IncludeEquipped==true)items=items.Concat(caster.Equipment.GetEquippedItems());
        return items.Where(i=>u.InventorySelector?.Matches(i)==true).Distinct().ToList();
    }
    public List<TownAlly> Targets(TownAlly caster, Utility u) => party.Where(h=>
        (u.Targeting!=SkillTargeting.Self && u.Selector?.Team!=TargetTeam.Self && u.Selector?.Area!=TargetArea.Self || h==caster) &&
        Benefits(caster,h,u)).ToList();
    private bool Benefits(TownAlly caster,TownAlly target,Utility u)
    {
        var stats=StatsFor(target);int hp=RecoveryMath.SavedVital(target.Hp,stats.HPMax),sp=RecoveryMath.SavedVital(target.Sp,stats.SPMax);
        if(hp<=0)return false;
        Amounts(caster,target,u,hp,stats.HPMax,out int heal,out int restore);
        return heal>0 && hp<stats.HPMax || restore>0 && sp<stats.SPMax;
    }
    private static void Amounts(TownAlly caster,TownAlly target,Utility u,int hp,int maximum,out int heal,out int restore)
    {
        heal=u.Hp;restore=u.Sp;
        int projectedHp=RecoveryMath.Restore(hp,heal,maximum);
        foreach(var effect in u.Effects ?? Enumerable.Empty<GameAction>())
        {
            int amount=effect is ScaledHealAction h ? RecoveryMath.Heal(h.BaseHeal,h.PerLevel,caster.Level,u.Rank,HealingMultiplier(caster,projectedHp,maximum) * HeroAttributes.MagicMultiplier(StatsFor(caster).MagicPower)) :
                effect is TakeHealAction direct ? u.Rank.Scaling.ScalePower(direct.healing,u.Rank.Rank) : 0;
            heal+=amount;projectedHp=RecoveryMath.Restore(projectedHp,amount,maximum);
            if(effect is RestoreSPAction s && !(s.ExcludeCaster && caster==target))restore+=u.Rank.Scaling?.ScalePower(s.Amount,u.Rank.Rank) ?? s.Amount;
        }
    }
    private static IEnumerable<(PassiveResponse Response,int Rank)> Passives(TownAlly hero)
    {
        foreach(var name in hero.Skills ?? new())
        {
            var skill=Common.Instance.SkillManager.GetSkillByName(name);
            if(skill?.ActivationType!=ActivationType.Passive || skill.PassiveResponses==null)continue;
            foreach(var response in skill.PassiveResponses)yield return (response,hero.GetRank(name));
        }
    }
    private static float HealingMultiplier(TownAlly caster,int hp,int maximum)
    {
        float value=1;
        foreach(var (response,rank) in Passives(caster))
            value*=response is HealingBonus bonus ? bonus.MultiplierAt(rank) : response is TriagePassive triage ? triage.MultiplierAt(rank,hp,maximum) : 1;
        return value;
    }
    private static float Preservation(TownAlly caster) => Mathf.Clamp(Passives(caster).Sum(p=>p.Response is ResourcefulPassive r ? r.ChanceAt(p.Rank) : 0),0,.9f);

    public bool Execute(TownAlly caster,Skill skill,InventoryItem item,TownAlly target,InventoryItem inventoryTarget,out string result)
    {
        result="That action is no longer available.";
        if(committing || !Describe(caster,skill,item,out var u,out result))return false;
        List<TownAlly> recipients=null;
        if(u.Targeting==SkillTargeting.InventoryItem)
        {if(!InventoryTargets(caster,u).Contains(inventoryTarget)) {result="Select an eligible item.";return false;}}
        else
        {
            var eligible=Targets(caster,u);
            recipients=u.Targeting==SkillTargeting.AllTargets ? eligible : new List<TownAlly>{u.Targeting==SkillTargeting.Self?caster:target};
            if(recipients.Any(h=>!eligible.Contains(h))) {result="That living party member does not need recovery.";return false;}
        }
        // All costs, identities, recipients and effects are now validated. No UI cancellation path reaches this block.
        committing=true;
        try
        {
            int totalHp=0,totalSp=0;
            caster.Sp=RecoveryMath.SavedVital(caster.Sp,StatsFor(caster).SPMax)-u.Cost;
            foreach(var recipient in recipients ?? Enumerable.Empty<TownAlly>())
            {
                var stats=StatsFor(recipient);int hp=RecoveryMath.SavedVital(recipient.Hp,stats.HPMax),sp=RecoveryMath.SavedVital(recipient.Sp,stats.SPMax);
                Amounts(caster,recipient,u,hp,stats.HPMax,out int heal,out int restore);
                recipient.Hp=RecoveryMath.Restore(hp,heal,stats.HPMax);recipient.Sp=RecoveryMath.Restore(sp,restore,stats.SPMax);
                totalHp+=recipient.Hp-hp;totalSp+=recipient.Sp-sp;
            }
            if(item!=null && !(UnityEngine.Random.value<Preservation(caster)))
            {
                if(item.HasStacks)item.Decrement();
                if(item.ShouldRemoveAfterUse())bag.Remove(item);
            }
            result=inventoryTarget!=null ? InspectInventoryItemEffect.Describe(inventoryTarget) : $"Recovered {totalHp} HP and {totalSp} SP.";
            persist?.Invoke();return true;
        }
        finally {committing=false;}
    }
}
