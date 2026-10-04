using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

public class TownBuildingManager : MonoBehaviour
{
    public List<TownBuilding> Spawn(TownConfiguration configuration, IReadOnlyList<Vector3Int> positions, WalkableMap map,
        TownPlan plan)
    {
        var definitions = configuration.SlotBuildings ?? configuration.Buildings;
        if (positions.Count < definitions.Count)
            throw new InvalidOperationException($"Town '{configuration.Id}' needs {configuration.Buildings.Count} building positions, but the map supplies {positions.Count}.");
        var buildings = new List<TownBuilding>();
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null) continue;
            var building = Instantiate(definition.Prefab, transform);
            building.Definition = definition;
            building.Name = definition.DisplayName;
            building.TilemapPosition = positions[i];
            // The door marker remains the approach and interaction cell. A bodyless
            // fallback has no front wall to mount the entrance on.
            var door = positions[i].ToGridPoint();
            var front = new GridPoint(door.X, door.Y + 1);
            var hasBody = plan.Footprints.Count > 0
                ? plan.Footprints.Any(f => f.Door.Equals(door) && f.Cells.Count > 0)
                : plan.Layers[TownLayers.Houses].At(front) || plan.Layers[TownLayers.ShopFloor].At(front);
            building.transform.position = map.CellToWorld(positions[i] + (hasBody ? Vector3Int.up : Vector3Int.zero));
            BiomeModel.ApplyAll(building.gameObject, map.TileWorldCreator.GetComponent<TownBiomeStyle>()?.Current ?? EternalEnigma.Core.World.OverworldBiome.Grassland);
            if (hasBody)
                foreach (var renderer in building.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (definition.Id == "inn")
            {
                var kit = EnvironmentKit.Load();
                if (kit != null)
                {
                    foreach (var renderer in building.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    kit.Create("InnSign", map.TileWorldCreator.GetComponent<TownBiomeStyle>()?.Current ?? EternalEnigma.Core.World.OverworldBiome.Grassland,
                        building.transform, new Vector3(.5f, .5f, 0) * map.TileWorldCreator.twcAsset.cellSize, map.TileWorldCreator.twcAsset.cellSize);
                }
            }
            if (definition.DialogId == "entrance" && !hasBody)
            {
                var kit = EnvironmentKit.Load();
                if (kit != null)
                {
                    foreach (var renderer in building.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    TownGateVisuals.Create(kit, map.TileWorldCreator.GetComponent<TownBiomeStyle>()?.Current ?? EternalEnigma.Core.World.OverworldBiome.Grassland,
                        building.transform, new EternalEnigma.Core.World.GridPoint(0, 0), map.TileWorldCreator.twcAsset.cellSize, "Dungeon gate", true);
                }
            }
            buildings.Add(building);
        }
        return buildings;
    }
}
