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
    /// <summary>West, east, south, north: the order of the per-cell neighbour arrays this replaces.</summary>
    private static readonly GridPoint[] Adjacent4 = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };

    /// <summary>
    /// Array-backed equivalents of GridSearch.Nearest and GridSearch.Path over Cardinals-ordered steps
    /// (`passable` plays the role of the neighbour filter), reusable across searches on one grid.
    /// Visit order, first-found target and BFS parents are exactly those of GridSearch, so results are identical.
    /// </summary>
    private sealed class CardinalSearch
    {
        readonly int w, h;
        readonly int[,] stamp;
        readonly int[,] parent;
        readonly GridPoint[] queue;
        int generation;

        public CardinalSearch(int w, int h)
        {
            this.w = w; this.h = h;
            stamp = new int[w, h]; parent = new int[w, h];
            queue = new GridPoint[w * h + 1];
        }

        bool Inside(GridPoint p) => p.X >= 0 && p.Y >= 0 && p.X < w && p.Y < h;

        // Mirrors GridSearch's HashSet visited; out-of-bounds cells are never passable, so only the origin can be outside.
        bool Visited(GridPoint p, GridPoint origin) => p.Equals(origin) || (Inside(p) && stamp[p.X, p.Y] == generation);
        void Visit(GridPoint p, GridPoint from) { stamp[p.X, p.Y] = generation; parent[p.X, p.Y] = from.Y * w + from.X; }

        public GridPoint? Nearest(GridPoint origin, Func<GridPoint, bool> passable, Func<GridPoint, bool> isTarget)
        {
            if (isTarget(origin)) return origin;
            generation++;
            int head = 0, tail = 0;
            queue[tail++] = origin;
            if (Inside(origin)) stamp[origin.X, origin.Y] = generation;
            while (head < tail)
            {
                var current = queue[head++];
                foreach (var step in Cardinals)
                {
                    var next = new GridPoint(current.X + step.X, current.Y + step.Y);
                    if (!passable(next) || Visited(next, origin)) continue;
                    if (isTarget(next)) return next;
                    Visit(next, current); queue[tail++] = next;
                }
            }
            return null;
        }

        public List<GridPoint> Path(GridPoint origin, GridPoint target, Func<GridPoint, bool> passable)
        {
            if (origin.Equals(target)) return new List<GridPoint> { origin };
            generation++;
            int head = 0, tail = 0;
            queue[tail++] = origin;
            if (Inside(origin)) stamp[origin.X, origin.Y] = generation;
            while (head < tail)
            {
                var current = queue[head++];
                if (current.Equals(target))
                {
                    var path = new List<GridPoint>();
                    var node = target;
                    while (!node.Equals(origin))
                    {
                        path.Add(node);
                        int index = parent[node.X, node.Y];
                        node = new GridPoint(index % w, index / w);
                    }
                    path.Add(origin);
                    path.Reverse();
                    return path;
                }
                foreach (var step in Cardinals)
                {
                    var next = new GridPoint(current.X + step.X, current.Y + step.Y);
                    if (!passable(next) || Visited(next, origin)) continue;
                    Visit(next, current); queue[tail++] = next;
                }
            }
            return new List<GridPoint>();
        }
    }

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
        bool CellFits(GridPoint cell) =>
            InsideTown(cell) && !mainRoad[cell.X, cell.Y] && !bodyCells.Contains(cell) &&
            !TownPlan.IsReservedCorridor(cell, H, options.SpineX) &&
            !cell.Equals(options.PartySpawn) && !cell.Equals(options.Exit);
        // A rear notch against both enclosing district walls would create an
        // outdoor pocket with no exit. Leave access around at least one side.
        bool BoundsFit(GridRect bounds, bool hasNotch) =>
            !(hasNotch && bounds.Top == H - 2 && (bounds.X == 1 || bounds.Right == W - 2));
        bool Spaced(GridPoint door, GridRect bounds)
        {
            // Bodies keep a two-cell gap, which leaves room for an alley between neighbours.
            foreach (var other in footprints)
                if (other.Bounds.Intersects(bounds, margin: 1) || bounds.Contains(other.Door) || other.Bounds.Contains(door)) return false;
            return true;
        }
        bool Fits(BuildingFootprint fp)
        {
            if (!BoundsFit(fp.Bounds, fp.HasNotch)) return false;
            foreach (var cell in fp.Cells) if (!CellFits(cell)) return false;
            return Spaced(fp.Door, fp.Bounds);
        }
        // Same verdict as Fits(shape.Build(door)) without building the footprint.
        bool ShapeFits(BuildingShapes.Shape shape, GridPoint door)
        {
            var bounds = shape.Bounds(door);
            if (!BoundsFit(bounds, shape.HasNotch)) return false;
            foreach (var offset in shape.Offsets) if (!CellFits(new GridPoint(door.X + offset.X, door.Y + offset.Y))) return false;
            return Spaced(door, bounds);
        }

        void Place(BuildingFootprint footprint)
        {
            doors.Add(footprint.Door);
            footprints.Add(footprint);
            layers[TownLayers.Buildings][footprint.Door.X, footprint.Door.Y] = true;
            foreach (var cell in footprint.Cells)
            {
                bodyCells.Add(cell);
                layers[TownLayers.Houses][cell.X, cell.Y] = true;
            }
        }

        if (options.OpeningNeighborhood)
        {
            // A short, clear southern street gives the first errands a stable local geography.
            // Their shared door row is deliberately first in raster order. Services retain
            // their full furnished footprint; only the domestic home gets a smaller body.
            var opening = new[] {
                BuildingFootprint.Rectangle(new GridPoint(options.SpineX - 17, 3), 3, 7),
                BuildingShapes.FurnishedDefault(new GridPoint(options.SpineX - 7, 3)),
                BuildingShapes.FurnishedDefault(new GridPoint(options.SpineX + 7, 3))
            };
            foreach (var footprint in opening)
            {
                if (!Fits(footprint)) throw new InvalidOperationException("Opening neighborhood footprint does not fit.");
                Place(footprint);
            }
        }

        var candidates = new List<GridPoint>();
        for (int x = 1; x <= W - 2; x++)
            for (int y = 1; y <= H - 2; y++)
            {
                var door = new GridPoint(x, y);
                // Furnished districts enclose the map border; the south-facing
                // doorway still needs its approach cell inside that enclosure.
                if (options.Furnished && y == 1) continue;
                if (options.OpeningNeighborhood && y < 14) continue;
                if (mainRoad[x, y] || TownPlan.IsReservedCorridor(door, H, options.SpineX)) continue;
                if (door.Equals(options.PartySpawn) || door.Equals(options.Exit)) continue;
                candidates.Add(door);
            }

        foreach (var door in plots.Shuffle(candidates))
        {
            if (doors.Count >= options.BuildingCount) break;
            bool crowded = false;
            foreach (var d in doors) if (Math.Max(Math.Abs(door.X - d.X), Math.Abs(door.Y - d.Y)) < 3) { crowded = true; break; }
            if (crowded) continue;
            if (bodyCells.Contains(door)) continue;
            foreach (var step in Cardinals) if (bodyCells.Contains(new GridPoint(door.X + step.X, door.Y + step.Y))) { crowded = true; break; }
            if (crowded) continue;

            // Shapes are drawn (same draws) and tested without building them; only the placed one becomes a footprint.
            BuildingShapes.Shape? placed = null;
            for (int attempt = 0; attempt < 3 && placed == null; attempt++)
            {
                var shape = options.Furnished ? BuildingShapes.FurnishedRandomShape(shapes) : BuildingShapes.RandomShape(shapes);
                if (ShapeFits(shape, door)) placed = shape;
            }
            if (placed == null)
            {
                var fallback = options.Furnished ? BuildingShapes.FurnishedDefaultShape : BuildingShapes.DefaultShape;
                if (ShapeFits(fallback, door)) placed = fallback;
            }
            if (placed == null) continue;

            Place(placed.Value.Build(door));
        }

        // Same fallback as the simple layout: a door with no body is better than a missing building.
        if (doors.Count < options.BuildingCount && !options.Furnished)
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
        var search = new CardinalSearch(W, H);
        var buildings = layers[TownLayers.Buildings];
        Func<GridPoint, bool> isRoad = c => roads[c.X, c.Y];
        foreach (var door in doors)
        {
            bool Open(GridPoint n) => InBounds(n) && !Solid(n) && !(buildings[n.X, n.Y] && !n.Equals(door));
            Func<GridPoint, bool> open = Open;
            var target = search.Nearest(door, open, isRoad);
            if (target == null) throw new InvalidOperationException($"No road reachable from door {door}");
            foreach (var cell in search.Path(door, target.Value, open))
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
                Func<GridPoint, bool> open = n => InBounds(n) && !Solid(n) && !buildings[n.X, n.Y];
                foreach (var end in new[] { lane[0], lane[lane.Count - 1] })
                {
                    var target = search.Nearest(end, open, c => roads[c.X, c.Y] && !laneSet.Contains(c));
                    if (target == null) continue;
                    link = search.Path(end, target.Value, open).Where(c => !roads[c.X, c.Y]).ToList();
                    joined = true;
                    break;
                }
            }
            if (!joined) continue;

            foreach (var cell in lane.Concat(link))
            {
                if (roads[cell.X, cell.Y]) continue; // never reclassify an existing road
                roads[cell.X, cell.Y] = true;
                // A turn or a join between neighbouring lanes can complete a 2x2
                // junction. Keep that connector as a street, not a widened alley.
                bool junction = false;
                for (int dx = -1; dx <= 1; dx += 2)
                    for (int dy = -1; dy <= 1; dy += 2)
                    {
                        int x = cell.X + dx, row = cell.Y + dy;
                        if (x >= 0 && x < W && row >= 0 && row < H &&
                            alleys[x, cell.Y] && alleys[cell.X, row] && alleys[x, row]) junction = true;
                    }
                alleys[cell.X, cell.Y] = !junction;
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
        // Resolved once; the per-cell tests below are plain loops (same results as the former Any lambdas).
        var blockerMasks = blockers.Select(layer => layers[layer]).ToArray();
        var buildings = layers[TownLayers.Buildings];
        var roads = layers[TownLayers.Roads];
        bool Has(bool[,] mask, int x, int y) => x >= 0 && x < W && y >= 0 && y < H && mask[x, y];
        bool AnyCardinal(bool[,] mask, int x, int y)
        {
            foreach (var step in Cardinals) if (Has(mask, x + step.X, y + step.Y)) return true;
            return false;
        }

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var cell = new GridPoint(x, y);
                bool blocked = false;
                foreach (var mask in blockerMasks) if (mask[x, y]) { blocked = true; break; }
                if (blocked) continue;
                if (TownPlan.IsReservedCorridor(cell, H, options.SpineX) || cell.Equals(options.PartySpawn) || cell.Equals(options.Exit)) continue;
                if (AnyCardinal(buildings, x, y)) continue; // keep doorways clear

                int edgeDistance = Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y));
                bool verge = AnyCardinal(roads, x, y);
                int percent = edgeDistance < PropFrameWidth ? PropFramePercent : verge ? PropVergePercent : PropFieldPercent;
                if (random.Range(100) < percent) props[x, y] = true;
            }
    }
}
