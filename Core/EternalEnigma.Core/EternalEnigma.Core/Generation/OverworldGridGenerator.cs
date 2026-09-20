using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Generation;

public sealed class OverworldGridOptions
{
    public int Width { get; }
    public int Height { get; }
    public int AreaExpansionRadius { get; }
    public OverworldGridOptions(int width = 256, int height = 256, int areaExpansionRadius = 6)
    {
        if (width < 16 || width > 1024 || height < 16 || height > 1024)
            throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be between 16 and 1024.");
        if (areaExpansionRadius < 0 || areaExpansionRadius > 12)
            throw new ArgumentOutOfRangeException(nameof(areaExpansionRadius), "Area expansion radius must be between 0 and 12.");
        Width = width; Height = height; AreaExpansionRadius = areaExpansionRadius;
    }
}

/// <summary>Deterministic two-dimensional districts, sealed branch gates and keyed inter-biome passages.</summary>
public static class OverworldGridGenerator
{
    public static OverworldGrid Generate(Campaign campaign, OverworldGridOptions? options = null)
    {
        if (campaign == null) throw new ArgumentNullException(nameof(campaign));
        var validation = CampaignValidator.Validate(campaign);
        if (!validation.IsValid) throw new ArgumentException("Invalid campaign:\n" + string.Join("\n", validation.Errors), nameof(campaign));
        options ??= new OverworldGridOptions();
        if (options.Width < 248 || options.Height < 248)
            throw new ArgumentException("Compact district embedding needs at least 248x248 tiles.", nameof(options));
        var diagnostics = new List<string>();
        for (int attempt = 0; attempt < 64; attempt++)
        {
            try { return Embed(campaign, options, attempt); }
            catch (InvalidOperationException error) { diagnostics.Add($"Attempt {attempt}: {error.Message}"); }
        }
        throw new InvalidOperationException($"Seed {campaign.Seed}: no valid embedding in 64 attempts.\n" + string.Join("\n", diagnostics));
    }

    private static OverworldGrid Embed(Campaign campaign, OverworldGridOptions options, int attempt)
    {
        var byId = campaign.Locations.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var physicalStage = campaign.Locations.ToDictionary(l => l.Id, l => l.Stage);
        foreach (var objective in campaign.ReturnObjectives)
            foreach (string id in objective.DestinationIds) physicalStage[id] = 0;
        var positions = new Dictionary<string, GridPoint>(StringComparer.Ordinal);
        int offsetX = (options.Width - 256) / 2, offsetY = (options.Height - 256) / 2;
        // Three-by-three biome slots: . F E / A B D / . . C. Extra stages stay inside F.
        GridPoint Center(int stage)
        {
            var centers = new[] { new GridPoint(40, 128), new GridPoint(128, 128), new GridPoint(216, 40),
                new GridPoint(216, 128), new GridPoint(216, 216), new GridPoint(128, 216), new GridPoint(80, 216), new GridPoint(40, 216) };
            var at = centers[stage]; return new GridPoint(offsetX + at.X, offsetY + at.Y);
        }
        var random = new SeedStream(campaign.Seed, (uint)(110 + attempt));
        foreach (var group in campaign.Locations.GroupBy(l => physicalStage[l.Id]).OrderBy(g => g.Key))
        {
            var center = Center(group.Key);
            var leaves = random.Shuffle(group.Where(l => l.Kind != LocationKind.Checkpoint)).OrderBy(l => l.RegionId, StringComparer.Ordinal).ToArray();
            if (leaves.Length > 20 || group.Key == 1 && leaves.Length > 10)
                throw new InvalidOperationException($"District {group.Key} has too many destinations ({leaves.Length}).");
            foreach (var checkpoint in group.Where(l => l.Kind == LocationKind.Checkpoint)) positions.Add(checkpoint.Id, center);
            // B reserves its east side for C/D/E and its center for F. C/E reserve their diagonal arrival corners.
            for (int i = 0; i < leaves.Length; i++)
            {
                int bank = i % 2, slot = i / 2;
                int dx = group.Key == 1 ? -(slot + 1) * 6 :
                    (group.Key == 4 && bank == 0 || group.Key == 2 && bank == 1) ? (slot + 1) * 6 :
                    (slot / 2 + 1) * 6 * (slot % 2 == 0 ? -1 : 1);
                positions.Add(leaves[i].Id, new GridPoint(center.X + dx, center.Y + (bank == 0 ? -24 : 24)));
            }
        }
        var rawPositions = positions.ToDictionary(p => p.Key, p => p.Value);
        int Bend(int y) { int phase = (y / 6 + campaign.Seed % 8 + 8) % 8; return phase <= 2 ? phase : phase <= 6 ? 4 - phase : phase - 8; }
        GridPoint Warp(GridPoint p) => new GridPoint(p.X + Bend(p.Y), p.Y);
        foreach (string id in positions.Keys.ToArray()) positions[id] = Warp(positions[id]);
        var masks = new Dictionary<string, bool[,]>(StringComparer.Ordinal);
        foreach (string name in new[] { OverworldLayers.Ground, OverworldLayers.Walkable, OverworldLayers.Roads,
            OverworldLayers.Mountains, OverworldLayers.Trees, OverworldLayers.Water, OverworldLayers.Reserved,
            OverworldLayers.Towns, OverworldLayers.StoryDungeons, OverworldLayers.RepeatableDungeons, OverworldLayers.FinalDungeon,
            OverworldLayers.Converters, OverworldLayers.Landmarks, OverworldLayers.Secrets, OverworldLayers.PlayerStart,
            OverworldLayers.Locks, OverworldLayers.AreaLocks, OverworldLayers.ObstacleLocks, OverworldLayers.InteractionLocks })
            masks[name] = new bool[options.Width, options.Height];
        foreach (var region in campaign.Regions) masks[OverworldLayers.Region(region.Id)] = new bool[options.Width, options.Height];
        var ground = masks[OverworldLayers.Ground];
        var regionOwner = new string?[options.Width, options.Height];
        void Carve(GridPoint point, string region, bool road)
        {
            ground[point.X, point.Y] = true;
            if (road) masks[OverworldLayers.Roads][point.X, point.Y] = true;
            string? previousRegion = regionOwner[point.X, point.Y];
            if (previousRegion != null) masks[OverworldLayers.Region(previousRegion)][point.X, point.Y] = false;
            regionOwner[point.X, point.Y] = region;
            masks[OverworldLayers.Region(region)][point.X, point.Y] = true;
        }
        var routePaths = new Dictionary<string, IReadOnlyList<GridPoint>>(StringComparer.Ordinal);
        var locks = new List<GridLock>();
        foreach (var route in campaign.Routes.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            if (route.IsWarp)
            {
                routePaths.Add(route.Id, Array.AsReadOnly(new[] { positions[route.From], positions[route.To] }));
                continue;
            }
            var from = rawPositions[route.From]; var to = rawPositions[route.To];
            var path = new List<GridPoint> { from };
            void LineTo(int x, int y)
            {
                var start = path[path.Count - 1]; int dx = x - start.X, dy = y - start.Y;
                int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
                for (int i = 1; i <= steps; i++)
                    path.Add(new GridPoint(start.X + (int)Math.Round((double)dx * i / steps), start.Y + (int)Math.Round((double)dy * i / steps)));
            }
            int a = physicalStage[route.From], b = physicalStage[route.To];
            if (route.ShortcutKind == ShortcutKind.Keyed)
            {
                if (a != 1 || b < 3 || b > 5) throw new InvalidOperationException("Expected a B-D/E/F key passage.");
                LineTo(to.X, to.Y);
            }
            else if (a == b)
            {
                LineTo(from.X, Center(a).Y); LineTo(to.X, Center(a).Y); LineTo(to.X, to.Y);
            }
            else
            {
                if (!route.IsProgressionBoundary) throw new InvalidOperationException($"Unsupported inter-district route {route.Id}.");
                LineTo(to.X, to.Y);
            }
            bool diagonalCorridor = route.ShortcutKind == ShortcutKind.Keyed || route.IsProgressionBoundary && a == 1 && b == 2;
            var winding = new List<GridPoint> { Warp(path[0]) };
            for (int i = 1; i < path.Count; i++)
            {
                var previous = winding[winding.Count - 1]; var next = Warp(path[i]);
                if (diagonalCorridor)
                {
                    while (!previous.Equals(next))
                    {
                        previous = new GridPoint(previous.X + Math.Sign(next.X - previous.X), previous.Y + Math.Sign(next.Y - previous.Y));
                        winding.Add(previous);
                    }
                }
                else
                {
                    if (previous.X != next.X && previous.Y != next.Y)
                        winding.Add(next.Y > previous.Y ? new GridPoint(next.X, previous.Y) : new GridPoint(previous.X, next.Y));
                    winding.Add(next);
                }
            }
            path = winding;
            var corridor = new HashSet<GridPoint>(path);
            if (diagonalCorridor)
                for (int i = 1; i < path.Count; i++)
                    if (path[i - 1].X != path[i].X && path[i - 1].Y != path[i].Y)
                    { corridor.Add(new GridPoint(path[i - 1].X, path[i].Y)); corridor.Add(new GridPoint(path[i].X, path[i - 1].Y)); }
            foreach (var cell in corridor)
                Carve(cell, Math.Abs(cell.X - positions[route.From].X) + Math.Abs(cell.Y - from.Y) <
                    Math.Abs(cell.X - positions[route.To].X) + Math.Abs(cell.Y - to.Y) ? byId[route.From].RegionId : byId[route.To].RegionId, true);
            routePaths.Add(route.Id, Array.AsReadOnly(path.ToArray()));
            if (route.HasGate)
            {
                int length = route.Form == LockForm.Area ? 3 : 1;
                int gateIndex = route.IsProgressionBoundary ? path.Count / 2 : path.Count - 6;
                var cells = diagonalCorridor
                    ? corridor.Where(p => from.Y != to.Y ? p.Y == (from.Y + to.Y) / 2 : p.X == (positions[route.From].X + positions[route.To].X) / 2).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray()
                    : path.Skip(gateIndex - length / 2).Take(length).ToArray();
                locks.Add(new GridLock(route.Id, cells));
                string layer = route.Form == LockForm.Area ? OverworldLayers.AreaLocks :
                    route.Form == LockForm.Obstacle ? OverworldLayers.ObstacleLocks : OverworldLayers.InteractionLocks;
                foreach (var cell in cells) { masks[OverworldLayers.Locks][cell.X, cell.Y] = true; masks[layer][cell.X, cell.Y] = true; }
            }
        }
        foreach (var location in campaign.Locations)
        {
            var at = positions[location.Id];
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) Carve(new GridPoint(at.X + x, at.Y + y), location.RegionId, false);
            string? layer = location.Kind switch
            {
                LocationKind.Town => OverworldLayers.Towns, LocationKind.StoryDungeon => OverworldLayers.StoryDungeons,
                LocationKind.RepeatableDungeon => OverworldLayers.RepeatableDungeons, LocationKind.FinalDungeon => OverworldLayers.FinalDungeon,
                LocationKind.Converter => OverworldLayers.Converters, LocationKind.Landmark => OverworldLayers.Landmarks,
                LocationKind.Secret => OverworldLayers.Secrets, _ => null
            };
            if (layer != null) masks[layer][at.X, at.Y] = true;
        }
        var start = positions[campaign.StartLocationId];
        masks[OverworldLayers.PlayerStart][start.X, start.Y] = true;
        var originalGround = (bool[,])ground.Clone();
        ExpandAreas(ground, masks[OverworldLayers.Locks], regionOwner, options.AreaExpansionRadius, campaign, positions);
        var decoration = new SeedStream(campaign.Seed, 101);
        var palette = SelectBiomes(campaign);
        var nearestBiome = new OverworldBiome[options.Width, options.Height];
        for (int y = 2; y < options.Height - 2; y++) for (int x = 2; x < options.Width - 2; x++)
        {
            int best = int.MaxValue;
            foreach (var location in campaign.Locations)
            {
                var p = positions[location.Id]; int distance = Math.Abs(p.X - x) + Math.Abs(p.Y - y);
                if (distance >= best) continue;
                best = distance; nearestBiome[x, y] = palette[location.RegionId];
            }
        }
        for (int y = 0; y < options.Height; y++)
        for (int x = 0; x < options.Width; x++)
        {
            bool border = x < 2 || y < 2 || x >= options.Width - 2 || y >= options.Height - 2;
            // Consume the original decoration stream even where expansion replaces background.
            int decorationRoll = decoration.Range(8);
            bool tree = nearestBiome[x, y] == OverworldBiome.Forest || nearestBiome[x, y] == OverworldBiome.Marsh && decorationRoll < 5;
            if (ground[x, y])
            {
                masks[OverworldLayers.Region(regionOwner[x, y]!)][x, y] = true;
                masks[OverworldLayers.Walkable][x, y] = !masks[OverworldLayers.Locks][x, y];
                // Reserve the floor and its sealing halo so decoration cannot widen a threshold or carve a bypass.
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    if (x + dx >= 0 && y + dy >= 0 && x + dx < options.Width && y + dy < options.Height)
                        masks[OverworldLayers.Reserved][x + dx, y + dy] = true;
            }
            else if (border)
                masks[OverworldLayers.Water][x, y] = true;
            else masks[tree ? OverworldLayers.Trees : OverworldLayers.Mountains][x, y] = true;
        }
        var biomes = AssignBiomes(campaign, masks, originalGround, regionOwner, locks);
        var grid = new OverworldGrid(campaign, masks.ToDictionary(m => m.Key, m => new GridLayer(m.Value), StringComparer.Ordinal), positions, routePaths, locks, biomes);
        var gridValidation = OverworldGridValidator.Validate(campaign, grid);
        if (!gridValidation.IsValid) throw new InvalidOperationException("Grid embedding failed validation:\n" + string.Join("\n", gridValidation.Errors));
        return grid;
    }

    private static Dictionary<string, OverworldBiome> SelectBiomes(Campaign campaign)
    {
        var random = new SeedStream(campaign.Seed, 103);
        string startingRegion = campaign.Locations.Single(l => l.Id == campaign.StartLocationId).RegionId;
        var palette = random.Shuffle(new[] { OverworldBiome.Desert, OverworldBiome.Water, OverworldBiome.Mountain,
            OverworldBiome.Forest, OverworldBiome.Tundra, OverworldBiome.Marsh, OverworldBiome.Volcanic });
        if (campaign.Regions.Count > palette.Count + 1)
            throw new NotSupportedException("Campaign has more regions than the distinct biome pool supports. Add biomes before adding regions.");
        var regions = new Dictionary<string, OverworldBiome>(StringComparer.Ordinal) { [startingRegion] = OverworldBiome.Grassland };
        int index = 0;
        foreach (var region in campaign.Regions.Where(r => r.Id != startingRegion).OrderBy(r => r.Id, StringComparer.Ordinal))
            regions[region.Id] = palette[index++];
        return regions;
    }

    private static Dictionary<string, OverworldBiome> AssignBiomes(Campaign campaign, Dictionary<string, bool[,]> masks,
        bool[,] originalGround, string?[,] owners, List<GridLock> locks)
    {
        var regions = SelectBiomes(campaign);
        var ground = masks[OverworldLayers.Ground];
        int width = ground.GetLength(0), height = ground.GetLength(1);
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) masks[OverworldLayers.Biome(biome)] = new bool[width, height];
        var water = masks[OverworldLayers.NavigableWater] = new bool[width, height];
        var bridges = masks[OverworldLayers.Bridges] = new bool[width, height];
        // Boat-only area gates become actual water crossings. Alternative-capability
        // routes keep their original requirements and dry floor.
        foreach (var gate in locks)
        {
            var route = campaign.Routes.Single(r => r.Id == gate.RouteId);
            if (route.Form == LockForm.Area && route.Requirement.Alternatives.All(a => a.Contains(Capability.Boat)))
                foreach (var cell in gate.Cells) water[cell.X, cell.Y] = true;
        }
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            if (!ground[x, y]) continue;
            var biome = regions[owners[x, y]!];
            if (biome == OverworldBiome.Water)
            {
                // Keep the original road network and location clearings as causeways
                // and islands. This guarantees boat providers are never flooded out.
                water[x, y] |= !originalGround[x, y];
                bridges[x, y] = !water[x, y] && masks[OverworldLayers.Roads][x, y];
                if (!water[x, y]) biome = OverworldBiome.Grassland;
            }
            if (water[x, y])
            {
                biome = OverworldBiome.Water;
                masks[OverworldLayers.Water][x, y] = true;
                masks[OverworldLayers.Walkable][x, y] = false;
            }
            masks[OverworldLayers.Biome(biome)][x, y] = true;
        }
        return regions;
    }

    private static void ExpandAreas(bool[,] ground, bool[,] gates, string?[,] regions, int radius,
        Campaign campaign, IReadOnlyDictionary<string, GridPoint> positions)
    {
        if (radius == 0) return;
        int width = ground.GetLength(0), height = ground.GetLength(1);
        var owners = new int[width, height];
        var protectedCells = new bool[width, height];
        var costs = new int[width, height];
        var random = new SeedStream(campaign.Seed, 102);
        var palette = SelectBiomes(campaign);
        string startingRegion = campaign.Locations.Single(l => l.Id == campaign.StartLocationId).RegionId;
        var directions = new[] { new GridPoint(1, 0), new GridPoint(0, 1), new GridPoint(-1, 0), new GridPoint(0, -1) };
        bool Contains(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            costs[x, y] = 1 + random.Range(2);
            if (!gates[x, y]) continue;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                if (Contains(x + dx, y + dy)) protectedCells[x + dx, y + dy] = true;
        }

        // Orthogonal components equal sealed eight-way components: a legal diagonal
        // always has both orthogonal side cells. Label ALL original floor before growth.
        int component = 0;
        var pending = new Queue<GridPoint>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!ground[x, y] || gates[x, y] || owners[x, y] != 0) continue;
            owners[x, y] = ++component;
            pending.Enqueue(new GridPoint(x, y));
            while (pending.Count > 0)
            {
                var at = pending.Dequeue();
                foreach (var direction in directions)
                {
                    int nx = at.X + direction.X, ny = at.Y + direction.Y;
                    if (!Contains(nx, ny) || !ground[nx, ny] || gates[nx, ny] || owners[nx, ny] != 0) continue;
                    owners[nx, ny] = component;
                    pending.Enqueue(new GridPoint(nx, ny));
                }
            }
        }

        // Integer cost buckets give stable ties without runtime-dependent priority queues.
        const int budget = 12;
        var frontier = Enumerable.Range(0, budget + 1)
            .Select(_ => new Queue<(GridPoint at, int owner, string region, int distance)>()).ToArray();
        void Offer(GridPoint at, int owner, string region, int distance, int spent)
        {
            var biome = palette[region];
            int reach = region == startingRegion ? Math.Min(3, radius) : radius;
            if (distance >= reach) return;
            foreach (var direction in directions)
            {
                int x = at.X + direction.X, y = at.Y + direction.Y;
                if (x < 2 || y < 2 || x >= width - 2 || y >= height - 2 || ground[x, y] || protectedCells[x, y]) continue;
                bool broken = biome == OverworldBiome.Tundra ? Math.Abs(x * 13 + y * 7) % 11 == 0 :
                    biome == OverworldBiome.Marsh ? Math.Abs(x * 3 - y * 5) % 13 < 3 :
                    biome == OverworldBiome.Forest && Math.Abs(x * 7 + y * 11) % 29 < 2;
                if (broken) continue;
                int terrainCost = biome == OverworldBiome.Grassland || biome == OverworldBiome.Desert || biome == OverworldBiome.Water ? 1 :
                    biome == OverworldBiome.Mountain && direction.Y != 0 ? 3 : costs[x, y];
                int cost = spent + terrainCost;
                if (cost <= budget) frontier[cost].Enqueue((new GridPoint(x, y), owner, region, distance + 1));
            }
        }
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            if (owners[x, y] != 0) Offer(new GridPoint(x, y), owners[x, y], regions[x, y]!, 0, 0);
        for (int cost = 1; cost <= budget; cost++)
        while (frontier[cost].Count > 0)
        {
            var next = frontier[cost].Dequeue();
            int x = next.at.X, y = next.at.Y;
            if (ground[x, y]) continue;
            bool touchesOther = false;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                if (owners[x + dx, y + dy] != 0 && owners[x + dx, y + dy] != next.owner) touchesOther = true;
            // Leave two tiles of impassable terrain between different region interiors.
            // Original floor is never removed: roads and gate approaches remain the
            // pre-existing connections through these broader biome boundaries.
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                if (Contains(x + dx, y + dy) && regions[x + dx, y + dy] != null && regions[x + dx, y + dy] != next.region)
                    touchesOther = true;
            if (touchesOther) continue;
            ground[x, y] = true;
            owners[x, y] = next.owner;
            regions[x, y] = next.region;
            Offer(next.at, next.owner, next.region, next.distance, cost);
        }
    }
}
