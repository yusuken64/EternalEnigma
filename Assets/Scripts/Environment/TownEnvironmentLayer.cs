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
            var diorama=DioramaCatalog.Load();
            var groundStyle=PaintedGroundStyle.Load();
            if(groundStyle!=null)
            {
                const int margin=4;
                var ground=new GroundSurface[plan.Width+margin*2,plan.Height+margin*2];
                for(int y=-margin;y<plan.Height+margin;y++) for(int x=-margin;x<plan.Width+margin;x++)
                {
                    var surface=PaintedGroundStyle.Surface(biome);
                    if(surface==GroundSurface.Water) surface=GroundSurface.Sand;
                    if(x>=0&&y>=0&&x<plan.Width&&y<plan.Height)
                    {
                        if(plan.Layers[TownLayers.Parks][x,y]) surface=GroundSurface.Grass;
                        if(plan.Layers[TownLayers.Roads][x,y]||plan.Layers[TownLayers.Buildings][x,y]) surface=GroundSurface.Cobble;
                        if(plan.Layers.TryGetValue(TownLayers.Alleys,out var alleys)&&alleys[x,y]) surface=GroundSurface.Dirt;
                        if(plan.Layers.TryGetValue(TownLayers.MainRoads,out var main)&&main[x,y]) surface=GroundSurface.Cobble;
                    }
                    else if(y<0&&Mathf.Abs(x-plan.Exit.X)<=1) surface=GroundSurface.Cobble;
                    ground[x+margin,y+margin]=surface;
                }
                PaintedGroundMesh.Build(root.transform,ground,groundStyle,size,creator.currentSeed,-margin,-margin);
            }
            for (int y = 0; y < plan.Height; y++) for (int x = 0; x < plan.Width; x++)
            {
                var position = new Vector3(x + .5f, y + .5f, 0) * size;
                if(groundStyle==null) batch.Add(Kit.Mesh("Paving"), Kit.Ground(biome), position + Vector3.forward * .02f, Vector3.one * size);
                if (plan.Layers[TownLayers.Trees][x, y])
                {
                    uint hash=OverworldCosmetics.Hash(creator.currentSeed ^ 15401,x,y);
                    var picker=TreeModels!=null?TreeModels:Kit.TreeModels;
                    string model=picker!=null?picker.Pick(biome,hash):"Tree";
                    var tree=diorama?.Get(model);
                    if(tree!=null)
                    {
                        float variation=DioramaPlacement.Variation(hash);
                        bool Protected(int a,int b)=>a>=0&&b>=0&&a<plan.Width&&b<plan.Height&&(plan.Layers[TownLayers.Roads][a,b]||plan.IsReserved(new GridPoint(a,b))||plan.Layers[TownLayers.Buildings][a,b]);
                        if(DioramaPlacement.TreePosition(tree,x,y,size,variation,Protected,out var treePosition))
                            diorama.Add(batch,model,biome,treePosition,(hash>>16)%360,variation);
                    }
                    else batch.Add(Kit.Mesh(model),Kit.Material(biome),position,Vector3.one*size*(.85f+(hash>>8)%16*.01f),(hash>>16)%360);
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
                    string replacement=DioramaPlacement.GroundCover(biome,hash);
                    if(diorama?.Get(replacement)!=null)model=replacement;
                    int triangles = Kit.Triangles(model);
                    if (count < 160 && propTriangles + triangles <= 600000)
                    {
                        if(diorama?.Get(model)!=null)diorama.Add(batch,model,biome,position,(hash>>16)%360,DioramaPlacement.Variation(hash));
                        else batch.Add(Kit.Mesh(model), Kit.Material(biome), position, Vector3.one * size * .45f, (hash >> 16) % 360);
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
                if(groundStyle==null) batch.Add(Kit.Mesh("Paving"), Kit.Ground(biome), position + Vector3.forward * .02f, Vector3.one * size);
                bool approach = y < 0 && Mathf.Abs(x - plan.Exit.X) <= 1;
                bool inner = ((x == -1 || x == plan.Width) && y >= -1 && y <= plan.Height) ||
                    ((y == -1 || y == plan.Height) && x >= -1 && x <= plan.Width);
                if (approach)
                {
                    if(groundStyle==null) batch.Add(Kit.Mesh("Paving"), Kit.Road, position, Vector3.one * size);
                }
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
                    string model=picker!=null?picker.Pick(biome,hash):"Tree";
                    if(diorama?.Get(model)!=null)diorama.Add(batch,model,biome,position,(hash>>16)%360,DioramaPlacement.Variation(hash));
                    else batch.Add(Kit.Mesh(model), Kit.Material(biome),position, Vector3.one * size, (hash >> 16) % 360);
                }
            }
            // Join the boundary to the gate on the exit tile, one row inward.
            // These short returns close the gaps beside its three-cell span.
            foreach (int side in new[] { -1, 1 })
                batch.Add(Kit.Mesh("Wall"), Kit.Material(biome),
                    new Vector3(plan.Exit.X + .5f + side * 1.5f, plan.Exit.Y, 0) * size,
                    Vector3.one * size, 90);
            if(diorama!=null)DioramaTownDressing.Build(batch,plan,biome,size,diorama);
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

