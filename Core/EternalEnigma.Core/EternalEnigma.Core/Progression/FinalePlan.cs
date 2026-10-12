using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Progression;

/// <summary>Two immutable maps; the original campaign and its progression are never replaced.</summary>
public sealed class FinalePlan
{
    public const int Version = 4, FloorCount = 2, CombatDifficulty = 38;
    public Campaign Campaign { get; }
    public IReadOnlyList<FinaleFloorPlan> Floors { get; }

    public FinalePlan(Campaign campaign, OverworldGrid grid, bool hasBoat = true, IEnumerable<string>? earnedWarps = null)
    {
        if (grid.CampaignFingerprint != CampaignFingerprint.Compute(campaign)) throw new ArgumentException("The finale must use its campaign's grid.");
        Campaign = campaign;
        var floors = new List<FinaleFloorPlan>();
        int townFamily = 0, dungeonFamily = 0;
        for (int number = 1; number <= FloorCount; number++)
        {
            for (int attempt = 0; ; attempt++)
            {
                var mapCampaign = number == 1 ? campaign : CampaignGenerator.GenerateVersion(
                    CampaignContext.LocationSeed(campaign.Seed, attempt == 0 ? "finale-floor" : "finale-floor-retry-" + attempt, number), campaign.GeneratorVersion);
                var map = number == 1 ? grid : OverworldGridGenerator.Generate(mapCampaign);
                int town = townFamily, dungeon = dungeonFamily;
                try
                {
                    floors.Add(new FinaleFloorPlan(number, mapCampaign, map, hasBoat,
                        number == 1 ? earnedWarps : null, ref town, ref dungeon));
                    townFamily = town; dungeonFamily = dungeon; break;
                }
                catch (InvalidOperationException) when (number > 1 && attempt < 63) { }
            }
        }
        Floors = Array.AsReadOnly(floors.ToArray());
        var roster = Floors.SelectMany(f => f.Enemies).Where(e => e.IsMimic).Select(e => e.Family).ToHashSet();
        if (FinaleRoster.Required.Any(f => !roster.Contains(f))) throw new InvalidOperationException("Incomplete finale mimic roster.");
    }
}

public sealed class FinaleEnemyPlacement
{
    public string Id { get; }
    public string RegionId { get; }
    public GridPoint Position { get; }
    public int Roll { get; }
    public string? LocationId { get; }
    public string? Family { get; }
    public bool IsTown { get; }
    public bool IsMimic => LocationId != null;
    public bool IsBoss => IsMimic && FinaleRoster.IsBoss(Family);
    internal FinaleEnemyPlacement(string id, string region, GridPoint position, int roll = 0,
        string? location = null, string? family = null, bool town = false)
    { Id = id; RegionId = region; Position = position; Roll = roll; LocationId = location; Family = family; IsTown = town; }
}

public sealed class FinaleRegionObjective
{
    public string Id { get; }
    public string RegionId { get; }
    public string Name { get; }
    public OverworldBiome Biome { get; }
    public IReadOnlyList<FinaleEnemyPlacement> Enemies { get; }
    internal FinaleRegionObjective(int floor, CampaignRegion region, OverworldBiome biome, IEnumerable<FinaleEnemyPlacement> enemies)
    { Id = $"floor-{floor}/{region.Id}"; RegionId = region.Id; Name = region.DisplayName; Biome = biome; Enemies = Array.AsReadOnly(enemies.ToArray()); }
}

public sealed class FinaleFloorPlan
{
    public int Number { get; }
    public int Seed => Campaign.Seed;
    public string Fingerprint { get; }
    public Campaign Campaign { get; }
    public OverworldGrid Grid { get; }
    public bool HasBoat { get; }
    public GridPoint Start => Grid.Locations[Campaign.FinalLocationId];
    public GridPoint Stairs => Grid.PlayerStart;
    public IReadOnlyList<FinaleRegionObjective> Regions { get; }
    public IReadOnlyList<FinaleEnemyPlacement> Enemies { get; }
    public IReadOnlyCollection<GridPoint> Reserved { get; }
    public IReadOnlyList<Placement> Traps { get; }
    readonly string?[,] owners;
    readonly bool[,] dryGround, boatGround;
    readonly Dictionary<GridPoint, List<(string RouteId, GridPoint Destination)>> warpNeighbors = new();

    internal FinaleFloorPlan(int number, Campaign campaign, OverworldGrid grid, bool boat, IEnumerable<string>? earnedWarps,
        ref int townFamily, ref int dungeonFamily)
    {
        // Plan and validate dry approaches for every party. Boat never changes seeds or populations.
        Number = number; Campaign = campaign; Grid = grid; HasBoat = false;
        var earned = new HashSet<string>(earnedWarps ?? grid.Warps.Select(w => w.Id));
        foreach (var warp in grid.Warps.Where(w => earned.Contains(w.Id)))
        {
            var a = grid.Locations[warp.From]; var b = grid.Locations[warp.To];
            if (!warpNeighbors.TryGetValue(a, out var forward)) warpNeighbors.Add(a, forward = new());
            if (!warpNeighbors.TryGetValue(b, out var backward)) warpNeighbors.Add(b, backward = new());
            forward.Add((warp.Id, b)); backward.Add((warp.Id, a));
        }
        dryGround = new bool[grid.Width, grid.Height]; boatGround = new bool[grid.Width, grid.Height];
        var crossings = grid.Locks.SelectMany(l => l.Cells).ToHashSet();
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
        {
            var cell = new GridPoint(x, y);
            boatGround[x, y] = grid.IsGround(cell);
            dryGround[x, y] = boatGround[x, y] && (!grid.RequiresBoat(cell) || crossings.Contains(cell));
        }
        owners = new string?[grid.Width, grid.Height];
        var regions = campaign.Regions.OrderByDescending(r => r.ProgressionOrder).ThenByDescending(r => r.Id, StringComparer.Ordinal).ToArray();
        foreach (var region in regions)
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
            if (grid.Layers[OverworldLayers.Region(region.Id)][x, y]) owners[x, y] = region.Id;
        if (RegionAt(Start) != regions[0].Id || RegionAt(Stairs) != regions[regions.Length - 1].Id)
            throw new InvalidOperationException($"Finale floor {number}: reversed region order does not join its endpoints.");
        var reserved = new HashSet<GridPoint>();
        foreach (var cell in grid.Locations.Values.Concat(grid.Locks.SelectMany(l => l.Cells))) reserved.UnionWith(Square(cell, 3));
        Reserved = new HashSet<GridPoint>(reserved);
        var placements = new List<FinaleEnemyPlacement>();
        foreach (var location in campaign.Locations.Where(l => l.ParentTownId == null && l.Id != campaign.FinalLocationId &&
                     (l.Kind == LocationKind.Town || l.IsDungeon)).OrderBy(l => l.Id, StringComparer.Ordinal))
        {
            bool town = location.Kind == LocationKind.Town;
            string family = town ? FinaleRoster.Towns[townFamily++ % FinaleRoster.Towns.Count] :
                FinaleRoster.Dungeons[dungeonFamily++ % FinaleRoster.Dungeons.Count];
            if (number == FinalePlan.FloorCount && location.Id == campaign.StartLocationId) family = "Castle Monster";
            var position = grid.Locations[location.Id];
            placements.Add(new FinaleEnemyPlacement($"floor-{number}/mimic/{location.Id}", RegionAt(position)!, position,
                location: location.Id, family: family, town: town));
        }
        var objectives = new List<FinaleRegionObjective>();
        var traps = new List<Placement>();
        var unlocked = new HashSet<string>();
        foreach (var region in regions)
        {
            unlocked.Add(region.Id);
            var reachable = Reachable(Start, unlocked);
            var biome = grid.RegionBiomes[region.Id];
            int budget = DungeonLayoutProfile.Options(Seed, biome, 4).EnemyCount;
            var random = new SeedStream(CampaignContext.LocationSeed(Seed, region.Id), 2501);
            var candidates = random.Shuffle(reachable.Keys.Where(p => RegionAt(p) == region.Id &&
                Square(p, 1).All(q => IsGround(q) && RegionAt(q) == region.Id && !reserved.Contains(q)))
                .OrderBy(p => p.Y).ThenBy(p => p.X));
            int placed = 0;
            foreach (var cell in candidates)
            {
                if (Square(cell, 2).Any(reserved.Contains)) continue;
                placements.Add(new FinaleEnemyPlacement($"floor-{number}/{region.Id}/pack-{placed}", region.Id, cell, random.Range(int.MaxValue)));
                reserved.UnionWith(Square(cell, 3));
                if (++placed == budget) break;
            }
            if (placed != budget) throw new InvalidOperationException($"Finale {Seed}/{region.Id}: insufficient reachable spawn cells ({placed}/{budget}).");
            // Keep the road network and a dry approach to every objective free of traps.
            foreach (var destination in placements.Where(e => e.RegionId == region.Id).Select(e => e.Position)
                         .Concat(region.Id == RegionAt(Stairs) ? new[] { Stairs } : Array.Empty<GridPoint>()))
            {
                var cell = destination;
                while (reachable.TryGetValue(cell, out int distance) && distance > 0)
                {
                    reserved.Add(cell);
                    var next = OverworldMovement.Neighbors(cell).Where(p => CanStep(cell, p, unlocked))
                        .Concat(WarpDestinations(cell, unlocked).Select(w => w.Destination))
                        .First(p => reachable.TryGetValue(p, out int d) && d == distance - 1);
                    if (Math.Abs(next.X - cell.X) == 1 && Math.Abs(next.Y - cell.Y) == 1)
                    { reserved.Add(new GridPoint(cell.X, next.Y)); reserved.Add(new GridPoint(next.X, cell.Y)); }
                    cell = next;
                }
            }
            var trapRandom = new SeedStream(CampaignContext.LocationSeed(Seed, region.Id), 2510);
            var trapCells = trapRandom.Shuffle(reachable.Keys.Where(p => RegionAt(p) == region.Id && !reserved.Contains(p) &&
                    !grid.Layers[OverworldLayers.Roads].At(p) && Square(p, 1).All(IsGround)).OrderBy(p => p.Y).ThenBy(p => p.X)).Take(5).ToArray();
            if (trapCells.Length != 5) throw new InvalidOperationException($"Finale {Seed}/{region.Id}: insufficient safe trap cells.");
            traps.AddRange(trapCells.Select(p => new Placement(p, trapRandom.Range(int.MaxValue))));
            objectives.Add(new FinaleRegionObjective(number, region, biome, placements.Where(e => e.RegionId == region.Id)));
        }
        Regions = Array.AsReadOnly(objectives.ToArray()); Enemies = Array.AsReadOnly(placements.ToArray());
        if (Regions.Any(r => !r.Enemies.Any(e => e.IsBoss)))
            throw new InvalidOperationException($"Finale floor {number}: every region needs a building boss.");
        Traps = Array.AsReadOnly(traps.ToArray());
        ValidateRoute();
        // Hash physical terrain and the complete planned population, independently of party capabilities.
        var canonical = new System.Text.StringBuilder(Grid.CampaignFingerprint).Append('/').Append(DungeonFloorFingerprint.Compute(CreateFloor()));
        foreach (var enemy in Enemies)
            canonical.Append('|').Append(enemy.Id).Append('/').Append(enemy.RegionId).Append('/').Append(enemy.Position)
                .Append('/').Append(enemy.Roll.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('/').Append(enemy.Family);
        using (var sha = System.Security.Cryptography.SHA256.Create())
            Fingerprint = string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical.ToString()))
                .Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        HasBoat = boat;
    }

    public static IEnumerable<GridPoint> Square(GridPoint cell, int radius)
    {
        for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++) yield return new GridPoint(cell.X + x, cell.Y + y);
    }
    public string? RegionAt(GridPoint cell) => Grid.Contains(cell) ? owners[cell.X, cell.Y] : null;
    // Old capability-gate crossings are bridges in the reverse world; clearance replaces their key/Boat requirements.
    public bool IsGround(GridPoint cell) => Grid.Contains(cell) && (HasBoat ? boatGround[cell.X, cell.Y] : dryGround[cell.X, cell.Y]);
    public bool IsWalkable(GridPoint cell, ISet<string> unlocked) => IsGround(cell) && RegionAt(cell) is { } id && unlocked.Contains(id);
    public bool CanStep(GridPoint from, GridPoint to, ISet<string> unlocked) => IsWalkable(from, unlocked) &&
        OverworldMovement.CanStep(from, to, p => IsWalkable(p, unlocked));
    public IEnumerable<(string RouteId, GridPoint Destination)> WarpDestinations(GridPoint from, ISet<string> unlocked)
    {
        if (!IsWalkable(from, unlocked) || !warpNeighbors.TryGetValue(from, out var routes)) yield break;
        foreach (var route in routes)
            if (IsWalkable(route.Destination, unlocked)) yield return route;
    }
    public Dictionary<GridPoint, int> Reachable(GridPoint from, ISet<string> unlocked)
    {
        var distances = Flood(from, unlocked); var result = new Dictionary<GridPoint, int>();
        for (int i = 0; i < distances.Length; i++)
            if (distances[i] >= 0) result.Add(new GridPoint(i % Grid.Width, i / Grid.Width), distances[i]);
        return result;
    }
    int[] Flood(GridPoint from, ISet<string> unlocked, ISet<GridPoint>? blocked = null)
    {
        int width = Grid.Width, height = Grid.Height, count = width * height;
        var walk = new bool[count]; var distances = new int[count]; var queue = new int[count];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int index = y * width + x; var p = new GridPoint(x, y);
            walk[index] = IsWalkable(p, unlocked) && (blocked == null || !blocked.Contains(p));
            distances[index] = -1;
        }
        if (!Grid.Contains(from) || !walk[from.Y * width + from.X]) return distances;
        int head = 0, tail = 0, start = from.Y * width + from.X;
        queue[tail++] = start; distances[start] = 0;
        while (head < tail)
        {
            int index = queue[head++], x = index % width, y = index / width, distance = distances[index] + 1;
            foreach (var delta in GridSteps.EightWay)
            {
                int nx = x + delta.X, ny = y + delta.Y;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int next = ny * width + nx;
                if (!walk[next] || distances[next] >= 0 ||
                    delta.X != 0 && delta.Y != 0 && (!walk[y * width + nx] || !walk[ny * width + x])) continue;
                distances[next] = distance; queue[tail++] = next;
            }
            if (!warpNeighbors.TryGetValue(new GridPoint(x, y), out var routes)) continue;
            foreach (var route in routes)
            {
                int next = route.Destination.Y * width + route.Destination.X;
                if (!walk[next] || distances[next] >= 0) continue;
                distances[next] = distance; queue[tail++] = next;
            }
        }
        return distances;
    }

    public void ValidateRoute()
    {
        var unlocked = new HashSet<string>();
        foreach (var region in Regions)
        {
            unlocked.Add(region.RegionId);
            var hidden = Enemies.Where(e => e.IsMimic && unlocked.Contains(e.RegionId)).Select(e => e.Position).ToHashSet();
            int[] reachable;
            while (true)
            {
                reachable = Flood(Start, unlocked, hidden);
                var revealed = hidden.Where(p => GridSteps.EightWay.Select(d => new GridPoint(p.X + d.X, p.Y + d.Y))
                    .Any(q => Grid.Contains(q) && reachable[q.Y * Grid.Width + q.X] >= 0 && CanStep(q, p, unlocked))).ToArray();
                if (revealed.Length == 0) break;
                hidden.ExceptWith(revealed);
            }
            if (hidden.Count > 0 || region.Enemies.Any(e => reachable[e.Position.Y * Grid.Width + e.Position.X] < 0))
                throw new InvalidOperationException($"Finale {Seed}/{region.RegionId}: an objective is unreachable with the current traversal capabilities.");
        }
        if (Flood(Start, unlocked)[Stairs.Y * Grid.Width + Stairs.X] < 0) throw new InvalidOperationException("Finale stairs are unreachable.");
    }

    public DungeonFloor CreateFloor()
    {
        var ground = Grid.Layers[OverworldLayers.Ground].ToArray(); var walls = new bool[Grid.Width, Grid.Height];
        for (int y = 0; y < Grid.Height; y++) for (int x = 0; x < Grid.Width; x++)
        { ground[x, y] = IsGround(new GridPoint(x, y)); walls[x, y] = !ground[x, y]; }
        return new DungeonFloor(Seed, false, new Dictionary<string, GridLayer> {
            [DungeonLayers.Floor] = new(ground), [DungeonLayers.Dungeon] = new(walls) }, Array.Empty<GridRect>(), Start, Stairs,
            Array.Empty<Placement>(), Array.Empty<Placement>(), Array.Empty<Placement>(), Traps, overworld: Grid);
    }
}
