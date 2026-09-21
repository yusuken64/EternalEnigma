using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Progression;

public enum LocationKind { Checkpoint, Town, StoryDungeon, RepeatableDungeon, FinalDungeon, Converter, Landmark, Secret }
public enum ShortcutKind { None, FarSide, Capability, Keyed }
public enum KeyAcquisition { AtLocation, DungeonCompletion }
public enum LockForm { None, Obstacle, Interaction, Area }

public sealed class CampaignRegion
{
    public string Id { get; }
    public string Theme { get; }
    public int Tier { get; }
    public int ProgressionOrder { get; }
    public string Label => ProgressionOrder >= 0 ? ((char)('A' + ProgressionOrder)).ToString() : Id;
    public CampaignRegion(string id, string theme, int tier, int progressionOrder = -1)
    { Id = id; Theme = theme; Tier = tier; ProgressionOrder = progressionOrder; }
}

public sealed class CampaignLocation
{
    public string Id { get; }
    public string RegionId { get; }
    public int Tier { get; }
    public LocationKind Kind { get; }
    public bool Required { get; }
    public int Stage { get; }
    /// <summary>Town containing this dungeon's entrance; null for an overworld destination.</summary>
    public string? ParentTownId { get; }
    public CampaignLocation(string id, string regionId, int tier, LocationKind kind, bool required = false, int stage = 0, string? parentTownId = null)
    { Id = id; RegionId = regionId; Tier = tier; Kind = kind; Required = required; Stage = stage; ParentTownId = parentTownId; }
}

/// <summary>Bidirectional abstract route. Area gates remain conditional; other locks latch open.</summary>
public sealed class CampaignRoute
{
    public string Id { get; }
    public string From { get; }
    public string To { get; }
    public Requirement Requirement { get; }
    public LockForm Form { get; }
    public bool Required { get; }
    public bool IsProgressionBoundary { get; }
    public ShortcutKind ShortcutKind { get; }
    public string? UnlockingEndpoint { get; }
    public string? KeyId { get; }
    public string? KeyLocationId { get; }
    public KeyAcquisition KeyCondition { get; }
    public bool IsStarterExit => KeyCondition == KeyAcquisition.DungeonCompletion;
    public bool IsWarp { get; }
    /// <summary>A gate enforced by leaving the town scene, rather than by an overworld tile.</summary>
    public bool IsTownExit { get; }
    public string GateHint => ShortcutKind == ShortcutKind.Keyed ? "Requires: " + KeyId :
        ShortcutKind == ShortcutKind.FarSide ? "Open shortcut at the far endpoint." : "Requires: " + Requirement;
    public bool HasGate => !Requirement.IsOpen || ShortcutKind == ShortcutKind.FarSide || ShortcutKind == ShortcutKind.Keyed;
    public CampaignRoute(string id, string from, string to, Requirement requirement, LockForm form, bool required = false, bool isProgressionBoundary = false, ShortcutKind shortcutKind = ShortcutKind.None, string? unlockingEndpoint = null, string? keyId = null, string? keyLocationId = null, bool isWarp = false, KeyAcquisition keyCondition = KeyAcquisition.AtLocation, bool isTownExit = false)
    { Id = id; From = from; To = to; Requirement = requirement ?? throw new ArgumentNullException(nameof(requirement)); Form = form; Required = required; IsProgressionBoundary = isProgressionBoundary; ShortcutKind = shortcutKind; UnlockingEndpoint = unlockingEndpoint; KeyId = keyId; KeyLocationId = keyLocationId; IsWarp = isWarp; KeyCondition = keyCondition; IsTownExit = isTownExit; }
    public string? Other(string location) => location == From ? To : location == To ? From : null;
    public bool CanTraverse(CapabilitySet held, ISet<string> resolved) =>
        (ShortcutKind == ShortcutKind.FarSide || ShortcutKind == ShortcutKind.Keyed) ? resolved.Contains(Id) :
        (Latches && resolved.Contains(Id)) || Requirement.IsSatisfiedBy(held);
    public bool Latches => ShortcutKind == ShortcutKind.None && (Form == LockForm.Obstacle || Form == LockForm.Interaction);
    public bool TryCollectKey(string locationId, ISet<string> resolved, ISet<string>? completed = null) =>
        ShortcutKind == ShortcutKind.Keyed && locationId == KeyLocationId && (KeyCondition == KeyAcquisition.AtLocation || completed?.Contains(locationId) == true) && resolved.Add(Id);
    public bool TryUnlock(string endpoint, ISet<string> resolved)
    {
        if (ShortcutKind != ShortcutKind.FarSide || endpoint != UnlockingEndpoint) return false;
        return resolved.Add(Id);
    }
}

public sealed class CampaignCompanion
{
    public string Id { get; }
    public Capability Capability { get; }
    public CampaignCompanion(string id, Capability capability) { Id = id; Capability = capability; }
}

/// <summary>A guaranteed reward after abstract completion of its location; combat is not simulated.</summary>
public sealed class CapabilitySource
{
    public string Id { get; }
    public string LocationId { get; }
    public Capability Capability { get; }
    public string? CompanionId { get; }
    public bool Guaranteed { get; }
    public Requirement Prerequisites { get; }
    public CapabilitySource(string id, string locationId, Capability capability, string? companionId = null,
        bool guaranteed = true, Requirement? prerequisites = null)
    {
        Id = id; LocationId = locationId; Capability = capability; CompanionId = companionId;
        Guaranteed = guaranteed; Prerequisites = prerequisites ?? Requirement.Open;
    }
}

public sealed class ReturnObjective
{
    public string RegionId { get; }
    public IReadOnlyList<string> DestinationIds { get; }
    public IReadOnlyList<string> GateIds { get; }
    public Capability EnablingCapability { get; }
    public int AcquisitionStage { get; }
    public bool Required { get; }
    public Capability? RewardCapability { get; }
    public ReturnObjective(string regionId, IEnumerable<string> destinations, IEnumerable<string> gates,
        Capability enablingCapability, int acquisitionStage, bool required, Capability? rewardCapability = null)
    {
        RegionId = regionId; DestinationIds = Array.AsReadOnly(destinations.ToArray()); GateIds = Array.AsReadOnly(gates.ToArray());
        EnablingCapability = enablingCapability; AcquisitionStage = acquisitionStage; Required = required; RewardCapability = rewardCapability;
    }
}

/// <summary>Immutable, engine-independent logical campaign. It contains no terrain or mutable player state.</summary>
public sealed class Campaign
{
    public IReadOnlyList<string> StarterLocations => Array.AsReadOnly(new[] { StartLocationId, "story-0" });
    public IReadOnlyList<string> StarterAreaLocations => Array.AsReadOnly(new[] { StartLocationId, "story-0", "repeatable-0" });
    public int Seed { get; }
    public int GeneratorVersion { get; }
    public string StartLocationId { get; }
    public string FinalLocationId { get; }
    public IReadOnlyList<ActivatedCapability> Manifest { get; }
    public IReadOnlyList<CampaignRegion> Regions { get; }
    public IReadOnlyList<CampaignLocation> Locations { get; }
    public IReadOnlyList<CampaignRoute> Routes { get; }
    public IReadOnlyList<CapabilitySource> Sources { get; }
    public IReadOnlyList<CampaignCompanion> Companions { get; }
    public IReadOnlyList<ReturnObjective> ReturnObjectives { get; }
    public Campaign(int seed, int generatorVersion, string startLocationId, string finalLocationId,
        IEnumerable<ActivatedCapability> manifest, IEnumerable<CampaignRegion> regions,
        IEnumerable<CampaignLocation> locations, IEnumerable<CampaignRoute> routes,
        IEnumerable<CapabilitySource> sources, IEnumerable<CampaignCompanion> companions, IEnumerable<ReturnObjective>? returnObjectives = null)
    {
        ReturnObjectives = Array.AsReadOnly((returnObjectives ?? Array.Empty<ReturnObjective>()).ToArray());
        Seed = seed; GeneratorVersion = generatorVersion; StartLocationId = startLocationId; FinalLocationId = finalLocationId;
        Manifest = Array.AsReadOnly(manifest.ToArray()); Regions = Array.AsReadOnly(regions.ToArray());
        Locations = Array.AsReadOnly(locations.ToArray()); Routes = Array.AsReadOnly(routes.ToArray());
        Sources = Array.AsReadOnly(sources.ToArray()); Companions = Array.AsReadOnly(companions.ToArray());
    }
}
