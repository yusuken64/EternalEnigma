using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Validation;

public sealed class OverworldGridValidationResult
{
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;
    internal OverworldGridValidationResult(IEnumerable<string> errors) { Errors = Array.AsReadOnly(errors.ToArray()); }
}

/// <summary>
/// Compares the graph obtained by removing gates with the map's closed-gate components, then each gate's
/// contacts. Non-touching gate footprints make this decomposition valid for every combination of open gates.
/// </summary>
public static class OverworldGridValidator
{
    public static OverworldGridValidationResult Validate(Campaign campaign, OverworldGrid grid)
    {
        var errors = new List<string>();
        void Check(bool condition, string error) { if (!condition) errors.Add(error); }
        Check(grid.CampaignFingerprint == CampaignFingerprint.Compute(campaign), "campaign: Map was generated for a different campaign.");
        Check(grid.Locations.Count == campaign.Locations.Count && campaign.Locations.All(l => grid.Locations.ContainsKey(l.Id)), "locations: Missing/extra location placements.");
        Check(grid.Routes.Count == campaign.Routes.Count && campaign.Routes.All(r => grid.Routes.ContainsKey(r.Id)), "routes: Missing/extra route realizations.");
        Check(grid.Locks.Select(l => l.RouteId).Distinct(StringComparer.Ordinal).Count() == grid.Locks.Count, "locks: Duplicate lock footprints.");
        Check(campaign.Routes.Where(r => r.HasGate && !r.IsWarp && !r.IsTownExit).Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(grid.Locks.Select(l => l.RouteId).OrderBy(id => id, StringComparer.Ordinal)), "locks: Footprints do not match campaign locks.");
        foreach (var location in grid.Locations)
            Check(grid.IsGround(location.Value) && grid.LockAt(location.Value) == null && !grid.RequiresBoat(location.Value), $"location: {location.Key} is blocked, flooded or inside a lock.");
        foreach (var interior in campaign.Locations.Where(l => l.ParentTownId != null))
            Check(grid.Locations.TryGetValue(interior.Id, out var at) && grid.Locations.TryGetValue(interior.ParentTownId!, out var townAt) && at.Equals(townAt),
                $"interior.position: {interior.Id} must project onto its parent town.");
        Check(campaign.Locations.Where(l => l.Kind == LocationKind.Town && l.ParentTownId == null).Select(l => l.Id).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(grid.TownFootprints.Select(t => t.LocationId).OrderBy(id => id, StringComparer.Ordinal)), "towns: Missing/extra town footprints.");
        var townCells = new HashSet<GridPoint>();
        foreach (var town in grid.TownFootprints)
        {
            Check(grid.Locations.TryGetValue(town.LocationId, out var entrance) && entrance.Equals(town.Entrance), "town.entrance: Location must be the gateway.");
            foreach (var cell in town.Cells)
            {
                Check(grid.Contains(cell) && townCells.Add(cell), "town.footprint: Towns overlap or leave the map.");
                Check(grid.IsGround(cell) == cell.Equals(town.Entrance) && grid.LockAt(cell) == null && !grid.RequiresBoat(cell),
                    "town.blocked: Only the gateway may be ground; settlements cannot contain locks/water.");
                foreach (var layer in new[] { OverworldLayers.TownFootprints, OverworldLayers.TownWalls, OverworldLayers.TownInteriors })
                    Check(grid.Layers.TryGetValue(layer, out var mask) && grid.Contains(cell) && mask[cell.X, cell.Y] ==
                        (layer == OverworldLayers.TownFootprints || (layer == OverworldLayers.TownWalls ? town.Walls : town.Interior).Contains(cell)),
                        "town.layer: Footprint and render masks disagree.");
            }
            var approaches = OverworldMovement.Neighbors(town.Entrance)
                .Where(p => OverworldMovement.CanStep(p, town.Entrance, grid.IsGround)).ToArray();
            Check(approaches.Length == 1 && approaches[0].Equals(town.Approach) && !grid.RequiresBoat(town.Approach),
                "town.approach: Town must have exactly one dry outside entrance.");
        }
        foreach (var gate in grid.Locks)
        {
            Check(gate.Cells.Count > 0 && gate.Cells.Distinct().Count() == gate.Cells.Count && gate.Cells.All(grid.IsGround), $"lock: Invalid footprint for {gate.RouteId}.");
            foreach (var cell in gate.Cells)
                foreach (var neighbor in OverworldMovement.Neighbors(cell))
                {
                    var other = grid.LockAt(neighbor);
                    Check(other == null || other.RouteId == gate.RouteId, $"lock.contact: {gate.RouteId} touches a different gate.");
                }
        }
        if (errors.Count > 0) return new OverworldGridValidationResult(errors);

        // A component label for every ungated tile, using the actual eight-way movement rule.
        var components = new int[grid.Width, grid.Height];
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++) components[x, y] = -1;
        bool ClosedFloor(GridPoint p) => grid.IsGround(p) && grid.LockAt(p) == null;
        int count = 0;
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var first = new GridPoint(x, y);
                if (!ClosedFloor(first) || components[x, y] >= 0) continue;
                var queue = new Queue<GridPoint>(); queue.Enqueue(first); components[x, y] = count;
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    foreach (var next in OverworldMovement.Neighbors(at))
                        if (OverworldMovement.CanStep(at, next, ClosedFloor) && components[next.X, next.Y] < 0)
                        { components[next.X, next.Y] = count; queue.Enqueue(next); }
                }
                count++;
            }
        var parents = campaign.Locations.ToDictionary(l => l.Id, l => l.Id, StringComparer.Ordinal);
        string Root(string id)
        {
            while (parents[id] != id) { parents[id] = parents[parents[id]]; id = parents[id]; }
            return id;
        }
        foreach (var route in campaign.Routes.Where(r => (!r.HasGate || r.IsTownExit) && !r.IsWarp)) parents[Root(route.To)] = Root(route.From);
        var graphToMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var mapToGraph = new Dictionary<int, string>();
        foreach (var location in campaign.Locations)
        {
            var point = grid.Locations[location.Id]; int label = components[point.X, point.Y]; string root = Root(location.Id);
            Check(!graphToMap.TryGetValue(root, out int previousLabel) || previousLabel == label, $"connectivity: Open graph component containing {location.Id} is split on the map.");
            Check(!mapToGraph.TryGetValue(label, out string? previousRoot) || previousRoot == root, $"bypass: {location.Id} joins a different graph component with all locks closed.");
            graphToMap[root] = label; mapToGraph[label] = root;
        }
        Check(mapToGraph.Count == count, "connectivity: Ungated terrain contains an isolated component with no campaign location.");
        foreach (var gate in grid.Locks)
        {
            var route = campaign.Routes.Single(r => r.Id == gate.RouteId);
            var from = grid.Locations[route.From]; var to = grid.Locations[route.To];
            var expected = new HashSet<int> { components[from.X, from.Y], components[to.X, to.Y] };
            var contacts = new HashSet<int>();
            var visited = new HashSet<GridPoint> { gate.Cells[0] };
            var queue = new Queue<GridPoint>(); queue.Enqueue(gate.Cells[0]);
            bool GateOpen(GridPoint p) => grid.IsGround(p) && (grid.LockAt(p) == null || grid.LockAt(p)!.RouteId == gate.RouteId);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var next in OverworldMovement.Neighbors(at))
                {
                    if (!OverworldMovement.CanStep(at, next, GateOpen)) continue;
                    if (grid.LockAt(next) == null) contacts.Add(components[next.X, next.Y]);
                    else if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            Check(visited.Count == gate.Cells.Count, $"lock.connectivity: {gate.RouteId} footprint is disconnected.");
            Check(expected.SetEquals(contacts), $"lock.contacts: {gate.RouteId} connects the wrong map components.");
        }
        foreach (var region in campaign.Regions)
        {
            if (grid.RegionBiomes[region.Id] == OverworldBiome.Water) continue;
            int cells = 0, broad = 0;
            var mask = grid.Layers[OverworldLayers.Region(region.Id)];
            for (int y = 2; y < grid.Height - 2; y++) for (int x = 2; x < grid.Width - 2; x++)
            {
                if (!mask[x, y]) continue;
                cells++;
                bool interior = true;
                for (int dy = -2; dy <= 2 && interior; dy++) for (int dx = -2; dx <= 2; dx++)
                    if (!ClosedFloor(new GridPoint(x + dx, y + dy)) || grid.RequiresBoat(new GridPoint(x + dx, y + dy))) { interior = false; break; }
                if (interior) broad++;
            }
            Check(cells > 0 && broad >= cells * .3, $"layout.interior: {region.Id} has only {broad}/{cells} broad cells (requires 30%).");
        }
        var all = CapabilitySet.From(campaign.Manifest.Select(c => c.Id));
        foreach (var route in campaign.Routes)
        {
            var path = grid.Routes[route.Id];
            bool interiorRoute = campaign.Locations.Any(l => l.ParentTownId != null && route.Other(l.Id) == l.ParentTownId);
            Check(path.Count >= (interiorRoute ? 1 : 2) && path[0].Equals(grid.Locations[route.From]) && path[path.Count - 1].Equals(grid.Locations[route.To]), $"route.endpoints: {route.Id} has incorrect endpoints.");
            if (route.IsWarp)
            {
                Check(path.Count == 2 && grid.Warps.Any(w => w.Id == route.Id), $"warp.endpoints: {route.Id} must have exactly two landing pads.");
                continue;
            }
            for (int i = 1; i < path.Count; i++)
            {
                Check(grid.CanStep(path[i - 1], path[i], all, new HashSet<string>(campaign.Routes.Select(r => r.Id))), $"route.step: {route.Id} has an illegal grid step.");
                if (grid.RequiresBoat(path[i]))
                    Check(route.Form == LockForm.Area && route.Requirement.Alternatives.All(a => a.Contains(Capability.Boat)),
                        $"route.water: {route.Id} adds a Boat requirement not present in the campaign.");
            }
        }
        var occupied = Enumerable.Range(0, grid.Width * grid.Height).Select(i => new GridPoint(i % grid.Width, i / grid.Width)).Where(grid.IsGround).ToArray();
        int spanX = occupied.Max(p => p.X) - occupied.Min(p => p.X) + 1, spanY = occupied.Max(p => p.Y) - occupied.Min(p => p.Y) + 1;
        Check(Math.Max(spanX, spanY) <= 1.5 * Math.Min(spanX, spanY), "layout.aspect: Occupied terrain exceeds aspect ratio 1.5.");
        var unlocked = new HashSet<string>(campaign.Routes.Select(r => r.Id));
        foreach (var route in campaign.Routes.Where(r => r.ShortcutKind != ShortcutKind.None && !r.IsStarterExit))
        {
            int before = Distance(grid, grid.Locations[route.From], grid.Locations[route.To], all, unlocked, route.Id);
            int after = Distance(grid, grid.Locations[route.From], grid.Locations[route.To], all, unlocked);
            Check(before > 0 && after > 0 && after * 4 <= before * 3, $"shortcut.savings: {route.Id} saves less than 25% ({before} -> {after}).");
        }
        return new OverworldGridValidationResult(errors.Distinct());
    }
    public static int Distance(OverworldGrid grid, GridPoint from, GridPoint to, CapabilitySet held, ISet<string> resolved, string? blockedRoute = null)
    {
        var distance = new Dictionary<GridPoint, int> { [from] = 0 };
        var queue = new Queue<GridPoint>(); queue.Enqueue(from);
        bool Walkable(GridPoint p) => grid.IsWalkable(p, held, resolved) && (blockedRoute == null || grid.LockAt(p)?.RouteId != blockedRoute);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            if (at.Equals(to)) return distance[at];
            foreach (var next in OverworldMovement.Neighbors(at).Where(p => OverworldMovement.CanStep(at, p, Walkable))
                .Concat(grid.WarpDestinations(at, held, resolved, blockedRoute)))
                if (!distance.ContainsKey(next))
                { distance[next] = distance[at] + 1; queue.Enqueue(next); }
        }
        return -1;
    }

}
