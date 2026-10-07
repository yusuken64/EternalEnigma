using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TWCSampleAudit
{
    [MenuItem("Tools/Eternal Enigma/Art/Audit TWC Cliff Samples")]
    public static void Run()
    {
        if(!Directory.Exists("Assets/TileWorldCreator/Demo")){Debug.Log("TWC demos were removed after migration. The retained report is Docs/Art/TWCSampleAudit.md.");return;}
        var report = new StringBuilder("# TWC sample inspection\n\nRead from the installed TileWorldCreator 3 package. Missing local demos were restored from the already-cached package, without replacing existing files. No separately named river demo is present in this package; CliffIsland provides water/sand/cliff transitions.\n");
        foreach (string path in new[] { "Assets/TileWorldCreator/Demo/06_CliffIsland/06_CliffIsland.unity", "Assets/TileWorldCreator/Demo/09_Ramps/09_RampsDemoA.unity", "Assets/TileWorldCreator/Demo/09_Ramps/09_RampsDemoB.unity", "Assets/TileWorldCreator/Demo/11_MixTilesets/11_MixTilesets.unity" })
        {
            if(!File.Exists(path))continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                report.AppendLine("\n## " + path);
                foreach (var creator in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TileWorldCreator>(true)))
                {
                    var asset = creator.twcAsset;
                    report.AppendLine($"\n{creator.name}: {AssetDatabase.GetAssetPath(asset)}, {asset.mapWidth}x{asset.mapHeight}, cell {asset.cellSize}, {asset.mapOrientation}");
                    foreach (var layer in asset.mapBlueprintLayers)
                    {
                        report.AppendLine($"\nBlueprint `{layer.layerName}` ({layer.guid}) active={layer.active}");
                        foreach (var action in layer.stack) report.AppendLine("- " + Describe(action.action));
                    }
                    foreach (var layer in asset.mapBuildLayers)
                    {
                        report.AppendLine("\nBuild: " + Describe(layer));
                        if (layer is InstantiateTiles tiles && tiles.tiles != null)
                            foreach (var entry in tiles.tiles) report.AppendLine("- Preset: " + AssetDatabase.GetAssetPath(entry.preset));
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        Directory.CreateDirectory("Docs/Art"); File.WriteAllText("Docs/Art/TWCSampleAudit.md", report.ToString());
        Debug.Log("TWC sample audit saved.");
    }
    private static string Describe(object obj) => obj.GetType().Name + ": " + string.Join(", ", obj.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public)
        .Where(f => f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string) || f.FieldType == typeof(Guid) || f.FieldType == typeof(Vector3))
        .Select(f => f.Name + "=" + f.GetValue(obj)));
}
