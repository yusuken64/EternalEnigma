using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

public static class DungeonPresentation
{
    private static readonly ConditionalWeakTable<DungeonFloor, HashSet<GridPoint>> protectedCells = new();
    private static HashSet<GridPoint> Protected(DungeonFloor floor)
    {
        var cells = new HashSet<GridPoint> { floor.Start, floor.Stairs };
        foreach (var p in floor.Scenery) cells.Add(p.Cell);
        foreach (var p in floor.Items) cells.Add(p.Cell);
        foreach (var p in floor.Gold) cells.Add(p.Cell);
        foreach (var p in floor.Traps) cells.Add(p.Cell);
        foreach (var p in floor.Enemies) cells.Add(p.Cell);
        foreach (var p in floor.Locks) cells.Add(p.Door);
        return cells;
    }
    public const float GroundPlaneZ = .001f;
    public static void GroundFloorObject(Transform root)
    {
        // Keep the logical cell/root fixed; authored pickup and stair meshes live below it.
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) return;
        var footprint = root.GetComponentInChildren<DungeonPickupFootprint>();
        float bottom = footprint != null ? footprint.GroundZ : renderers.Max(renderer => renderer.bounds.max.z);
        var shift = Vector3.forward * (GroundPlaneZ - bottom);
        foreach (Transform child in root) child.position += shift;
    }
    public static void PrepareMapRoot(Transform root, bool legacy)
    {
        // Configure before building: moving this root afterwards shifts the ground into units.
        root.position = legacy ? new Vector3(0, 0, -1.51f) : Vector3.zero;
        root.localScale = legacy ? new Vector3(1, 1, 3.35f) : Vector3.one;
    }
    // Legacy TWC output sits at Z=-1.51 to compensate for its old tile meshes.
    // Themed meshes are already authored on the XY ground plane; don't inherit that lift.
    public static void SetGroundHeight(Transform layer, float height = 0)
    {
        // Leave a small gap beneath unit feet and selection sprites at Z=0.
        var position = layer.position; position.z = GroundPlaneZ + height; layer.position = position;
    }
    public static TileWorldCreatorAsset CloneTemplate(TileWorldCreatorAsset source)
    {
        var clone=UnityEngine.Object.Instantiate(source);clone.hideFlags=HideFlags.DontSave;
        // Preview textures are generated per clone, never borrowed from an authored asset.
        foreach(var layer in clone.mapBlueprintLayers) layer.previewTextureMap=null;
        return clone;
    }
    public static void ReleaseTemplate(TileWorldCreatorAsset asset)
    {
        if(asset==null)return;
        foreach(var texture in asset.mapBlueprintLayers.Select(l=>l.previewTextureMap).Where(t=>t!=null).Distinct())
        {
#if UNITY_EDITOR
            if(UnityEditor.EditorUtility.IsPersistent(texture))continue;
#endif
            Release(texture);
        }
        Release(asset);
    }
    public static void ClearOutput(GameObject root)
    {
        // Baked scene meshes are not assets. Shared imported/persistent meshes must survive.
        var owned=new HashSet<Mesh>(root.GetComponentsInChildren<EnvironmentMeshOwner>(true).SelectMany(o=>o.Meshes));
        foreach(var mesh in owned) if(mesh!=null) Release(mesh);
        foreach(var owner in root.GetComponentsInChildren<EnvironmentMeshOwner>(true))
        {
            owner.Meshes.Clear();
            foreach(var material in owner.Materials)if(material!=null)Release(material);
            owner.Materials.Clear();
            foreach(var texture in owner.Textures)if(texture!=null)Release(texture);
            owner.Textures.Clear();
        }
        foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Where(m=>m!=null).Distinct())
        {
            if(owned.Contains(mesh)) continue;
#if UNITY_EDITOR
            if(UnityEditor.EditorUtility.IsPersistent(mesh)) continue;
#endif
            // Only TWC's combined meshes are owned here; prefab meshes are shared.
            if(mesh.name.Contains("_Cluster_")) Release(mesh);
        }
        foreach(Transform child in root.transform.Cast<Transform>().ToArray()) Release(child.gameObject);
    }
    public static void TrackLegacyMeshes(GameObject root)
    {
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;
            if(mesh==null || !mesh.name.Contains("_Cluster_")) continue;
#if UNITY_EDITOR
            if(UnityEditor.EditorUtility.IsPersistent(mesh)) continue;
#endif
            var owner=filter.GetComponent<EnvironmentMeshOwner>() ?? filter.gameObject.AddComponent<EnvironmentMeshOwner>();
            if(!owner.Meshes.Contains(mesh)) owner.Meshes.Add(mesh);
        }
    }
    public static void Release(UnityEngine.Object obj)
    {
        if(obj==null)return;
#if UNITY_EDITOR
        if(UnityEditor.EditorUtility.IsPersistent(obj))return;
#endif
        if(obj is GameObject go) go.SetActive(false);
        if(Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
    }
    public static bool DecorationAllowed(DungeonFloor floor,int x,int y)
    {
        // Blocked cells also contain walls. Decorate additionally fits props into the
        // paved shoulder outside the rendered wall footprint, never the cell center.
        if (x<0 || y<0 || x>=floor.Width || y>=floor.Height || floor.Layers[DungeonLayers.Floor][x,y]) return false;
        return !protectedCells.GetValue(floor, Protected).Contains(new GridPoint(x,y));
    }
    static Dictionary<GridPoint, Bounds> BoundaryFootprints(TileWorldCreator creator, Transform space)
    {
        var footprints = new Dictionary<GridPoint, Bounds>();
        float size = creator.twcAsset.cellSize;
        var boundaries = creator.twcAsset.mapBuildLayers.OfType<DungeonThemeTileLayer>()
            .Where(layer => layer.active && layer.Role == DungeonThemeRole.Boundary).Select(layer => layer.guid).ToHashSet();
        foreach (var layer in creator.worldObject.GetComponentsInChildren<LayerIdentifier>())
        {
            if (!boundaries.Contains(layer.assignedLayer)) continue;
            foreach (var surfaces in layer.GetComponentsInChildren<BiomeDecorationSurfaceSet>())
            {
                var matrix = space.worldToLocalMatrix * surfaces.transform.localToWorldMatrix;
                foreach (var piece in surfaces.SolidBounds)
                {
                    var bounds = BiomeDecorationSurfaceSet.TransformBounds(piece, matrix);
                    if (bounds.min.z >= -.01f) continue; // Paving beneath the wall is support, not an obstacle.
                    int minX = Mathf.FloorToInt((bounds.min.x + .0001f) / size), maxX = Mathf.FloorToInt((bounds.max.x - .0001f) / size);
                    int minY = Mathf.FloorToInt((bounds.min.y + .0001f) / size), maxY = Mathf.FloorToInt((bounds.max.y - .0001f) / size);
                    for (int y = minY; y <= Mathf.Max(minY, maxY); y++) for (int x = minX; x <= Mathf.Max(minX, maxX); x++)
                    {
                        var cell = new GridPoint(x, y);
                        if (footprints.TryGetValue(cell, out var occupied)) { occupied.Encapsulate(bounds); footprints[cell] = occupied; }
                        else footprints.Add(cell, bounds);
                    }
                }
            }
        }
        return footprints;
    }
    static bool DecorationPose(DungeonFloor floor, int x, int y, uint hash, float size, Mesh mesh,
        Bounds wall, out Vector3 position, out float scale, out float angle)
    {
        position = default; scale = 0; angle = hash % 360;
        var rotation = Quaternion.Euler(0, 0, angle);
        var bounds = mesh.bounds;
        var extent = rotation * new Vector3(bounds.size.x, 0, 0);
        var other = rotation * new Vector3(0, bounds.size.y, 0);
        float width = Mathf.Abs(extent.x) + Mathf.Abs(other.x), depth = Mathf.Abs(extent.y) + Mathf.Abs(other.y);
        float margin = size * .025f;
        var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        for (int i = 0; i < directions.Length; i++)
        {
            var direction = directions[(i + (int)(hash % 4)) % 4];
            if (!floor.IsWalkable(new GridPoint(x + direction.x, y + direction.y))) continue;
            float left = x * size + margin, right = (x + 1) * size - margin;
            float bottom = y * size + margin, top = (y + 1) * size - margin;
            if (direction.x > 0) left = Mathf.Max(left, wall.max.x + margin);
            if (direction.x < 0) right = Mathf.Min(right, wall.min.x - margin);
            if (direction.y > 0) bottom = Mathf.Max(bottom, wall.max.y + margin);
            if (direction.y < 0) top = Mathf.Min(top, wall.min.y - margin);
            if (right - left < size * .08f || top - bottom < size * .08f) continue;
            float fit = Mathf.Min((right - left) / Mathf.Max(.01f, width), (top - bottom) / Mathf.Max(.01f, depth),
                size * .6f / Mathf.Max(.01f, Mathf.Max(width, depth)), .85f / Mathf.Max(.01f, bounds.size.z));
            if (Mathf.Max(width, depth) * fit < size * .12f) continue;
            scale = fit;
            var center = rotation * new Vector3(bounds.center.x, bounds.center.y, 0) * scale;
            position = new Vector3((left + right) * .5f - center.x, (bottom + top) * .5f - center.y, -bounds.max.z * scale);
            return true;
        }
        return false;
    }
    public static uint Hash(int seed,int x,int y)
    { unchecked { uint h=(uint)seed ^ (uint)x*0x9e3779b9u ^ (uint)y*0x85ebca6bu ^ 0x6d2b79f5u; h=(h^(h>>16))*0x7feb352du;return h^(h>>15); } }
    public static void PreviewScenery(TileWorldCreator creator,DungeonFloor floor,DungeonTheme theme)
    {
        if(floor.Scenery.Count==0)return;
        var root=new GameObject("Scenery preview");root.transform.SetParent(creator.worldObject.transform,false);
        SetGroundHeight(root.transform);
        var dungeon=root.AddComponent<TileWorldDungeon>();dungeon.Interactables=new();dungeon.Setup(creator,floor);
        foreach(var definition in floor.Scenery)
        {
            var prop=DungeonProp.Create(dungeon,definition,theme);
            var fog = prop.GetComponent<FogHiddenVisual>();
            if (fog != null) fog.enabled = false;
        }
    }
    public static void Decorate(TileWorldCreator creator,DungeonFloor floor,DungeonTheme theme)
    {
        var kit=EnvironmentKit.Load();
        var interiors=TownInteriorCatalog.Load();
        if(kit==null) return;
        var root=new GameObject("Theme cosmetics"); root.transform.SetParent(creator.worldObject.transform,false);
        SetGroundHeight(root.transform);
        var batch=new EnvironmentBatch(root.transform);
        float size=creator.twcAsset.cellSize;
        var walls = BoundaryFootprints(creator, root.transform);
        for(int cy=0;cy<floor.Height;cy+=32) for(int cx=0;cx<floor.Width;cx+=32)
        {
            var candidates=new List<(int x,int y,uint hash)>();
            for(int y=cy;y<Math.Min(cy+32,floor.Height);y++) for(int x=cx;x<Math.Min(cx+32,floor.Width);x++)
                if(DecorationAllowed(floor,x,y) && (floor.Layers[DungeonLayers.Floor].At(new GridPoint(x-1,y)) || floor.Layers[DungeonLayers.Floor].At(new GridPoint(x+1,y)) || floor.Layers[DungeonLayers.Floor].At(new GridPoint(x,y-1)) || floor.Layers[DungeonLayers.Floor].At(new GridPoint(x,y+1)))) candidates.Add((x,y,Hash(floor.Seed,x,y)));
            int placed = 0;
            foreach(var p in candidates.OrderBy(p=>p.hash))
            {
                if (placed == 16) break;
                if (!walls.TryGetValue(new GridPoint(p.x, p.y), out var wall)) continue;
                // Dungeon alcoves use the compact theme kit; overworld trees follow
                // a larger hero-height ladder and cannot fit below dungeon fog.
                string id=theme.Decorations.Length>0 ? theme.Decorations[p.hash%(uint)theme.Decorations.Length] : "Rock";
                var model=kit.Models.FirstOrDefault(m=>m.Id==id);
                var interior=interiors?.Get(id);
                Mesh mesh=model?.Mesh ?? interior?.Mesh;
                if(id=="CryptRoots") mesh=Resources.Load<Mesh>("DungeonThemes/CryptRoots");
                int triangleBudget=250;
                if(mesh==null || mesh.triangles.Length/3>triangleBudget) continue;
                if (!DecorationPose(floor, p.x, p.y, p.hash, size, mesh, wall, out var position, out float scale, out float angle)) continue;
                var material=interior != null && model == null ? interiors.Material(theme.Biome) : theme.DecorationMaterial;
                batch.Add(mesh,material,position,Vector3.one*scale,angle);
                placed++;
            }
        }
        batch.Finish();
        creator.worldObject.GetComponentInChildren<DungeonBoundaryCutaway>()?.ApplyMaterials(root,batch.Owner,true);
    }
}
