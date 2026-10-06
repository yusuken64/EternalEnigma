using System.Collections;
using System.Linq;
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
    {
        var result = new Line { Endpoint = caster.TilemapPosition };
        if (!IsDirection(direction)) return result;
        var dungeon = Game.Instance.CurrentDungeon;
        var seen = new HashSet<Character>();
        bool Open(Vector3Int cell) => dungeon.IsFloorCell(cell) && dungeon.PropAt(cell)?.Alive != true && dungeon.PropAt(cell)?.BlocksMovement != true;
        for (int i = 0; i < range; i++)
        {
            var cell = result.Endpoint;
            var next = cell + direction;
            if (!dungeon.IsFloorCell(next) || direction.x != 0 && direction.y != 0 &&
                (!Open(cell + new Vector3Int(direction.x, 0)) || !Open(cell + new Vector3Int(0, direction.y)))) break;
            result.Endpoint = next;
            if (dungeon.PropAt(next)?.Alive == true || dungeon.PropAt(next)?.BlocksMovement == true) break;
            var hit = Game.Instance.AllCharacters.FirstOrDefault(c => c != null && c != caster && c.Vitals.HP > 0 && c.OverlapsWith(new BoundsInt(next, Vector3Int.one)));
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

    internal static Hit Trace(Character caster, Vector3Int direction, int range)
    {
        var cell = caster.TilemapPosition;
        if (!IsDirection(direction)) return new Hit(cell, null);
        var dungeon = Game.Instance.CurrentDungeon;
        for (int i = 0; i < range; i++)
        {
            var next = cell + direction;
            if (!dungeon.IsFloorCell(next) || (direction.x != 0 && direction.y != 0 &&
                (!dungeon.IsFloorCell(cell + new Vector3Int(direction.x, 0)) ||
                 !dungeon.IsFloorCell(cell + new Vector3Int(0, direction.y))))) break;
            cell = next;
            if (dungeon.PropAt(cell)?.Alive == true) return new Hit(cell,null);
            // Every living character blocks a shot, regardless of the effect's team filter.
            var hit = Game.Instance.AllCharacters.FirstOrDefault(c => c != caster && c != null &&
                c.Vitals.HP > 0 && c.OverlapsWith(new BoundsInt(cell, Vector3Int.one)));
            if (hit != null) return new Hit(cell, hit);
        }
        return new Hit(cell, null);
    }

    internal static IEnumerator Animate(Character caster, Vector3Int cell, GameObject prefab)
    {
        prefab ??= Game.Instance.ThrownItemProjectilePrefab;
        if (prefab == null) yield break;
        var projectile = Object.Instantiate(prefab, caster.VisualParent.transform.position, Quaternion.identity);
        try
        {
            var destination = Game.Instance.CurrentDungeon.CellToWorld(cell);
            if (destination != projectile.transform.position) projectile.transform.LookAt(destination);
            yield return projectile.transform.DOMove(destination, 0.25f).SetEase(Ease.Linear).WaitForCompletion();
        }
        finally { if (projectile != null) Object.Destroy(projectile); }
    }
}
