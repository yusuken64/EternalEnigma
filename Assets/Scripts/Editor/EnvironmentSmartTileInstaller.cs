using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEngine;

public static class EnvironmentSmartTileInstaller
{
    private const string Root = "Assets/Art/EnvironmentKit/SmartTiles";
    public static void Install(EnvironmentKit kit, TileWorldCreatorAsset world, TileWorldCreatorAsset town)
    {
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        var presets = new Dictionary<string,TileWorldCreator4TilesPreset>();
        foreach (string family in new[] { "Mountain", "House", "Road", "Summit" })
        {
            string path = Root + "/" + family + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<TileWorldCreator4TilesPreset>(path);
            if (preset == null) { preset = ScriptableObject.CreateInstance<TileWorldCreator4TilesPreset>(); AssetDatabase.CreateAsset(preset,path); }
            preset.edgeTile = Piece(kit,"Smart"+family+"Edge"); preset.exteriorCornerTile = Piece(kit,"Smart"+family+"Outer");
            preset.interiorCornerTile = Piece(kit,"Smart"+family+"Inner"); preset.fillTile = Piece(kit,"Smart"+family+"Fill");
            EditorUtility.SetDirty(preset); presets[family] = preset;
        }
        var walls = AssetDatabase.LoadAssetAtPath<TileWorldCreator6TilesPreset>(Root+"/Wall.asset");
        if (walls == null) { walls = ScriptableObject.CreateInstance<TileWorldCreator6TilesPreset>(); AssetDatabase.CreateAsset(walls,Root+"/Wall.asset"); }
        walls.singleTile = Piece(kit,"SmartWallSingle"); walls.deadEndTile = Piece(kit,"SmartWallEnd"); walls.straightTile = Piece(kit,"SmartWallStraight");
        walls.cornerTile = Piece(kit,"SmartWallCorner"); walls.threeWayTile = Piece(kit,"SmartWallTee"); walls.fourWayTile = Piece(kit,"SmartWallCross");
        EditorUtility.SetDirty(walls);

        world.mapBuildLayers.RemoveAll(l => l is EnvironmentSmartTileLayer || l is OverworldOceanLayer);
        foreach (var layer in world.mapBuildLayers) if (!(layer is OverworldCosmeticLayer)) layer.active = false;
        var mountain = Blueprint(world, SmartEnvironmentMasks.Mountains);
        Build(world,mountain,"Mountain",0);
        for (int i=1;i<=2;i++)
        {
            var next = Blueprint(world,"Smart/Mountains Tier "+(i+1));
            next.stack = new List<TileWorldCreatorAsset.BlueprintLayerData.ActionStack> {
                new("Lower cliff mask",new Add { guidCopyLayer = mountain.guid }), new("Inset upper cliff",new Shrink()) };
            Build(world,next,"Mountain",i*.41f); mountain = next;
        }
        var tops = Blueprint(world,SmartEnvironmentMasks.Summits);
        tops.stack = new() { new("Highest cliff plateau",new Add {guidCopyLayer=mountain.guid}),
            new("Safe plateau inset",new Shrink()), new("Dense small summit patches",new MountainTopNoise()) };
        Build(world,tops,"Summit",1.23f);
        Build(world,Blueprint(world,SmartEnvironmentMasks.Roads),"Road",0);
        Build(world,Blueprint(world,SmartEnvironmentMasks.Houses),"House",0);
        Build(world,Blueprint(world,SmartEnvironmentMasks.Walls),"Wall",0);
        var ocean = OceanMaterial("Ocean",new Color(.20f,.47f,.64f));
        world.mapBuildLayers.Add(new OverworldOceanLayer {guid=Guid.NewGuid(),layerName="Ocean/Surrounding water",Water=ocean,
            assignedGenerationLayerGuid=world.mapBlueprintLayers[0].guid,Padding=128});
        var noise=SurfaceMaterial("OceanNoise","EternalEnigma/Ocean Noise");
        noise.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/EnvironmentKit/Textures/OceanNoise.png");
        noise.mainTextureScale=new Vector2(.04f,.04f); EditorUtility.SetDirty(noise);
        world.mapBuildLayers.Add(new OverworldOceanLayer {guid=Guid.NewGuid(),layerName="Ocean/Noise variation",Water=noise,NoiseOverlay=true,
            assignedGenerationLayerGuid=world.mapBlueprintLayers[0].guid,Padding=128});
        var shoreline=SurfaceMaterial("Shoreline","EternalEnigma/Smart Shoreline");
        shoreline.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/EnvironmentKit/Textures/Ocean.png");
        shoreline.SetTexture("_NoiseTex",noise.mainTexture);EditorUtility.SetDirty(shoreline);
        var shoreSet=AssetDatabase.LoadAssetAtPath<TileWorldCreator4TilesPreset>(Root+"/Shoreline.asset");
        if(shoreSet==null) {shoreSet=ScriptableObject.CreateInstance<TileWorldCreator4TilesPreset>();AssetDatabase.CreateAsset(shoreSet,Root+"/Shoreline.asset");}
        shoreSet.edgeTile=Piece(kit,"SmartShoreEdge",shoreline);shoreSet.exteriorCornerTile=Piece(kit,"SmartShoreOuter",shoreline);
        shoreSet.interiorCornerTile=Piece(kit,"SmartShoreInner",shoreline);shoreSet.fillTile=null;EditorUtility.SetDirty(shoreSet);
        var coast=Blueprint(world,SmartEnvironmentMasks.Coast);
        world.mapBuildLayers.Add(new EnvironmentSmartTileLayer {guid=Guid.NewGuid(),layerName=SmartEnvironmentMasks.Coast,assignedGenerationLayerGuid=coast.guid,
            Kit=kit,QuarterTiles=shoreSet,SkipMapBoundary=true,SurfaceMaterial=shoreline});

        town.mapBuildLayers.RemoveAll(l => l is EnvironmentSmartTileLayer);
        var roads = Blueprint(town, SmartEnvironmentMasks.Roads);
        roads.stack = new() { new("Street mask",new CoreTownLayerGenerator { LayerName = TownLayers.Roads }),
            new("Shop floor mask",new CoreTownLayerGenerator { LayerName = TownLayers.ShopFloor }) };
        var wall = Blueprint(town,SmartEnvironmentMasks.Walls);
        wall.stack = new() { new("Shop wall mask",new CoreTownLayerGenerator { LayerName = TownLayers.ShopWalls }) };
        Build(town,roads,"Road",0); Build(town,wall,"Wall",0);
        Build(town,town.mapBlueprintLayers.First(l=>l.layerName=="Houses"),"House",0);
        EditorUtility.SetDirty(world); EditorUtility.SetDirty(town);
        CreateRuleExamples(kit, world);

        void Build(TileWorldCreatorAsset asset, TileWorldCreatorAsset.BlueprintLayerData source, string family,float elevation)
        {
            asset.mapBuildLayers.Add(new EnvironmentSmartTileLayer { guid = Guid.NewGuid(), layerName = source.layerName,
                assignedGenerationLayerGuid = source.guid, Kit = kit, QuarterTiles = family == "Wall" ? null : presets[family],
                WallTiles = family == "Wall" ? walls : null, Road = family == "Road", Elevation = elevation,
                HeightScale = family == "House" ? 2 : 1, Buildings = family == "House" });
        }
    }
    private static Material SurfaceMaterial(string name,string shader)
    {
        string path="Assets/Art/EnvironmentKit/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) {mat=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(mat,path);}
        else mat.shader=Shader.Find(shader);
        return mat;
    }
    private static Material OceanMaterial(string name,Color color)
    {
        string path="Assets/Art/EnvironmentKit/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find("EternalEnigma/Animated Ocean");
        if(mat==null) {mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.shader=shader;
        mat.color=color;mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/EnvironmentKit/Textures/Ocean.png");
        mat.SetFloat("_WaveStrength",name=="Shore"?.55f:.35f);
        mat.SetVector("_WaveSpeed",name=="Shore"?new Vector4(.026f,.012f,-.018f,.018f):new Vector4(.018f,.007f,-.009f,.013f));
        EditorUtility.SetDirty(mat);return mat;
    }
    public const string ExamplePath = "Assets/Art/EnvironmentKit/SmartTiles/RuleExamples.asset";
    private static void CreateRuleExamples(EnvironmentKit kit, TileWorldCreatorAsset world)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(ExamplePath);
        if (asset == null) { asset = ScriptableObject.CreateInstance<TileWorldCreatorAsset>(); AssetDatabase.CreateAsset(asset, ExamplePath); }
        asset.worldName = "Smart rule examples"; asset.mapWidth = 32; asset.mapHeight = 24; asset.cellSize = 2;
        asset.mapOrientation = TileWorldCreatorAsset.MapOrientation.XY;
        asset.mapBlueprintLayers = new(); asset.mapBuildLayers = new();
        var host = new GameObject("Rule authoring"); var creator = host.AddComponent<TileWorldCreator>(); creator.twcAsset = asset;
        foreach (var original in world.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().Where(l=>l.SurfaceMaterial==null))
        {
            var source = Blueprint(asset,original.layerName);
            var paint = new Paint(); paint.FillMap(false,creator);
            bool[,] mask = new bool[32,24];
            void Rect(int x,int y,int w,int h) { for(int yy=y;yy<y+h;yy++) for(int xx=x;xx<x+w;xx++) mask[xx,yy]=true; }
            if (original.layerName.Contains("Mountain"))
            {
                Rect(1,1,10,9); Rect(8,7,4,5); mask[2,10]=true; mask[4,10]=true;
                int tier = original.layerName.EndsWith("2") ? 1 : original.layerName.EndsWith("3") ? 2 : 0;
                if(original.layerName==SmartEnvironmentMasks.Summits) tier=3;
                for(int t=0;t<tier;t++) mask = new Shrink().Execute(mask,creator);
                if(original.layerName==SmartEnvironmentMasks.Summits) mask=new MountainTopNoise().Execute(mask,creator);
            }
            else if (original.Road) { Rect(16,2,13,1); Rect(20,1,1,10); Rect(24,2,1,7); Rect(20,8,5,1); Rect(27,5,3,4); mask[29,10]=true; }
            else if (original.WallTiles != null) { Rect(16,15,13,1); Rect(16,15,1,7); Rect(28,15,1,7); Rect(16,21,13,1); Rect(22,13,1,10); mask[16,18]=false; mask[30,23]=true; }
            else { Rect(1,15,4,3); Rect(1,17,2,5); Rect(7,16,5,5); mask[9,18]=false; mask[5,23]=true; }
            for(int y=0;y<24;y++) for(int x=0;x<32;x++) if(mask[x,y]) paint.ModifyMap(x,y,true,creator);
            source.stack = new() { new("Editable rule examples",paint) };
            var build = (EnvironmentSmartTileLayer)original.Clone(); build.guid=Guid.NewGuid(); build.assignedGenerationLayerGuid=source.guid;
            asset.mapBuildLayers.Add(build);
        }
        UnityEngine.Object.DestroyImmediate(host); EditorUtility.SetDirty(asset);
    }
    private static TileWorldCreatorAsset.BlueprintLayerData Blueprint(TileWorldCreatorAsset asset,string name)
    {
        var layer = asset.mapBlueprintLayers.FirstOrDefault(l => l.layerName == name);
        if (layer == null) { layer = new TileWorldCreatorAsset.BlueprintLayerData(name,true); asset.mapBlueprintLayers.Add(layer); }
        return layer;
    }
    private static GameObject Piece(EnvironmentKit kit,string model,Material surface=null)
    {
        var root = new GameObject(model+" TWC");
        var child = kit.Create(model,OverworldBiome.Grassland,root.transform,Vector3.zero);
        if(surface!=null) child.GetComponent<MeshRenderer>().sharedMaterial=surface;
        child.transform.localRotation = Quaternion.Euler(90,0,0);
        string path = Root+"/"+model+".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root); return prefab;
    }
}
