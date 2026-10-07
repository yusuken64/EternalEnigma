using System;
using System.IO;
using System.Linq;
using TWC;
using UnityEditor;
using UnityEngine;

public static class DioramaGroundAuthoring
{
    public const string Output="Assets/Art/Diorama/Ground";
    [MenuItem("Tools/Eternal Enigma/Diorama/Build Ground Style")]
    public static void Build()
    {
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        foreach(string name in PaintedGroundStyle.TextureNames.Concat(new[]{"Water"}))
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                source.LoadImage(File.ReadAllBytes("ArtSource/PaintedEnvironment/Diorama/"+name+".png"));
                PaintedEnvironmentAuthoring.WriteSurface(source,new Rect(0,0,1,1),Output+"/"+name+".png",Color.white);
            }
            finally {UnityEngine.Object.DestroyImmediate(source);}
        }
        string stylePath="Assets/Resources/EnvironmentKit/DioramaGround.asset";
        var style=AssetDatabase.LoadAssetAtPath<PaintedGroundStyle>(stylePath);
        if(style==null) {style=ScriptableObject.CreateInstance<PaintedGroundStyle>();AssetDatabase.CreateAsset(style,stylePath);}
        style.DryGround=Material("Ground","EternalEnigma/Painted Ground");
        foreach(string name in PaintedGroundStyle.TextureNames) style.DryGround.SetTexture("_"+name,Texture(name));
        style.DryGround.SetFloat("_TileSize",4);
        style.Water=Material("Water","EternalEnigma/Animated Ocean");style.Water.mainTexture=Texture("Water");style.Water.color=Color.white;
        style.Bridge=Material("Bridge","Standard");
        style.Bridge.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PaintedEnvironment/Bridge.png");
        style.Bridge.color=new Color(.88f,.77f,.61f);style.Bridge.SetFloat("_Glossiness",.08f);
        var template=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Overworld/CampaignTerrain.asset");
        foreach(var input in OverworldGroundLayer.DefaultInputs())
            if(!template.mapBlueprintLayers.Any(l=>l.layerName==input.BlueprintLayer))
                template.mapBlueprintLayers.Add(new TileWorldCreatorAsset.BlueprintLayerData(input.BlueprintLayer,true));
        var ground=template.mapBuildLayers.OfType<OverworldGroundLayer>().FirstOrDefault();
        if(ground==null)
        {
            ground=new OverworldGroundLayer {guid=new Guid("e731be52-e434-496a-8f26-5f4a9c9caca1"),layerName="Painted Ground",active=true};
            template.mapBuildLayers.Insert(0,ground);
        }
        ground.assignedGenerationLayerGuid=template.mapBlueprintLayers.First(l=>l.layerName==EternalEnigma.Core.World.OverworldLayers.Roads).guid;
        ground.Style=style;ground.Inputs=OverworldGroundLayer.DefaultInputs();
        foreach(var ocean in template.mapBuildLayers.OfType<OverworldOceanLayer>().Where(l=>!l.NoiseOverlay)) ocean.Water=style.Water;
        EditorUtility.SetDirty(template);EditorUtility.SetDirty(style);
        foreach(var m in new[]{style.DryGround,style.Water,style.Bridge})EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        Debug.Log("Diorama ground: ten seamless textures, nine dry channels, explicit water/bridges, TWC bindings saved.");
    }
    static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/"+name+".png");
    static Material Material(string name,string shader)
    {
        string path=Output+"/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(value==null){value=new Material(Shader.Find(shader)){name="Diorama "+name};AssetDatabase.CreateAsset(value,path);}
        else value.shader=Shader.Find(shader);
        return value;
    }
}
