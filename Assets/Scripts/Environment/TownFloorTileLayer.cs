using System;
using TWC;
using TWC.Actions;
using UnityEngine;

/// <summary>Continuous interior flooring up to the outer house facade.</summary>
[Serializable, ActionName(Name = "Town house floors")]
public sealed class TownFloorTileLayer : TWCBuildLayer
{
    public EnvironmentKit Kit;
    public Material Material;

    public override TWCBuildLayer Clone() => new TownFloorTileLayer {
        guid = guid, layerName = layerName, active = active,
        assignedGenerationLayerGuid = assignedGenerationLayerGuid, Kit = Kit, Material = Material
    };

    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            if (Kit == null || Material == null) throw new InvalidOperationException("House floor assets are missing.");
            var mask = creator.twcAsset.mapBlueprintLayers.Find(l => l.guid == assignedGenerationLayerGuid)?.map;
            if (mask == null) return;
            var root = creator.AddLayerObject(layerName, guid);
            root.transform.SetParent(creator.worldObject.transform, false);
            root.transform.localRotation = Quaternion.identity;
            DungeonPresentation.ClearOutput(root);
            foreach (var owner in root.GetComponents<EnvironmentMeshOwner>()) DungeonPresentation.Release(owner);
            var batch = new EnvironmentBatch(root.transform);
            float size = creator.twcAsset.cellSize;
            for (int x = 0; x < mask.GetLength(0); x++)
            for (int y = 0; y < mask.GetLength(1); y++)
                if (mask[x, y]) batch.Add(Kit.Mesh("Paving"), Material,
                    new Vector3(x + .5f, y + .5f, -.01f) * size, Vector3.one * size);
            batch.Finish();
        }
        finally { creator.executedBuildLayersCount++; }
    }
}
