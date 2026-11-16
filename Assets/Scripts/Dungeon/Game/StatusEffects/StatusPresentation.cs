using System.Collections.Generic;
using UnityEngine;

/// <summary>Player-facing details from the live effect, shared by the HUD and Equipment menu.</summary>
public static class StatusPresentation
{
    public static string Name(StatusEffect status) => GameplayText.Name(status.GetEffectName());

    public static string Duration(StatusEffect status) => status switch
    {
        { Expiration: StatusExpiration.None } => "Until cleansed",
        GoopiRootStatusEffect => "Until the holder is defeated",
        _ => $"{Mathf.Max(0, status.TurnsLeft)} {(status.TurnsLeft == 1 ? "turn" : "turns")} left"
    };

    public static string Badge(StatusEffect status) => status switch
    {
        FrailtyStatusEffect frailty => $"x{frailty.Stacks}",
        GoopiRootStatusEffect => "",
        _ => status.TurnsLeft.ToString()
    };

    public static string Summary(StatusEffect status) => Name(status) +
        (status is FrailtyStatusEffect frailty ? $" x{frailty.Stacks}" : "") + ", " + Duration(status);

    public static string Description(StatusEffect status, Character owner = null) =>
        Effect(status, owner) + "\n\nDuration: " + Duration(status) + ".";

    public static string Effect(StatusEffect status, Character owner = null) => status switch
    {
        FrailtyStatusEffect frailty => $"Attack -{frailty.Stacks}. Each stack reduces Attack by 1. Reapplying adds a stack; waiting does not remove it.",
        StrengthStatusEffect strength => $"Attack +{strength.Stacks * strength.StrengthPerStack}. {strength.Stacks} {(strength.Stacks == 1 ? "stack" : "stacks")}, +{strength.StrengthPerStack} per stack. Reapplying adds a stack without extending the duration.",
        GoopiRootStatusEffect => "Cannot move or be moved. Attacks, skills and items remain available. Defeat the holder to break free.",
        StuckStatusEffect => "Cannot move or be moved. Attacks, skills and items remain available.",
        ArmBindStatusEffect => "Cannot use basic attacks, ranged attacks, weapon skills or skills that use arrows.",
        SilenceStatusEffect => "Cannot cast spells or use skills.",
        SleepStatusEffect => "Skips actions and prevents using items or skills until sleep ends.",
        StunStatusEffect => "Skips actions and prevents using items or skills until the stun ends.",
        ParalysisStatusEffect paralysis => $"{Percent(paralysis.SkipChance)} chance each turn to skip actions and be unable to use items or skills.",
        ConfusionStatusEffect => "Acts in random directions, moving, waiting or attacking whoever is in the way, including allies. Cannot choose actions.",
        FearStatusEffect => "Cannot attack, cast spells or use skills. Enemies also flee from the party.",
        TauntStatusEffect taunt => "Pursues " + (taunt.Taunter != null ? GameMessages.Name(taunt.Taunter) : "the taunter") + " and prioritizes them when choosing an enemy target.",
        BurnStatusEffect burn => $"Deals {burn.TickDamage} Fire damage each turn, before damage modifiers.",
        DotStatusEffect dot => $"Deals {dot.TickDamage} damage each turn, before damage modifiers.",
        HotStatusEffect regeneration => $"Restores {regeneration.TickDamage} HP each turn.",
        CurseStatusEffect curse => $"Reflects {Percent(curse.ReflectFraction)} of damage dealt by your direct hits back to you. Follow-up damage does not trigger it.",
        DamageReductionStatusEffect reduction => $"Reduces incoming damage by {Percent(reduction.Reduction)}.",
        DamageShieldStatusEffect shield => $"Absorbs {shield.RemainingAbsorb} more damage. Ends when the shield is depleted.",
        EndureStatusEffect => "Survive the next fatal hit with 1 HP if you had more than 1 HP before the hit. Ends when triggered.",
        ParryStatusEffect parry => $"{Percent(parry.BlockChance)} chance to block a hit from an adjacent attacker.",
        AmplifyStatusEffect amplify => $"The next elemental damage skill deals {Percent(amplify.Bonus)} more damage. Consumed after that attack.",
        StealthStatusEffect => "Enemies cannot target you. Ends when you attack." +
            (owner != null && ClassPassives.KeepsStealthOnKill(owner) ? " Your passive preserves stealth if the attack defeats its target." : ""),
        SafePassageStatusEffect => "Enemies cannot target you. Ends for the whole party when any party member attacks an enemy.",
        BarrierStatusEffect barrier => $"Reduces {barrier.Element} damage taken by {Percent(barrier.Reduction)}." + AdditionalStats(barrier.Modification),
        FollowUpMarkStatusEffect mark => $"Physical hits by the caster's team trigger a {mark.Element} follow-up hit while the caster is alive. Base damage is {Percent(mark.DamagePercent)} of the caster's Attack, adjusted by defense and follow-up bonuses.",
        SongStatusEffect song => Song(song),
        CommandStatusEffect command => Command(command),
        BossEffectStatus boss => Boss(boss, owner),
        BlindStatusEffect or WeakenStatusEffect or ResistanceShiftStatusEffect or TimedBuffStatusEffect => Stats(status.GetStatModification()),
        _ => "No additional effect."
    };

    private static string Percent(float value) => $"{value * 100:0.#}%";
    private static string Stats(StatModification modification)
    {
        var changes = modification?.DescribeEffect();
        return changes != null && changes.Count > 0 ? string.Join(", ", changes) + "." : "No stat changes.";
    }
    private static string AdditionalStats(StatModification modification) =>
        modification != null && modification.DescribeEffect().Count > 0 ? " " + Stats(modification) : "";

    private static string Song(SongStatusEffect song)
    {
        var parts = new List<string> { $"Affects the performer and allies within {song.Radius} tiles." };
        if (song.Modification != null && song.Modification.DescribeEffect().Count > 0) parts.Add(Stats(song.Modification));
        if (song.HealPerTurn > 0) parts.Add($"Restores {song.HealPerTurn} HP each turn to allies in range.");
        parts.Add("Only the strongest song bonus to each stat applies.");
        return string.Join(" ", parts);
    }
    private static string Command(CommandStatusEffect command)
    {
        var parts = new List<string>();
        var modification = BuffStacking.Scale(command.Modification, command.Multiplier);
        if (modification.DescribeEffect().Count > 0) parts.Add(Stats(modification));
        if (command.HealPerTurn > 0) parts.Add($"Restores {Mathf.RoundToInt(command.HealPerTurn * command.Multiplier)} HP each turn.");
        if (command.BonusElementPercent > 0)
            parts.Add($"Direct hits on enemies add a {command.BonusElement} hit for {command.BonusElementPercent * command.Multiplier:0.#}% of the original damage.");
        if (command.Issuer != null)
        {
            float heal = PassiveModifiers.SumFloat<CommandExpiryHeal>(command.Issuer, bonus => bonus.HealPercent);
            if (heal > 0) parts.Add($"Restores {Percent(heal)} of maximum HP when the command ends.");
        }
        parts.Add("Only the strongest command bonus to each stat applies.");
        return string.Join(" ", parts);
    }
    private static string Boss(BossEffectStatus boss, Character owner) => boss.Kind switch
    {
        "Root" => "Cannot move or be moved. Attacks, skills and items remain available.",
        "Sleep" => "Skips actions and prevents using items or skills until sleep ends.",
        "Confusion" => "Moves in a random direction or waits if blocked. Cannot choose actions.",
        "Silence" => "Cannot cast spells or use skills.",
        "Poison" or "Bleed" => owner != null
            ? $"Deals {Mathf.Max(1, owner.FinalStats.HPMax / 12)} damage each turn, based on one twelfth of maximum HP, before damage modifiers."
            : "Deals one twelfth of maximum HP as damage each turn, rounded down with a minimum of 1, before damage modifiers.",
        "Guard" => "Reduces incoming damage to one quarter, with a minimum of 1. Counters direct hits for damage based on Attack.",
        "Armored" => "Reduces incoming damage to one quarter, with a minimum of 1.",
        "Exposed" => "Takes double damage.",
        "Empowered" => Stats(boss.GetStatModification()),
        _ => "No additional effect."
    };
}
