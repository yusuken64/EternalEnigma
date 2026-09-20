using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.Validation;

public sealed class AcquisitionWitness
{
    public string SourceId { get; }
    public string FromTownId { get; }
    public CapabilitySet PartyCapabilities { get; }
    public IReadOnlyList<string> RouteIds { get; }
    internal AcquisitionWitness(string sourceId, string townId, CapabilitySet party, IEnumerable<string> routes)
    { SourceId = sourceId; FromTownId = townId; PartyCapabilities = party; RouteIds = Array.AsReadOnly(routes.ToArray()); }
}

public sealed class ExplorationResult
{
    public IReadOnlyCollection<string> ReachableLocations { get; }
    public IReadOnlyCollection<string> AcquiredSources { get; }
    public IReadOnlyList<AcquisitionWitness> Witnesses { get; }
    internal ExplorationResult(IEnumerable<string> locations, IEnumerable<string> sources, IEnumerable<AcquisitionWitness> witnesses)
    {
        ReachableLocations = Array.AsReadOnly(locations.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        AcquiredSources = Array.AsReadOnly(sources.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Witnesses = Array.AsReadOnly(witnesses.ToArray());
    }
}

/// <summary>
/// Monotone logical exploration. Each excursion starts at a visited town with a fixed legal party.
/// Bidirectional routes and unrestricted travel to visited towns let excursions be composed.
/// This is not a terrain, encounter or player-skill proof.
/// </summary>
public static class CampaignExplorer
{
    public static ExplorationResult Explore(Campaign campaign, bool guaranteedOnly = false,
        bool criticalOnly = false, int maxPersonalCapabilities = 3, string? excludedCompanionId = null)
    {
        if (maxPersonalCapabilities < 0 || maxPersonalCapabilities > 3) throw new ArgumentOutOfRangeException(nameof(maxPersonalCapabilities));
        var byLocation = campaign.Locations.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var manifest = campaign.Manifest.ToDictionary(c => c.Id);
        var adjacency = campaign.Locations.ToDictionary(l => l.Id,
            l => campaign.Routes.Where(r => r.From == l.Id || r.To == l.Id).OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var towns = new HashSet<string>(StringComparer.Ordinal) { campaign.StartLocationId };
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var sources = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        var ownedPersonal = CapabilitySet.Empty;
        var permanent = CapabilitySet.Empty;
        var witnesses = new List<AcquisitionWitness>();
        bool changed;
        do
        {
            changed = false;
            // Snapshot capability choices; new recruits are available on the next round, only at towns.
            foreach (var party in Parties(ownedPersonal, maxPersonalCapabilities))
            foreach (var town in towns.OrderBy(x => x, StringComparer.Ordinal).ToArray())
            {
                var held = permanent.Union(party);
                var paths = new Dictionary<string, string[]>(StringComparer.Ordinal) { [town] = Array.Empty<string>() };
                var queue = new Queue<string>();
                queue.Enqueue(town);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (reachable.Add(current)) changed = true;
                    if (byLocation[current].Kind == LocationKind.Town && towns.Add(current)) changed = true;
                    foreach (var source in campaign.Sources.Where(s => s.LocationId == current).OrderBy(s => s.Id, StringComparer.Ordinal))
                    {
                        if (sources.Contains(source.Id) || (guaranteedOnly && !source.Guaranteed) ||
                            (excludedCompanionId != null && source.CompanionId == excludedCompanionId) ||
                            (criticalOnly && manifest[source.Capability].Role != CapabilityRole.Critical) ||
                            !source.Prerequisites.IsSatisfiedBy(held)) continue;
                        sources.Add(source.Id);
                        changed = true;
                        if (source.Capability.Kind() == CapabilityKind.Personal)
                            ownedPersonal = ownedPersonal.Union(CapabilitySet.Of(source.Capability));
                        else permanent = permanent.Union(CapabilitySet.Of(source.Capability));
                        held = permanent.Union(party);
                        witnesses.Add(new AcquisitionWitness(source.Id, town, party, paths[current]));
                    }
                    foreach (var route in adjacency[current])
                    {
                        if (!route.CanTraverse(held, resolved)) continue;
                        if (route.Latches && resolved.Add(route.Id)) changed = true;
                        string next = route.Other(current)!;
                        if (paths.ContainsKey(next)) continue;
                        paths.Add(next, paths[current].Concat(new[] { route.Id }).ToArray());
                        queue.Enqueue(next);
                    }
                }
            }
        } while (changed);
        return new ExplorationResult(reachable, sources, witnesses);
    }

    private static IEnumerable<CapabilitySet> Parties(CapabilitySet owned, int limit)
    {
        var values = owned.Values.ToArray();
        for (int bits = 0; bits < (1 << values.Length); bits++)
        {
            var party = CapabilitySet.From(values.Where((_, index) => (bits & (1 << index)) != 0));
            if (party.Count <= limit) yield return party;
        }
    }
}
