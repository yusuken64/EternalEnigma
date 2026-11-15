using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports Blender-authored lock pieces using the game's existing palette and mesh pipeline.</summary>
public static class LockModelAuthoring
{
    private const string Folder = "Assets/Art/Diorama/Locks";
    [Serializable] public sealed class Model { public string id; public DioramaMeshAuthoring.Source[] pieces; }
    [Serializable] public sealed class Source { public Model[] models; }

    [MenuItem("Tools/Eternal Enigma/Locks/Import Blender Lock Models")]
    public static void Import()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Import lock assets in Edit Mode.");
        try
        {
            using var compressed = File.OpenRead("ArtSource/Locks/Meshes.json.gz");
            using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed);
            var source = JsonUtility.FromJson<Source>(reader.ReadToEnd());
            var catalog = DioramaCatalog.Load();
            if (catalog == null || source?.models == null) throw new InvalidDataException("Missing diorama catalog or Blender lock export.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var models = new List<DioramaModel>();
            foreach (var data in source.models)
            {
                var root = new GameObject(data.id);
                try
                {
                    foreach (var piece in data.pieces)
                    {
                        var mesh = new Mesh { name = data.id + " " + piece.id, vertices = piece.vertices, normals = piece.normals,
                            colors = piece.colors, uv = piece.uv, subMeshCount = piece.parts.Length };
                        for (int i = 0; i < piece.parts.Length; i++) mesh.SetTriangles(piece.parts[i].triangles, i);
                        mesh.RecalculateBounds();
                        string path = Folder + "/" + data.id + "_" + piece.id.Replace(' ', '_') + ".asset";
                        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (saved == null) { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                        else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                        var child = new GameObject(piece.id); child.transform.SetParent(root.transform, false);
                        child.AddComponent<MeshFilter>().sharedMesh = saved;
                        child.AddComponent<MeshRenderer>().sharedMaterials = piece.parts.Select(p => catalog.Material(p.role, OverworldBiome.Grassland)).ToArray();
                        if (piece.id == "Side frame") child.SetActive(false);
                    }
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + data.id + ".prefab");
                    var bounds = root.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a,b) => { a.Encapsulate(b); return a; });
                    models.Add(new DioramaModel { Id = data.id, Prefab = prefab, Height = bounds.size.z,
                        Width = Mathf.Max(bounds.size.x, bounds.size.y), Triangles = data.pieces.Sum(p => p.parts.Sum(part => part.triangles.Length / 3)) });
                }
                finally { Object.DestroyImmediate(root); }
            }
            // Reuse existing animal and cargo art without instantiating their gameplay components.
            foreach (string id in new[] { "Dog", "Crate", "Barrel" })
            {
                var prefab = Resources.Load<GameObject>("TownInteriors/" + id);
                if (prefab == null) throw new InvalidDataException("Missing reused town prop: " + id);
                if (id == "Dog") prefab = BakeAnimal(prefab);
                models.Add(new DioramaModel { Id = "Lock" + id, Prefab = prefab });
            }
            catalog.Models = catalog.Models.Where(m => !models.Any(n => n.Id == m.Id)).Concat(models).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log($"Imported {source.models.Length} Blender lock designs and reused 3 existing game props. Frames and closures are separate.");
        }
        catch (Exception error) { Debug.LogError("Lock model import failed: " + error); throw; }
    }

    private static GameObject BakeAnimal(GameObject source)
    {
        var animal = Object.Instantiate(source);
        var root = new GameObject("LockDog");
        try
        {
            int index = 0;
            foreach (var skin in animal.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh { name = "Lock dog pose " + index };
                skin.BakeMesh(mesh);
                var matrix = animal.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                mesh.normals = mesh.normals.Select(normal => matrix.inverse.transpose.MultiplyVector(normal).normalized).ToArray();
                mesh.RecalculateBounds();
                string path = Folder + "/LockDog_" + index++ + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                var child = new GameObject("Reused dog mesh"); child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = saved;
                child.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            }
            if (index == 0) throw new InvalidDataException("The reused dog has no skinned mesh to bake.");
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/LockDog.prefab");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(animal); }
    }
}
