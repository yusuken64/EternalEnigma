using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Native TWC rule selection, combined directly into chunks instead of temporary tile GameObjects.</summary>
[Serializable, ActionName(Name = "Biome smart tiles (batched)")]
public sealed class EnvironmentSmartTileLayer : TWCBuildLayer
{
    public EnvironmentKit Kit;
    public TileWorldCreator4TilesPreset QuarterTiles;
    public TileWorldCreator6TilesPreset WallTiles;
    public bool Road;
    public bool Buildings;
    [Tooltip("Height above ground, in cells. XY worlds raise geometry toward negative Z.")]
    public float Elevation;
    public float HeightScale = 1;
    public bool PerimeterOnly;
    public bool SkipMapBoundary;
    public Material SurfaceMaterial;
    public bool TownPalette;
    public override TWCBuildLayer Clone() => new EnvironmentSmartTileLayer { guid = guid, layerName = layerName,
        assignedGenerationLayerGuid = assignedGenerationLayerGuid, active = active, Kit = Kit,
        QuarterTiles = QuarterTiles, WallTiles = WallTiles, Road = Road, Buildings = Buildings, Elevation = Elevation, HeightScale = HeightScale,
        PerimeterOnly=PerimeterOnly,SkipMapBoundary=SkipMapBoundary,SurfaceMaterial=SurfaceMaterial,TownPalette=TownPalette };

    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            // Terrain and walls use whole cells. Quarter tiles use TWC's subdivided map.
            bool sixTerrain = IsSixTerrainLayer(layerName);
            var map = creator.GetGeneratedBlueprintMap(assignedGenerationLayerGuid + (WallTiles != null || sixTerrain ? "_UNSUBD" : ""));
            if (map == null || Kit == null) return;
            var root = creator.AddLayerObject(layerName, guid);
            root.transform.SetParent(creator.worldObject.transform, false); root.transform.localRotation = Quaternion.identity;
            foreach (var child in root.transform.Cast<Transform>().ToArray()) Release(child.gameObject);
            foreach (var old in root.GetComponents<EnvironmentMeshOwner>()) Release(old);
            var batch = new EnvironmentBatch(root.transform);
            var decorationFaces=root.GetComponent<BiomeDecorationSurfaceSet>() ?? root.AddComponent<BiomeDecorationSurfaceSet>();
            decorationFaces.Faces.Clear();
            var grid = creator.GetComponent<CampaignOverworld>()?.CurrentGrid;
            CoreLayoutCache.TryGetTown(creator, out var town);
            var biome = creator.GetComponent<TownBiomeStyle>()?.Current ?? OverworldBiome.Grassland;
            var townCatalog = TownPalette ? TownInteriorCatalog.Load() : null;
            float size = creator.twcAsset.cellSize;
            if (grid != null && sixTerrain)
            {
                if (BuildSixTerrain(creator, grid, batch, size)) return;
            }
            if (Buildings && town != null)
            {
                var colors = TownHouseTiles.Palette(biome);
                var source = Kit.BuildingMaterial(biome);
                var plaster = TownHouseTiles.ColorMaterial(source, colors.plaster, batch.Owner, "House plaster");
                var timber = TownHouseTiles.ColorMaterial(source, colors.timber, batch.Owner, "House timber");
                var door = TownHouseTiles.ColorMaterial(source, colors.door, batch.Owner, "House doors");
                var glass = TownHouseTiles.ColorMaterial(source, colors.glass, batch.Owner, "House windows");
                // Roofs captures each complete body before shop rooms are carved
                // out of the blocking Houses mask. Facades must wrap shops too.
                var houses = town.Layers[TownLayers.Roofs];
                var entrances = town.BuildingSlots.ToHashSet();
                var bodyCells = town.Footprints.Count > 0
                    ? town.Footprints.SelectMany(f => f.Cells).ToHashSet()
                    : new System.Collections.Generic.HashSet<GridPoint>();
                for (int row = 0; row < houses.Height; row++) for (int column = 0; column < houses.Width; column++)
                {
                    var cell = new GridPoint(column, row);
                    if (houses.At(cell) && !entrances.Contains(cell)) bodyCells.Add(cell);
                }
                var buildingOf = new System.Collections.Generic.Dictionary<GridPoint, GridPoint>();
                foreach (var entry in town.Footprints)
                    foreach (var cell in entry.Cells) buildingOf[cell] = entry.Door;
                if (town.Footprints.Count == 0)
                    foreach (var entry in town.BuildingSlots)
                        for (int row = 1; row <= 4; row++) for (int column = -1; column <= 1; column++)
                        {
                            var cell = new GridPoint(entry.X + column, entry.Y + row);
                            if (bodyCells.Contains(cell)) buildingOf[cell] = entry;
                        }
                for (int y = 0; y < houses.Height; y++) for (int x = 0; x < houses.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (!bodyCells.Contains(cell)) continue;
                    foreach (var side in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                    {
                        var neighbor = new GridPoint(x + side.Item1, y + side.Item2);
                        if (bodyCells.Contains(neighbor) &&
                            buildingOf.TryGetValue(cell, out var currentDoor) &&
                            buildingOf.TryGetValue(neighbor, out var neighborDoor) && currentDoor.Equals(neighborDoor)) continue;
                        bool entrance = side.Item2 == -1 && entrances.Contains(new GridPoint(x, y - 1));
                        bool window = !entrance && (x + y) % 2 == 0;
                        TownHouseTiles.Facade(batch, plaster, timber, door, glass, size, cell,
                            side.Item1, side.Item2, entrance, window);
                        if (!entrance && !window && town.Layers[TownLayers.ShopWalls].At(cell))
                            decorationFaces.Faces.Add(new BiomeDecorationFace {
                                Center = new Vector3(x + .5f + side.Item1 * .46f,
                                    y + .5f + side.Item2 * .46f, -.53f) * size,
                                Normal = new Vector3(-side.Item1, -side.Item2, 0),
                                Width = .78f * size, Height = .82f * size,
                                Biome = biome, Surface = DecorationSurface.BuiltWall
                            });
                    }
                }
                batch.Finish();
                return;
            }
            // Solid houses retain their blocked front wall in the Core plan. Omit
            // only the facade panels in front of the entrance art to make a recess.
            var solidEntrances = Buildings && town != null
                ? town.BuildingSlots.Select(d => new GridPoint(d.X, d.Y + 1))
                    .Where(c => town.Layers[TownLayers.Houses].At(c)).ToHashSet()
                : null;
            foreach (var tile in map.clusters.Values.SelectMany(c => c.Values))
            {
                if(PerimeterOnly && tile.position.x!=0 && tile.position.z!=0 && tile.position.x!=creator.twcAsset.mapWidth*2-1 && tile.position.z!=creator.twcAsset.mapHeight*2-1) continue;
                if(SkipMapBoundary && (tile.position.x==0 || tile.position.z==0 || tile.position.x==creator.twcAsset.mapWidth*2-1 || tile.position.z==creator.twcAsset.mapHeight*2-1)) continue;
                bool wall = WallTiles != null;
                float unit = wall ? size : size * .5f;
                int x = (int)tile.position.x / (wall ? 1 : 2), y = (int)tile.position.z / (wall ? 1 : 2);
                if (solidEntrances != null && solidEntrances.Contains(new GridPoint(x, y))) continue;
                var palette = grid == null ? biome : OverworldCosmetics.Biome(grid, x, y);
                GameObject prefab; float angle;
                if (wall) (prefab, angle) = SelectWall(WallTiles, tile.neighboursLocation);
                else
                {
                    prefab = tile.tileType switch { TileData.TileType.edge => QuarterTiles.edgeTile,
                        TileData.TileType.exteriorCorner => QuarterTiles.exteriorCornerTile,
                        TileData.TileType.interiorCorner => QuarterTiles.interiorCornerTile, _ => QuarterTiles.fillTile };
                    angle = -tile.yRotation;
                }
                if (prefab == null) continue; // Edge-only presets deliberately omit their interior fill.
                // Authored preset child rotates the XY mesh into TWC's canonical XZ plane.
                var mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                bool alley = Road && town != null && town.Layers.TryGetValue(TownLayers.Alleys, out var alleys) && alleys[x,y];
                if(!Road && (wall || tile.tileType==TileData.TileType.edge) && (Buildings || wall || mesh.bounds.size.z*HeightScale>.25f))
                    decorationFaces.Add(mesh,Matrix4x4.TRS(new Vector3((tile.position.x+.5f)*unit,(tile.position.z+.5f)*unit,-Elevation*size),Quaternion.Euler(0,0,angle),new Vector3(unit,unit,unit*HeightScale)),palette,
                        Buildings && town!=null && town.Layers[TownLayers.ShopWalls].At(new GridPoint(x,y)) ? DecorationSurface.BuiltWall : Buildings?DecorationSurface.Facade:wall?DecorationSurface.BuiltWall:DecorationSurface.NaturalWall,size,Buildings?(town?.Footprints.FirstOrDefault(f=>f.Cells.Contains(new GridPoint(x,y)))?.Door.ToString()??x+","+y):null,splitPanels:wall && town!=null && town.Interiors.Count>0);
                batch.Add(mesh, townCatalog != null ? townCatalog.Material(palette) : SurfaceMaterial != null ? SurfaceMaterial : Road ? (alley ? Kit.Paving : Kit.Road) : Buildings ? Kit.BuildingMaterial(palette) : Kit.Material(palette),
                    new Vector3((tile.position.x + .5f) * unit, (tile.position.z + .5f) * unit, -Elevation * size),
                    new Vector3(unit, unit, unit * HeightScale), angle,
                    Road ? SilhouetteRole.Receiver : Buildings || wall ? SilhouetteRole.Caster : (SilhouetteRole?)null);
            }
            if (solidEntrances != null)
                foreach (var cell in solidEntrances)
                    batch.Add(Kit.Mesh("Wall"), Kit.BuildingMaterial(biome),
                        new Vector3(cell.X + .5f, cell.Y + .8f, 0) * size, Vector3.one * size);
            batch.Finish();
        }
        finally { creator.executedBuildLayersCount += 1; }
    }

    private static bool IsSixTerrainLayer(string name) =>
        name.StartsWith("Smart/Mountains", StringComparison.Ordinal) ||
        name == SmartEnvironmentMasks.Summits || name == SmartEnvironmentMasks.Coast;

    // Six canonical pieces (single, end, straight, corner, tee and cross) cover
    // the cardinal connections. Diagonals reshape the four corners independently.
    private bool BuildSixTerrain(TileWorldCreator creator, OverworldGrid grid, EnvironmentBatch batch, float size)
    {
        var full = creator.GetGeneratedBlueprintMap(assignedGenerationLayerGuid + "_UNSUBD");
        if (full == null) return false;
        var cells = full.clusters.Values.SelectMany(c => c.Values)
            .Select(t => new Vector2Int((int)t.position.x, (int)t.position.z)).ToHashSet();
        var shapes = new System.Collections.Generic.Dictionary<int, (Mesh mesh, float angle)>();
        bool coast = layerName == SmartEnvironmentMasks.Coast;
        bool Connected(Vector2Int cell) => cells.Contains(cell) || (coast && SkipMapBoundary &&
            (cell.x < 0 || cell.y < 0 || cell.x >= grid.Width || cell.y >= grid.Height));
        foreach (var cell in cells)
        {
            int x = cell.x, y = cell.y;
            int mask = (Connected(cell + Vector2Int.up) ? 1 : 0) |
                (Connected(cell + Vector2Int.right) ? 2 : 0) |
                (Connected(cell + Vector2Int.down) ? 4 : 0) |
                (Connected(cell + Vector2Int.left) ? 8 : 0);
            int diagonals = (Connected(cell + new Vector2Int(1, 1)) ? 1 : 0) |
                (Connected(cell + new Vector2Int(1, -1)) ? 2 : 0) |
                (Connected(cell + new Vector2Int(-1, -1)) ? 4 : 0) |
                (Connected(cell + new Vector2Int(-1, 1)) ? 8 : 0);
            int key = mask | diagonals << 4;
            if (!shapes.TryGetValue(key, out var variant))
            {
                // The authored six prefabs provide the canonical shapes. Only a
                // missing diagonal needs a generated concave-corner variant.
                bool innerCorner =
                    ((mask & 3) == 3 && (diagonals & 1) == 0) ||
                    ((mask & 6) == 6 && (diagonals & 2) == 0) ||
                    ((mask & 12) == 12 && (diagonals & 4) == 0) ||
                    ((mask & 9) == 9 && (diagonals & 8) == 0);
                Mesh shape = null;
                float angle = 0;
                if (!innerCorner && WallTiles != null && Mathf.Approximately(HeightScale, 1))
                {
                    var selected = SelectSixTerrainPrefab(WallTiles, mask);
                    // The child rotates the authored XY mesh into TWC's XZ plane.
                    // This batch builds in XY directly and uses the original mesh.
                    shape = selected.prefab?.GetComponentInChildren<MeshFilter>()?.sharedMesh;
                    angle = selected.angle;
                }
                if (shape == null)
                {
                    angle = 0;
                    shape = SixTerrainMesh(mask, diagonals, coast, .42f * HeightScale);
                    batch.Owner.Meshes.Add(shape);
                }
                variant = (shape, angle);
                shapes.Add(key, variant);
            }
            if (variant.mesh.vertexCount == 0) continue;
            var material = SurfaceMaterial != null ? SurfaceMaterial : coast ? Kit.Shore : Kit.Ground(OverworldBiome.Mountain);
            batch.Add(variant.mesh, material, new Vector3(x + .5f, y + .5f, -Elevation) * size,
                Vector3.one * size, variant.angle, coast ? SilhouetteRole.Receiver : SilhouetteRole.Caster);
        }
        batch.Finish(smoothTerrain: !coast);
        return true;
    }

    public static Mesh SixTerrainMesh(int neighbors, int diagonals, bool coast, float depth)
    {
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var uv = new System.Collections.Generic.List<Vector2>();
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool corner = false)
        {
            int first = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            if (coast)
                uv.AddRange(corner
                    ? new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,1) }
                    : new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) });
            else uv.AddRange(new[] { a,b,c,d }.Select(v => new Vector2(v.x + .5f, v.y + .5f)));
            // Mountain quads are supplied counterclockwise; shore quads clockwise.
            triangles.AddRange(coast
                ? new[] { first, first + 1, first + 2, first, first + 2, first + 3 }
                : new[] { first, first + 3, first + 2, first, first + 2, first + 1 });
        }
        if (coast)
        {
            // The water surface is drawn by OverworldBiomeRenderer. Draw only a
            // narrow shore; a full top here hides the animated water in squares.
            const float inner = .32f;
            const float outerZ = .005f, innerZ = -.02f;
            bool n = (neighbors & 1) == 0, e = (neighbors & 2) == 0;
            bool s = (neighbors & 4) == 0, w = (neighbors & 8) == 0;
            // Miter adjacent strips at the shared corner instead of overlapping
            // two transparent quads. UV.y measures distance from land to water.
            if (n) Face(new(-.5f,.5f,outerZ),new(.5f,.5f,outerZ),new(e?inner:.5f,inner,innerZ),new(w?-inner:-.5f,inner,innerZ));
            if (e) Face(new(.5f,.5f,outerZ),new(.5f,-.5f,outerZ),new(inner,s?-inner:-.5f,innerZ),new(inner,n?inner:.5f,innerZ));
            if (s) Face(new(.5f,-.5f,outerZ),new(-.5f,-.5f,outerZ),new(w?-inner:-.5f,-inner,innerZ),new(e?inner:.5f,-inner,innerZ));
            if (w) Face(new(-.5f,-.5f,outerZ),new(-.5f,.5f,outerZ),new(-inner,n?inner:.5f,innerZ),new(-inner,s?-inner:-.5f,innerZ));
            // An empty diagonal between two connected sides is an inner corner.
            if ((neighbors & 3) == 3 && (diagonals & 1) == 0) Face(new(.5f,.5f,outerZ),new(.5f,inner,innerZ),new(inner,inner,innerZ),new(inner,.5f,innerZ),true);
            if ((neighbors & 6) == 6 && (diagonals & 2) == 0) Face(new(.5f,-.5f,outerZ),new(inner,-.5f,innerZ),new(inner,-inner,innerZ),new(.5f,-inner,innerZ),true);
            if ((neighbors & 12) == 12 && (diagonals & 4) == 0) Face(new(-.5f,-.5f,outerZ),new(-.5f,-inner,innerZ),new(-inner,-inner,innerZ),new(-inner,-.5f,innerZ),true);
            if ((neighbors & 9) == 9 && (diagonals & 8) == 0) Face(new(-.5f,.5f,outerZ),new(-inner,.5f,innerZ),new(-inner,inner,innerZ),new(-.5f,inner,innerZ),true);
        }
        else
        {
            // A 3x3 top slopes down to every exposed side. All four corner
            // heights use the diagonal too, so concave and convex joins meet.
            float[] axis = { -.5f, 0, .5f };
            float Height(int ix, int iy)
            {
                bool north = (neighbors & 1) != 0, east = (neighbors & 2) != 0;
                bool south = (neighbors & 4) != 0, west = (neighbors & 8) != 0;
                if (ix == 1 && iy == 1) return -depth;
                if (ix == 1) return (iy == 2 ? north : south) ? -depth : 0;
                if (iy == 1) return (ix == 2 ? east : west) ? -depth : 0;
                bool sides = (ix == 2 ? east : west) && (iy == 2 ? north : south);
                int bit = ix == 2 ? (iy == 2 ? 1 : 2) : (iy == 2 ? 8 : 4);
                return sides && (diagonals & bit) != 0 ? -depth : 0;
            }
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                Face(new(axis[x],axis[y],Height(x,y)),new(axis[x+1],axis[y],Height(x+1,y)),
                    new(axis[x+1],axis[y+1],Height(x+1,y+1)),new(axis[x],axis[y+1],Height(x,y+1)));
        }
        var mesh = new Mesh { name = "Six-piece terrain " + neighbors + "/" + diagonals, hideFlags = HideFlags.DontSave };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    public static (GameObject prefab, float angle) SelectSixTerrainPrefab(TileWorldCreator6TilesPreset set, int mask)
    {
        if (mask == 0) return (set.singleTile, 0);
        if (mask == 15) return (set.fourWayTile, 0);
        int[] canonical = { 1, 5, 9, 13 };
        GameObject[] pieces = { set.deadEndTile, set.straightTile, set.cornerTile, set.threeWayTile };
        for (int i = 0; i < canonical.Length; i++)
            for (int turns = 0; turns < 4; turns++)
                if ((((canonical[i] << turns) | (canonical[i] >> (4 - turns))) & 15) == mask)
                    return (pieces[i], -90 * turns);
        throw new InvalidOperationException("Unmatched terrain neighborhood.");
    }

    // Same canonical orientations as TWC Instantiate6Tiles: end=N, corner=N+W, tee=N+S+W.
    public static (GameObject prefab, float angle) SelectWall(TileWorldCreator6TilesPreset set, NeighboursLocation n)
    {
        int mask = (n.north ? 1 : 0) | (n.east ? 2 : 0) | (n.south ? 4 : 0) | (n.west ? 8 : 0);
        return SelectSixTerrainPrefab(set, mask);
    }
    private static void Release(UnityEngine.Object obj)
    {
        if (obj is GameObject go) go.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
    }
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)
    {
        layerName = EditorGUILayout.TextField("Layer name", layerName);
        var layers = asset.mapBlueprintLayers;
        int selected = layers.FindIndex(l => l.guid == assignedGenerationLayerGuid);
        int next = EditorGUILayout.Popup("Blueprint", Math.Max(0, selected), layers.Select(l => l.layerName).ToArray());
        if (layers.Count > 0) assignedGenerationLayerGuid = layers[next].guid;
        Kit = (EnvironmentKit)EditorGUILayout.ObjectField("Biome kit", Kit, typeof(EnvironmentKit), false);
        QuarterTiles = (TileWorldCreator4TilesPreset)EditorGUILayout.ObjectField("4-tile preset", QuarterTiles, typeof(TileWorldCreator4TilesPreset), false);
        WallTiles = (TileWorldCreator6TilesPreset)EditorGUILayout.ObjectField("6-tile preset", WallTiles, typeof(TileWorldCreator6TilesPreset), false);
        Road = EditorGUILayout.Toggle("Paving material", Road);
        Buildings = EditorGUILayout.Toggle("Detailed building material", Buildings);
        Elevation = EditorGUILayout.FloatField("Elevation (cells)", Elevation);
        HeightScale = EditorGUILayout.FloatField("Height scale", HeightScale);
        PerimeterOnly=EditorGUILayout.Toggle("Outer map boundary only",PerimeterOnly);
        SkipMapBoundary=EditorGUILayout.Toggle("Open sea at map boundary",SkipMapBoundary);
        SurfaceMaterial=(Material)EditorGUILayout.ObjectField("Surface override",SurfaceMaterial,typeof(Material),false);
    }
#endif
}
