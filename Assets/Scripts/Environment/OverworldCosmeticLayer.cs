using System;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable, ActionName(Name = "Campaign cosmetic props")]
public sealed class OverworldCosmeticLayer : TWCBuildLayer
{
    public EnvironmentKit Kit;
    public TreeModelPicker TreeModels;
    public override TWCBuildLayer Clone() => new OverworldCosmeticLayer {
        guid = guid, assignedGenerationLayerGuid = assignedGenerationLayerGuid, layerName = layerName, active = active, Kit = Kit, TreeModels=TreeModels };
    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            var grid = creator.GetComponent<CampaignOverworld>()?.CurrentGrid;
            if (grid == null || Kit == null) return;
            var root = creator.AddLayerObject(layerName, guid); root.transform.SetParent(creator.worldObject.transform, false);
            root.transform.localRotation = Quaternion.identity;
            foreach (var child in root.transform.Cast<Transform>().ToArray()) { child.gameObject.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject); else UnityEngine.Object.DestroyImmediate(child.gameObject); }
            var previous = root.GetComponent<EnvironmentMeshOwner>();
            if (previous != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(previous); else UnityEngine.Object.DestroyImmediate(previous); }
            var batch = new EnvironmentBatch(root.transform); float size = creator.twcAsset.cellSize;
            foreach (var p in OverworldCosmetics.Plan(grid, Kit,TreeModels))
                batch.Add(Kit.Mesh(p.Model), Kit.Material(p.Biome), new Vector3(p.X + .5f, p.Y + .5f, -p.Height) * size,
                    Vector3.one * (size * p.Scale), p.Rotation,
                    p.Model.Contains("Bridge")?SilhouetteRole.Receiver:(SilhouetteRole?)null);
            batch.Finish();
        }
        finally { creator.executedBuildLayersCount += 1; }
    }
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)
    {
        layerName=UnityEditor.EditorGUILayout.TextField("Layer name",layerName);
        Kit=(EnvironmentKit)UnityEditor.EditorGUILayout.ObjectField("Biome kit",Kit,typeof(EnvironmentKit),false);
        TreeModels=TreeModelPicker.Draw(TreeModels);
    }
#endif
}

