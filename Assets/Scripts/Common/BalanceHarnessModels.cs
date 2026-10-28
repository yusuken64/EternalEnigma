using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class BalanceAdjustment
{
    public string Name;
    public float Multiplier = 1;
}

[Serializable]
public sealed class BalanceExperimentOptions
{
    public const string PolicyVersion = "cautious-v2";
    public string ExperimentId, CandidateId = "baseline", ContentId, HeroId;
    public string Cohort = "training";
    public List<BalanceAdjustment> ItemPrices = new(), AbilityMana = new();
    public int MaxRetreats = 8;

    public bool Validate(out string reason)
    {
        reason = string.IsNullOrWhiteSpace(ExperimentId) || string.IsNullOrWhiteSpace(CandidateId) || string.IsNullOrWhiteSpace(ContentId)
            ? "Balance experiments require experiment, candidate and content identities." :
            Cohort is not ("training" or "validation" or "pilot") ? "Unknown balance cohort." :
            MaxRetreats < 0 || MaxRetreats > 30 ? "Retreat budget must be between 0 and 30." : null;
        foreach (var rows in new[] { ItemPrices, AbilityMana })
        {
            if (rows == null || rows.Any(r => r == null || string.IsNullOrWhiteSpace(r.Name) ||
                float.IsNaN(r.Multiplier) || float.IsInfinity(r.Multiplier) || r.Multiplier < .75f || r.Multiplier > 1.25f) ||
                rows.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count() != rows.Count)
                reason = "Adjustments must be unique named entries bounded to 0.75–1.25 of authored costs.";
        }
        return reason == null;
    }
}

[Serializable]
public sealed class BalanceActorSnapshot
{
    public string Id, Class;
    public int Level, HP, MaxHP, Mana, MaxMana, Hunger, MaxHunger;
    public string[] Equipment, Skills;
}

[Serializable]
public sealed class BalanceStage
{
    public string Location, Kind, Outcome = "Running";
    public string GenerationFingerprint;
    public int Tier, Floor, Visit, EntryAction, ExitAction, EntryGold, ExitGold, Turns, DamageTaken, HealingReceived;
    public int Downings, Retreats, ItemUses, VisibleEnemyDecisions, DangerousDecisions, DiscoveredCells;
    public float MinimumHealthFraction = 1;
    public List<BalanceActorSnapshot> EntryParty = new(), ExitParty = new();
}

[Serializable]
public sealed class BalanceDecision
{
    public string Kind, Name, ActorId, Reason, Location;
    public int Action, Floor, Cost, Gold, Rank, Mana;
    public float Value, IncomingDamage;
    public bool Chosen;
}

[Serializable]
public sealed class BalanceAbilityObservation
{
    public string ActorId, Skill;
    public int Rank, Decisions, LegalOpportunities, Unaffordable, Selected, Started, Releases, Interrupted, ManaSpent;
    public float EstimatedValue;
    public string LastReason;
}

[Serializable]
public sealed class BalancePurchaseObservation
{
    public string Item, LastReason;
    public int Evaluations, Unaffordable, Wanted, Purchases, GoldSpent;
}

[Serializable]
public sealed class BalanceRunReport
{
    public string Policy = BalanceExperimentOptions.PolicyVersion;
    public string ExperimentId, CandidateId, ContentId, Cohort, HeroId;
    public string Observation = "Discovered dungeon terrain and personal sight; known campaign travel graph. Enemy estimates use visible archetype stats, never future random rolls.";
    public List<BalanceStage> Stages = new();
    public List<BalanceAbilityObservation> Abilities = new();
    public List<BalancePurchaseObservation> Purchases = new();
    public List<BalanceEffectTotal> Effects = new();
    public int Retreats, RestVisits, Decisions, ActualTurns;
    public string[] Limitations = { "Companions use production companion AI", "Encounter-specific puzzle helpers are scripted", "Ability value estimates require effect-specific calibration" };
}

public sealed class BalanceActionSource
{
    public string ActorId, Skill, Item, RootAction;
    public int Rank;
}

[Serializable]
public sealed class BalanceEffectTotal
{
    public string ActorId, Skill, Item, RootAction, TargetTeam;
    public int Rank, HPRemoved, HPRestored, ManaRemoved, ManaRestored, HungerRestored, Changes;
}
