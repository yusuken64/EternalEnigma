using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class AllyNavigation
{
    internal static GameAction Travel(Game game, Ally ally)
    {
        ally.Awareness.Refresh(game);
        if (ally.AllyStrategy == AllyStrategy.HoldPosition || ally.IsMovementBlocked) return new WaitAction();
        if (ally.AllyStrategy == AllyStrategy.Follow && ally.Awareness.LeaderVisible &&
            AllyCombat.VisibleHostiles(game, ally).Count > 0) return new WaitAction();
        var routes = new Routes(game, ally);
        if (ally.AllyStrategy == AllyStrategy.Aggresive)
        {
            var combat = CombatStep(game, ally, routes);
            if (combat != null) return combat;
        }
        ally.PursuitTarget = null;
        ally.PursuitPosition = null;
        return FollowOrSearch(game, ally, routes);
    }

    private static GameAction CombatStep(Game game, Ally ally, Routes routes)
    {
        var enemies = AllyCombat.VisibleHostiles(game, ally);
        if (enemies.Count == 0) return null;
        // Only currently visible cells can be prospective firing/attack positions.
        var sight = game.CurrentDungeon.GetVisibleTiles(ally, ally.TilemapPosition);
        var positions = routes.Cells.Where(sight.Contains).OrderBy(routes.Cost).ThenBy(p => p.y).ThenBy(p => p.x);
        var choices = new List<(Vector3Int cell, Character target, float cost)>();
        float bestCost = float.PositiveInfinity;
        foreach (var cell in positions)
        {
            float cost = routes.Cost(cell);
            if (cost > bestCost) break;
            foreach (var attack in AllyCombat.AttacksFrom(game, ally, cell, enemies))
            {
                choices.Add((cell, attack.Target, cost));
                bestCost = cost;
            }
        }
        if (choices.Count == 0) return null;
        var best = choices.OrderBy(c => c.cost).ThenByDescending(c => c.target == ally.PursuitTarget)
            .ThenBy(c => c.target.TilemapPosition.y).ThenBy(c => c.target.TilemapPosition.x)
            .ThenBy(c => c.target.GetInstanceID()).ThenBy(c => c.cell.y).ThenBy(c => c.cell.x).First();
        ally.PursuitTarget = best.target;
        ally.PursuitPosition = best.target.TilemapPosition;
        return Step(ally, routes.Next(best.cell));
    }

    internal static GameAction FollowOrSearch(Game game, Ally ally, Routes routes = null)
    {
        ally.Awareness.Refresh(game);
        var memory = ally.Awareness;
        if (ally.AllyStrategy == AllyStrategy.HoldPosition || ally.IsMovementBlocked || memory.Leader == ally)
            return new WaitAction();
        routes ??= new Routes(game, ally);
        if (memory.LeaderVisible)
        {
            var destination = routes.Cells.Where(p => TileWorldDungeon.ChevDistance(p, memory.LastSeenLeader.Value) <= 1)
                .OrderBy(routes.Cost).ThenBy(p => p.y).ThenBy(p => p.x).Select(p => (Vector3Int?)p).FirstOrDefault();
            ally.PursuitPosition = memory.LastSeenLeader;
            return Step(ally, destination.HasValue ? routes.Next(destination.Value) : null);
        }
        if (memory.LastSeenLeader.HasValue && !memory.SearchedLastSeen)
        {
            var lastSeen = memory.LastSeenLeader.Value;
            if (lastSeen != ally.TilemapPosition && routes.Contains(lastSeen))
            {
                ally.PursuitPosition = lastSeen;
                return Step(ally, routes.Next(lastSeen));
            }
            memory.SearchedLastSeen = true;
        }
        ally.PursuitPosition = null;
        return Wander(game, ally);
    }

    private static GameAction Wander(Game game, Ally ally)
    {
        var from = ally.TilemapPosition;
        var legal = SkillCastOptions.Directions.Select(d => from + d)
            .Where(p => DungeonPlacement.CanStep(game.CurrentDungeon, ally, from, p)).ToList();
        if (legal.Count == 0) return new WaitAction();
        var safe = legal.Where(p => !KnownHazard(game, p)).ToList();
        if (safe.Count > 0) legal = safe;
        var forward = from + Dungeon.GetFacingOffset(ally.CurrentFacing);
        var alternatives = legal.Where(p => p != ally.Awareness.PreviousSearchTile &&
            p != from - Dungeon.GetFacingOffset(ally.CurrentFacing)).ToList();
        if (alternatives.Count > 0) legal = alternatives;
        var next = legal.Contains(forward) ? forward : legal[UnityEngine.Random.Range(0, legal.Count)];
        ally.Awareness.PreviousSearchTile = from;
        return Step(ally, next);
    }

    private static bool KnownHazard(Game game, Vector3Int cell) => game.CurrentDungeon.IsHazard(cell) ||
        game.CurrentDungeon.Interactables.OfType<Trap>().Any(t => t.Position == cell && t.VisualObject != null && t.VisualObject.activeInHierarchy);

    private static GameAction Step(Ally ally, Vector3Int? next)
    {
        if (!next.HasValue || next == ally.TilemapPosition ||
            !DungeonPlacement.CanStep(Game.Instance.CurrentDungeon, ally, ally.TilemapPosition, next.Value)) return new WaitAction();
        ally.SetFacingByTargetPosition(next.Value);
        return new MovementAction(ally, ally.TilemapPosition, next.Value);
    }

    // One shortest-path search per decision, shared by target and firing-cell selection.
    internal sealed class Routes
    {
        private readonly Dictionary<Vector3Int, float> costs = new();
        private readonly Dictionary<Vector3Int, Vector3Int> previous = new();
        private readonly Vector3Int origin;
        internal IEnumerable<Vector3Int> Cells => costs.Keys;
        internal float Cost(Vector3Int cell) => costs.TryGetValue(cell, out var cost) ? cost : float.PositiveInfinity;
        internal bool Contains(Vector3Int cell) => costs.ContainsKey(cell);
        internal Routes(Game game, Ally ally)
        {
            origin = ally.TilemapPosition;
            var dungeon = game.CurrentDungeon;
            var sight = dungeon.GetVisibleTiles(ally, origin);
            // Hidden actors must not change a remembered route. Check occupancy again at the actual step.
            var occupied = game.AllCharacters.Where(c => c != null && c != ally && dungeon.CanSee(ally, c))
                .Select(c => c.ToBounds()).ToList();
            var open = new bool[dungeon.dungeonWidth, dungeon.dungeonHeight];
            for (int y = 0; y < dungeon.dungeonHeight; y++) for (int x = 0; x < dungeon.dungeonWidth; x++)
            {
                var cell = new Vector3Int(x,y);
                var bounds = Character.ToBounds(ally.FootPrint, cell);
                open[x,y] = DungeonPlacement.Fits(dungeon, ally, cell, false) && !occupied.Any(b => b.Overlaps2D(bounds));
            }
            bool Open(Vector3Int p) => GridMovement.Contains(dungeon.dungeonWidth, dungeon.dungeonHeight, p) && open[p.x,p.y];
            var frontier = new SortedSet<(float cost, int y, int x)> { (0, origin.y, origin.x) };
            costs[origin] = 0;
            while (frontier.Count > 0)
            {
                var item = frontier.Min;
                frontier.Remove(item);
                var cell = new Vector3Int(item.x, item.y);
                if (item.cost > Cost(cell)) continue;
                foreach (var next in GridMovement.GetNeighbors(cell, Open, DiagonalMovement.RequireOpenSides))
                {
                    float cost = item.cost + (next - cell).magnitude + (sight.Contains(next) && KnownHazard(game, next) ? 100 : 0);
                    if (cost >= Cost(next)) continue;
                    costs[next] = cost;
                    previous[next] = cell;
                    frontier.Add((cost, next.y, next.x));
                }
            }
        }
        internal Vector3Int? Next(Vector3Int destination)
        {
            if (destination == origin || !Contains(destination)) return null;
            while (previous.TryGetValue(destination, out var parent) && parent != origin) destination = parent;
            return destination;
        }
    }
}
