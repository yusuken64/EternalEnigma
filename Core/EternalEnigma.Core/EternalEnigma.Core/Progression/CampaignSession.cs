using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Progression;

/// <summary>Logical campaign traversal. The host calls ClaimSource only after its encounter/quest is completed.</summary>
public sealed class CampaignSession
{
    private readonly Campaign campaign;
    private readonly Dictionary<string, CampaignLocation> locations;
    private readonly HashSet<string> roster = new(StringComparer.Ordinal);
    private readonly HashSet<string> claimed = new(StringComparer.Ordinal);
    private readonly HashSet<string> resolved = new(StringComparer.Ordinal);
    private readonly HashSet<string> towns = new(StringComparer.Ordinal);
    private string[] active = Array.Empty<string>();
    public string LocationId { get; private set; }
    public CapabilitySet PermanentCapabilities { get; private set; }
    public IReadOnlyCollection<string> RecruitedCompanions => Array.AsReadOnly(roster.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    public IReadOnlyCollection<string> ResolvedLocks => Array.AsReadOnly(resolved.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    public IReadOnlyCollection<string> VisitedTowns => Array.AsReadOnly(towns.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    public IReadOnlyList<string> ActiveCompanions => Array.AsReadOnly(active);
    public bool IsAtFinalDungeon => LocationId == campaign.FinalLocationId;
    public CapabilitySet HeldCapabilities => PermanentCapabilities.Union(CapabilitySet.From(
        campaign.Companions.Where(c => active.Contains(c.Id)).Select(c => c.Capability)));

    public CampaignSession(Campaign campaign)
    {
        this.campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        locations = campaign.Locations.ToDictionary(l => l.Id, StringComparer.Ordinal);
        LocationId = campaign.StartLocationId;
        if (!locations.TryGetValue(LocationId, out var start) || start.Kind != LocationKind.Town)
            throw new ArgumentException("A campaign must start in a town.", nameof(campaign));
        towns.Add(LocationId);
    }

    public bool TrySetParty(params string[] companionIds)
    {
        // One protagonist plus up to three companions. Recruitment is permanent; dismissal only changes active slots.
        if (locations[LocationId].Kind != LocationKind.Town || companionIds == null || companionIds.Length > 3 ||
            companionIds.Distinct(StringComparer.Ordinal).Count() != companionIds.Length || companionIds.Any(id => !roster.Contains(id))) return false;
        active = companionIds.ToArray();
        return true;
    }

    public bool TryMove(string routeId)
    {
        var route = campaign.Routes.FirstOrDefault(r => r.Id == routeId);
        var destination = route?.Other(LocationId);
        if (route == null || destination == null || !route.CanTraverse(HeldCapabilities, resolved)) return false;
        if (route.Latches) resolved.Add(route.Id);
        LocationId = destination;
        if (locations[destination].Kind == LocationKind.Town) towns.Add(destination);
        return true;
    }

    public bool TryClaimSource(string sourceId)
    {
        var source = campaign.Sources.FirstOrDefault(s => s.Id == sourceId);
        if (source == null || source.LocationId != LocationId || claimed.Contains(sourceId) ||
            !source.Prerequisites.IsSatisfiedBy(HeldCapabilities)) return false;
        if (source.Capability.Kind() == CapabilityKind.Personal)
        {
            if (source.CompanionId == null || !campaign.Companions.Any(c => c.Id == source.CompanionId && c.Capability == source.Capability)) return false;
            roster.Add(source.CompanionId);
        }
        else PermanentCapabilities = PermanentCapabilities.Union(CapabilitySet.Of(source.Capability));
        claimed.Add(sourceId);
        return true;
    }

    public bool TryFastTravel(string townId)
    {
        if (!towns.Contains(townId)) return false;
        LocationId = townId;
        return true;
    }

    public void ReturnAfterDefeat()
    {
        // Host combat losses must not alter campaign state. Start is always a visited, safe town.
        LocationId = campaign.StartLocationId;
    }
}
