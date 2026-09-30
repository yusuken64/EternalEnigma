using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>
/// Detailed towns: buildings with their own footprints, a road hierarchy (wide main roads, single-cell
/// arteries to every door, single-cell back alleys) and a layer of decoration cells for the town biome.
/// </summary>
public static partial class TownPlanGenerator
{
    /// <summary>Main roads are 2 * MainHalf + 1 cells wide.</summary>
    private const int MainHalf = 1;
    private const int AlleyChancePercent = 70;
    /// <summary>Cells this close to the town edge form the decorated frame around the city.</summary>
    private const int PropFrameWidth = 2;
    private const int PropFramePercent = 65, PropVergePercent = 14, PropFieldPercent = 6;

    private static readonly GridPoint[] Cardinals = { new(0, 1), new(-1, 0), new(1, 0), new(0, -1) };

    /// <summary>The wide vertical spine and the wide cross avenue. Buildings never cover these cells.</summary>
    private static bool[,] MainRoadMask(TownPlanOptions options)
    {
        var mask = new bool[options.Width, options.Height];
        int avenueY = options.Height / 2 + 1;
        for (int x = 0; x < options.Width; x++)
            for (int y = 0; y < options.Height; y++)
            {
                bool spine = Math.Abs(x - options.SpineX) <= MainHalf;
                bool avenue = x >= 1 && x <= options.Width - 2 && Math.Abs(y - avenueY) <= MainHalf;
                mask[x, y] = spine || avenue;
            }
        return mask;
    }

    private static (List<GridPoint> doors, List<BuildingFootprint> footprints) GenerateDetailedPlots(
        TownPlanOptions options, Dictionary<string, bool[,]> layers, SeedStream plots, SeedStream shapes, bool[,] mainRoad)
    {
        int W = options.Width, H = options.Height;
        var doors = new List<GridPoint>();
        var footprints = new List<BuildingFootprint>();
        var bodyCells = new HashSet<GridPoint>();

        bool InsideTown(GridPoint c) => c.X >= 1 && c.X <= W - 2 && c.Y >= 1 && c.Y <= H - 2;
        bool Fits(BuildingFootprint fp)
        {
            foreach (var cell in fp.Cells)
            {
                if (!InsideTown(cell) || mainRoad[cell.X, cell.Y] || bodyCells.Contains(cell) ||
                    TownPlan.IsReservedCorridor(cell, H, options.SpineX) ||
                    cell.Equals(options.PartySpawn) || cell.Equals(options.Exit))
                    return false;
            }
            // Bodies keep a two-cell gap, which leaves room for an alley between neighbours.
            return !footprints.Any(other => other.Bounds.Intersects(fp.Bounds, margin: 1) || fp.Bounds.Contains(other.Door) || other.Bounds.Contains(fp.Door));
        }

        var candidates = new List<GridPoint>();
        for (int x = 1; x <= W - 2; x++)
            for (int y = 1; y <= H - 2; y++)
            {
                var door = new GridPoint(x, y);
                if (mainRoad[x, y] || TownPlan.IsReservedCorridor(door, H, options.SpineX)) continue;
                if (door.Equals(options.PartySpawn) || door.Equals(options.Exit)) continue;
                candidates.Add(door);
            }

        foreach (var door in plots.Shuffle(candidates))
        {
            if (doors.Count >= options.BuildingCount) break;
            if (doors.Any(d => Math.Max(Math.Abs(door.X - d.X), Math.Abs(door.Y - d.Y)) < 3)) continue;
            if (bodyCells.Contains(door) || Cardinals.Any(step => bodyCells.Contains(new GridPoint(door.X + step.X, door.Y + step.Y)))) continue;

            BuildingFootprint? placed = null;
            for (int attempt = 0; attempt < 3 && placed == null; attempt++)
            {
                var shape = BuildingShapes.Random(shapes, door);
                if (Fits(shape)) placed = shape;
            }
            if (placed == null)
            {
                var fallback = BuildingShapes.Default(door);
                if (Fits(fallback)) placed = fallback;
            }
            if (placed == null) continue;

            doors.Add(door);
            footprints.Add(placed);
            layers[TownLayers.Buildings][door.X, door.Y] = true;
            foreach (var cell in placed.Cells)
            {
                bodyCells.Add(cell);
                layers[TownLayers.Houses][cell.X, cell.Y] = true;
            }
        }

        // Same fallback as the simple layout: a door with no body is better than a missing building.
        if (doors.Count < options.BuildingCount)
        {
            var walkableSoFar = new bool[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    walkableSoFar[x, y] = !layers[TownLayers.Houses][x, y] && !layers[TownLayers.Buildings][x, y] && !mainRoad[x, y];
            foreach (var door in TownPlacement.CompleteBuildingPositions(new GridLayer(walkableSoFar), null, doors, options.PartySpawn, options.BuildingCount))
            {
                if (doors.Contains(door)) continue;
                doors.Add(door);
                footprints.Add(BuildingFootprint.Bodyless(door));
                layers[TownLayers.Buildings][door.X, door.Y] = true;
            }
        }

        if (doors.Count < options.BuildingCount)
            throw new InvalidOperationException($"Could not place {options.BuildingCount} buildings; only placed {doors.Count}");
        return (doors, footprints);
    }

    private static List<ShopRoom> RoomsFromFootprints(List<BuildingFootprint> footprints, List<GridPoint> doorsInScanOrder, List<bool> shopFlagsInScanOrder)
    {
        var rooms = new List<ShopRoom>();
        for (int i = 0; i < doorsInScanOrder.Count; i++)
        {
            if (!shopFlagsInScanOrder[i]) continue;
            var footprint = footprints.First(f => f.Door.Equals(doorsInScanOrder[i]));
            var room = footprint.TryBuildRoom();
            if (room != null) rooms.Add(room); // a missing room is reported by the validator
        }
        return rooms;
    }

    private static void GenerateDetailedRoads(Dictionary<string, bool[,]> layers, List<GridPoint> doors, List<BuildingFootprint> footprints,
        bool[,] mainRoad, TownPlanOptions options, SeedStream random)
    {
        int W = options.Width, H = options.Height;
        var roads = layers[TownLayers.Roads];
        var main = layers[TownLayers.MainRoads];
        var alleys = layers[TownLayers.Alleys];
        bool InBounds(GridPoint c) => c.X >= 0 && c.X < W && c.Y >= 0 && c.Y < H;
        bool Solid(GridPoint c) => layers[TownLayers.Houses][c.X, c.Y] || layers[TownLayers.ShopWalls][c.X, c.Y] || layers[TownLayers.ShopFloor][c.X, c.Y];

        // 1. Wide main roads.
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                if (mainRoad[x, y] && !Solid(new GridPoint(x, y))) { roads[x, y] = true; main[x, y] = true; }

        // 2. Single-cell arteries: every door gets the shortest way to the network.
        foreach (var door in doors)
        {
            IEnumerable<GridPoint> Open(GridPoint c) => Cardinals
                .Select(step => new GridPoint(c.X + step.X, c.Y + step.Y))
                .Where(n => InBounds(n) && !Solid(n) && !(layers[TownLayers.Buildings][n.X, n.Y] && !n.Equals(door)));
            var target = GridSearch.Nearest(door, Open, c => roads[c.X, c.Y]);
            if (target == null) throw new InvalidOperationException($"No road reachable from door {door}");
            foreach (var cell in GridSearch.Path(door, target.Value, Open))
                if (!cell.Equals(door)) roads[cell.X, cell.Y] = true;
        }

        // 3. Back alleys: a single-cell lane along the rear of most buildings, joined to the network.
        foreach (var footprint in footprints)
        {
            bool wanted = random.Range(100) < AlleyChancePercent; // drawn for every building so one rejection does not shift the rest
            if (!wanted || footprint.Cells.Count == 0) continue;
            int y = footprint.Bounds.Top + 1;
            if (y < 1 || y > H - 2) continue;

            bool Free(int x) => x >= 1 && x <= W - 2 && !Solid(new GridPoint(x, y)) && !layers[TownLayers.Buildings][x, y] && !mainRoad[x, y];
            int centre = footprint.Door.X;
            if (!Free(centre)) continue;
            int reach = footprint.Bounds.Width / 2 + 1;
            int left = centre, right = centre;
            while (left - 1 >= centre - reach && Free(left - 1)) left--;
            while (right + 1 <= centre + reach && Free(right + 1)) right++;
            if (right - left + 1 < 3) continue;

            var lane = Enumerable.Range(left, right - left + 1).Select(x => new GridPoint(x, y)).ToList();
            var laneSet = new HashSet<GridPoint>(lane);
            bool joined = lane.Any(c => roads[c.X, c.Y] ||
                Cardinals.Any(step => { var n = new GridPoint(c.X + step.X, c.Y + step.Y); return InBounds(n) && !laneSet.Contains(n) && roads[n.X, n.Y]; }));

            var link = new List<GridPoint>();
            if (!joined)
            {
                IEnumerable<GridPoint> Open(GridPoint c) => Cardinals
                    .Select(step => new GridPoint(c.X + step.X, c.Y + step.Y))
                    .Where(n => InBounds(n) && !Solid(n) && !layers[TownLayers.Buildings][n.X, n.Y]);
                foreach (var end in new[] { lane[0], lane[lane.Count - 1] })
                {
                    var target = GridSearch.Nearest(end, Open, c => roads[c.X, c.Y] && !laneSet.Contains(c));
                    if (target == null) continue;
                    link = GridSearch.Path(end, target.Value, Open).Where(c => !roads[c.X, c.Y]).ToList();
                    joined = true;
                    break;
                }
            }
            if (!joined) continue;

            foreach (var cell in lane.Concat(link))
            {
                if (roads[cell.X, cell.Y]) continue; // never reclassify an existing road
                roads[cell.X, cell.Y] = true;
                alleys[cell.X, cell.Y] = true;
            }
        }
    }

    /// <summary>
    /// Non-blocking decoration cells: densest in a frame around the town edge, lighter along road verges,
    /// sparse elsewhere. Doorways, roads, buildings, allies and the entrance corridor stay clear.
    /// </summary>
    private static void GenerateProps(Dictionary<string, bool[,]> layers, SeedStream random, TownPlanOptions options)
    {
        int W = options.Width, H = options.Height;
        var props = layers[TownLayers.Props];
        var blockers = new[]
        {
            TownLayers.Roads, TownLayers.Houses, TownLayers.Trees, TownLayers.ShopWalls, TownLayers.ShopFloor,
            TownLayers.Buildings, TownLayers.Allies, TownLayers.Dungeon,
        };
        bool Has(string layer, int x, int y) => x >= 0 && x < W && y >= 0 && y < H && layers[layer][x, y];

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var cell = new GridPoint(x, y);
                if (blockers.Any(layer => layers[layer][x, y])) continue;
                if (TownPlan.IsReservedCorridor(cell, H, options.SpineX) || cell.Equals(options.PartySpawn) || cell.Equals(options.Exit)) continue;
                if (Cardinals.Any(step => Has(TownLayers.Buildings, x + step.X, y + step.Y))) continue; // keep doorways clear

                int edgeDistance = Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y));
                bool verge = Cardinals.Any(step => Has(TownLayers.Roads, x + step.X, y + step.Y));
                int percent = edgeDistance < PropFrameWidth ? PropFramePercent : verge ? PropVergePercent : PropFieldPercent;
                if (random.Range(100) < percent) props[x, y] = true;
            }
    }
}
