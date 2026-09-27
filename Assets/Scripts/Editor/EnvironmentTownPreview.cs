using System.IO;
using System.Linq;
using TWC;
using UnityEditor;
using UnityEngine;

/// <summary>Replace legacy serialized TWC clusters with a saved preview of the production smart layers.</summary>
public static class EnvironmentTownPreview
{
    public static void Bake(TileWorldCreator creator, TileWorldCreatorAsset template)
    {
        // Only the generator's output is replaced; scene controls, camera, and gameplay objects remain.
        var old=creator.worldObject;
        Object.DestroyImmediate(old);creator.worldObject=null;
        var working=Object.Instantiate(template);working.hideFlags=HideFlags.DontSave;
        creator.twcAsset=working;
        try
        {
            CoreTownLayerGenerator.Configure(working,TownSceneLoader.Default);
            working.useRandomSeed=true;working.randomSeed=42;
            CoreLayoutCache.Clear(creator);creator.ExecuteAllBlueprintLayers();
            CoreLayoutCache.ClearResultFlags(working);creator.ExecuteAllBuildLayers(true);
            const string folder="Assets/Art/EnvironmentKit/TownPreview";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            int index=0;
            foreach(var filter in creator.worldObject.GetComponentsInChildren<MeshFilter>())
            {
                var source=filter.sharedMesh;
                string path=$"{folder}/Chunk{index++:000}.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null) {saved=Object.Instantiate(source);AssetDatabase.CreateAsset(saved,path);}
                else EditorUtility.CopySerialized(source,saved);
                EditorUtility.SetDirty(saved);filter.sharedMesh=saved;
            }
            // Saved preview meshes are project assets; generated runtime meshes retain normal ownership.
            foreach(var owner in creator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>()) Object.DestroyImmediate(owner);
            AssetDatabase.SaveAssets();
        }
        finally {creator.twcAsset=template;Object.DestroyImmediate(working);CoreLayoutCache.Clear(creator);}
    }
}
