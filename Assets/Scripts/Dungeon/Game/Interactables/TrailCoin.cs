using System;
using System.Linq;
using UnityEngine;

// Find Exit's paid trail uses ordinary gold collection, but never gets a minimap marker.
public sealed class TrailCoin : Gold
{
    internal static TrailCoin Prefab => Resources.Load<TrailCoin>("DungeonThemes/TrailCoin");

    protected override void Awake()
    {
        SeededAmount = 1;
        base.Awake();
    }

    internal static void Place(TileWorldDungeon dungeon, Vector3Int cell)
    {
        var existing = dungeon.Interactables.OfType<TrailCoin>()
            .FirstOrDefault(coin => coin != null && !coin.Opened && coin.Position == cell);
        if (existing != null)
        {
            existing.SeededAmount = checked((existing.SeededAmount ?? 1) + 1);
            AutoplayRunner.ObserveLoot(existing);
            return;
        }

        var prefab = Prefab;
        if (prefab == null) throw new InvalidOperationException("Find Exit trail coin prefab is missing.");
        var coin = Instantiate(prefab, dungeon.transform);
        coin.transform.position = dungeon.CellToWorld(cell);
        coin.Setup(cell);
        coin.SeededAmount = 1;
        DungeonPresentation.GroundFloorObject(coin.transform);
        // Offset only the artwork: logical pickup and fog coordinates remain the tile.
        if (dungeon.Interactables.Any(item => item != null && item.Position == cell))
            foreach (Transform visual in coin.transform)
                visual.localPosition += new Vector3(.55f, -.55f, 0);
        dungeon.Interactables.Add(coin);
        AutoplayRunner.ObserveLoot(coin);
    }
}
