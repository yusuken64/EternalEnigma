using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Explicit, repeatable art import. Never runs automatically on project load.
public static class InstallStoneStairs
{
    private const string Folder = "Assets/Art/EternalEnigma/Props/Stairs/";
    private const string PrefabPath = "Assets/Prefabs/Dungeon/Interactables/Stairs.prefab";

    [MenuItem("Tools/Eternal Enigma/Art/Install Stone Stairs")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing art.");
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "EE_Prop_StoneStairs.fbx");
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.isReadable = false;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + "EE_Stairs_Palette.png");
        textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        textureImporter.mipmapEnabled = false;
        textureImporter.filterMode = FilterMode.Point;
        textureImporter.wrapMode = TextureWrapMode.Clamp;
        textureImporter.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "EE_Stairs_Stone.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, Folder + "EE_Stairs_Stone.mat");
        }
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "EE_Stairs_Palette.png");
        material.color = Color.white;
        material.SetFloat("_Glossiness", 0.13f);
        material.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(material);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "EE_Prop_StoneStairs.fbx");
        if (model == null) throw new InvalidOperationException("Stair FBX did not import.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<Stairs>() == null) throw new InvalidOperationException("Missing stair interaction.");
            var wrapper = root.transform.GetChild(0);
            var previous = wrapper.Find("Stone Stair Model");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            // Keep the existing three collider objects and their exact transforms.
            foreach (var renderer in wrapper.GetComponentsInChildren<MeshRenderer>()) UnityEngine.Object.DestroyImmediate(renderer);
            foreach (var filter in wrapper.GetComponentsInChildren<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
            instance.name = "Stone Stair Model";
            instance.transform.SetParent(wrapper, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            instance.transform.localScale = Vector3.one;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var meshes = instance.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
            var triangles = meshes.Sum(m => (int)m.GetIndexCount(0) / 3);
            if (renderers.Length != 1 || meshes.Any(m => m.subMeshCount != 1) || triangles > 1000)
                throw new InvalidOperationException("Unexpected mesh/material budget.");
            if (bounds.size.x > 2 || bounds.size.y > 2 || bounds.size.z > 1.5f || bounds.size.x < 1)
                throw new InvalidOperationException("FBX scale/axis validation failed: " + bounds);
            if (Mathf.Abs(bounds.max.z - 0.2f) > 0.02f)
                throw new InvalidOperationException("Stairs are not grounded towards negative Z: " + bounds);
            if (root.GetComponentsInChildren<BoxCollider>().Length != 3)
                throw new InvalidOperationException("Original collider setup changed.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("ArtSource/Stairs");
            File.WriteAllText("ArtSource/Stairs/unity-validation.json", JsonUtility.ToJson(new Report
            {
                triangles = triangles, renderers = renderers.Length, submeshes = meshes.Sum(m => m.subMeshCount),
                colliders = 3, boundsCenter = bounds.center, boundsSize = bounds.size,
                prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath),
                shader = material.shader.name, texture = AssetDatabase.GetAssetPath(material.mainTexture)
            }, true));
            Debug.Log("Stone stairs installed and validated: " + triangles + " triangles; " + bounds);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Serializable]
    private class Report
    {
        public int triangles, renderers, submeshes, colliders;
        public Vector3 boundsCenter, boundsSize;
        public string prefabGuid, shader, texture;
    }
}
