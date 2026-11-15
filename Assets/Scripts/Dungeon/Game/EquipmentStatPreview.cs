using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Final character totals for an equipment choice, including displaced items and other bonuses.</summary>
public sealed class EquipmentStatPreview
{
    public string Caption { get; }
    public IReadOnlyList<StatComparison> Values { get; }

    public EquipmentStatPreview(Stats before, Stats after, bool removing)
    {
        Caption = removing ? "Unequip preview" : "Equip preview";
        var values = new List<StatComparison>();
        void Add(string label, float a, float b, string suffix = "", bool always = false)
        {
            var value = new StatComparison(label, a, b, suffix);
            if (always || value.Direction != 0) values.Add(value);
        }
        // Keep the same three columns visible even when only one stat changes.
        Add("Attack", before.Strength, after.Strength, always: true);
        Add("Defense", before.Defense, after.Defense, always: true);
        Add("Magic", before.MagicPower, after.MagicPower, always: true);
        Add("HP", before.HPMax, after.HPMax);
        Add("Mana", before.ManaMax, after.ManaMax);
        Add("Food", before.HungerMax, after.HungerMax);
        Add("EXP / defeat", before.EXPOnKill, after.EXPOnKill);
        Add("Hunger interval", before.HungerAccumulateThreshold, after.HungerAccumulateThreshold);
        Add("Loot", before.DropRate, after.DropRate, "×");
        Add("Hit", Math.Clamp(.8f + before.HitBonus, .05f, 1f) * 100, Math.Clamp(.8f + after.HitBonus, .05f, 1f) * 100, "%");
        Add("Evasion", before.Evasion * 100, after.Evasion * 100, "%");
        Add("Crit", before.CritChance * 100, after.CritChance * 100, "%");
        Add("Actions", before.ActionsPerTurnMax, after.ActionsPerTurnMax);
        Add("Attacks", before.AttacksPerTurnMax, after.AttacksPerTurnMax);
        Add("HP recovery", before.HPRegenAcccumlateThreshold, after.HPRegenAcccumlateThreshold);
        Add("Mana recovery", before.ManaRegenThreshold, after.ManaRegenThreshold);
        Add("Fire resist", before.FireResistance, after.FireResistance);
        Add("Ice resist", before.IceResistance, after.IceResistance);
        Add("Lightning resist", before.LightningResistance, after.LightningResistance);
        Values = values;
    }
}

public readonly struct StatComparison
{
    public readonly string Label, Suffix;
    public readonly float Before, After;
    public StatComparison(string label, float before, float after, string suffix = "")
    { Label = label; Before = before; After = after; Suffix = suffix; }
    public int Direction => Math.Abs(After - Before) < .0001f ? 0 : Math.Sign(After - Before);
    private string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture) + Suffix;
    public string BeforeText => Format(Before);
    public string AfterText => Format(After);
    public string DeltaText => Direction == 0 ? "—" : (Direction > 0 ? "+" : "−") + Format(Math.Abs(After - Before));
}
