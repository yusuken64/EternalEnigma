using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.World;

/// <summary>Physical gate interactions, separate from acquiring their requirements.</summary>
public sealed class OverworldGates
{
    private readonly OverworldGrid grid;
    private readonly Dictionary<string, CampaignRoute> routes;
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);
    private readonly HashSet<string> opened = new(StringComparer.Ordinal);
    private readonly ISet<string> resolved;

    public OverworldGates(Campaign campaign, OverworldGrid grid, ISet<string> resolved)
    { this.grid = grid; this.resolved = resolved; routes = campaign.Routes.ToDictionary(r => r.Id); }

    public IEnumerable<string> CollectedKeys => keys.OrderBy(k => k, StringComparer.Ordinal);
    public bool HasKey(CampaignRoute route) => route.KeyId != null && keys.Contains(route.KeyId);
    public bool CollectKey(CampaignRoute route, string locationId) =>
        route.ShortcutKind == ShortcutKind.Keyed && route.KeyLocationId == locationId && route.KeyId != null && keys.Add(route.KeyId);

    public bool IsWalkable(GridPoint cell, CapabilitySet held)
    {
        if (!grid.IsWalkable(cell, held, resolved)) return false;
        var gate = grid.LockAt(cell);
        // Open water remains a traversal capability, not an interactive door.
        return gate == null || grid.RequiresBoat(cell) || opened.Contains(gate.RouteId) || resolved.Contains(gate.RouteId);
    }

    public IEnumerable<CampaignRoute> Nearby(GridPoint position) => routes.Values.Where(route =>
        route.IsWarp ? grid.WarpsAt(position).Any(w => w.Id == route.Id) :
        grid.Locks.Any(g => g.RouteId == route.Id && g.Cells.Any(c =>
            !grid.RequiresBoat(c) && Math.Abs(c.X - position.X) + Math.Abs(c.Y - position.Y) == 1)));

    public bool NeedsOpening(CampaignRoute route) => !opened.Contains(route.Id) && !resolved.Contains(route.Id);
    public string Hint(CampaignRoute route, CapabilitySet held) => NeedsOpening(route) &&
        (route.ShortcutKind == ShortcutKind.Keyed ? HasKey(route) : route.CanTraverse(held, resolved))
        ? "Enter / A: Use " + (route.KeyId ?? route.Requirement.ToString()) + " to open " + route.Id
        : route.GateHint;

    public bool TryOpen(string routeId, GridPoint position, CapabilitySet held)
    {
        if (!routes.TryGetValue(routeId, out var route) || !NeedsOpening(route) ||
            !Nearby(position).Any(r => r.Id == routeId) || !IsWalkable(position, held)) return false;
        if (route.ShortcutKind == ShortcutKind.Keyed)
        {
            if (!HasKey(route)) return false;
            resolved.Add(route.Id);
        }
        else
        {
            if (!route.CanTraverse(held, resolved)) return false;
            if (route.Latches) resolved.Add(route.Id);
        }
        opened.Add(route.Id);
        return true;
    }
}
