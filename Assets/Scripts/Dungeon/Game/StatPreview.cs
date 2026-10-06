using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct Loadout
{
    public readonly EquipableInventoryItem Weapon, OffHand, Accessory;
    public Loadout(EquipableInventoryItem weapon, EquipableInventoryItem offHand, EquipableInventoryItem accessory)
    { Weapon = weapon; OffHand = offHand; Accessory = accessory; }
    public IEnumerable<EquipableInventoryItem> Items => new[] { Weapon, OffHand, Accessory }.Where(i => i != null);
    public StatModification Modification => Items.Aggregate(new StatModification(), (sum, item) => sum + item.GetEquipmentStatModification());
    public Loadout Without(EquipmentSlot slot) => slot == EquipmentSlot.MainHand || slot == EquipmentSlot.TwoHand
        ? new Loadout(null, OffHand, Accessory) : slot == EquipmentSlot.OffHand
        ? new Loadout(Weapon, null, Accessory) : new Loadout(Weapon, OffHand, null);
    public Loadout With(EquipableInventoryItem item)
    {
        if (item?.EquipmentItemDefinition == null) return this;
        var weapon = Weapon; var offHand = OffHand; var accessory = Accessory;
        if(item.EquipmentItemDefinition.WeaponType==WeaponType.BowAndArrow && !item.EquipmentItemDefinition.IsAmmunition)
            return new Loadout(item,ArrowSupply.IsArrow(offHand)?offHand:null,accessory);
        switch (item.EquipmentSlot)
        {
            case EquipmentSlot.MainHand:
                weapon = item;
                if (item.EquipmentItemDefinition.WeaponType == WeaponType.BowAndArrow && !ArrowSupply.IsArrow(offHand)) offHand = null;
                break;
            case EquipmentSlot.TwoHand: weapon = item; offHand = null; break;
            case EquipmentSlot.OffHand:
                offHand = item;
                if (weapon?.EquipmentSlot == EquipmentSlot.TwoHand ||
                    (weapon?.EquipmentItemDefinition?.WeaponType == WeaponType.BowAndArrow && !ArrowSupply.IsArrow(item))) weapon = null;
                break;
            case EquipmentSlot.Accessory: accessory = item; break;
        }
        return new Loadout(weapon, offHand, accessory);
    }
}

public static class StatPreview
{
    public static IEnumerable<(string Source, StatModification Value)> Parts(Character hero, Loadout loadout)
    {
        var passive = hero.Skills.Where(s => s != null && s.ActivationType == ActivationType.Passive)
            .Aggregate(new StatModification(), (sum, s) => sum + s.GetScaledPassiveModification());
        var statuses = hero.StatusEffects.Aggregate(new StatModification(), (sum, s) => sum + s.GetStatModification());
        yield return ("Equipment",loadout.Modification);
        yield return ("Passives",passive);
        yield return ("Status",statuses);
        yield return ("Song",SongAura.ModificationFor(hero));
        yield return ("Command",CommandStatusEffect.ModificationFor(hero));
        yield return ("Class conditions",ClassPassives.ConditionalStats(hero,loadout));
    }
    public static Stats Final(Character hero, Loadout loadout) =>
        Parts(hero,loadout).Aggregate(new Stats(hero.BaseStats),(stats,part)=>stats+part.Value);
    public static string Diff(Stats before, Stats after)
    {
        var lines = new List<string>();
        void Add(string name, int a, int b) { if (a != b) lines.Add($"{name} {a}→{b}"); }
        void Percent(string name, float a, float b) { if (Math.Abs(a-b) > .0001f) lines.Add($"{name} {a*100:0.#}%→{b*100:0.#}%"); }
        Add("HP",before.HPMax,after.HPMax);Add("SP",before.SPMax,after.SPMax);
        Add("Food",before.HungerMax,after.HungerMax);Add("Attack",before.Strength,after.Strength);
        Add("Magic",before.MagicPower,after.MagicPower);Add("Defense",before.Defense,after.Defense);
        Add("EXP on kill",before.EXPOnKill,after.EXPOnKill);
        Add("Food threshold",before.HungerAccumulateThreshold,after.HungerAccumulateThreshold);
        if(Math.Abs(before.DropRate-after.DropRate)>.0001f)lines.Add($"Drop rate {before.DropRate:0.##}→{after.DropRate:0.##}");
        Percent("Hit",before.HitBonus,after.HitBonus);Percent("Evasion",before.Evasion,after.Evasion);Percent("Crit",before.CritChance,after.CritChance);
        Add("Actions",before.ActionsPerTurnMax,after.ActionsPerTurnMax);Add("Attacks",before.AttacksPerTurnMax,after.AttacksPerTurnMax);
        Add("HP regen threshold",before.HPRegenAcccumlateThreshold,after.HPRegenAcccumlateThreshold);
        Add("SP regen threshold",before.SPRegenAcccumlateThreshold,after.SPRegenAcccumlateThreshold);
        Add("Fire",before.FireResistance,after.FireResistance);Add("Ice",before.IceResistance,after.IceResistance);Add("Lightning",before.LightningResistance,after.LightningResistance);
        return lines.Count == 0 ? "No stat change" : string.Join("  ",lines);
    }
}
