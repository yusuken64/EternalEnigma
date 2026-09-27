using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DungeonPropModelImporter
{
    [MenuItem("Tools/Eternal Enigma/Art/Import Dungeon Props")]
    public static void Import()
    {
        const string folder = "Assets/Resources/DungeonProps";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var colors = new[] { new Color(.24f,.105f,.045f), new Color(.46f,.25f,.10f),
            new Color(.14f,.19f,.22f), new Color(.74f,.52f,.20f), new Color(.33f,.38f,.40f),
            new Color(.23f,.76f,.83f), new Color(.18f,.43f,.12f), new Color(.72f,.20f,.10f) };
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Palette.asset");
        if (texture == null) { texture = new Texture2D(8, 1); AssetDatabase.CreateAsset(texture, folder + "/Palette.asset"); }
        texture.SetPixels(colors); texture.filterMode = FilterMode.Point; texture.Apply(); EditorUtility.SetDirty(texture);
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Palette.mat");
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, folder + "/Palette.mat"); }
        material.mainTexture = texture; material.SetFloat("_Glossiness", .18f); EditorUtility.SetDirty(material);
        foreach (string path in Directory.GetFiles("Assets/Art/EternalEnigma/Props/Dungeon", "*.fbx"))
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = true; importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instances = new List<CombineInstance>();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    instances.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub,
                        transform = Matrix4x4.Rotate(Quaternion.Euler(-90, 0, 0)) * filter.transform.localToWorldMatrix });
            string id = Path.GetFileNameWithoutExtension(path);
            var mesh = new Mesh { name = id, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.CombineMeshes(instances.ToArray(), true, true);
            mesh.RecalculateBounds();
            if (mesh.bounds.size.z < .05f || mesh.bounds.max.z > .05f)
                throw new InvalidOperationException("Invalid dungeon prop orientation: " + id + " " + mesh.bounds);
            string target = folder + "/" + id + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(target);
            if (saved == null) AssetDatabase.CreateAsset(mesh, target);
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
        }
        AssetDatabase.SaveAssets();
        ReplaceTrap("DamageTrap", "SpikeTrap");
        ReplaceTrap("BumpTrap", "BumpTrap");
        AssetDatabase.SaveAssets();
        Debug.Log("Imported Blender dungeon props and replaced trap prefab visuals.");
    }

    static void ReplaceTrap(string prefabName, string model)
    {
        string path = "Assets/Prefabs/Dungeon/Traps/" + prefabName + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var trap = root.GetComponent<Trap>();
            bool revealed = trap.VisualObject != null && trap.VisualObject.activeSelf;
            if (trap.VisualObject != null) UnityEngine.Object.DestroyImmediate(trap.VisualObject);
            // Production dungeon cells are 2.5 units, matching the authored trap center.
            trap.VisualObject = DungeonPropModels.Create(model, root.transform, 2.5f);
            trap.VisualObject.SetActive(revealed);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
