using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Progression;

public static class CampaignGuidance
{
    public static IEnumerable<Capability> Acquired(CampaignContext context) => context.Permanent.Values
        .Concat(context.Campaign.Companions.Where(c => context.Roster.Contains(c.Id)).Select(c => c.Capability))
        .Distinct().OrderBy(c => c.DisplayName(), StringComparer.Ordinal);

    public static string CapabilitySource(CampaignContext context, Capability capability, Func<string, string>? companionName = null)
    {
        if (context.Permanent.Contains(capability)) return "Permanent";
        return string.Join(" or ", context.Campaign.Companions
            .Where(c => c.Capability == capability && context.Roster.Contains(c.Id))
            .Select(c => companionName?.Invoke(c.Id) ?? c.Id).Distinct());
    }

    public static string Bearing(GridPoint from, GridPoint to)
    {
        if (from.Equals(to)) return "here";
        string[] names = { "east", "northeast", "north", "northwest", "west", "southwest", "south", "southeast" };
        int sector = (int)Math.Floor(Math.Atan2(to.Y - from.Y, to.X - from.X) / (Math.PI / 4) + .5);
        return names[(sector + 8) % 8];
    }

    public static string LocationName(CampaignContext context, string locationId)
    {
        if (locationId == context.BookDungeon.Id) return context.BookDungeon.Title;
        var location = context.Campaign.Locations.Single(l => l.Id == locationId);
        string anchor = location.ParentTownId ?? location.Id;
        var region = context.Campaign.Regions.Single(r => r.Id == location.RegionId);
        return location.Kind == LocationKind.Town ? context.GetTownDisplayName(location.Id) :
            location.Encounter != null ? region.DisplayName + " " + location.Encounter.DisplayName.ToLowerInvariant() :
            location.ParentTownId != null ? "the dungeon in " + context.GetTownDisplayName(anchor) : DungeonName(location, region);
    }

    // Place names, not design categories: "the Keep of the Harrowmark" rather than "story dungeon".
    private static string DungeonName(CampaignLocation location, CampaignRegion region) => location.Kind switch
    {
        LocationKind.StoryDungeon => "the " + Landmark(region.Theme) + " of " + region.DisplayName,
        LocationKind.RepeatableDungeon => "the Depths of " + region.DisplayName,
        LocationKind.FinalDungeon => "the Inkbound Town",
        _ => region.DisplayName + " " + location.Kind.ToString().ToLowerInvariant()
    };

    private static string Landmark(string theme) => theme switch
    {
        Generation.LockSkinCatalog.Highlands => "Keep",
        Generation.LockSkinCatalog.Marsh => "Sunken Halls",
        Generation.LockSkinCatalog.Coast => "Sea Caves",
        Generation.LockSkinCatalog.Forest => "Barrow",
        Generation.LockSkinCatalog.Desert => "Tomb",
        Generation.LockSkinCatalog.Tundra => "Ice Caverns",
        Generation.LockSkinCatalog.Ruins => "Vaults",
        Generation.LockSkinCatalog.Volcanic => "Forge",
        _ => "Halls"
    };

    public static string Location(CampaignContext context, string townId, string locationId)
    {
        var location = context.ResolveDungeonLocation(locationId) ?? context.Campaign.Locations.Single(l => l.Id == locationId);
        string anchor = location.ParentTownId ?? location.Id;
        string name = LocationName(context, locationId);
        if (anchor == townId) return name + " here in town, in the " + context.Grid.RegionBiomes[location.RegionId];
        return name + " in the " + context.Grid.RegionBiomes[location.RegionId] + ", " +
            Bearing(context.Grid.Locations[townId], context.Grid.Locations[anchor]) + " from here";
    }

    public static string TownHint(CampaignContext context, string townId, Func<string, string>? companionName = null)
    {
        if (townId == context.Campaign.StartLocationId && context.IsPreparingForOpening)
            return context.OpeningObjective;
        var route = context.Campaign.Routes.Where(r => r.HasGate && (r.Required || r.IsTownExit && r.From == townId) && context.Gates.NeedsOpening(r))
            .OrderBy(r => r.IsTownExit && r.From == townId ? 0 : 1)
            .ThenBy(r => context.Campaign.Locations.Single(l => l.Id == r.To).Stage)
            .ThenBy(r => context.Campaign.Regions.Single(region => region.Id == context.Campaign.Locations.Single(l => l.Id == r.To).RegionId).ProgressionOrder)
            .FirstOrDefault();
        return route == null ? "" : DescribeLock(context, townId, route, companionName);
    }

    internal static string DescribeLock(CampaignContext context, string townId, CampaignRoute route, Func<string, string>? companionName = null)
    {
        if (route.CompletionLocationId != null && !context.Completed.Contains(route.CompletionLocationId))
            return ClearLocationHint(context, townId, route.CompletionLocationId, companionName);
        if (route.ShortcutKind == ShortcutKind.FarSide)
            return "Open the shortcut at " + HintLocation(context, townId, route.UnlockingEndpoint!) + ".";
        if (route.KeyId != null)
            return context.Keys.Contains(route.KeyId)
                ? "Use " + context.Campaign.KeyLabel(route.KeyId) + " at " + GateLocation(context, townId, route) + "."
                : route.KeyCondition == KeyAcquisition.DungeonCompletion
                    ? ClearLocationHint(context, townId, route.KeyLocationId!, companionName, " to obtain " + context.Campaign.KeyLabel(route.KeyId))
                    : "Collect " + context.Campaign.KeyLabel(route.KeyId) + " at " + HintLocation(context, townId, route.KeyLocationId!) + ".";

        var held = context.Held;
        if (route.Requirement.IsSatisfiedBy(held))
        {
            var solution = route.Requirement.Alternatives.Where(held.ContainsAll).OrderBy(a => a.Count).First();
            return "Use " + string.Join(" and ", solution.Values.Select(c => CampaignRewardNames.Name(context, c))) +
                " at " + GateLocation(context, townId, route) + ".";
        }
        return NextRequirementHint(context, townId, route.Requirement, companionName, new HashSet<Capability>()) ?? "";
    }

    private static string? NextRequirementHint(CampaignContext context, string townId, Requirement requirement,
        Func<string, string>? companionName, HashSet<Capability> visiting)
    {
        var held = context.Held;
        var acquired = CapabilitySet.From(Acquired(context));
        // Prefer a solution the player already owns, then give just its next missing step.
        foreach (var solution in requirement.Alternatives
            .OrderBy(a => a.Values.Count(c => !acquired.Contains(c)))
            .ThenBy(a => a.Values.Count(c => !held.Contains(c))).ThenBy(a => a.Count))
        foreach (var capability in solution.Values.Where(c => !held.Contains(c)).OrderBy(c => acquired.Contains(c) ? 0 : 1))
        {
            if (!visiting.Add(capability)) continue;
            try
            {
                var companion = context.Campaign.Companions.FirstOrDefault(c => c.Capability == capability && context.Roster.Contains(c.Id));
                if (companion != null)
                    return "Add " + (companionName?.Invoke(companion.Id) ?? companion.Id) + " to your party here in town to use " +
                        CampaignRewardNames.Name(context, capability) + ".";

                foreach (var source in context.Campaign.Sources.Where(s => s.Capability == capability && !context.Claimed.Contains(s.Id))
                    .OrderByDescending(s => s.Prerequisites.IsSatisfiedBy(held))
                    .ThenBy(s => context.Campaign.Locations.Single(l => l.Id == s.LocationId).Stage))
                {
                    if (!source.Prerequisites.IsSatisfiedBy(held))
                    {
                        var prerequisite = NextRequirementHint(context, townId, source.Prerequisites, companionName, visiting);
                        if (prerequisite != null) return prerequisite;
                        continue;
                    }
                    string reward = CampaignRewardNames.Name(context, capability);
                    string name = source.CompanionId == null ? reward : companionName?.Invoke(source.CompanionId) ?? source.CompanionId;
                    var location = context.Campaign.Locations.Single(l => l.Id == source.LocationId);
                    if (location.IsDungeon && !context.Completed.Contains(location.Id))
                        return ClearLocationHint(context, townId, location.Id, companionName,
                            source.CompanionId == null ? " to obtain " + reward : " to recruit " + name, visiting);
                    return (source.CompanionId == null ? "Collect " : "Recruit ") + name + " at " + HintLocation(context, townId, source.LocationId) + ".";
                }
            }
            finally { visiting.Remove(capability); }
        }
        return null;
    }

    private static string ClearLocationHint(CampaignContext context, string townId, string locationId,
        Func<string, string>? companionName, string purpose = "", HashSet<Capability>? visiting = null)
    {
        var location = context.Campaign.Locations.Single(l => l.Id == locationId);
        if (location.Encounter != null && !location.Encounter.EntryRequirement.IsSatisfiedBy(context.Held))
        {
            var prerequisite = NextRequirementHint(context, townId, location.Encounter.EntryRequirement, companionName, visiting ?? new HashSet<Capability>());
            if (prerequisite != null) return prerequisite;
        }
        string action = location.Encounter?.Kind == EncounterKind.Bridge ? "Reach the far end of " : "Clear ";
        return action + HintLocation(context, townId, locationId) + purpose + ".";
    }

    private static string HintLocation(CampaignContext context, string townId, string locationId)
    {
        var location = context.Campaign.Locations.Single(l => l.Id == locationId);
        string anchor = location.ParentTownId ?? location.Id;
        return LocationName(context, locationId) + (anchor == townId ? " here in town" :
            ", " + Bearing(context.Grid.Locations[townId], context.Grid.Locations[anchor]) + " from here");
    }

    private static string GateLocation(CampaignContext context, string townId, CampaignRoute route)
    {
        if (route.IsTownExit) return route.From == townId ? "the town gate here in town" : "the town gate in " + HintLocation(context, townId, route.From);
        var town = context.Grid.Locations[townId];
        var gate = context.Grid.Locks.Where(g => g.RouteId == route.Id).SelectMany(g => g.Cells)
            .OrderBy(p => Math.Abs(p.X - town.X) + Math.Abs(p.Y - town.Y))
            .DefaultIfEmpty(context.Grid.Locations[route.From]).First();
        return "the gate toward " + LocationName(context, route.To) + ", " + Bearing(town, gate) + " from here";
    }
}
