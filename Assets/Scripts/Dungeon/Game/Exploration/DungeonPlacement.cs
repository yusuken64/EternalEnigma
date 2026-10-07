using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Shared safe placement, including recovery from invalid wall positions.</summary>
internal static class DungeonPlacement
{
    internal static bool Fits(TileWorldDungeon dungeon, Character actor, Vector3Int cell, bool occupied = true)
    {
        foreach (var tile in Character.ToBounds(actor.FootPrint, cell).allPositionsWithin)
            if (!dungeon.CanWalk(tile)) return false;
        return !occupied || dungeon.OverlapsAnyOtherCharacter(actor, Character.ToBounds(actor.FootPrint, cell), false) == null;
    }

    internal static List<Vector3Int> OpenCells(TileWorldDungeon dungeon, Character actor = null, bool items = false)
    {
        var result = new List<Vector3Int>();
        for (int y = 0; y < dungeon.dungeonHeight; y++)
            for (int x = 0; x < dungeon.dungeonWidth; x++)
            {
                var p = new Vector3Int(x, y);
                if (!dungeon.CanWalk(p) || (actor != null && !Fits(dungeon, actor, p))) continue;
                if (items && (dungeon.GetInteractable(p) != null || dungeon.GetCharacterAtPosition(p) != null)) continue;
                result.Add(p);
            }
        return result;
    }

    internal static bool CanStep(TileWorldDungeon dungeon, Character actor, Vector3Int from, Vector3Int to)
    {
        if (!dungeon.CanWalkTo(from, to) || !Fits(dungeon, actor, to)) return false;
        var delta = to - from;
        return delta.x == 0 || delta.y == 0 ||
            (Fits(dungeon, actor, from + new Vector3Int(delta.x, 0)) && Fits(dungeon, actor, from + new Vector3Int(0, delta.y)));
    }

    internal static HashSet<Vector3Int> Reachable(TileWorldDungeon dungeon, Vector3Int start)
    {
        var result = new HashSet<Vector3Int> { start };
        var queue = new Queue<Vector3Int>(); queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                var next = cell + new Vector3Int(x, y);
                if (!result.Contains(next) && dungeon.CanWalkTo(cell, next)) { result.Add(next); queue.Enqueue(next); }
            }
        }
        return result;
    }

    internal static bool TryDrop(TileWorldDungeon dungeon, Vector3Int origin, InventoryItem item, out DroppedItem dropped)
    {
        dropped = null;
        if (item?.ItemDefinition == null || item.ItemDefinition.ResolveDroppedPrefab(dungeon.DroppedItemPrefabs)==null) return false;
        var reachable = Reachable(dungeon, origin);
        var cells = OpenCells(dungeon, items: true).Where(reachable.Contains)
            .OrderBy(p => TileWorldDungeon.ChevDistance(origin, p)).ThenBy(p => p.y).ThenBy(p => p.x).ToList();
        if (cells.Count == 0) return false;
        dropped = dungeon.SetDroppedItem(cells[0], item.ItemDefinition, item.StackStock);
        dropped.InventoryItem = item;
        return true;
    }

    internal static bool Recover(TileWorldDungeon dungeon)
    {
        var game = Game.Instance;
        if (game == null || dungeon == null || dungeon.Floor == null) return false;
        bool changed = false;
        foreach (var actor in game.AllCharacters.Concat(game.DownedAllies).Distinct().ToArray())
        {
            if (actor == null || Fits(dungeon, actor, actor.TilemapPosition, false)) continue;
            var cells = OpenCells(dungeon, actor);
            if (cells.Count == 0) continue; // Retry when a valid destination becomes available.
            actor.TilemapPosition = cells[Random.Range(0, cells.Count)];
            actor.transform.position = dungeon.CellToWorld(actor.TilemapPosition);
            if (actor is Ally ally) ally.currentInteractable = null;
            GameMessages.ForCharacter(actor, $"{GameMessages.Name(actor)} was inside a wall and warped to a random open spot.");
            changed = true;
        }
        foreach (var item in dungeon.Interactables.Where(i => i is DroppedItem or Gold).ToArray())
        {
            if (dungeon.CanWalk(item.Position)) continue;
            var cells = OpenCells(dungeon, items: true);
            if (cells.Count == 0) continue;
            item.Position = cells[Random.Range(0, cells.Count)];
            item.transform.position = dungeon.CellToWorld(item.Position);
            changed = true;
        }
        return changed;
    }
}
