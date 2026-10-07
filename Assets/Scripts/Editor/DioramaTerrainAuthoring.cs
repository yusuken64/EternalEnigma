using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using UnityEditor;
using UnityEngine;

public static class DioramaTerrainAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Integrate Terrain")]
    public static void Build()
    {
        var catalog=DioramaCatalog.Load();const string folder="Assets/Art/Diorama/Cliffs";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var materials=Enum.GetValues(typeof(OverworldBiome)).Cast<OverworldBiome>().Select(b=>{
            string path=folder+"/"+b+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("EternalEnigma/Diorama Cliff"));AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_RockTex",AssetDatabase.LoadAssetAtPath<Texture2D>(DioramaGroundAuthoring.Output+"/Mountain.png"));
            var surface=PaintedGroundStyle.Surface(b);if(surface==GroundSurface.Water)surface=GroundSurface.Sand;
            material.SetTexture("_TopTex",AssetDatabase.LoadAssetAtPath<Texture2D>(DioramaGroundAuthoring.Output+"/"+PaintedGroundStyle.TextureNames[(int)surface]+".png"));
            material.color=b==OverworldBiome.Desert?new Color(1.15f,.91f,.66f):b==OverworldBiome.Volcanic?new Color(.47f,.43f,.47f):b==OverworldBiome.Tundra?new Color(.85f,1,1.16f):Color.white;
            EditorUtility.SetDirty(material);return material;
        }).ToArray();
        var pieces=new GameObject[6];var names=new[]{"CliffSingle","CliffEnd","CliffStraight","CliffOuter","CliffTee","CliffFill"};
        for(int i=0;i<names.Length;i++)
        {
            var source=catalog.Get(names[i]).Prefab.GetComponent<MeshFilter>().sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);
            mesh.name=names[i];mesh.subMeshCount=1;mesh.SetTriangles(source.triangles,0);DioramaCliffGeometry.Paint(mesh);
            string path=folder+"/"+names[i]+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            var root=new GameObject(names[i]);var visual=new GameObject("Cliff mesh");visual.transform.SetParent(root.transform,false);visual.transform.localRotation=Quaternion.Euler(90,0,0);
            visual.AddComponent<MeshFilter>().sharedMesh=saved;visual.AddComponent<MeshRenderer>().sharedMaterial=materials[0];
            pieces[i]=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+names[i]+".prefab");UnityEngine.Object.DestroyImmediate(root);
            if(i==5)catalog.CliffGrid=saved;
        }
        string presetPath=folder+"/CliffSixTerrain.asset";var preset=AssetDatabase.LoadAssetAtPath<TileWorldCreator6TilesPreset>(presetPath);
        if(preset==null){preset=ScriptableObject.CreateInstance<TileWorldCreator6TilesPreset>();AssetDatabase.CreateAsset(preset,presetPath);}
        preset.singleTile=pieces[0];preset.deadEndTile=pieces[1];preset.straightTile=pieces[2];preset.cornerTile=pieces[3];preset.threeWayTile=pieces[4];preset.fourWayTile=pieces[5];EditorUtility.SetDirty(preset);
        var template=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Overworld/CampaignTerrain.asset");
        foreach(var layer in template.mapBuildLayers.OfType<EnvironmentSmartTileLayer>())
            if(layer.layerName.StartsWith("Smart/Mountains")||layer.layerName==SmartEnvironmentMasks.Summits)
            {layer.WallTiles=preset;layer.QuarterTiles=null;layer.SurfaceMaterial=null;layer.HeightScale=1;layer.DioramaCliffs=true;}
        var walls=template.mapBuildLayers.OfType<OverworldTreeWallLayer>().FirstOrDefault();
        if(walls==null){walls=new OverworldTreeWallLayer {guid=new Guid("e731be52-e434-496a-8f26-5f4a9c9caca2"),layerName="Cosmetic/Tree Walls",active=true};template.mapBuildLayers.Add(walls);}
        var trees=template.mapBlueprintLayers.FirstOrDefault(l=>l.layerName==OverworldLayers.Trees);
        if(trees==null){trees=new TileWorldCreatorAsset.BlueprintLayerData(OverworldLayers.Trees,true);template.mapBlueprintLayers.Add(trees);}
        walls.Kit=EnvironmentKit.Load();walls.assignedGenerationLayerGuid=trees.guid;
        catalog.Cliffs=materials;catalog.TreeWalls=true;
        var shore=EnvironmentKit.Load().Shore;
        shore.SetColor("_SandColor",new Color(.83f,.77f,.56f));shore.SetColor("_ShallowColor",new Color(.19f,.78f,.80f));shore.SetColor("_FoamColor",new Color(.91f,1,1));
        shore.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(DioramaGroundAuthoring.Output+"/Water.png");EditorUtility.SetDirty(shore);
        EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(template);AssetDatabase.SaveAssets();
        Debug.Log("Diorama terrain: six cliff prefabs, concave adapters, rock summits, cyan shore and budgeted tree walls.");
    }
}
