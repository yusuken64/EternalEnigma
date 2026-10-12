using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>A connected maze carved only inside a normal forest dungeon footprint.</summary>
internal static class ForestMazeGenerator
{
    internal static DungeonFloor Footprint(DungeonFloorOptions options) => DungeonFloorGenerator.Generate(
        new DungeonFloorOptions(options.Seed, options.Width, options.Height, enemyCount: 0, goldCount: 0,
            itemCount: 0, trapCount: 0, gatheringCount: 0, layoutVersion: 1, biome: OverworldBiome.Forest, tier: options.Tier));

    internal static (bool[,] Mask, GridPoint Start) Generate(DungeonFloorOptions options)
    {
        var footprint = Footprint(options);
        var mask = new bool[options.Width, options.Height];
        var random = new SeedStream(options.Seed, 1706);
        var start = footprint.Rooms[0].Center;
        var carved = new HashSet<GridPoint>();
        void Carve(GridPoint p) { mask[p.X, p.Y] = true; carved.Add(p); }
        // The arrival pad remains wide enough for the party. DFS branches cannot join
        // an existing passage, so former rooms become winding, single-cell corridors.
        foreach (var p in new GridRect(start.X - 1, start.Y - 1, 3, 3).Cells()) Carve(p);
        var stack = new Stack<GridPoint>(random.Shuffle(carved.OrderBy(p => p.Y).ThenBy(p => p.X)));
        void Grow()
        {
            while (stack.Count > 0)
            {
                var p = stack.Peek();
                var candidates = PuzzleState.CardinalNeighbors(p).Where(q => footprint.IsWalkable(q) && !carved.Contains(q) &&
                    PuzzleState.CardinalNeighbors(q).Count(carved.Contains) == 1).ToArray();
                if (candidates.Length == 0) { stack.Pop(); continue; }
                var next = candidates[random.Range(candidates.Length)]; Carve(next); stack.Push(next);
            }
        }
        Grow();
        // A narrow connector can meet an already carved room corner. Connect each
        // remaining room through the footprint, never through its exterior walls.
        foreach (var room in footprint.Rooms)
        {
            if (room.Cells().Any(carved.Contains)) continue;
            var destination = GridSearch.Nearest(room.Center, p => PuzzleState.CardinalNeighbors(p).Where(footprint.IsWalkable), carved.Contains);
            if (!destination.HasValue) throw new InvalidOperationException("Disconnected dungeon footprint.");
            foreach (var p in GridSearch.Path(room.Center, destination.Value, q => PuzzleState.CardinalNeighbors(q).Where(footprint.IsWalkable))) { Carve(p); stack.Push(p); }
            Carve(room.Center);
            Grow();
        }
        return (mask, start);
    }
}
