using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DioramaVegetationAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Integrate Vegetation")]
    public static void Build()
    {
        var catalog=DioramaCatalog.Load();if(catalog==null)throw new InvalidOperationException("Import the Blender kit first.");
        string folder="Assets/Art/Diorama/Production";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var report=JsonUtility.FromJson<DioramaFitAuthoring.Report>(File.ReadAllText(DioramaFitAuthoring.Evidence+"/Measurements.json"));
        var names=new[]{"Tree01","Tree02","Tree03","Tree04","Tree05","Grass01","Grass03","Grass04","Flower01","Flower02","Flower03","Flower04","Flower05","Rock01","Rock05"};
        var models=catalog.Models.Where(m=>!m.Id.StartsWith("Pack")).ToList();var readable=new SortedSet<string>(StringComparer.Ordinal);
        foreach(string name in names)
        {
            var record=report.environment.First(r=>Path.GetFileNameWithoutExtension(r.path)==name);
            foreach(string path in record.meshes.Select(m=>m.path).Distinct())
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)throw new InvalidOperationException("Expected source FBX: "+path);
                if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
                readable.Add(path);
            }
            var fit=DioramaFitAuthoring.Adapt(record.path);
            var obj=PrefabUtility.LoadPrefabContents(fit);
            try
            {
                // Normalize the wrapper pivot to visible base centre, retaining every source mesh.
                obj.transform.GetChild(0).localPosition=new Vector3(-record.center.x,-record.center.z,record.center.y-record.size.y*.5f);
                var tag=obj.GetComponent<DioramaModelTag>()??obj.AddComponent<DioramaModelTag>();tag.Id="Pack"+name;
                if(name.StartsWith("Tree")||name.StartsWith("Grass")||name.StartsWith("Flower"))
                    foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(Foliage).ToArray();
                var prefab=PrefabUtility.SaveAsPrefabAsset(obj,folder+"/"+name+".prefab");
                float target=name=="Tree01"||name=="Tree02"?DioramaScale.Pine:name=="Tree05"?DioramaScale.HeroHeight*2.2f:
                    name.StartsWith("Tree")?DioramaScale.Broadleaf:name.StartsWith("Grass")?DioramaScale.HeroHeight*.27f:
                    name.StartsWith("Flower")?DioramaScale.HeroHeight*.23f:name=="Rock01"?DioramaScale.HeroHeight*.7f:DioramaScale.HeroHeight*.95f;
                models.Add(new DioramaModel {Id="Pack"+name,Prefab=prefab,Height=record.size.y,TargetHeight=target,
                    Width=Mathf.Max(record.size.x,record.size.z),Triangles=record.lod0Triangles,Lod=0,Tree=name.StartsWith("Tree")});
            }
            finally {PrefabUtility.UnloadPrefabContents(obj);}
        }
        catalog.Models=models.ToArray();EditorUtility.SetDirty(catalog);
        var picker=EnvironmentKit.Load().TreeModels;
        var choices=new List<TreeModelChoice>();
        Add("PackTree01",2,OverworldBiome.Grassland,OverworldBiome.Forest,OverworldBiome.Mountain);
        Add("PackTree02",1,OverworldBiome.Mountain);
        Add("PackTree03",4,OverworldBiome.Grassland,OverworldBiome.Forest);
        Add("PackTree04",2,OverworldBiome.Grassland,OverworldBiome.Forest);
        Add("PackTree05",3,OverworldBiome.Desert);
        Add("Palm",2,OverworldBiome.Desert,OverworldBiome.Water);
        Add("Willow",3,OverworldBiome.Marsh,OverworldBiome.Water);
        Add("SnowPine",4,OverworldBiome.Tundra);
        Add("DeadTree",1,OverworldBiome.Tundra,OverworldBiome.Marsh,OverworldBiome.Volcanic);
        Add("CharredTree",4,OverworldBiome.Volcanic);
        picker.Models=choices.ToArray();EditorUtility.SetDirty(picker);AssetDatabase.SaveAssets();
        File.WriteAllText("Docs/Art/Previews/Diorama/Fit/SelectedReadableFBXs.txt",
            "Selected for TWC EnvironmentBatch. Only ModelImporter.isReadable was changed.\nLOD0: town/cosmetics; LOD1: dense tree walls. No mesh/prefab/material vendor edits.\n"+string.Join("\n",readable));
        Debug.Log("Diorama vegetation: "+names.Length+" original-mesh adapters; selected FBX Read/Write list saved.");
        void Add(string id,int weight,params OverworldBiome[] biomes)=>choices.Add(new TreeModelChoice {Prefab=catalog.Get(id).Prefab,Weight=weight,Biomes=biomes});
    }
    static Material Foliage(Material source)
    {
        if(source==null)return null;
        string path="Assets/Art/Diorama/Materials/"+source.name+"_Foliage.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
        material.shader=Shader.Find("EternalEnigma/Diorama Foliage");material.SetFloat("_Wind",.015f);
        material.SetFloat("_Glossiness",.08f);EditorUtility.SetDirty(material);return material;
    }
}
