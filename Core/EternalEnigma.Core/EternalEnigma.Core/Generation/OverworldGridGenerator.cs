using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Validation;

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

/// <summary>Embeds the current campaign tree without crossings; unsupported cyclic graphs fail explicitly.</summary>
public static class OverworldGridGenerator
{
    public static OverworldGrid Generate(Campaign campaign, OverworldGridOptions? options = null)
    {
        if (campaign == null) throw new ArgumentNullException(nameof(campaign));
        var validation = CampaignValidator.Validate(campaign);
        if (!validation.IsValid) throw new ArgumentException("Invalid campaign:\n" + string.Join("\n", validation.Errors), nameof(campaign));
        options ??= new OverworldGridOptions();
        if (campaign.Routes.Count != campaign.Locations.Count - 1)
            throw new NotSupportedException("Grid generation currently supports tree campaigns only; cycles/parallel routes need a planar embedding strategy.");
        var byId = campaign.Locations.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var children = byId.Keys.ToDictionary(id => id, _ => new List<(string child, CampaignRoute route)>(), StringComparer.Ordinal);
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var postorder = new List<string>();
        var random = new SeedStream(campaign.Seed, 100);
        void Orient(string node, string? parent, int depth)
        {
            if (depths.ContainsKey(node)) throw new NotSupportedException("Campaign routes contain a cycle.");
            depths[node] = depth;
            var edges = random.Shuffle(campaign.Routes.Where(r => r.Other(node) != null && r.Other(node) != parent).OrderBy(r => r.Id, StringComparer.Ordinal));
            foreach (var edge in edges)
            { string child = edge.Other(node)!; children[node].Add((child, edge)); Orient(child, node, depth + 1); }
            postorder.Add(node);
        }
        // Checkpoint-rooted layout is compact; player start remains the campaign's start town.
        string root = campaign.Locations.Where(l => l.Kind == LocationKind.Checkpoint).OrderBy(l => l.Stage).ThenBy(l => l.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? campaign.StartLocationId;
        Orient(root, null, 0);
        if (depths.Count != byId.Count) throw new ArgumentException("Campaign is disconnected.", nameof(campaign));
        const int margin = 4, depthSpacing = 12, leafSpacing = 5;
        int requiredWidth = margin * 2 + depths.Values.Max() * depthSpacing + 3;
        int leafCount = children.Values.Count(c => c.Count == 0);
        int requiredHeight = margin * 2 + (leafCount - 1) * leafSpacing + 3;
        if (requiredWidth > options.Width || requiredHeight > options.Height)
            throw new ArgumentException($"Campaign needs at least {requiredWidth}x{requiredHeight} tiles; requested {options.Width}x{options.Height}. Increase map dimensions.", nameof(options));

        int leaf = 0;
        var positions = new Dictionary<string, GridPoint>(StringComparer.Ordinal);
        var spans = new Dictionary<string, (int low, int high)>(StringComparer.Ordinal);
        int offsetX = (options.Width - requiredWidth) / 2, offsetY = (options.Height - requiredHeight) / 2;
        foreach (var node in postorder)
        {
            var next = children[node];
            int low = next.Count == 0 ? margin + leaf++ * leafSpacing : spans[next[0].child].low;
            int high = next.Count == 0 ? low : spans[next[next.Count - 1].child].high;
            spans[node] = (low, high);
            positions[node] = new GridPoint(offsetX + margin + depths[node] * depthSpacing, offsetY + (low + high) / 2);
        }
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
        foreach (var parent in postorder)
        foreach (var connection in children[parent])
        {
            var from = positions[parent]; var to = positions[connection.child];
            int branchX = from.X + depthSpacing / 2;
            var path = new List<GridPoint> { from };
            void LineTo(int x, int y)
            {
                var current = path[path.Count - 1];
                while (current.X != x || current.Y != y)
                {
                    current = new GridPoint(current.X + Math.Sign(x - current.X), current.Y + Math.Sign(y - current.Y));
                    path.Add(current);
                }
            }
            LineTo(branchX, from.Y); LineTo(branchX, to.Y); LineTo(to.X, to.Y);
            foreach (var cell in path) Carve(cell, byId[parent].RegionId, true);
            if (connection.route.From != parent) path.Reverse();
            routePaths.Add(connection.route.Id, Array.AsReadOnly(path.ToArray()));
            if (!connection.route.Requirement.IsOpen)
            {
                int length = connection.route.Form == LockForm.Area ? 3 : 1;
                var cells = Enumerable.Range(0, length).Select(i => new GridPoint(to.X - 3 - i, to.Y)).ToArray();
                locks.Add(new GridLock(connection.route.Id, cells));
                string layer = connection.route.Form == LockForm.Area ? OverworldLayers.AreaLocks :
                    connection.route.Form == LockForm.Obstacle ? OverworldLayers.ObstacleLocks : OverworldLayers.InteractionLocks;
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
        ExpandAreas(ground, masks[OverworldLayers.Locks], regionOwner, options.AreaExpansionRadius, campaign.Seed);
        var decoration = new SeedStream(campaign.Seed, 101);
        for (int y = 0; y < options.Height; y++)
        for (int x = 0; x < options.Width; x++)
        {
            bool border = x < 2 || y < 2 || x >= options.Width - 2 || y >= options.Height - 2;
            // Consume the original decoration stream even where expansion replaces background.
            bool tree = !originalGround[x, y] && !border && decoration.Range(4) == 0;
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
        var grid = new OverworldGrid(campaign, masks.ToDictionary(m => m.Key, m => new GridLayer(m.Value), StringComparer.Ordinal), positions, routePaths, locks);
        var gridValidation = OverworldGridValidator.Validate(campaign, grid);
        if (!gridValidation.IsValid) throw new InvalidOperationException("Grid embedding failed validation:\n" + string.Join("\n", gridValidation.Errors));
        return grid;
    }

    private static void ExpandAreas(bool[,] ground, bool[,] gates, string?[,] regions, int radius, int seed)
    {
        if (radius == 0) return;
        int width = ground.GetLength(0), height = ground.GetLength(1);
        var owners = new int[width, height];
        var protectedCells = new bool[width, height];
        var costs = new int[width, height];
        var random = new SeedStream(seed, 102);
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
            if (distance >= radius) return;
            foreach (var direction in directions)
            {
                int x = at.X + direction.X, y = at.Y + direction.Y;
                if (x < 2 || y < 2 || x >= width - 2 || y >= height - 2 || ground[x, y] || protectedCells[x, y]) continue;
                int cost = spent + costs[x, y];
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
            if (touchesOther) continue;
            ground[x, y] = true;
            owners[x, y] = next.owner;
            regions[x, y] = next.region;
            Offer(next.at, next.owner, next.region, next.distance, cost);
        }
    }
}
