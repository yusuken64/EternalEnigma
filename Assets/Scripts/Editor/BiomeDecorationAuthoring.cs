using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEngine;

public static class BiomeDecorationAuthoring
{
    const string Folder="Assets/Resources/BiomeDecorations";
    [MenuItem("Tools/Eternal Enigma/Art/Import Biome Decorations")]
    public static void Import()
    {
        if (!Application.dataPath.Replace('\\','/').EndsWith("/EternalEnigma/Assets",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Wrong Unity project: "+Application.dataPath);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var colors=new[] {new Color(.13f,.16f,.19f),new Color(.38f,.20f,.085f),new Color(.78f,.52f,.18f),new Color(.98f,.72f,.25f),new Color(.19f,.42f,.15f),new Color(.67f,.70f,.64f),new Color(.22f,.65f,.73f),new Color(.61f,.19f,.14f),new Color(.36f,.22f,.47f),new Color(.67f,.84f,.94f),new Color(.11f,.10f,.14f),new Color(.98f,.28f,.055f)};
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Palette.asset");
        if(texture==null) {texture=new Texture2D(colors.Length,1);AssetDatabase.CreateAsset(texture,Folder+"/Palette.asset");}
        texture.SetPixels(colors);texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;texture.Apply();EditorUtility.SetDirty(texture);
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Palette.mat");
        if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,Folder+"/Palette.mat");}
        material.mainTexture=texture;material.SetFloat("_Glossiness",.12f);EditorUtility.SetDirty(material);
        var catalog=AssetDatabase.LoadAssetAtPath<BiomeDecorationCatalog>(Folder+"/Catalog.asset");
        if(catalog==null){catalog=ScriptableObject.CreateInstance<BiomeDecorationCatalog>();AssetDatabase.CreateAsset(catalog,Folder+"/Catalog.asset");}
        var assets=new List<BiomeDecorationAsset>();
        foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
        foreach(BiomeDecorationKind kind in Enum.GetValues(typeof(BiomeDecorationKind))) {
            string id=biome+"_"+kind,path="Assets/Art/EternalEnigma/BiomeDecorations/"+id+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;importer.importAnimation=false;
            importer.animationType=ModelImporterAnimationType.None;importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.addCollider=false;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var combines=new List<CombineInstance>();
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>()) for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                combines.Add(new CombineInstance {mesh=filter.sharedMesh,subMeshIndex=sub,transform=Matrix4x4.Rotate(Quaternion.Euler(-90,0,0))*filter.transform.localToWorldMatrix});
            var mesh=new Mesh {name=id};mesh.CombineMeshes(combines.ToArray(),true,true);mesh.RecalculateBounds();
            int budget=kind==BiomeDecorationKind.LampPost || kind==BiomeDecorationKind.SignPost ? 240 : 120;
            if(mesh.triangles.Length/3>budget || kind!=BiomeDecorationKind.SignPanel && mesh.bounds.size.z>.1f && mesh.bounds.center.z>0)throw new InvalidOperationException("Invalid model "+id+" "+mesh.bounds);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+id+".asset");
            if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,Folder+"/"+id+".asset");}
            else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            var obj=new GameObject(id);obj.AddComponent<MeshFilter>().sharedMesh=saved;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            var prefab=PrefabUtility.SaveAsPrefabAsset(obj,Folder+"/"+id+".prefab");UnityEngine.Object.DestroyImmediate(obj);
            var entry=new BiomeDecorationAsset {Biome=biome,Kind=kind,Mesh=saved,Prefab=prefab,
                MountOffset=-new Vector3(saved.bounds.center.x,saved.bounds.max.y,saved.bounds.center.z),
                Surfaces=kind==BiomeDecorationKind.Accent?DecorationSurface.NaturalWall|DecorationSurface.BuiltWall|DecorationSurface.Facade:
                    kind==BiomeDecorationKind.Fixture||kind==BiomeDecorationKind.Ornament?DecorationSurface.BuiltWall|DecorationSurface.Facade:DecorationSurface.Verge};
            if(kind==BiomeDecorationKind.Fixture||kind==BiomeDecorationKind.LampPost) {
                entry.EffectOffset=new Vector3(0,-.24f,kind==BiomeDecorationKind.LampPost?-1.02f:-.22f);
                entry.Effect=CreateEffect(biome,colors[new[]{3,3,6,6,3,9,6,11}[(int)biome]]);
            }
            assets.Add(entry);
        }
        catalog.Material=material;catalog.Assets=assets.ToArray();catalog.Facades=Facades();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log("Biome decorations imported in "+Application.dataPath+": "+assets.Count+" bounded meshes. GUIDs preserved.");
    }
    static BiomeFacadeDefinition[] Facades()
    {
        // Coordinates audited against the editable EnvironmentKit.blend meshes (XY/-Z after import).
        var window=new Bounds(new Vector3(.14f,.52f,-.36f),new Vector3(.27f,.08f,.26f));
        var eaves=new Bounds(new Vector3(0,.5f,-.65f),new Vector3(1.1f,.15f,.15f));
        var fixture=new BiomeFacadeSocket{Center=new Vector3(-.20f,.505f,-.36f),Normal=Vector3.up,Width=.32f,Height=.34f,Kind=BiomeDecorationKind.Fixture};
        var ornament=new BiomeFacadeSocket{Center=new Vector3(.37f,.505f,-.36f),Normal=Vector3.up,Width=.16f,Height=.32f,Kind=BiomeDecorationKind.Ornament};
        return new[]{
            new BiomeFacadeDefinition {Model="SmartHouseEdge",Exclusions=new[]{window,eaves},Sockets=new[]{fixture,ornament}},
            new BiomeFacadeDefinition {Model="SmartHouseOuter",Exclusions=new[]{window,eaves},Sockets=new[]{fixture,new BiomeFacadeSocket{Center=new Vector3(.505f,-.12f,-.36f),Normal=Vector3.right,Width=.45f,Height=.34f,Kind=BiomeDecorationKind.Ornament}}},
            new BiomeFacadeDefinition {Model="House",Exclusions=new[]{new Bounds(new Vector3(.1f,-.4f,-.275f),new Vector3(.24f,.1f,.35f)),new Bounds(new Vector3(-.23f,-.4f,-.45f),new Vector3(.18f,.1f,.2f)),new Bounds(new Vector3(.29f,-.4f,-.45f),new Vector3(.18f,.1f,.2f)),new Bounds(new Vector3(0,0,-.85f),new Vector3(1.1f,1,.4f))},Sockets=new[]{new BiomeFacadeSocket{Center=new Vector3(-.21f,.36f,-.40f),Normal=Vector3.up,Width=.32f,Height=.34f,Kind=BiomeDecorationKind.Fixture},new BiomeFacadeSocket{Center=new Vector3(.21f,.36f,-.4f),Normal=Vector3.up,Width=.32f,Height=.34f,Kind=BiomeDecorationKind.Ornament}}}
        };
    }
    static GameObject CreateEffect(OverworldBiome biome,Color color)
    {
        string path=Folder+"/"+biome+"_Glow.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Unlit/Color"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;EditorUtility.SetDirty(mat);
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Glow.asset");
        if(mesh==null){mesh=new Mesh {name="Glow octahedron",vertices=new[]{Vector3.up,Vector3.right,Vector3.down,Vector3.left,Vector3.forward,Vector3.back},triangles=new[]{0,1,4,1,2,4,2,3,4,3,0,4,1,0,5,2,1,5,3,2,5,0,3,5}};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Glow.asset");}
        var obj=new GameObject(biome+" glow");obj.transform.localScale=Vector3.one*.075f;
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=mat;obj.AddComponent<BiomeDecorationEffect>();
        var prefab=PrefabUtility.SaveAsPrefabAsset(obj,Folder+"/"+biome+"_Glow.prefab");UnityEngine.Object.DestroyImmediate(obj);return prefab;
    }
}
