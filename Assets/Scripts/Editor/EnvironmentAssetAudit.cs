using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using TWC;
using TWC.Actions;

public static class EnvironmentAssetAudit
{
    [MenuItem("Tools/Eternal Enigma/Art/Audit Environment")]
    public static void Run()
    {
        Directory.CreateDirectory("Docs/Art");
        var report = new StringBuilder("# Environment asset audit\n\nMeasured in Unity before replacing art. Triangle counts are imported mesh index counts for active prefab renderers, including repeated instances; renderer/material slots are not GPU timings.\n\n");
        report.AppendLine("| Asset | Triangles | Vertices | Renderers / slots | Colliders | Bounds |\n|---|---:|---:|---:|---:|---|");
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Overworld", "Assets/Prefabs/Town", "Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("/Allies/") && !p.Contains("DungeonTier") && !p.Contains("SkillGrid")).OrderBy(p => p);
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var filters = prefab.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null && m.GetComponent<Renderer>()?.enabled == true).ToArray();
            if (filters.Length == 0) continue;
            long triangles = filters.Sum(f => Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(i => (long)f.sharedMesh.GetIndexCount(i) / 3));
            var renders = filters.Select(f=>f.GetComponent<Renderer>()).ToArray();
            var bounds = renders[0].bounds; foreach(var r in renders) bounds.Encapsulate(r.bounds);
            report.AppendLine($"| {path} | {triangles} | {filters.Sum(f=>f.sharedMesh.vertexCount)} | {renders.Length} / {renders.Sum(r=>r.sharedMaterials.Length)} | {prefab.GetComponentsInChildren<Collider>().Length} | {bounds.size} |");
        }
        report.AppendLine("\n## TWC authoring\n");
        foreach(string path in new[]{"Assets/Overworld/CampaignTerrain.asset","Assets/TileWorldCreator/VillageLSystemAsset.asset"})
        {
            var asset=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(path);
            report.AppendLine($"\n### {path}\n\nMap {asset.mapWidth} x {asset.mapHeight}; cell {asset.cellSize}.\n");
            foreach(var layer in asset.mapBlueprintLayers) report.AppendLine($"- Blueprint `{layer.layerName}`: {string.Join(", ",layer.stack.Select(s=>s.action.GetType().Name))}");
            foreach(var layer in asset.mapBuildLayers) report.AppendLine($"- Build `{layer.layerName}`: {layer.GetType().Name}; active={layer.active}; source={asset.GetBlueprintLayerData(layer.assignedGenerationLayerGuid)?.layerName}");
        }
        report.AppendLine("\n## Material and texture specifications\n\n| Material | Shader | Texture size |\n|---|---|---|");
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Overworld"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var tex=mat.mainTexture;
            report.AppendLine($"| {path} | {mat.shader.name} | {(tex!=null ? tex.width+" x "+tex.height : "none")} |");
        }
        report.AppendLine("\n## Current runtime geometry\n\nOverworldBiomeRenderer emits 2 triangles per floor cell, 4 per raised mountain/tree cell, in 32-cell chunks. It currently disables all TWC renderers, so cosmetic layers require a visibility exception. Overworld uses XY ground, negative Z elevation, 2-unit cells. Town roads currently use the modern road kit. Terrain navigation is owned by core masks; all new decorations must be collider-free and excluded from road, bridge, town, marker, gate and warp clearances.\n");
        File.WriteAllText("Docs/Art/EnvironmentAudit.md", report.ToString());
        Debug.Log("Environment audit written to Docs/Art/EnvironmentAudit.md");
    }
}
