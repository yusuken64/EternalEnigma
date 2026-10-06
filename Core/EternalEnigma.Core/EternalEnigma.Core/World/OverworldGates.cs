using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.World;

/// <summary>Physical gate interactions, separate from acquiring their requirements.</summary>
public sealed class OverworldGates
{
    private readonly OverworldGrid grid;
    private readonly Dictionary<string, CampaignRoute> routes;
    private readonly ISet<string> keys;
    private readonly ISet<string> opened;
    private readonly ISet<string> completed;
    private readonly ISet<string> resolved;

    public OverworldGates(Campaign campaign, OverworldGrid grid, ISet<string> resolved, ISet<string>? keys = null, ISet<string>? opened = null, ISet<string>? completed = null)
    { this.keys = keys ?? new HashSet<string>(); this.opened = opened ?? new HashSet<string>(); this.completed = completed ?? new HashSet<string>(); this.grid = grid; this.resolved = resolved; routes = campaign.Routes.ToDictionary(r => r.Id); }

    public IEnumerable<string> CollectedKeys => keys.OrderBy(k => k, StringComparer.Ordinal);
    public bool HasKey(CampaignRoute route) => route.KeyId != null && keys.Contains(route.KeyId);
    public bool CollectKey(CampaignRoute route, string locationId) =>
        route.ShortcutKind == ShortcutKind.Keyed && route.KeyLocationId == locationId && route.KeyId != null && (route.KeyCondition == KeyAcquisition.AtLocation || completed.Contains(locationId)) && keys.Add(route.KeyId);

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
    public LockInteractionSession? BeginInteraction(string routeId, Func<GridPoint> position, Func<CapabilitySet> held, Func<bool>? current = null)
    {
        if (!routes.TryGetValue(routeId, out var route) || !route.HasGate || route.ShortcutKind == ShortcutKind.FarSide) return null;
        bool Valid() => (current?.Invoke() ?? true) && NeedsOpening(route) &&
            Nearby(position()).Any(r => r.Id == routeId) && IsWalkable(position(), held());
        if (!Valid()) return null;
        return new LockInteractionSession(route, Valid, action => action switch
        {
            CapabilityLockAction capability => held().Contains(capability.Capability),
            KeyLockAction key => keys.Contains(key.KeyId),
            _ => false
        }, () =>
        {
            if (!Valid()) return false;
            if (route.ShortcutKind == ShortcutKind.Keyed || route.Latches) resolved.Add(route.Id);
            return opened.Add(route.Id);
        });
    }
    public string Hint(CampaignRoute route, CapabilitySet held) => NeedsOpening(route)
        ? "Enter / A: Interact: " + route.GateHint : route.GateHint;

    /// <summary>What the party opens a gate with: the key's name, or the cheapest satisfied way through.</summary>
    public static string UsedLabel(CampaignRoute route, CapabilitySet held)
    {
        if (route.ShortcutKind == ShortcutKind.Keyed) return route.KeyLabel ?? route.Id;
        var used = route.Requirement.Alternatives.Where(held.ContainsAll).OrderBy(a => a.Count).FirstOrDefault();
        return route.Requirement.IsOpen || used.Count == 0 ? route.Id : string.Join(" + ", used.Values.Select(c => c.DisplayName()));
    }

    /// <summary>Message after a gate opens. The fiction leads; the ability or key used follows.</summary>
    public static string OpenedMessage(CampaignRoute route, CapabilitySet held) =>
        "Opened gate: " + (route.LockText ?? route.Id + ".") + " Used " + UsedLabel(route, held) + ".";

    /// <summary>Compatibility for console callers: submit explicit actions through the same evaluator.</summary>
    public bool TryOpen(string routeId, GridPoint position, CapabilitySet held)
    {
        using var session = BeginInteraction(routeId, () => position, () => held);
        if (session == null) return false;
        foreach (var key in keys)
            if (session.Attempt(new KeyLockAction(key)) == LockOutcome.Opened) return true;
        foreach (var capability in held.Values)
            if (session.Attempt(new CapabilityLockAction(capability)) == LockOutcome.Opened) return true;
        return false;
    }
}
