using System;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable, ActionName(Name = "Medieval town environment")]
public sealed class TownEnvironmentLayer : TWCBuildLayer
{
    public EnvironmentKit Kit;
    public TreeModelPicker TreeModels;
    public override TWCBuildLayer Clone() => new TownEnvironmentLayer {
        guid = guid, assignedGenerationLayerGuid = assignedGenerationLayerGuid, layerName = layerName, active = active, Kit = Kit, TreeModels=TreeModels };
    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            if (Kit == null || !CoreLayoutCache.TryGetTown(creator, out var plan)) return;
            var root = creator.AddLayerObject(layerName, guid); root.transform.SetParent(creator.worldObject.transform, false);
            root.transform.localRotation = Quaternion.identity;
            foreach (var child in root.transform.Cast<Transform>().ToArray()) { child.gameObject.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject); else UnityEngine.Object.DestroyImmediate(child.gameObject); }
            var previous = root.GetComponent<EnvironmentMeshOwner>();
            if (previous != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(previous); else UnityEngine.Object.DestroyImmediate(previous); }
            var biome = creator.GetComponent<TownBiomeStyle>()?.Current ?? OverworldBiome.Grassland;
            var batch = new EnvironmentBatch(root.transform); float size = creator.twcAsset.cellSize;
            var faces=root.GetComponent<BiomeDecorationSurfaceSet>() ?? root.AddComponent<BiomeDecorationSurfaceSet>();faces.Faces.Clear();
            var propCounts = new Dictionary<(int, int), int>();
            int propTriangles = 0;
            for (int y = 0; y < plan.Height; y++) for (int x = 0; x < plan.Width; x++)
            {
                var position = new Vector3(x + .5f, y + .5f, 0) * size;
                batch.Add(Kit.Mesh("Paving"), Kit.Ground(biome), position + Vector3.forward * .02f, Vector3.one * size);
                if (plan.Layers[TownLayers.Trees][x, y])
                {
                    uint hash=OverworldCosmetics.Hash(creator.currentSeed ^ 15401,x,y);
                    var picker=TreeModels!=null?TreeModels:Kit.TreeModels;
                    string model=picker!=null?picker.Pick(biome,hash):"Tree";
                    batch.Add(Kit.Mesh(model),Kit.Material(biome),position,Vector3.one*size*(.85f+(hash>>8)%16*.01f),(hash>>16)%360);
                }
                if (plan.Layers.TryGetValue(TownLayers.Props, out var props) && props[x,y])
                {
                    uint hash = OverworldCosmetics.Hash(creator.currentSeed ^ 18013, x, y);
                    var chunk = (x / 32, y / 32);
                    propCounts.TryGetValue(chunk, out int count);
                    string model = biome switch {
                        OverworldBiome.Desert => "Cactus", OverworldBiome.Tundra => "SnowRock",
                        OverworldBiome.Marsh => "Mushrooms", OverworldBiome.Water => "Reeds",
                        OverworldBiome.Volcanic => "Basalt", OverworldBiome.Mountain => "Rock",
                        _ => hash % 2 == 0 ? "Flowers" : "Mushrooms"
                    };
                    int triangles = Kit.Triangles(model);
                    if (count < 48 && propTriangles + triangles <= 120000)
                    {
                        batch.Add(Kit.Mesh(model), Kit.Material(biome), position, Vector3.one * size * .45f, (hash >> 16) % 360);
                        propCounts[chunk] = count + 1;
                        propTriangles += triangles;
                    }
                }
            }
            // Expand the TWC scenery beyond the playable grid. Out-of-bounds cells are
            // impassable in WalkableMap, so the visible enclosure matches movement.
            const int border = 4;
            for (int y = -border; y < plan.Height + border; y++)
            for (int x = -border; x < plan.Width + border; x++)
            {
                if (x >= 0 && y >= 0 && x < plan.Width && y < plan.Height) continue;
                var position = new Vector3(x + .5f, y + .5f, 0) * size;
                batch.Add(Kit.Mesh("Paving"), Kit.Ground(biome), position + Vector3.forward * .02f, Vector3.one * size);
                bool approach = y < 0 && Mathf.Abs(x - plan.Exit.X) <= 1;
                bool inner = ((x == -1 || x == plan.Width) && y >= -1 && y <= plan.Height) ||
                    ((y == -1 || y == plan.Height) && x >= -1 && x <= plan.Width);
                if (approach)
                    batch.Add(Kit.Mesh("Paving"), Kit.Road, position, Vector3.one * size);
                else if (inner)
                {
                    faces.Add(Kit.Mesh("Wall"),Matrix4x4.TRS(position,Quaternion.Euler(0,0,x==-1||x==plan.Width?90:0),Vector3.one*size),biome,DecorationSurface.BuiltWall,size);
                    batch.Add(Kit.Mesh("Wall"), Kit.Material(biome), position, Vector3.one * size,
                        x == -1 || x == plan.Width ? 90 : 0);
                    if ((x == -1 || x == plan.Width) && (y == -1 || y == plan.Height))
                        batch.Add(Kit.Mesh("Wall"), Kit.Material(biome), position, Vector3.one * size);
                }
                else
                {
                    uint hash = OverworldCosmetics.Hash(creator.currentSeed ^ 15401, x, y);
                    var picker = TreeModels != null ? TreeModels : Kit.TreeModels;
                    batch.Add(Kit.Mesh(picker != null ? picker.Pick(biome, hash) : "Tree"), Kit.Material(biome),
                        position, Vector3.one * size, (hash >> 16) % 360);
                }
            }
            batch.Finish();
            TownGateVisuals.Create(Kit, biome, root.transform, plan.Exit, size, "Town gate", false);
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

