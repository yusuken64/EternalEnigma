using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

internal static class MissileTargeting
{
    internal sealed class Line
    {
        internal Vector3Int Endpoint;
        internal readonly List<Hit> Encounters = new();
    }
    internal static Line TraceLine(Character caster, Vector3Int direction, int range, int cap = 1, bool splash = false)
        => TraceLineFrom(caster, caster.TilemapPosition, direction, range, cap, splash);

    // Evaluate a prospective firing position without moving the actor or changing sight.
    internal static Line TraceLineFrom(Character caster, Vector3Int origin, Vector3Int direction, int range, int cap = 1, bool splash = false)
    {
        var result = new Line { Endpoint = origin };
        if (!IsDirection(direction)) return result;
        var dungeon = Game.Instance.CurrentDungeon;
        var seen = new HashSet<Character>();
        for (int i = 0; i < range; i++)
        {
            var cell = result.Endpoint;
            var next = cell + direction;
            if (!CanStep(dungeon, cell, direction)) break;
            result.Endpoint = next;
            if (BlocksMissile(dungeon, next)) break;
            var hit = dungeon.LivingOccupant(next, caster);
            if (hit == null || !seen.Add(hit)) continue;
            if (splash) { result.Encounters.Add(new Hit(next, hit)); break; }
            if (hit.Team == caster.Team) continue;
            result.Encounters.Add(new Hit(next, hit));
            if (cap > 0 && result.Encounters.Count >= cap) break;
        }
        return result;
    }
    internal readonly struct Hit
    {
        internal readonly Vector3Int Cell;
        internal readonly Character Character;
        internal Hit(Vector3Int cell, Character character) { Cell = cell; Character = character; }
    }

    internal static bool IsDirection(Vector3Int direction) => direction.z == 0 &&
        direction != Vector3Int.zero && Mathf.Abs(direction.x) <= 1 && Mathf.Abs(direction.y) <= 1;

    private static bool CanStep(TileWorldDungeon dungeon, Vector3Int cell, Vector3Int direction)
    {
        bool Open(Vector3Int side) => dungeon.IsFloorCell(side) && !BlocksMissile(dungeon, side);
        return dungeon.IsFloorCell(cell + direction) && (direction.x == 0 || direction.y == 0 ||
            Open(cell + new Vector3Int(direction.x, 0)) && Open(cell + new Vector3Int(0, direction.y)));
    }

    private static bool BlocksMissile(TileWorldDungeon dungeon, Vector3Int cell)
    {
        var prop = dungeon.PropAt(cell);
        return prop != null && (prop.Alive || prop.BlocksMovement);
    }

    internal static Hit Trace(Character caster, Vector3Int direction, int range)
    {
        var cell = caster.TilemapPosition;
        if (!IsDirection(direction)) return new Hit(cell, null);
        var dungeon = Game.Instance.CurrentDungeon;
        for (int i = 0; i < range; i++)
        {
            var next = cell + direction;
            if (!CanStep(dungeon, cell, direction)) break;
            cell = next;
            if (BlocksMissile(dungeon, cell)) return new Hit(cell,null);
            // Every living character blocks a shot, regardless of the effect's team filter.
            var hit = dungeon.LivingOccupant(cell, caster);
            if (hit != null) return new Hit(cell, hit);
        }
        return new Hit(cell, null);
    }

    internal static IEnumerator Animate(Character caster, Vector3Int cell, GameObject prefab)
    {
        prefab ??= Game.Instance.ThrownItemProjectilePrefab;
        if (prefab == null) yield break;
        var origin = CombatEffectPlayer.Body(caster, Game.Instance.CurrentDungeon.WorldToCell(caster.transform.position));
        var destination = CombatEffectPlayer.Body(null, cell);
        var delta = destination - origin;
        var rotation = delta.sqrMagnitude > .001f ? Quaternion.LookRotation(delta, Vector3.back) : Quaternion.identity;
        var projectile = Object.Instantiate(prefab, origin, rotation * prefab.transform.localRotation);
        try
        {
            yield return projectile.transform.DOMove(destination, 0.25f).SetEase(Ease.Linear).SetUpdate(true).WaitForCompletion();
        }
        finally { if (projectile != null) { projectile.transform.DOKill(); Object.Destroy(projectile); } }
    }
}
