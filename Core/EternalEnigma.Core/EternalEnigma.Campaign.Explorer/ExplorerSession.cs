using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using CampaignDefinition = EternalEnigma.Core.Progression.Campaign;

namespace EternalEnigma.ConsoleExplorer;

/// <summary>Console host progression at physical tiles; rewards simulate completed encounters.</summary>
public sealed class ExplorerSession
{
    private readonly HashSet<string> completed = new();
    private readonly HashSet<string> claimed = new();
    private readonly HashSet<string> recruited = new();
    private readonly HashSet<string> active = new();
    private readonly HashSet<string> resolved = new();
    private readonly HashSet<string> towns = new();
    private CapabilitySet permanent = CapabilitySet.Empty;
    private readonly Dictionary<GridPoint, CampaignLocation> locations;
    private readonly Dictionary<string, CampaignRoute> routes;
    public CampaignDefinition Campaign { get; }
    public OverworldGrid Grid { get; }
    public GridPoint Position { get; private set; }
    public CampaignLocation? Location => locations.GetValueOrDefault(Position);
    public string Message { get; private set; } = "Explore with arrows/WASD. Stand on a location and press Enter to claim its reward.";
    public CapabilitySet Held => permanent.Union(CapabilitySet.From(Campaign.Companions.Where(c => active.Contains(c.Id)).Select(c => c.Capability)));
    public IReadOnlyList<CampaignCompanion> Roster => Campaign.Companions.Where(c => recruited.Contains(c.Id)).ToArray();
    public IReadOnlyList<string> VisitedTowns => towns.OrderBy(t => t, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> CollectedKeys => gates.CollectedKeys.ToArray();
    private readonly OverworldGates gates;
    public bool IsActive(string id) => active.Contains(id);
    public bool IsWalkable(GridPoint point) => gates.IsWalkable(point, Held);
    public IReadOnlyList<CampaignRoute> WarpsHere => Grid.WarpsAt(Position).ToArray();
    public string WarpLabel(CampaignRoute route) => "Warp to biome " + Campaign.Regions.Single(r => r.Id == Campaign.Locations.Single(l => l.Id == route.Other(Location!.Id)).RegionId).Label +
        (route.CanTraverse(Held, resolved) ? "" : " | " + gates.Hint(route, Held));
    public bool Warp(string routeId)
    {
        if (!Grid.TryWarp(routeId, Position, Held, resolved, out var destination))
        { Message = WarpsHere.FirstOrDefault(r => r.Id == routeId) is CampaignRoute blocked ? gates.Hint(blocked, Held) : "Stand on a warp gate."; return false; }
        Position = destination;
        Message = "Warped to " + Location!.Id + ". V: warp destinations";
        return true;
    }

    public ExplorerSession(CampaignDefinition campaign, OverworldGrid grid)
    {
        Campaign = campaign;
        Grid = grid;
        gates = new OverworldGates(campaign, grid, resolved, completed: completed);
        if (grid.CampaignFingerprint != Core.Generation.CampaignFingerprint.Compute(campaign))
            throw new ArgumentException("Grid does not belong to this campaign.", nameof(grid));
        locations = campaign.Locations.Where(l => l.ParentTownId == null).ToDictionary(l => grid.Locations[l.Id]);
        routes = campaign.Routes.ToDictionary(r => r.Id);
        Position = grid.PlayerStart;
        towns.Add(campaign.StartLocationId);
    }

    /// <summary>Debug flight: ignores gates, water and terrain. Foreshadows the airship, which will fly over the same grid.</summary>
    public bool NoClip { get; private set; }
    public void ToggleNoClip() { NoClip = !NoClip; Message = NoClip ? "No-clip enabled: ignoring gates, water and terrain." : "No-clip disabled."; }

    public bool Move(int dx, int dy)
    {
        var next = new GridPoint(Position.X + dx, Position.Y + dy);
        if (NoClip)
        {
            if (!Grid.Contains(next)) { Message = "Edge of the map."; return false; }
            Position = next;
            Message = Location == null ? "No-clip | " + Position : $"No-clip | {Location.Id} ({Location.Kind})";
            return true;
        }
        if (!Grid.CanStep(Position, next, Held, resolved) || !OverworldMovement.CanStep(Position, next, IsWalkable))
        {
            var gate = Grid.LockAt(next);
            var blockingExit = gate == null ? Campaign.Routes.FirstOrDefault(r => r.IsTownExit && Grid.Locations[r.From].Equals(Position) && !r.CanTraverse(Held, resolved)) : null;
            Message = Grid.RequiresBoat(next) && !Held.Contains(Capability.Boat) ? "Requires Boat to sail." :
                gate != null ? gates.Hint(routes[gate.RouteId], Held) :
                blockingExit != null ? gates.Hint(blockingExit, Held) : "Blocked.";
            return false;
        }
        Position = next;
        var crossed = Grid.LockAt(next);
        if (crossed != null && routes[crossed.RouteId].Latches) resolved.Add(crossed.RouteId);
        if (Location?.Kind == LocationKind.Town) towns.Add(Location.Id);
        Message = Location == null ? "" : Location.Id == Campaign.FinalLocationId
            ? "You reached the final dungeon! Esc to quit, or keep exploring."
            : $"{Location.Id} ({Location.Kind}) | Enter: claim rewards";
        var nearby = OverworldMovement.Neighbors(Position).Select(Grid.LockAt).Where(g => g != null).Select(g => g!.RouteId).Distinct();
        foreach (string id in nearby)
        {
            var route = routes[id];
            if (!gates.NeedsOpening(route)) continue;
            Message += " | " + gates.Hint(route, Held);
        }
        if (Campaign.Routes.Any(r => r.UnlockingEndpoint == Location?.Id && !resolved.Contains(r.Id))) Message += " | Enter: Open shortcut";
        foreach (var warp in WarpsHere) Message += " | V: " + WarpLabel(warp);
        foreach (var route in Campaign.Routes.Where(r => r.KeyLocationId != null && r.KeyLocationId == Location?.Id && !gates.HasKey(r)))
            Message += " | Enter: Collect " + route.KeyId;
        return true;
    }

    public string RequiredReturn => string.Join("; ", Campaign.ReturnObjectives.Where(o => o.Required).Select(o =>
        $"Return to {o.RegionId}: {o.EnablingCapability} -> {o.RewardCapability}"));
    public bool OpenGate(string? routeId = null)
    {
        foreach (var route in gates.Nearby(Position))
            if ((routeId == null || route.Id == routeId) && gates.TryOpen(route.Id, Position, Held))
            { Message = "Opened gate: " + route.Id + ". Used " + (route.KeyId ?? route.Requirement.ToString()) + "."; return true; }
        return false;
    }

    public void ClaimRewards()
    {
        if (OpenGate()) return;
        foreach (var route in Campaign.Routes)
            if (route.TryUnlock(Location?.Id ?? "", resolved)) { Message = "Opened shortcut: " + route.Id; return; }
        var rewardLocation = Campaign.Locations.FirstOrDefault(l => l.ParentTownId != null && l.ParentTownId == Location?.Id) ?? Location;
        if (rewardLocation?.Kind == LocationKind.StoryDungeon || rewardLocation?.Kind == LocationKind.RepeatableDungeon || rewardLocation?.Kind == LocationKind.FinalDungeon) completed.Add(rewardLocation.Id);
        var rewards = new List<string>();
        foreach (var route in Campaign.Routes)
            if (gates.CollectKey(route, rewardLocation?.Id ?? "")) rewards.Add(route.KeyId + " (use it at the gate)");
        foreach (var route in Campaign.Routes.Where(r => r.IsTownExit && gates.HasKey(r))) resolved.Add(route.Id);
        foreach (var source in Campaign.Sources.Where(s => s.LocationId == rewardLocation?.Id && !claimed.Contains(s.Id)))
        {
            if (!source.Prerequisites.IsSatisfiedBy(Held)) continue;
            if (source.Capability.Kind() == CapabilityKind.Personal)
            {
                if (source.CompanionId == null) continue;
                recruited.Add(source.CompanionId);
                rewards.Add(source.Capability + " recruited (equip at town: P)");
            }
            else { permanent = permanent.Union(CapabilitySet.Of(source.Capability)); rewards.Add(source.Capability.ToString()); }
            claimed.Add(source.Id);
        }
        Message = rewards.Count == 0 ? "No unclaimed eligible rewards here." : "Reward: " + string.Join(", ", rewards);
    }

    public bool ToggleCompanion(string id)
    {
        if (Location?.Kind != LocationKind.Town) { Message = "Party changes require standing on a town."; return false; }
        if (!recruited.Contains(id)) return false;
        if (!active.Remove(id))
        {
            if (active.Count == 3) { Message = "Three companion slots are full. Dismiss one first."; return false; }
            active.Add(id);
        }
        Message = "Party updated.";
        return true;
    }

    public bool FastTravel(string id)
    {
        if (!towns.Contains(id)) return false;
        Position = Grid.Locations[id];
        Message = "Arrived at " + id;
        return true;
    }
}
