using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable, ActionName(Name = "Painted campaign ground")]
public sealed class OverworldGroundLayer : TWCBuildLayer
{
    public PaintedGroundStyle Style;
    public static readonly string[] PointsOfInterest={OverworldLayers.StoryDungeons,OverworldLayers.RepeatableDungeons,OverworldLayers.FinalDungeon,OverworldLayers.Converters,OverworldLayers.Landmarks,OverworldLayers.Secrets};
    public CampaignLayerBinding[] Inputs = DefaultInputs();
    public static CampaignLayerBinding[] DefaultInputs() => Enum.GetValues(typeof(OverworldBiome)).Cast<OverworldBiome>()
        .SelectMany(b => new[] {OverworldLayers.Biome(b),OverworldLayers.Landscape(b)})
        .Concat(new[] {OverworldLayers.Roads,OverworldLayers.Water,OverworldLayers.Bridges,OverworldLayers.TownFootprints})
        .Concat(PointsOfInterest)
        .Select(n => new CampaignLayerBinding(n,n)).ToArray();
    public override TWCBuildLayer Clone() => new OverworldGroundLayer {
        guid=guid,assignedGenerationLayerGuid=assignedGenerationLayerGuid,layerName=layerName,active=active,Style=Style,
        Inputs=Inputs.Select(i=>new CampaignLayerBinding(i.CoreLayer,i.BlueprintLayer)).ToArray()
    };
    public override void Execute(TileWorldCreator creator,bool force)
    {
        try
        {
            if (Style == null) return;
            var root=creator.AddLayerObject(layerName,guid); root.transform.SetParent(creator.worldObject.transform,false);
            root.transform.localRotation=Quaternion.identity;
            foreach(var child in root.transform.Cast<Transform>().ToArray()) Release(child.gameObject);
            var inputs=Inputs.ToDictionary(i=>i.CoreLayer,i=>creator.GetMapOutputFromBlueprintLayer(i.BlueprintLayer));
            int width=creator.twcAsset.mapWidth,height=creator.twcAsset.mapHeight;
            var cells=new GroundSurface[width,height];
            var biomes=Enum.GetValues(typeof(OverworldBiome)).Cast<OverworldBiome>().ToArray();
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                GroundSurface surface=GroundSurface.Grass;
                foreach(var biome in biomes) if(At(OverworldLayers.Landscape(biome),x,y)) {surface=PaintedGroundStyle.Surface(biome);break;}
                foreach(var biome in biomes) if(At(OverworldLayers.Biome(biome),x,y)) {surface=PaintedGroundStyle.Surface(biome);break;}
                // Highest first: bridge > water > plaza > road > biome > landscape.
                if(At(OverworldLayers.Roads,x,y)) surface=GroundSurface.Dirt;
                if(At(OverworldLayers.TownFootprints,x,y)) surface=GroundSurface.Cobble;
                foreach(string poi in PointsOfInterest)
                    for(int yy=Math.Max(0,y-1);yy<=Math.Min(height-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(width-1,x+1);xx++)
                        if(At(poi,xx,yy))surface=GroundSurface.Cobble;
                if(At(OverworldLayers.Water,x,y)) surface=GroundSurface.Water;
                if(At(OverworldLayers.Bridges,x,y)) surface=GroundSurface.Bridge;
                cells[x,y]=surface;
            }
            PaintedGroundMesh.Build(root.transform,cells,Style,creator.twcAsset.cellSize,creator.currentSeed);
            bool At(string name,int x,int y)=>inputs.TryGetValue(name,out var mask)&&mask!=null&&mask[x,y];
        }
        finally {creator.executedBuildLayersCount++;}
    }
    static void Release(GameObject obj) {obj.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)
    {
        layerName=UnityEditor.EditorGUILayout.TextField("Layer name",layerName);
        Style=(PaintedGroundStyle)UnityEditor.EditorGUILayout.ObjectField("Ground style",Style,typeof(PaintedGroundStyle),false);
    }
#endif
}
