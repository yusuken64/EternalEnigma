using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Progression;

public enum EncounterKind { OvertakenCity, LostWoods, BoulderCave, Prison, Bridge, Ambush, BossArena, Sokoban, LightPattern, Lure, Herding }

/// <summary>Immutable location content. Rewards remain the campaign's keyed/source rewards, never a generic combat bonus.</summary>
public sealed class EncounterDefinition
{
    public EncounterKind Kind { get; }
    public string DisplayName => Kind switch {
        EncounterKind.OvertakenCity => "Overtaken city", EncounterKind.LostWoods => "Lost woods", EncounterKind.BoulderCave => "Boulder-blocked cave",
        EncounterKind.BossArena => "Boss arena", EncounterKind.LightPattern => "Light pattern", EncounterKind.Lure => "Lure into trap", EncounterKind.Herding => "Guide a fleeing enemy", _ => Kind.ToString() };
    public int FloorCount { get; }
    public Requirement EntryRequirement { get; }
    public bool RequiresResetFloor => Kind == EncounterKind.Sokoban;
    public bool IsPuzzle => Kind == EncounterKind.Sokoban || Kind == EncounterKind.LightPattern || Kind == EncounterKind.Lure || Kind == EncounterKind.Herding;
    public string? ReturnLocationId { get; }
    public string? BridgeRouteId { get; }
    public EncounterDefinition(EncounterKind kind, int tier, Requirement? entryRequirement = null, string? returnLocationId = null, string? bridgeRouteId = null)
    {
        if (tier < 0 || tier > 4) throw new ArgumentOutOfRangeException(nameof(tier));
        if (!Enum.IsDefined(typeof(EncounterKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        FloorCount = kind == EncounterKind.OvertakenCity || kind == EncounterKind.LostWoods || kind == EncounterKind.BoulderCave || kind == EncounterKind.Prison
            ? new[] { 2, 2, 3, 3, 4 }[tier] : 1;
        EntryRequirement = entryRequirement ?? Requirement.Open;
        ReturnLocationId = returnLocationId; BridgeRouteId = bridgeRouteId;
    }
    public bool CanEnter(CapabilitySet held, ISet<string> completed) => EntryRequirement.IsSatisfiedBy(held) && (!RequiresResetFloor || completed.Contains("repeatable-0"));
}

public sealed class DungeonRunFloor
{
    public int Number { get; }
    public int CombatDifficulty { get; }
    public DungeonFloorRole Role { get; }
    public DungeonObjectiveKind ObjectiveKind { get; }
    public DungeonFloorOptions Options { get; }
    internal DungeonRunFloor(int number, int difficulty, DungeonFloorOptions options)
    { Number = number; CombatDifficulty = difficulty; Role = options.Role; ObjectiveKind = options.Objective.Kind; Options = options; }
}

/// <summary>Floor ordinal, difficulty and presentation are independent for short encounters.</summary>
public sealed class DungeonRunPlan
{
    public DungeonVariant Variant { get; }
    public string LocationId { get; }
    public IReadOnlyList<DungeonRunFloor> Floors { get; }
    public int StartFloor => Floors[0].Number;
    public int EndFloor => Floors[Floors.Count - 1].Number;
    public bool IsEncounter { get; }
    public DungeonRunFloor Floor(int number) => Floors.FirstOrDefault(f => f.Number == number) ?? throw new ArgumentOutOfRangeException(nameof(number));
    public DungeonRunPlan(int campaignSeed, CampaignLocation location, OverworldBiome biome = OverworldBiome.Grassland, int layoutVersion = 1)
    {
        LocationId = location.Id; IsEncounter = location.Encounter != null;
        Variant = location.Variant;
        if (location.Kind == LocationKind.FinalDungeon)
        {
            Variant = DungeonVariant.Overworld;
            Floors = Array.AsReadOnly(new[] { new DungeonRunFloor(30, 38,
                new DungeonFloorOptions(CampaignContext.LocationSeed(campaignSeed, location.Id), enemyCount: 0,
                    goldCount: 0, itemCount: 0, trapCount: 0, gatheringCount: 0, tier: 4, variant: DungeonVariant.Overworld)) });
            return;
        }
        var range = IsEncounter ? (Start: 1, End: location.Encounter!.FloorCount) : CampaignContext.Floors(location.Tier);
        var floors = new List<DungeonRunFloor>();
        for (int number = range.Start; number <= range.End; number++)
        {
            int difficulty = IsEncounter ? Math.Min(CampaignContext.Floors(location.Tier).End - 1, CampaignContext.Floors(location.Tier).Start + number) : number;
            var options = IsEncounter ? EncounterFloorGenerator.Options(campaignSeed, location, number, biome) :
                CampaignContext.DungeonFloorOptionsFor(campaignSeed, location.Id, number, location.Tier, biome, layoutVersion, Variant);
            floors.Add(new DungeonRunFloor(number, difficulty, options));
        }
        Floors = Array.AsReadOnly(floors.ToArray());
    }
}
