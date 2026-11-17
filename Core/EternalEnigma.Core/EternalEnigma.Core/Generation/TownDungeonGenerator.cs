using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Intact furnished towns adapted to enclosed, seamless combat districts.</summary>
public static class TownDungeonGenerator
{
    public const int Version = 2;

    public static TownPlanOptions TownOptions(DungeonFloorOptions options)
    {
        if (options.Variant != DungeonVariant.Town) throw new ArgumentException("A town variant is required.", nameof(options));
        int count = 8 + options.Tier;
        var types = new[] { new TownInteriorSpec(TownInteriorKind.Inn), new TownInteriorSpec(TownInteriorKind.Trainer),
            new TownInteriorSpec(TownInteriorKind.Shop, TownShopTheme.Bakery), new TownInteriorSpec(TownInteriorKind.Shop, TownShopTheme.Equipment),
            new TownInteriorSpec(TownInteriorKind.Shop, TownShopTheme.Consumables) };
        var interiors = new SeedStream(options.Seed, 1900).Shuffle(Enumerable.Range(0, count)
            .Select(i => i < types.Length ? types[i] : new TownInteriorSpec(TownInteriorKind.Residential)));
        return new TownPlanOptions(options.Seed, options.Width, options.Height, Enumerable.Repeat(true, count).ToArray(),
            allyCount: 0, partySpawn: new GridPoint(options.Width / 2, 4), exit: new GridPoint(options.Width / 2, 1),
            spineX: options.Width / 2, detailed: true, interiors: interiors);
    }

    /// <summary>Five clear cells across, leaving room for a 3x3 boss plus adjacent combatants.</summary>
    public static GridRect BossSquare(TownPlan town) => new(town.SpineX - 2, town.Height / 2 - 5, 5, 5);

    internal static DungeonFloor Generate(DungeonFloorOptions options)
    {
        var town = TownPlanGenerator.Generate(TownOptions(options));
        var layers = town.Layers.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
        int w = town.Width, h = town.Height;
        var boundary = new bool[w, h];
        var walkable = layers[TownLayers.Walkable];
        var trees = layers[TownLayers.Trees];
        var props = layers[TownLayers.Props];
        // The visible enclosing wall sits on the border; both district gates are inside it.
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
            if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
            {
                boundary[x, y] = true; walkable[x, y] = false; trees[x, y] = false; props[x, y] = false;
            }
        layers[TownLayers.Boundary] = boundary;

        // Town trees can leave cells connected only by a diagonal corner. Open paths through
        // exterior vegetation, never through building walls or furniture, using cardinal steps.
        bool Inside(GridPoint p) => p.X > 0 && p.Y > 0 && p.X < w - 1 && p.Y < h - 1;
        bool Open(GridPoint p) => Inside(p) && walkable[p.X, p.Y];
        bool Clearable(GridPoint p) => Inside(p) && (Open(p) || trees[p.X, p.Y]);
        Func<GridPoint, IEnumerable<GridPoint>> openSteps = p => GridSteps.CardinalNeighbors(p, Open);
        Func<GridPoint, IEnumerable<GridPoint>> clearableSteps = p => GridSteps.CardinalNeighbors(p, Clearable);
        var reachable = GridSearch.VisitOrder(town.PartySpawn, openSteps).ToHashSet();
        // Repairs only ever open cells, so the reachable set only grows: extend it from each repaired path
        // instead of re-flooding the town. `reached` mirrors set membership; the ordered set is rebuilt once below.
        var reached = new bool[w, h];
        foreach (var p in reachable) if (p.X >= 0 && p.Y >= 0 && p.X < w && p.Y < h) reached[p.X, p.Y] = true;
        bool IsReached(GridPoint p) => p.X >= 0 && p.Y >= 0 && p.X < w && p.Y < h ? reached[p.X, p.Y] : reachable.Contains(p);
        Func<GridPoint, bool> isReached = IsReached;
        var grow = new Queue<GridPoint>();
        bool repaired = false;
        for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
        {
            var cell = new GridPoint(x, y);
            if (!Open(cell) || IsReached(cell)) continue;
            var target = GridSearch.Nearest(cell, clearableSteps, isReached)
                ?? throw new InvalidOperationException($"District interior at {cell} cannot reach a street.");
            var path = GridSearch.Path(cell, target, clearableSteps);
            foreach (var p in path)
            { trees[p.X, p.Y] = false; props[p.X, p.Y] = false; walkable[p.X, p.Y] = true; }
            // The path ends on a reached cell, so everything now open and connected to it joins the set.
            grow.Clear();
            foreach (var p in path) if (!reached[p.X, p.Y]) { reached[p.X, p.Y] = true; grow.Enqueue(p); }
            while (grow.Count > 0)
            {
                var at = grow.Dequeue();
                foreach (var next in GridSteps.CardinalNeighbors(at, Open))
                    if (!reached[next.X, next.Y]) { reached[next.X, next.Y] = true; grow.Enqueue(next); }
            }
            repaired = true;
        }
        if (repaired) reachable = GridSearch.VisitOrder(town.PartySpawn, openSteps).ToHashSet();

        var square = BossSquare(town);
        foreach (var p in square.Cells())
        {
            if (!Open(p)) throw new InvalidOperationException("District square must remain clear.");
            layers[TownLayers.Roads][p.X, p.Y] = true;
            layers[TownLayers.MainRoads][p.X, p.Y] = true;
            props[p.X, p.Y] = false;
        }
        // Choose different approaches to the district using an independent stream.
        // Keep the building orientation intact: doorway movement remains cardinal.
        var gates = new SeedStream(options.Seed, 1901);
        var approaches = reachable.Where(p => layers[TownLayers.Roads][p.X, p.Y] &&
            !layers[TownLayers.ShopFloor][p.X, p.Y] &&
            new GridRect(p.X - 1, p.Y - 1, 3, 3).Cells().All(Open)).ToArray();
        int side = gates.Range(4);
        int Edge(GridPoint p, int direction) => direction switch { 0 => p.Y, 1 => w - 1 - p.X, 2 => h - 1 - p.Y, _ => p.X };
        var arrival = approaches.OrderBy(p => Edge(p, side)).ThenBy(p => Math.Abs(p.X - w / 2) + Math.Abs(p.Y - h / 2)).ThenBy(p => p.X).First();
        int exitSide = (side + 1 + gates.Range(3)) % 4;
        var stairs = approaches.Where(p => Math.Max(Math.Abs(p.X - arrival.X), Math.Abs(p.Y - arrival.Y)) >= 8)
            .OrderBy(p => Edge(p, exitSide)).ThenBy(p => Math.Abs(p.X - w / 2) + Math.Abs(p.Y - h / 2)).ThenBy(p => p.X).First();
        layers[TownLayers.Dungeon][town.DungeonEntrance.X, town.DungeonEntrance.Y] = false;
        layers[TownLayers.Dungeon][stairs.X, stairs.Y] = true;
        town = new TownPlan(town.Seed, layers.ToDictionary(p => p.Key, p => new GridLayer(p.Value), StringComparer.Ordinal),
            town.BuildingSlots, town.AllySlots, town.ShopRooms, arrival, arrival, stairs,
            town.SpineX, town.Footprints, town.Interiors);
        return BiomeDungeonGenerator.Populate(options, walkable, new List<GridRect> { square }, arrival, stairs, town);
    }
}
