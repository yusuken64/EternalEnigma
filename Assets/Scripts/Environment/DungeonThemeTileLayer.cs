using System;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEngine;

public enum DungeonThemeRole { Custom, Floor, Accent, Boundary }

/// <summary>Authored TWC presentation, using generated quarter tiles without consuming randomness.</summary>
[Serializable, ActionName(Name = "Dungeon floor / accent")]
public class DungeonThemeTileLayer : TWCBuildLayer
{
    public DungeonThemeRole Role;
    public bool UseThemePreset = true;
    public TileWorldCreator4TilesPreset Preset;
    public Vector3 Offset;
    public string[] IgnoreLayers = Array.Empty<string>();

    public override TWCBuildLayer Clone()
    {
        var copy = (DungeonThemeTileLayer)MemberwiseClone();
        // TWC's duplicate button requires a new identity. Its priority-build API restores the original GUID.
        copy.guid = Guid.NewGuid();
        copy.IgnoreLayers = IgnoreLayers?.ToArray() ?? Array.Empty<string>();
        return copy;
    }
    protected bool[][,] Exclusions(TileWorldCreator creator) => (IgnoreLayers ?? Array.Empty<string>())
        .Select(id => Guid.TryParse(id, out var value) ? creator.GetMapOutputFromBlueprintLayer(value) : null)
        .Where(mask => mask != null).ToArray();
    public static bool At(bool[,] map, int x, int y) => map != null && x >= 0 && y >= 0 &&
        x < map.GetLength(0) && y < map.GetLength(1) && map[x, y];

    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            DungeonLayerOutput.Remove(creator, guid);
            var map = creator.GetGeneratedBlueprintMap(assignedGenerationLayerGuid.ToString());
            if (!active || map == null || Preset == null) return;
            var root = DungeonLayerOutput.Create(creator, this, Offset);
            var batch = new EnvironmentBatch(root.transform);
            var faces = root.AddComponent<BiomeDecorationSurfaceSet>();
            var exclusions = Exclusions(creator);
            float unit = creator.twcAsset.cellSize * .5f;
            foreach (var tile in map.clusters.Values.SelectMany(c => c.Values))
            {
                int x = (int)tile.position.x / 2, y = (int)tile.position.z / 2;
                if (exclusions.Any(mask => At(mask, x, y))) continue;
                var prefab = tile.tileType switch {
                    TileData.TileType.edge => Preset.edgeTile,
                    TileData.TileType.exteriorCorner => Preset.exteriorCornerTile,
                    TileData.TileType.interiorCorner => Preset.interiorCornerTile,
                    _ => Preset.fillTile
                };
                if (prefab == null) continue;
                var position = new Vector3((tile.position.x + .5f) * unit, (tile.position.z + .5f) * unit, 0);
                var rotation = Quaternion.Euler(0, 0, -tile.yRotation);
                AddPrefab(batch, faces, prefab, position, Vector3.one * unit, rotation,
                    Role == DungeonThemeRole.Boundary, creator.twcAsset.cellSize);
            }
            batch.Finish();
        }
        finally { creator.executedBuildLayersCount++; }
    }

    protected static void AddPrefab(EnvironmentBatch batch, BiomeDecorationSurfaceSet faces, GameObject prefab,
        Vector3 position, Vector3 scale, Quaternion rotation, bool boundary, float cellSize, Material materialOverride = null)
    {
        if (prefab == null) return;
        // Quarter-tile floors retain receiver classification and continuous planar UVs.
        var filters = prefab.GetComponentsInChildren<MeshFilter>();
        if (filters.All(f => f.sharedMesh != null && f.sharedMesh.subMeshCount == 1))
        {
            var rootMatrix = Matrix4x4.TRS(position,rotation,scale) * prefab.transform.worldToLocalMatrix;
            foreach (var filter in filters)
            {
                var tileMatrix = rootMatrix * filter.transform.localToWorldMatrix;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                    batch.Add(filter.sharedMesh,materialOverride != null ? materialOverride : renderer.sharedMaterial,
                        tileMatrix.GetColumn(3),tileMatrix.lossyScale,tileMatrix.rotation);
            }
        }
        else batch.AddPrefab(prefab, position, scale, rotation, materialMap: materialOverride == null ? null : _ => materialOverride);
        if (!boundary) return;
        var matrix = Matrix4x4.TRS(position, rotation, scale) * prefab.transform.worldToLocalMatrix;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null)
                faces.Add(filter.sharedMesh, matrix * filter.transform.localToWorldMatrix,
                    EternalEnigma.Core.World.OverworldBiome.Grassland, DecorationSurface.BuiltWall, cellSize);
    }

#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)
    {
        using (var change = new UnityEditor.EditorGUI.ChangeCheckScope())
        {
            layerName = UnityEditor.EditorGUILayout.TextField("Layer name", layerName);
            Role = (DungeonThemeRole)UnityEditor.EditorGUILayout.EnumPopup("Theme role", Role);
            assignedGenerationLayerGuid = BlueprintField("Blueprint", assignedGenerationLayerGuid, asset);
            UseThemePreset = UnityEditor.EditorGUILayout.Toggle("Use theme preset", UseThemePreset);
            Preset = (TileWorldCreator4TilesPreset)UnityEditor.EditorGUILayout.ObjectField(
                "Four-piece preset", Preset, typeof(TileWorldCreator4TilesPreset), false);
            Offset = UnityEditor.EditorGUILayout.Vector3Field("Offset", Offset);
            DrawExtraGUI();
            UnityEditor.EditorGUILayout.LabelField("Exclude blueprint cells", UnityEditor.EditorStyles.boldLabel);
            var ids = (IgnoreLayers ?? Array.Empty<string>()).ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                using (new UnityEditor.EditorGUILayout.HorizontalScope())
                {
                    Guid.TryParse(ids[i], out var id);
                    ids[i] = BlueprintField("Exclusion", id, asset).ToString();
                    if (GUILayout.Button("Remove", GUILayout.Width(65))) { ids.RemoveAt(i); i--; }
                }
            }
            if (GUILayout.Button("Add exclusion")) ids.Add(Guid.Empty.ToString());
            IgnoreLayers = ids.ToArray();
            if (change.changed) UnityEditor.EditorUtility.SetDirty(asset);
        }
    }
    protected virtual void DrawExtraGUI() { }
    static Guid BlueprintField(string label, Guid current, TileWorldCreatorAsset asset)
    {
        var layers = asset.mapBlueprintLayers;
        var names = new[] { "None" }.Concat(layers.Select(l => string.IsNullOrEmpty(l.layerName) ? l.guid.ToString() : l.layerName)).ToArray();
        int selected = layers.FindIndex(l => l.guid == current) + 1;
        int next = UnityEditor.EditorGUILayout.Popup(label, selected, names);
        return next == selected ? current : next == 0 ? Guid.Empty : layers[next - 1].guid;
    }
#endif
}
