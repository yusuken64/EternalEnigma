using System;
using System.IO;
using System.Linq;
using System.Text;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Capabilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repeatable, labelled contact sheets of the production lock geometry at a fixed scale.</summary>
public static class LockVisualAudit
{
    public const string Folder = "Temp/LockTestLab/VisualAudit";

    [MenuItem("Tools/Eternal Enigma/Locks/Capture Model Contact Sheets")]
    public static void Capture()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D tile = null, sheet = null;
        Camera camera = null;
        var previous = RenderTexture.active;
        try
        {
            Directory.CreateDirectory(Folder);
            var gallery = new LockTestWorld();
            var cameraObject = new GameObject("Lock audit camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 1.65f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .19f, .23f);
            camera.transform.position = new Vector3(0, -5.5f, -8);
            camera.transform.LookAt(new Vector3(0, 0, -.6f), Vector3.up);
            camera.nearClipPlane = .1f; camera.farClipPlane = 30;
            var lightObject = new GameObject("Lock audit light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(-40, -25, 0);
            var label = new GameObject("Lock audit label").AddComponent<TextMesh>();
            label.transform.SetParent(camera.transform, false);
            label.transform.localPosition = new Vector3(0, -1.05f, 5);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 36; label.characterSize = .075f; label.anchor = TextAnchor.UpperCenter;
            label.alignment = TextAlignment.Center; label.color = Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            const int pixels = 320, columns = 6, rows = 6, perPage = columns * rows;
            target = new RenderTexture(pixels, pixels, 24);
            camera.targetTexture = target;
            tile = new Texture2D(pixels, pixels, TextureFormat.RGB24, false);
            sheet = new Texture2D(pixels * columns, pixels * rows, TextureFormat.RGB24, false);
            var report = new StringBuilder("Station\tSkin\tModel\tMeshes\tBounds\n");
            for (int i = 0; i < gallery.Stations.Count; i++)
            {
                if (i % perPage == 0) sheet.SetPixels(Enumerable.Repeat(camera.backgroundColor, sheet.width * sheet.height).ToArray());
                var station = gallery.Stations[i];
                var root = new GameObject("Audit " + station.Route.Id);
                SceneManager.MoveGameObjectToScene(root, scene);
                try
                {
                    var cell = station.Approach;
                    var biome = OverworldCosmetics.Biome(gallery.Grid, cell.X, cell.Y);
                    if (!station.Route.IsWarp) root.transform.rotation = OverworldLockVisual.GateRotation(gallery.Grid, gallery.Grid.Locks.Single(gate => gate.RouteId == station.Route.Id));
                    OverworldLockVisual.Build(root, station.Route, biome);
                    var meshes = root.GetComponentsInChildren<MeshFilter>();
                    if (meshes.Length == 0) throw new InvalidOperationException("Missing lock geometry: " + station.Route.Id);
                    var bounds = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).Select(r => r.bounds).Aggregate((a,b) => { a.Encapsulate(b); return a; });
                    report.AppendLine($"{i + 1}\t{station.Route.SkinId}\t{root.GetComponent<OverworldLockVisual>().ModelId}\t{meshes.Length}\t{bounds}");
                    var skin = LockSkinCatalog.Skins.FirstOrDefault(candidate => candidate.Id == station.Route.SkinId);
                    string title = skin?.Capability.DisplayName() ?? (station.Route.IsWarp ? "Waystone" : station.Route.KeyId != null ? "Keyed gate" : "Far-side shortcut");
                    string subtitle = (station.Horizontal ? "E-W | " : "N-S | ") + (station.Route.KeyLabel ?? station.Route.Form + " / " + station.Route.SkinId?.Split('.').Last());
                    label.text = $"{i + 1}. {title}\n{subtitle}";
                    int longest = label.text.Split('\n').Max(line => line.Length);
                    label.characterSize = Mathf.Min(.064f, 1.9f / Mathf.Max(1, longest));
                    label.font.RequestCharactersInTexture(label.text, label.fontSize);
                    camera.Render(); RenderTexture.active = target;
                    tile.ReadPixels(new Rect(0, 0, pixels, pixels), 0, 0); tile.Apply();
                    int offset = i % perPage;
                    sheet.SetPixels(offset % columns * pixels, (rows - 1 - offset / columns) * pixels, pixels, pixels, tile.GetPixels());
                    if (offset == perPage - 1 || i == gallery.Stations.Count - 1)
                    {
                        sheet.Apply(); File.WriteAllBytes($"{Folder}/models-{i / perPage + 1:00}.png", sheet.EncodeToPNG());
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            File.WriteAllText(Folder + "/models.tsv", report.ToString());
            Debug.Log($"Captured all {gallery.Stations.Count} lock models to {Folder}.");
        }
        catch (Exception error) { Debug.LogError("Lock model audit failed: " + error); throw; }
        finally
        {
            RenderTexture.active = previous;
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (tile != null) UnityEngine.Object.DestroyImmediate(tile);
            if (sheet != null) UnityEngine.Object.DestroyImmediate(sheet);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
