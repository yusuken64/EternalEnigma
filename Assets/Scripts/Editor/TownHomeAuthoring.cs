using UnityEditor;
using UnityEngine;

/// <summary>Authored home surfaces keep their GUIDs when the opening presentation is rebuilt.</summary>
public static class TownHomeAuthoring
{
    private const string Folder = "Assets/Resources/TownInteriors";

    [MenuItem("Tools/Eternal Enigma/Art/Build Opening Home Materials")]
    public static void Build()
    {
        var catalog = TownInteriorCatalog.Load();
        if (catalog == null) throw new System.InvalidOperationException("Import town interiors before building home materials.");
        T Save<T>(T value, string name) where T : Object
        {
            string path = Folder + "/" + name;
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing); return existing;
        }
        var surface = new Mesh { name = "Home surface" };
        surface.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        surface.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        surface.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        surface.triangles = new[] { 0, 2, 1, 0, 3, 2 }; surface.RecalculateBounds();
        catalog.HomeSurface = Save(surface, "HomeSurface.asset");
        var surfaceShader = Shader.Find("Eternal Enigma/Home Surface");
        var furnitureShader = Shader.Find("Eternal Enigma/Home Furniture");
        if (surfaceShader == null || furnitureShader == null) throw new System.InvalidOperationException("Home shaders have not imported.");
        var wood = new Material(surfaceShader) { name = "Home warm timber" };
        wood.SetColor("_Color", new Color32(120, 83, 62, 255)); wood.SetFloat("_Woven", 0);
        catalog.HomeWood = Save(wood, "HomeWood.mat");
        var rug = new Material(surfaceShader) { name = "Home terracotta woven rug" };
        rug.SetColor("_Color", new Color32(149, 100, 81, 255));
        rug.SetColor("_Trim", new Color32(206, 184, 143, 255)); rug.SetFloat("_Woven", 1);
        catalog.HomeRug = Save(rug, "HomeRug.mat");
        var furniture = new Material(furnitureShader) { name = "Home quiet furniture", mainTexture = catalog.BiomeMaterials[0].mainTexture };
        furniture.SetColor("_Fabric", new Color32(122, 128, 101, 255));
        catalog.HomeFurniture = Save(furniture, "HomeFurniture.mat");
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        Debug.Log("Opening home: warm timber, terracotta woven rug with cream border, muted sage bed linen.");
    }
}
