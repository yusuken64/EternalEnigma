using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Ordinary traps share one deterministic placement policy across combat floor variants.</summary>
internal static class DungeonTrapPlacement
{
    internal static HashSet<GridPoint> ProtectedCells(DungeonFloor floor)
    {
        var safe = new HashSet<GridPoint> { floor.Start, floor.Stairs };
        // Cardinal paths protect their own diagonal supports and obey town doorway rules.
        safe.UnionWith(GridSearch.Path(floor.Start, floor.Stairs, p => PuzzleState.CardinalNeighbors(p).Where(floor.IsWalkable)));
        foreach (var p in new GridRect(floor.Start.X - 3, floor.Start.Y - 3, 7, 7).Cells()) safe.Add(p);
        safe.UnionWith(floor.PuzzleObjects.Select(p => p.Position));
        safe.UnionWith(floor.RecoveryStashes.Select(p => p.Position));
        foreach (var enemy in floor.ObjectiveEnemies)
            safe.UnionWith(new GridRect(enemy.Position.X - enemy.Footprint / 2, enemy.Position.Y - enemy.Footprint / 2, enemy.Footprint, enemy.Footprint).Cells());
        if (floor.TownPlan is { } town)
        {
            safe.UnionWith(TownDungeonGenerator.BossSquare(town).Cells());
            foreach (var door in town.BuildingSlots) safe.UnionWith(new GridRect(door.X - 1, door.Y - 1, 3, 4).Cells());
        }
        if (floor.Objective.AllowsReset)
        {
            var puzzle = new PuzzleState(floor);
            foreach (var step in floor.SolutionWitness)
            {
                if (!puzzle.TryMove(step)) throw new InvalidOperationException("Invalid puzzle witness during trap placement.");
                safe.Add(puzzle.Player);
                if (puzzle.Enemy.HasValue) safe.Add(puzzle.Enemy.Value);
                safe.UnionWith(puzzle.Boulders.Values);
            }
            safe.UnionWith(GridSearch.Path(puzzle.Player, floor.Stairs, p => PuzzleState.CardinalNeighbors(p)
                .Where(q => floor.IsWalkable(q) && !puzzle.Boulders.Values.Contains(q))));
        }
        return safe;
    }

    internal static DungeonFloor Populate(DungeonFloor floor, DungeonFloorOptions options)
    {
        if (floor.IsThroneFloor || options.TrapCount == 0) return floor;
        var reserved = ProtectedCells(floor);
        reserved.UnionWith(floor.Enemies.Concat(floor.Gold).Concat(floor.Items).Select(p => p.Cell));
        reserved.UnionWith(floor.Scenery.Select(p => p.Cell));
        reserved.UnionWith(floor.Locks.Select(p => p.Door));
        reserved.UnionWith(floor.GatheringSites.Select(p => p.Cell));
        // Preserve a route around blocking scenery as well as the bare-terrain route.
        var blockers = floor.Scenery.Select(p => p.Cell).Concat(floor.Locks.Select(p => p.Door)).ToHashSet();
        reserved.UnionWith(GridSearch.Path(floor.Start, floor.Stairs, p => PuzzleState.CardinalNeighbors(p)
            .Where(q => floor.IsWalkable(q) && !blockers.Contains(q))));
        var random = new SeedStream(options.Seed, 1710);
        var candidates = GridSearch.VisitOrder(floor.Start, floor.Neighbors).Where(p => !reserved.Contains(p)).OrderBy(p => p.Y).ThenBy(p => p.X);
        var traps = random.Shuffle(candidates).Take(options.TrapCount).Select(p => new Placement(p, random.Range(int.MaxValue))).ToArray();
        if (traps.Length == 0) throw new InvalidOperationException("Combat floor has no safe trap placement: " + options);
        return new DungeonFloor(floor.Seed, floor.IsThroneFloor, floor.Layers.ToDictionary(p => p.Key, p => p.Value), floor.Rooms,
            floor.Start, floor.Stairs, floor.Enemies, floor.Gold, floor.Items, traps, floor.GatheringSites, floor.Scenery, floor.Locks,
            floor.Objective, floor.PuzzleObjects, floor.ObjectiveEnemies, floor.RecoveryStashes, floor.SolutionWitness, floor.TownPlan, floor.Overworld);
    }
}
