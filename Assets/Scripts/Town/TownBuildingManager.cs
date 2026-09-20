using System;
using System.Collections.Generic;
using UnityEngine;

public class TownBuildingManager : MonoBehaviour
{
    public List<TownBuilding> Spawn(TownConfiguration configuration, IReadOnlyList<Vector3Int> positions, WalkableMap map)
    {
        if (positions.Count < configuration.Buildings.Count)
            throw new InvalidOperationException($"Town '{configuration.Id}' needs {configuration.Buildings.Count} building positions, but the map supplies {positions.Count}.");
        var buildings = new List<TownBuilding>();
        for (int i = 0; i < configuration.Buildings.Count; i++)
        {
            var definition = configuration.Buildings[i];
            var building = Instantiate(definition.Prefab, transform);
            building.Definition = definition;
            building.Name = definition.DisplayName;
            building.TilemapPosition = positions[i];
            building.transform.position = map.CellToWorld(positions[i]);
            buildings.Add(building);
        }
        return buildings;
    }
}
