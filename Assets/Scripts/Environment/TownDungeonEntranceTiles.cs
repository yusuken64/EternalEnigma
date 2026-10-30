using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Town dungeon entrances reuse the dungeon's six-piece stone boundary kit.</summary>
public static class TownDungeonEntranceTiles
{
    public static HashSet<GridPoint> Doors(TownPlan plan, string[] services) => plan.BuildingSlots
        .Where((_, slot) => services != null && slot < services.Length && services[slot] == "Dungeon").ToHashSet();

    public static HashSet<GridPoint> Build(EnvironmentBatch batch, BiomeDecorationSurfaceSet faces,
        TownPlan plan, string[] services, OverworldBiome biome, float size)
    {
        var doors = Doors(plan, services);
        var cells = plan.Footprints.Where(f => doors.Contains(f.Door)).SelectMany(f => f.Cells).ToHashSet();
        if (cells.Count == 0) return cells;
        var theme = Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Get(new DungeonVisualSelection {
            Biome = biome, Environment = DungeonEnvironmentKind.Interior, UseBiomePresentation = true
        });
        var preset = theme.RegularSmartBoundary;
        if (preset == null || preset.Tiles == null)
            throw new System.InvalidOperationException("Town dungeon entrance requires the dungeon smart boundary preset.");
        var kit = preset.Tiles;
        var pieces = new[] { kit.singleTile, kit.deadEndTile, kit.straightTile, kit.cornerTile, kit.threeWayTile, kit.fourWayTile };
        var mask = new bool[plan.Width, plan.Height];
        foreach (var cell in cells) mask[cell.X, cell.Y] = true;
        // The portal sits in a recess in the front blocked cell. Interaction remains
        // at the existing door approach; this does not add a walkable interior.
        foreach (var door in doors) mask[door.X, door.Y + 1] = false;
        var scale = new Vector3(size / preset.AuthoredCellSize, size / preset.AuthoredCellSize,
            size * 1.35f / preset.AuthoredHeight);
        foreach (var cell in cells)
        {
            if (!mask[cell.X, cell.Y]) continue;
            var position = new Vector3(cell.X + .5f, cell.Y + .5f, 0) * size;
            var (cardinal, diagonal) = DungeonBoundaryLayer.Neighbors(mask, cell.X, cell.Y);
            if (cardinal == 15 && diagonal == 15 && preset.SolidFill != null) { Add(preset.SolidFill, 0); continue; }
            var (piece, turns) = DungeonBoundaryLayer.Classify(cardinal);
            Add(pieces[piece], turns);
            for (int quadrant = 0; quadrant < 4; quadrant++)
                if ((cardinal & DungeonBoundaryLayer.RotateMask(3, quadrant)) == DungeonBoundaryLayer.RotateMask(3, quadrant))
                    Add((diagonal & (1 << quadrant)) != 0 ? preset.QuadrantFill : preset.ConcaveCorner, quadrant);

            void Add(GameObject prefab, int rotation)
            {
                if (prefab == null) return;
                var orientation = Quaternion.Euler(0, 0, -90 * rotation);
                batch.AddPrefab(prefab, position, scale, orientation,
                    materialMap: preset.MaterialOverride == null ? null : _ => preset.MaterialOverride);
                var matrix = Matrix4x4.TRS(position, orientation, scale) * prefab.transform.worldToLocalMatrix;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh != null)
                        faces.Add(filter.sharedMesh, matrix * filter.transform.localToWorldMatrix,
                            biome, DecorationSurface.BuiltWall, size);
            }
        }
        return cells;
    }
}
