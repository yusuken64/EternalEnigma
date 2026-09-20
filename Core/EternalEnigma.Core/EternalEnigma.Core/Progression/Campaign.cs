using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Progression;

public enum LocationKind { Checkpoint, Town, StoryDungeon, RepeatableDungeon, FinalDungeon, Converter, Landmark, Secret }
public enum LockForm { None, Obstacle, Interaction, Area }

public sealed class CampaignRegion
{
    public string Id { get; }
    public string Theme { get; }
    public int Tier { get; }
    public CampaignRegion(string id, string theme, int tier) { Id = id; Theme = theme; Tier = tier; }
}

public sealed class CampaignLocation
{
    public string Id { get; }
    public string RegionId { get; }
    public int Tier { get; }
    public LocationKind Kind { get; }
    public bool Required { get; }
    public int Stage { get; }
    public CampaignLocation(string id, string regionId, int tier, LocationKind kind, bool required = false, int stage = 0)
    { Id = id; RegionId = regionId; Tier = tier; Kind = kind; Required = required; Stage = stage; }
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
    public CampaignRoute(string id, string from, string to, Requirement requirement, LockForm form, bool required = false, bool isProgressionBoundary = false)
    { Id = id; From = from; To = to; Requirement = requirement ?? throw new ArgumentNullException(nameof(requirement)); Form = form; Required = required; IsProgressionBoundary = isProgressionBoundary; }
    public string? Other(string location) => location == From ? To : location == To ? From : null;
    public bool CanTraverse(CapabilitySet held, ISet<string> resolved) =>
        (Form != LockForm.Area && resolved.Contains(Id)) || Requirement.IsSatisfiedBy(held);
    public bool Latches => Form == LockForm.Obstacle || Form == LockForm.Interaction;
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

/// <summary>Immutable, engine-independent logical campaign. It contains no terrain or mutable player state.</summary>
public sealed class Campaign
{
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
    public Campaign(int seed, int generatorVersion, string startLocationId, string finalLocationId,
        IEnumerable<ActivatedCapability> manifest, IEnumerable<CampaignRegion> regions,
        IEnumerable<CampaignLocation> locations, IEnumerable<CampaignRoute> routes,
        IEnumerable<CapabilitySource> sources, IEnumerable<CampaignCompanion> companions)
    {
        Seed = seed; GeneratorVersion = generatorVersion; StartLocationId = startLocationId; FinalLocationId = finalLocationId;
        Manifest = Array.AsReadOnly(manifest.ToArray()); Regions = Array.AsReadOnly(regions.ToArray());
        Locations = Array.AsReadOnly(locations.ToArray()); Routes = Array.AsReadOnly(routes.ToArray());
        Sources = Array.AsReadOnly(sources.ToArray()); Companions = Array.AsReadOnly(companions.ToArray());
    }
}
