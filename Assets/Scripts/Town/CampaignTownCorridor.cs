using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TWC;
using TWC.Actions;

/// <summary>Reserve the southern entrance through the interior before TWC finalizes meshes and navigation.</summary>
[Serializable]
public sealed class CampaignTownCorridor : TWCBlueprintAction, ITWCAction
{
    public bool Floor;
    public override bool ShowFoldout => false;
    public float GetGUIHeight() => 0;
    public ITWCAction Clone() => new CampaignTownCorridor { Floor = Floor };
    public bool[,] Execute(bool[,] map, TileWorldCreator creator)
    {
        for (int y = 0; y <= map.GetLength(1) / 2; y++)
            for (int x = 8; x <= 12 && x < map.GetLength(0); x++) map[x, y] = Floor;
        return map;
    }
    public static bool IsReserved(Vector3Int cell, int height) =>
        cell.x >= 8 && cell.x <= 12 && cell.y >= 0 && cell.y <= height / 2;

    // Corridor carving can remove authored placement points. Keep the surviving points
    // and fill any shortfall from reachable ground in deterministic BFS order.
    public static List<Vector3Int> CompleteBuildingPositions(bool[,] walkable, bool[,] allies,
        IEnumerable<Vector3Int> existing, Vector3Int spawn, int required)
    {
        int width = walkable.GetLength(0), height = walkable.GetLength(1);
        bool Open(Vector3Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height && walkable[p.x, p.y];
        var reachable = new HashSet<Vector3Int>();
        var candidates = new List<Vector3Int>();
        var pending = new Queue<Vector3Int>();
        if (Open(spawn)) { pending.Enqueue(spawn); reachable.Add(spawn); }
        var directions = new[] { Vector3Int.up, Vector3Int.left, Vector3Int.right, Vector3Int.down };
        while (pending.Count > 0)
        {
            var at = pending.Dequeue(); candidates.Add(at);
            foreach (var direction in directions)
            {
                var next = at + direction;
                if (Open(next) && reachable.Add(next)) pending.Enqueue(next);
            }
        }
        var result = existing.Where(p => reachable.Contains(p) && !IsReserved(p, height)).Distinct().ToList();
        foreach (var cell in candidates)
        {
            if (result.Count >= required) break;
            if (IsReserved(cell, height) || cell == spawn || cell.x == 0 || cell.y == 0 || cell.x == width - 1 || cell.y == height - 1 ||
                allies != null && allies[cell.x, cell.y] ||
                result.Any(p => Math.Abs(p.x - cell.x) <= 2 && Math.Abs(p.y - cell.y) <= 2)) continue;
            result.Add(cell);
        }
        return result;
    }

    public static void Configure(TileWorldCreator creator)
    {
        creator.twcAsset = UnityEngine.Object.Instantiate(creator.twcAsset);
        creator.twcAsset.hideFlags = UnityEngine.HideFlags.DontSave;
        foreach (var layer in creator.twcAsset.mapBlueprintLayers)
        {
            bool obstacle = layer.layerName == "Houses" || layer.layerName == "Trees" || layer.layerName == "Buildings" || layer.layerName == "Allies";
            bool floor = layer.layerName == "Ground" || layer.layerName == "Floor" || layer.layerName == "Grass";
            if (obstacle || floor) layer.stack.Add(new TileWorldCreatorAsset.BlueprintLayerData.ActionStack("Reserved southern corridor", new CampaignTownCorridor { Floor = floor }));
        }
    }
}
