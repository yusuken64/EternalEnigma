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
        var positions = routes.CellsByCost;
        var choices = new List<(Vector3Int cell, Character target, float cost)>();
        float bestCost = float.PositiveInfinity;
        foreach (var cell in positions)
        {
            float cost = routes.Cost(cell);
            if (cost > bestCost) break;
            if (!sight.Contains(cell)) continue;
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
            var destination = routes.CellsByCost.Where(p => TileWorldDungeon.ChevDistance(p, memory.LastSeenLeader.Value) <= 1)
                .Select(p => (Vector3Int?)p).FirstOrDefault();
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
        readonly Dictionary<Vector3Int, float> costs = new();
        readonly Dictionary<Vector3Int, Vector3Int> previous = new();
        readonly HashSet<Vector3Int> settled = new(), hazards;
        readonly List<Vector3Int> ordered = new();
        readonly SortedSet<(float cost, int y, int x)> frontier = new();
        readonly List<BoundsInt> occupied;
        readonly HashSet<Vector3Int> sight;
        readonly TileWorldDungeon dungeon;
        readonly Ally ally;
        readonly Vector3Int origin;
        readonly sbyte[,] open;
        readonly int minX, minY;
        readonly bool world;
        internal float Cost(Vector3Int cell) => costs.TryGetValue(cell, out var cost) ? cost : float.PositiveInfinity;

        // Dijkstra's settled order already has the same cost/y/x tie-breaking as the policy.
        // Stop as soon as a follow or firing destination is found; resume if another caller needs more.
        internal IEnumerable<Vector3Int> CellsByCost
        {
            get
            {
                using var measurement = new FinalePerformance.Scope("Companion routes");
                foreach (var cell in ordered) yield return cell;
                while (Advance(out var cell)) yield return cell;
            }
        }
        internal bool Contains(Vector3Int cell)
        {
            using var measurement = new FinalePerformance.Scope("Companion routes");
            while (!settled.Contains(cell) && Advance(out _)) { }
            return settled.Contains(cell);
        }
        internal Routes(Game game, Ally ally)
        {
            using var measurement = new FinalePerformance.Scope("Companion routes");
            this.ally = ally; dungeon = game.CurrentDungeon; origin = ally.TilemapPosition;
            sight = dungeon.GetVisibleTiles(ally, origin);
            // Hidden actors must not change a remembered route. Recheck occupancy at the actual step.
            occupied = game.AllCharacters.Where(c => c != null && c != ally && dungeon.CanSee(ally, c))
                .Select(c => c.ToBounds()).ToList();
            hazards = dungeon.Interactables.OfType<DungeonProp>().Where(p => !p.Opened).GroupBy(p => p.Position)
                .Where(g => g.First().Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Hazard).Select(g => g.Key)
                .Concat(dungeon.Interactables.OfType<Trap>().Where(t => t.VisualObject != null && t.VisualObject.activeInHierarchy).Select(t => t.Position))
                .ToHashSet();
            world = dungeon.Floor.Variant == EternalEnigma.Core.World.DungeonVariant.Overworld;
            minX = world ? Mathf.Max(0, origin.x - 24) : 0; minY = world ? Mathf.Max(0, origin.y - 24) : 0;
            int maxX = world ? Mathf.Min(dungeon.dungeonWidth, origin.x + 25) : dungeon.dungeonWidth;
            int maxY = world ? Mathf.Min(dungeon.dungeonHeight, origin.y + 25) : dungeon.dungeonHeight;
            open = new sbyte[maxX - minX, maxY - minY];
            costs[origin] = 0; frontier.Add((0, origin.y, origin.x));
        }
        bool Open(Vector3Int cell)
        {
            int x = cell.x - minX, y = cell.y - minY;
            if (x < 0 || y < 0 || x >= open.GetLength(0) || y >= open.GetLength(1)) return false;
            if (open[x,y] == 0)
            {
                var bounds = Character.ToBounds(ally.FootPrint, cell);
                open[x,y] = (sbyte)(DungeonPlacement.Fits(dungeon, ally, cell, false) && !occupied.Any(b => b.Overlaps2D(bounds)) ? 1 : -1);
            }
            return open[x,y] > 0;
        }
        bool Advance(out Vector3Int cell)
        {
            while (frontier.Count > 0)
            {
                var item = frontier.Min; frontier.Remove(item);
                cell = new Vector3Int(item.x, item.y);
                if (item.cost > Cost(cell) || !settled.Add(cell)) continue;
                ordered.Add(cell);
                foreach (var direction in SkillCastOptions.Directions)
                {
                    var next = cell + direction;
                    if (!Open(next) || direction.x != 0 && direction.y != 0 &&
                        (!Open(cell + new Vector3Int(direction.x, 0)) || !Open(cell + new Vector3Int(0, direction.y)))) continue;
                    // Overworld doorway checks duplicate the same ground, gate and diagonal rules above.
                    if (!world && !dungeon.AllowsDoorwayStep(cell, next)) continue;
                    float cost = item.cost + direction.magnitude + (sight.Contains(next) && hazards.Contains(next) ? 100 : 0);
                    if (cost >= Cost(next)) continue;
                    costs[next] = cost; previous[next] = cell; frontier.Add((cost, next.y, next.x));
                }
                return true;
            }
            cell = default; return false;
        }
        internal Vector3Int? Next(Vector3Int destination)
        {
            if (destination == origin || !Contains(destination)) return null;
            while (previous.TryGetValue(destination, out var parent) && parent != origin) destination = parent;
            return destination;
        }
    }
}
