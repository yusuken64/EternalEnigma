using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Generation;

public sealed class OverworldGridOptions
{
    public int Width { get; }
    public int Height { get; }
    /// <summary>Boundary variation strength (0–12). Zero keeps smooth, broad territories.</summary>
    public int AreaExpansionRadius { get; }
    public OverworldGridOptions(int width = 256, int height = 256, int areaExpansionRadius = 6)
    {
        if (width < 16 || width > 1024 || height < 16 || height > 1024)
            throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be between 16 and 1024.");
        if (areaExpansionRadius < 0 || areaExpansionRadius > 12)
            throw new ArgumentOutOfRangeException(nameof(areaExpansionRadius), "Boundary variation strength must be between 0 and 12.");
        Width = width; Height = height; AreaExpansionRadius = areaExpansionRadius;
    }
}

/// <summary>Deterministic open territories, protected destinations and short sealed passes.</summary>
public static class OverworldGridGenerator
{
    public static OverworldGrid Generate(Campaign campaign, OverworldGridOptions? options = null)
    {
        if (campaign == null) throw new ArgumentNullException(nameof(campaign));
        var validation = CampaignValidator.Validate(campaign);
        if (!validation.IsValid) throw new ArgumentException("Invalid campaign:\n" + string.Join("\n", validation.Errors), nameof(campaign));
        options ??= new OverworldGridOptions();
        if (options.Width < 248 || options.Height < 248)
            throw new ArgumentException("Continuous territory embedding needs at least 248x248 tiles.", nameof(options));
        var diagnostics = new List<string>();
        for (int attempt = 0; attempt < 64; attempt++)
        {
            try { return Embed(campaign, options, attempt); }
            catch (InvalidOperationException error) { diagnostics.Add($"Attempt {attempt}: {error.Message}"); }
        }
        throw new InvalidOperationException($"Seed {campaign.Seed}: no valid embedding in 64 attempts.\n" + string.Join("\n", diagnostics));
    }

    private static readonly GridPoint[] Directions = { new GridPoint(1, 0), new GridPoint(0, 1), new GridPoint(-1, 0), new GridPoint(0, -1) };

    private static OverworldGrid Embed(Campaign campaign, OverworldGridOptions options, int attempt)
    {
        int w = options.Width, h = options.Height;
        // Preserve square-ish geography on wide/tall map options; surplus space becomes ocean.
        int landWidth = Math.Min(w, h * 4 / 3) * 7 / 10, landHeight = Math.Min(h, w * 4 / 3) * 7 / 10;
        var random = new SeedStream(campaign.Seed, (uint)(110 + attempt));
        var palette = SelectBiomes(campaign);
        var stages = campaign.Locations.ToDictionary(l => l.Id, l => l.Stage);
        foreach (var objective in campaign.ReturnObjectives)
            foreach (var id in objective.DestinationIds) stages[id] = 0;
        var parents = campaign.Locations.ToDictionary(l => l.Id, l => l.Id);
        string Root(string id) { while (parents[id] != id) id = parents[id]; return id; }
        foreach (var r in campaign.Routes.Where(r => (!r.HasGate || r.IsTownExit) && !r.IsWarp)) parents[Root(r.To)] = Root(r.From);
        var groups = campaign.Locations.GroupBy(l => Root(l.Id)).ToArray();
        var groupOf = groups.SelectMany((g, i) => g.Select(l => (l.Id, i))).ToDictionary(p => p.Id, p => p.i);
        int stageCount = stages.Values.Max() + 1;
        var primary = Enumerable.Range(0, stageCount).Select(i => groupOf["checkpoint-" + i]).ToArray();
        var anchors = new[] { (48, 104), (124, 102), (195, 48), (214, 128), (203, 208), (128, 211), (76, 214), (35, 213) };
        var centers = anchors.Take(stageCount).Select(a => new GridPoint((a.Item1 + random.Range(9) - 4) * landWidth / 256 + (w - landWidth) / 2,
            (a.Item2 + random.Range(9) - 4) * landHeight / 256 + (h - landHeight) / 2)).ToArray();
        // Smooth seeded coordinate noise bends a weighted Voronoi partition without a route skeleton.
        var noise = new double[18, 18, 2];
        for (int y = 0; y < 18; y++) for (int x = 0; x < 18; x++) for (int d = 0; d < 2; d++) noise[x, y, d] = (random.Range(2001) - 1000) / 1000.0;
        double Noise(int x, int y, int d)
        {
            double xx = x * 16.0 / w, yy = y * 16.0 / h;
            int ix = (int)xx, iy = (int)yy; double u = xx - ix, v = yy - iy;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            return (noise[ix, iy, d] * (1 - u) + noise[ix + 1, iy, d] * u) * (1 - v) +
                (noise[ix, iy + 1, d] * (1 - u) + noise[ix + 1, iy + 1, d] * u) * v;
        }
        // Independent fields keep rainfall, vegetation and relief from following identical contours.
        var terrainRandom = new SeedStream(campaign.Seed, (uint)(310 + attempt));
        var terrainNoise = new double[w / 12 + 2, h / 12 + 2, 3];
        for (int y = 0; y < terrainNoise.GetLength(1); y++)
            for (int x = 0; x < terrainNoise.GetLength(0); x++)
                for (int d = 0; d < 3; d++) terrainNoise[x, y, d] = terrainRandom.Range(2001) / 1000.0 - 1;
        double TerrainNoise(int x, int y, int d)
        {
            int ix = x / 12, iy = y / 12;
            double u = x % 12 / 12.0, v = y % 12 / 12.0;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            return (terrainNoise[ix, iy, d] * (1 - u) + terrainNoise[ix + 1, iy, d] * u) * (1 - v) +
                (terrainNoise[ix, iy + 1, d] * (1 - u) + terrainNoise[ix + 1, iy + 1, d] * u) * v;
        }
        var pocketRandom = new SeedStream(campaign.Seed, (uint)(410 + attempt));
        var territory = new int[w, h]; var owner = new int[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            double nx = x + Noise(x, y, 0) * options.AreaExpansionRadius * .75, ny = y + Noise(x, y, 1) * options.AreaExpansionRadius * .75;
            int best = 0; double score = double.MaxValue;
            for (int i = 0; i < stageCount; i++)
            {
                double value = (nx - centers[i].X) * (nx - centers[i].X) + (ny - centers[i].Y) * (ny - centers[i].Y);
                // A is the return-trip home and needs more room for protected rewards.
                if (i == 0) value *= .72;
                if (value < score) { score = value; best = i; }
            }
            territory[x, y] = best;
            double ex = (nx - w * .5) / (landWidth * .48), ey = (ny - h * .5) / (landHeight * .48);
            owner[x, y] = x < 3 || y < 3 || x >= w - 3 || y >= h - 3 || ex * ex + ey * ey > 1.12 ? -1 : primary[best];
        }
        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
        bool DiskFits(GridPoint p, int stage, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                if (!Inside(p.X + dx, p.Y + dy) || owner[p.X + dx, p.Y + dy] != primary[stage]) return false;
            return true;
        }
        var positions = new Dictionary<string, GridPoint>(StringComparer.Ordinal);
        // Reserve one compact pocket for the two starting locations before scattering other sites.
        if (campaign.Routes.Any(r => r.IsStarterExit))
        {
            GridPoint starterCenter = default;
            bool found = false;
            for (int trial = 0; trial < 6000; trial++)
            {
                starterCenter = new GridPoint(random.Range(w), random.Range(h));
                if (DiskFits(starterCenter, 0, 12)) { found = true; break; }
            }
            if (!found) throw new InvalidOperationException("No starting enclosure fits.");
            int starterGroup = groupOf[campaign.StartLocationId];
            for (int dy = -7; dy <= 7; dy++) for (int dx = -7; dx <= 7; dx++)
                owner[starterCenter.X + dx, starterCenter.Y + dy] = starterGroup;
            positions.Add("town-0", new GridPoint(starterCenter.X - 3, starterCenter.Y));
            if (campaign.GeneratorVersion >= 7)
                positions.Add("repeatable-0", new GridPoint(starterCenter.X + 3, starterCenter.Y));
            else positions.Add("story-0", new GridPoint(starterCenter.X + 3, starterCenter.Y));
        }
        // Sample pockets first so each has a sealing halo; all destinations share a minimum spacing.
        foreach (var location in campaign.Locations.Where(l => l.ParentTownId == null).OrderBy(l => groupOf[l.Id] == primary[stages[l.Id]] ? 1 : 0).ThenBy(l => l.Id, StringComparer.Ordinal))
        {
            if (positions.ContainsKey(location.Id)) continue;
            int stage = stages[location.Id], group = groupOf[location.Id];
            bool pocket = group != primary[stage];
            GridPoint at = default; bool found = false;
            for (int trial = 0; trial < 6000; trial++)
            {
                at = new GridPoint(random.Range(w), random.Range(h));
                if (!DiskFits(at, stage, pocket ? 10 : 4)) continue;
                if (positions.Values.Any(p => (p.X - at.X) * (p.X - at.X) + (p.Y - at.Y) * (p.Y - at.Y) < 121)) continue;
                found = true; break;
            }
            if (!found) throw new InvalidOperationException($"Separated placement failed for {location.Id} in stage {stage}.");
            positions.Add(location.Id, at);
            if (pocket)
            {
                // Periodic angular noise varies the whole boundary, rather than just the circle's size.
                double phase = pocketRandom.Range(6283) / 1000.0, secondPhase = pocketRandom.Range(6283) / 1000.0;
                for (int dy = -7; dy <= 7; dy++) for (int dx = -7; dx <= 7; dx++)
                {
                    double angle = Math.Atan2(dy, dx);
                    double radius = 5.8 + .6 * Math.Sin(3 * angle + phase) + .4 * Math.Sin(5 * angle + secondPhase);
                    if (dx * dx + dy * dy <= radius * radius) owner[at.X + dx, at.Y + dy] = group;
                }
            }
        }
        // Interior nodes project onto their town, without a separate overworld marker.
        foreach (var interior in campaign.Locations.Where(l => l.ParentTownId != null))
            positions.Add(interior.Id, positions[interior.ParentTownId!]);
        var masks = new Dictionary<string, bool[,]>(StringComparer.Ordinal);
        foreach (string name in new[] { OverworldLayers.Ground, OverworldLayers.Walkable, OverworldLayers.Roads,
            OverworldLayers.Mountains, OverworldLayers.Trees, OverworldLayers.Water, OverworldLayers.Reserved,
            OverworldLayers.Towns, OverworldLayers.StoryDungeons, OverworldLayers.RepeatableDungeons, OverworldLayers.FinalDungeon,
            OverworldLayers.Converters, OverworldLayers.Landmarks, OverworldLayers.Secrets, OverworldLayers.PlayerStart,
            OverworldLayers.Locks, OverworldLayers.AreaLocks, OverworldLayers.ObstacleLocks, OverworldLayers.InteractionLocks }) masks[name] = new bool[w, h];
        foreach (var region in campaign.Regions) masks[OverworldLayers.Region(region.Id)] = new bool[w, h];
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) masks[OverworldLayers.Landscape(biome)] = new bool[w, h];
        var ground = masks[OverworldLayers.Ground];
        var regionOwner = new string?[w, h];
        for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
        {
            if (owner[x, y] < 0) continue;
            string region = "region-" + Math.Min(5, territory[x, y]);
            regionOwner[x, y] = region;
            masks[OverworldLayers.Landscape(palette[region])][x, y] = true;
            bool interior = true;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                if (owner[x + dx, y + dy] != owner[x, y]) interior = false;
            ground[x, y] = interior;
        }
        // Remove tiny disconnected erosion artifacts; never silently discard a destination.
        var retained = new bool[w, h];
        foreach (var g in groups)
        {
            var at = positions[g.First().Id]; var queue = new Queue<GridPoint>(); queue.Enqueue(at); retained[at.X, at.Y] = true;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Directions)
                {
                    int x = p.X + d.X, y = p.Y + d.Y;
                    if (!Inside(x, y) || retained[x, y] || !ground[x, y] || owner[x, y] != owner[at.X, at.Y]) continue;
                    retained[x, y] = true; queue.Enqueue(new GridPoint(x, y));
                }
            }
            if (g.Any(l => !retained[positions[l.Id].X, positions[l.Id].Y])) throw new InvalidOperationException("Disconnected territory " + g.Key);
        }
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) ground[x, y] &= retained[x, y];
        var locks = new List<GridLock>();
        var gateCells = new Dictionary<string, GridPoint[]>();
        foreach (var route in campaign.Routes.Where(r => r.HasGate && !r.IsWarp && !r.IsTownExit))
        {
            int a = groupOf[route.From], b = groupOf[route.To];
            bool Broad(int x, int y, int group)
            {
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    if (!Inside(x + dx, y + dy) || !ground[x + dx, y + dy] || owner[x + dx, y + dy] != group) return false;
                return true;
            }
            var candidates = new List<GridPoint[]>();
            for (int y = 2; y < h - 2; y++) for (int x = 2; x < w - 2; x++)
            {
                if (!ground[x, y] || owner[x, y] != a) continue;
                foreach (var d in Directions)
                {
                    var cells = new List<GridPoint>();
                    for (int step = 1; step <= 7; step++)
                    {
                        int xx = x + d.X * step, yy = y + d.Y * step;
                        if (!Inside(xx, yy) || owner[xx, yy] < 0) break;
                        if (ground[xx, yy])
                        {
                            if (owner[xx, yy] == b && cells.Count >= 2 && cells.Count <= 4 && Broad(x - d.X * 2, y - d.Y * 2, a) && Broad(xx + d.X * 2, yy + d.Y * 2, b) && cells.All(p => !locks.Any(g => g.Cells.Any(c => Math.Abs(c.X - p.X) <= 2 && Math.Abs(c.Y - p.Y) <= 2)))) candidates.Add(cells.ToArray());
                            break;
                        }
                        if (owner[xx, yy] != a && owner[xx, yy] != b) break;
                        cells.Add(new GridPoint(xx, yy));
                    }
                }
            }
            if (candidates.Count == 0) throw new InvalidOperationException("No short shared boundary for " + route.Id);
            var chosen = candidates[random.Range(candidates.Count)];
            locks.Add(new GridLock(route.Id, chosen)); gateCells.Add(route.Id, chosen);
            string layer = route.Form == LockForm.Area ? OverworldLayers.AreaLocks : route.Form == LockForm.Obstacle ? OverworldLayers.ObstacleLocks : OverworldLayers.InteractionLocks;
            foreach (var p in chosen) { ground[p.X, p.Y] = true; masks[OverworldLayers.Locks][p.X, p.Y] = true; masks[layer][p.X, p.Y] = true; }
        }
        List<GridPoint> Path(GridPoint from, GridPoint to, int a, int b, string? gate = null)
        {
            bool Allowed(GridPoint p) => Inside(p.X, p.Y) && ground[p.X, p.Y] &&
                (owner[p.X, p.Y] == a || owner[p.X, p.Y] == b) &&
                (!masks[OverworldLayers.Locks][p.X, p.Y] || gate != null && gateCells[gate].Contains(p));
            // Cardinal BFS keeps the visible roads aligned to the movement grid.
            var previous = new int[w, h]; var queue = new Queue<GridPoint>(); queue.Enqueue(from); previous[from.X, from.Y] = -1;
            while (queue.Count > 0 && previous[to.X, to.Y] == 0)
            {
                var at = queue.Dequeue();
                foreach (var d in Directions)
                {
                    var next = new GridPoint(at.X + d.X, at.Y + d.Y);
                    if (!Allowed(next) || previous[next.X, next.Y] != 0) continue;
                    previous[next.X, next.Y] = at.Y * w + at.X + 1; queue.Enqueue(next);
                }
            }
            if (previous[to.X, to.Y] == 0) throw new InvalidOperationException($"No terrain path from {from} to {to} through gate {gate ?? "none"}.");
            var result = new List<GridPoint> { to }; var p = to;
            while (!p.Equals(from)) { int index = previous[p.X, p.Y] - 1; p = new GridPoint(index % w, index / w); result.Add(p); }
            result.Reverse(); return result;
        }
        var routePaths = new Dictionary<string, IReadOnlyList<GridPoint>>();
        var dry = new bool[w, h];
        void Dry(IEnumerable<GridPoint> path, bool road)
        {
            foreach (var p in path)
            {
                if (road) masks[OverworldLayers.Roads][p.X, p.Y] = true;
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) if (Inside(p.X + dx, p.Y + dy)) dry[p.X + dx, p.Y + dy] = true;
            }
        }
        foreach (var route in campaign.Routes)
        {
            var path = route.IsWarp ? new List<GridPoint> { positions[route.From], positions[route.To] } :
                Path(positions[route.From], positions[route.To], groupOf[route.From], groupOf[route.To], route.HasGate && !route.IsTownExit ? route.Id : null);
            routePaths.Add(route.Id, path.AsReadOnly()); Dry(path, route.HasGate && !route.IsWarp && !route.IsTownExit);
        }
        // Prim's minimum spanning network, followed by one shortest unused edge.
        foreach (var g in groups)
        {
            var points = g.Select(l => positions[l.Id]).ToArray(); var joined = new HashSet<int> { 0 }; var edges = new HashSet<(int, int)>();
            var pairs = (from a in Enumerable.Range(0, points.Length)
                         from b in Enumerable.Range(a + 1, points.Length - a - 1)
                         select (a, b, d: Math.Abs(points[a].X - points[b].X) + Math.Abs(points[a].Y - points[b].Y))).OrderBy(e => e.d).ThenBy(e => e.a).ThenBy(e => e.b).ToArray();
            while (joined.Count < points.Length)
            {
                var edge = pairs.First(e => joined.Contains(e.a) != joined.Contains(e.b)); edges.Add((edge.a, edge.b)); joined.Add(edge.a); joined.Add(edge.b);
            }
            if (points.Length >= 3) { var edge = pairs.First(e => !edges.Contains((e.a, e.b))); edges.Add((edge.a, edge.b)); }
            foreach (var e in edges) Dry(Path(points[e.Item1], points[e.Item2], groupOf[g.First().Id], groupOf[g.First().Id]), true);
        }
        foreach (var location in campaign.Locations.Where(l => l.ParentTownId == null))
        {
            var at = positions[location.Id]; Dry(new[] { at }, false);
            string? layer = location.Kind switch
            {
                LocationKind.Town => OverworldLayers.Towns,
                LocationKind.StoryDungeon => OverworldLayers.StoryDungeons,
                LocationKind.RepeatableDungeon => OverworldLayers.RepeatableDungeons,
                LocationKind.FinalDungeon => OverworldLayers.FinalDungeon,
                LocationKind.Converter => OverworldLayers.Converters,
                LocationKind.Landmark => OverworldLayers.Landmarks,
                LocationKind.Secret => OverworldLayers.Secrets,
                _ => null
            };
            if (layer != null) masks[layer][at.X, at.Y] = true;
        }
        // Preserve the short, broad gate approaches as well as all dry navigation buffers.
        foreach (var gate in locks) foreach (var p in gate.Cells)
            for (int dy = -5; dy <= 5; dy++) for (int dx = -5; dx <= 5; dx++)
                if (Inside(p.X + dx, p.Y + dy)) dry[p.X + dx, p.Y + dy] = true;
        var flooded = new bool[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            if (!ground[x, y] || dry[x, y]) continue;
            var biome = palette[regionOwner[x, y]!];
            double waterThreshold = biome switch
            {
                OverworldBiome.Water => -1,
                OverworldBiome.Marsh => .35,
                OverworldBiome.Grassland => .55,
                OverworldBiome.Forest => .6,
                OverworldBiome.Tundra => .65,
                OverworldBiome.Desert => .78,
                _ => .75
            };
            double treeThreshold = biome switch
            {
                OverworldBiome.Forest => .25,
                OverworldBiome.Grassland => .48,
                OverworldBiome.Marsh => .45,
                OverworldBiome.Desert => .85,
                OverworldBiome.Volcanic => .9,
                _ => .7
            };
            double mountainThreshold = biome switch
            {
                OverworldBiome.Mountain => .3,
                OverworldBiome.Volcanic => .4,
                OverworldBiome.Grassland => .58,
                _ => .7
            };
            flooded[x, y] = biome == OverworldBiome.Water ? Noise(x, y, 0) <= -.15 : TerrainNoise(x, y, 0) > waterThreshold;
            if (flooded[x, y]) continue;
            string? feature = TerrainNoise(x, y, 2) > mountainThreshold ? OverworldLayers.Mountains :
                TerrainNoise(x, y, 1) > treeThreshold ? OverworldLayers.Trees : null;
            if (feature == null) continue;
            ground[x, y] = false;
            masks[feature][x, y] = true;
        }
        // Noise may enclose tiny terrain fragments. Fold those into the surrounding obstacles,
        // keeping every closed-gate exploration area connected to its protected destination paths.
        retained = new bool[w, h];
        foreach (var g in groups)
        {
            var at = positions[g.First().Id]; var queue = new Queue<GridPoint>();
            queue.Enqueue(at); retained[at.X, at.Y] = true;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Directions)
                {
                    int x = p.X + d.X, y = p.Y + d.Y;
                    if (!Inside(x, y) || retained[x, y] || !ground[x, y] || owner[x, y] != owner[at.X, at.Y] || masks[OverworldLayers.Locks][x, y]) continue;
                    retained[x, y] = true; queue.Enqueue(new GridPoint(x, y));
                }
            }
        }
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            if (ground[x, y] && !retained[x, y] && !masks[OverworldLayers.Locks][x, y])
            { ground[x, y] = false; flooded[x, y] = false; masks[OverworldLayers.Trees][x, y] = true; }
        var start = positions[campaign.StartLocationId]; masks[OverworldLayers.PlayerStart][start.X, start.Y] = true;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            if (ground[x, y])
            {
                masks[OverworldLayers.Region(regionOwner[x, y]!)][x, y] = true;
                masks[OverworldLayers.Walkable][x, y] = !masks[OverworldLayers.Locks][x, y];
                masks[OverworldLayers.Reserved][x, y] = true;
            }
            else if (owner[x, y] < 0) masks[OverworldLayers.Water][x, y] = true;
            else if (!masks[OverworldLayers.Trees][x, y] && !masks[OverworldLayers.Mountains][x, y])
                masks[palette[regionOwner[x, y]!] == OverworldBiome.Forest ? OverworldLayers.Trees : OverworldLayers.Mountains][x, y] = true;
        }
        var biomes = AssignBiomes(campaign, masks, flooded, regionOwner, locks);
        var grid = new OverworldGrid(campaign, masks.ToDictionary(m => m.Key, m => new GridLayer(m.Value)), positions, routePaths, locks, biomes);
        var validation = OverworldGridValidator.Validate(campaign, grid);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
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
        bool[,] flooded, string?[,] owners, List<GridLock> locks)
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
            water[x, y] |= flooded[x, y];
            if (biome == OverworldBiome.Water)
            {
                // Keep navigation paths and broad seeded islands dry, including Boat providers.

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


}
