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

    public static string Location(CampaignContext context, string townId, string locationId)
    {
        var location = context.Campaign.Locations.Single(l => l.Id == locationId);
        string anchor = location.ParentTownId ?? location.Id;
        var region = context.Campaign.Regions.Single(r => r.Id == location.RegionId);
        string name = location.Kind == LocationKind.Town ? context.GetTownDisplayName(location.Id) :
            location.ParentTownId != null ? "the dungeon in " + context.GetTownDisplayName(anchor) : region.DisplayName + " " + location.Kind.ToString().Replace("Dungeon", " dungeon").ToLowerInvariant();
        if (anchor == townId) return name + " here in town (" + context.Grid.RegionBiomes[location.RegionId] + ")";
        return name + " (" + context.Grid.RegionBiomes[location.RegionId] + ", " +
            Bearing(context.Grid.Locations[townId], context.Grid.Locations[anchor]) + " from here)";
    }

    public static string TownHint(CampaignContext context, string townId, Func<string, string>? companionName = null)
    {
        var route = context.Campaign.Routes.Where(r => r.HasGate && (r.Required || r.IsTownExit && r.From == townId) && context.Gates.NeedsOpening(r))
            .OrderBy(r => r.IsTownExit && r.From == townId ? 0 : 1)
            .ThenBy(r => context.Campaign.Locations.Single(l => l.Id == r.To).Stage)
            .ThenBy(r => context.Campaign.Regions.Single(region => region.Id == context.Campaign.Locations.Single(l => l.Id == r.To).RegionId).ProgressionOrder)
            .FirstOrDefault();
        return route == null ? "" : DescribeLock(context, townId, route, companionName);
    }

    internal static string DescribeLock(CampaignContext context, string townId, CampaignRoute route, Func<string, string>? companionName = null)
    {
        string obstacle = (route.LockText ?? route.GateHint) + " The way leads to " + Location(context, townId, route.To) + ". ";
        if (route.ShortcutKind == ShortcutKind.FarSide)
            return obstacle + "Open the shortcut from " + Location(context, townId, route.UnlockingEndpoint!) + ".";
        if (route.KeyId != null)
            return obstacle + (context.Keys.Contains(route.KeyId)
                ? "You have " + context.Campaign.KeyLabel(route.KeyId) + ". Try it at the gate."
                : "You need " + context.Campaign.KeyLabel(route.KeyId) + ", found " +
                    (route.KeyCondition == KeyAcquisition.DungeonCompletion ? "by clearing " : "at ") + Location(context, townId, route.KeyLocationId!) + ".");
        string Describe(Capability capability)
        {
            string name = capability.DisplayName();
            if (Acquired(context).Contains(capability))
                return context.Held.Contains(capability) ? name + " (already acquired; try it at the obstacle)" :
                    name + " (add " + CapabilitySource(context, capability, companionName) + " to your travelling party at a town)";
            var sources = context.Campaign.Sources.Where(s => s.Capability == capability).Select(s => Location(context, townId, s.LocationId)).Distinct();
            return name + " (acquire at " + string.Join(" or ", sources) + ")";
        }
        return obstacle + "Try " + string.Join("; alternatively, ", route.Requirement.Alternatives.Select(a =>
            (a.Count > 1 ? "both " : "") + string.Join(" and ", a.Values.Select(Describe)))) + ".";
    }
}
