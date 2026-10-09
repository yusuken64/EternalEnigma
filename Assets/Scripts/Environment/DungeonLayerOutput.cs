using System;
using System.Linq;
using TWC;
using UnityEngine;

/// <summary>Owns one dungeon layer's output, including disabling and independent rebuilds.</summary>
public sealed class DungeonLayerOutput : MonoBehaviour
{
    TileWorldCreator creator;
    Guid layer;
    public static GameObject Create(TileWorldCreator creator, DungeonThemeTileLayer layer, Vector3 offset)
    {
        var root = creator.AddLayerObject(layer.layerName, layer.guid);
        root.transform.SetParent(creator.worldObject.transform, false);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localPosition = offset;
        // Authored dungeon geometry is already in world-height units, including
        // previews whose legacy map root retains its historical Z compensation.
        root.transform.localScale=new Vector3(1,1,1/Mathf.Max(.001f,creator.worldObject.transform.lossyScale.z));
        DungeonPresentation.SetGroundHeight(root.transform, offset.z);
        var lifetime = root.AddComponent<DungeonLayerOutput>();
        lifetime.creator = creator;
        lifetime.layer = layer.guid;
        lifetime.Subscribe();
        return root;
    }
    public static void Remove(TileWorldCreator creator, Guid guid)
    {
        if (creator.worldObject == null) return;
        foreach (var identifier in creator.worldObject.GetComponentsInChildren<LayerIdentifier>(true)
            .Where(id => id.assignedLayer == guid).ToArray())
        {
            DungeonPresentation.ClearOutput(identifier.gameObject);
            identifier.transform.SetParent(null);
            DungeonPresentation.Release(identifier.gameObject);
        }
    }
    void Built(TileWorldCreator owner)
    {
        if (!owner.twcAsset.mapBuildLayers.Any(l => l.guid == layer && l.active)) Remove(owner, layer);
    }
    void OnDisable() { if (creator != null) creator.OnBuildLayersComplete -= Built; }
    void OnEnable() => Subscribe();
    void Subscribe()
    {
        if (creator == null) return;
        creator.OnBuildLayersComplete -= Built;
        creator.OnBuildLayersComplete += Built;
    }
}
