using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Marks a bounded route toward undiscovered stairs with paid, collectible coins.</summary>
[Serializable]
public sealed class FindExitAction : GameAction, ISkillCastCondition
{
    internal const int Range = 15;
    internal const int GoldPerCoin = 2;
    [NonSerialized] private Ally scout;
    [NonSerialized] private bool executed;

    // Breadth-first search finds a shortest route. If the goal is unreachable,
    // keep the closest reachable non-origin cell, preferring its shortest route.
    // Search the whole reachable area before truncating: mazes can require moving
    // away from the exit before turning back toward it.
    internal static IReadOnlyList<GridPoint> FindRoute(GridPoint origin, GridPoint exit,
        Func<GridPoint, IEnumerable<GridPoint>> neighbors)
    {
        if (origin.Equals(exit)) return Array.Empty<GridPoint>();
        var parents = new Dictionary<GridPoint, GridPoint>();
        var seen = new HashSet<GridPoint> { origin };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(origin);
        GridPoint? destination = null;
        int bestDistance = int.MaxValue;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Equals(exit)) { destination = current; break; }
            if (!current.Equals(origin))
            {
                int distance = Math.Max(Math.Abs(current.X - exit.X), Math.Abs(current.Y - exit.Y));
                if (distance < bestDistance) { bestDistance = distance; destination = current; }
            }
            foreach (var next in neighbors(current))
            {
                if (!seen.Add(next)) continue;
                parents.Add(next, current);
                queue.Enqueue(next);
            }
        }
        if (!destination.HasValue) return Array.Empty<GridPoint>();
        var path = new List<GridPoint>();
        for (var cell = destination.Value; !cell.Equals(origin); cell = parents[cell]) path.Add(cell);
        path.Reverse();
        return path.Take(Range).ToArray();
    }

    internal static IReadOnlyList<Vector3Int> Route(Ally ally)
    {
        var dungeon = Game.Instance?.CurrentDungeon;
        if (ally == null || ally.Team != Team.Player || ally.Vitals.HP <= 0 || ally.IsMovementBlocked ||
            dungeon?.Floor == null) return Array.Empty<Vector3Int>();
        var unsafeCells = dungeon.Interactables.Where(i =>
            i is DungeonProp prop && !prop.Opened && prop.Definition.Kind == DungeonSceneryKind.Hazard ||
            i is Trap trap && trap.VisualObject != null && trap.VisualObject.activeSelf)
            .Select(i => i.Position).ToHashSet();
        var open = new Dictionary<GridPoint, bool>();
        bool Open(GridPoint point)
        {
            if (!open.TryGetValue(point, out bool value))
            {
                var cell = point.ToCell();
                value = SkillMovement.CanOccupy(ally, cell) && !unsafeCells.Contains(cell);
                open.Add(point, value);
            }
            return value;
        }
        return FindRoute(ally.TilemapPosition.ToGridPoint(), dungeon.Floor.Stairs,
            p => dungeon.Floor.Neighbors(p).Where(q => Open(q) &&
                MovementAction.FootprintCanStep(ally, p.ToCell(), q.ToCell())))
            .Select(p => p.ToCell()).ToArray();
    }

    internal static int GoldCost(Ally ally) => Route(ally).Count * GoldPerCoin;

    public bool CanCast(Character caster, out string reason)
    {
        int cost = GoldCost(caster as Ally);
        reason = cost == 0 ? "No safe step toward the exit" :
            TrailCoin.Prefab == null ? "Exit trail coins are unavailable" :
            Game.Instance.PlayerController.Gold < cost ? $"Not enough gold (needs {cost})" : null;
        return reason == null;
    }
    internal override GameAction AsTargetedSkill(Character caster, Character target) => new FindExitAction { scout = caster as Ally };
    internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) => AsTargetedSkill(caster, target);
    internal override bool IsValid(Character character) => !executed && CanCast(scout ?? character, out _);
    internal override List<GameAction> ExecuteImmediate(Character character)
    {
        var ally = scout ?? character as Ally;
        if (executed || TrailCoin.Prefab == null) return new();
        var route = Route(ally);
        int cost = route.Count * GoldPerCoin;
        if (cost == 0 || Game.Instance.PlayerController.Gold < cost) return new();
        executed = true;
        // Every created gold piece is paid for, including recasts, scroll effects,
        // and overlapping trails. Never apply an infinite-resource waiver here.
        Game.Instance.PlayerController.Gold -= cost;
        foreach (var cell in route) TrailCoin.Place(Game.Instance.CurrentDungeon, cell);
        GameMessages.ForCharacter(ally, $"{GameMessages.Name(ally)} spends {cost} gold to leave a trail of {route.Count} coins toward the exit.");
        return new();
    }
    internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false) { yield break; }
}
