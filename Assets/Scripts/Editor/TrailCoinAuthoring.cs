using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TrailCoinAuthoring
{
    public const string PrefabPath = "Assets/Resources/DungeonThemes/TrailCoin.prefab";

    [MenuItem("Tools/Eternal Enigma/Classes/Author Find Exit Trail Coin")]
    public static void Author()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(DioramaItemAuthoring.Atlas + "coin.prefab");
        var material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Art/Diorama/Items/2a44ae95e37a2ce499809a354abe1139_FFFFFF.mat");
        if (source == null || material == null) throw new InvalidOperationException("Missing existing coin art or dungeon item material.");

        var root = new GameObject("Find Exit trail coin");
        try
        {
            root.AddComponent<TrailCoin>().DroppedItemVisual = DroppedItemVisual.Gold;
            var pose = new GameObject("Floor pose").transform;
            pose.SetParent(root.transform, false);
            var model = Object.Instantiate(source, pose);
            model.name = "Coin";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();

            // A small, tilted coin reads from the gameplay camera and fits beside the stairs.
            pose.localRotation = Quaternion.Euler(-45, 0, 0);
            var bounds = DungeonPickupAuthoring.VisualBounds(root);
            pose.localScale = Vector3.one * (.5f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z));
            bounds = DungeonPickupAuthoring.VisualBounds(root);
            var points = DungeonPickupAuthoring.Points(root);
            pose.position += new Vector3(1 - bounds.center.x, 1 - bounds.center.y, -points.Max(point => point.z));
            points = DungeonPickupAuthoring.Points(root);
            pose.gameObject.AddComponent<DungeonPickupFootprint>().SupportPoint =
                pose.InverseTransformPoint(points.OrderByDescending(point => point.z).First());
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Authored Find Exit's single-coin dungeon pickup.");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
