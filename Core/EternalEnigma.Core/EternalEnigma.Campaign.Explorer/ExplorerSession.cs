using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using CampaignDefinition = EternalEnigma.Core.Progression.Campaign;

namespace EternalEnigma.ConsoleExplorer;

/// <summary>Console host progression at physical tiles; rewards simulate completed encounters.</summary>
public sealed class ExplorerSession
{
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
    public bool IsActive(string id) => active.Contains(id);
    public bool IsWalkable(GridPoint point) => Grid.IsWalkable(point, Held, resolved);

    public ExplorerSession(CampaignDefinition campaign, OverworldGrid grid)
    {
        Campaign = campaign;
        Grid = grid;
        if (grid.CampaignFingerprint != Core.Generation.CampaignFingerprint.Compute(campaign))
            throw new ArgumentException("Grid does not belong to this campaign.", nameof(grid));
        locations = campaign.Locations.ToDictionary(l => grid.Locations[l.Id]);
        routes = campaign.Routes.ToDictionary(r => r.Id);
        Position = grid.PlayerStart;
        towns.Add(campaign.StartLocationId);
    }

    public bool Move(int dx, int dy)
    {
        var next = new GridPoint(Position.X + dx, Position.Y + dy);
        if (!Grid.CanStep(Position, next, Held, resolved))
        {
            var gate = Grid.LockAt(next);
            Message = gate == null ? "Blocked." : "Requires: " + routes[gate.RouteId].Requirement;
            return false;
        }
        Position = next;
        var crossed = Grid.LockAt(next);
        if (crossed != null && routes[crossed.RouteId].Latches) resolved.Add(crossed.RouteId);
        if (Location?.Kind == LocationKind.Town) towns.Add(Location.Id);
        Message = Location == null ? "" : Location.Id == Campaign.FinalLocationId
            ? "You reached the final dungeon! Esc to quit, or keep exploring."
            : $"{Location.Id} ({Location.Kind}) | Enter: claim rewards";
        return true;
    }

    public void ClaimRewards()
    {
        var rewards = new List<string>();
        foreach (var source in Campaign.Sources.Where(s => s.LocationId == Location?.Id && !claimed.Contains(s.Id)))
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
