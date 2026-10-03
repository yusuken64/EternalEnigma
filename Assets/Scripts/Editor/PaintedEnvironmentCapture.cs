using System;
using System.IO;
using System.Linq;
using System.Text;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Fixed seed and camera protocol shared by before/after captures. Never saves scenes.
public static class PaintedEnvironmentCapture
{
    static EnvironmentPlayground playground;
    static SceneSetup[] setup;
    static string destination;
    static int index, settle;
    const int Last=53;
    static double deadline;
    static readonly StringBuilder stats = new StringBuilder();
    [MenuItem("Tools/Eternal Enigma/Painted Environment/Capture Before")]
    public static void Before() => Begin("Before");
    [MenuItem("Tools/Eternal Enigma/Painted Environment/Capture After")]
    public static void After() => Begin("After");
    [MenuItem("Tools/Eternal Enigma/Painted Environment/Resume Before Features")]
    public static void ResumeBeforeFeatures() => Begin("Before",50);
    static void Begin(string stage,int start=0)
    {
        if (EditorApplication.isPlaying || playground != null) throw new InvalidOperationException("Capture requires idle edit mode.");
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save your scene edits before capturing.");
        setup = EditorSceneManager.GetSceneManagerSetup();
        destination = "Docs/Art/Previews/PaintedEnvironment/" + stage;
        Directory.CreateDirectory(destination);
        EditorSceneManager.OpenScene("Assets/Scenes/EnvironmentPlayground.unity");
        playground = UnityEngine.Object.FindFirstObjectByType<EnvironmentPlayground>();
        playground.Seed = 12345;
        index = start;
        stats.Clear().AppendLine("view,renderers,materials,triangles,textureBytes,batches,setPassCalls,seed");
        if(start>0)stats.Clear().Append(string.Join("\n",File.ReadAllLines(destination+"/Stats.csv").Take(start+1))).AppendLine();
        try { Next(); EditorApplication.update += Tick; }
        catch { Finish(); throw; }
    }
    static void Next()
    {
        settle = 0; deadline = EditorApplication.timeSinceStartup + 120;
        if (index < 32)
        {
            var d = playground.DungeonExplorer;
            d.LegacyLayout = false; d.Biome = (OverworldBiome)(index / 4);
            d.Environment = (DungeonEnvironmentKind)((index / 2) % 2); d.Throne = index % 2 != 0;
            playground.ShowDungeon(); d.Rebuild();
        }
        else if (index < 40)
        { playground.TownBiome = (OverworldBiome)(index - 32); playground.ShowTown(); playground.Rebuild(); }
        else
        {
            int seed=12345;
            if((index>40&&index<49)||index==50)
            {
                var biome=index==50?OverworldBiome.Water:(OverworldBiome)(index-41);
                while(true)
                {
                    var campaign=CampaignGenerator.Generate(seed);
                    if(campaign.Regions.Any(r=>OverworldGridGenerator.BiomeForRegion(campaign,r.Id)==biome))break;
                    seed++;
                }
            }
            bool changed=playground.Overworld.CurrentGrid!=null&&playground.Seed!=seed;
            playground.Seed=seed;playground.ShowOverworld();
            if(changed)playground.Rebuild();
        }
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Environment capture " + index);
            if (index < 32 && playground.DungeonExplorer.IsBuilding) return;
            if (index >= 32 && index < 40 && !CoreLayoutCache.TryGetTown(playground.TownCreator, out _)) return;
            if (index >= 40 && playground.Overworld.CurrentGrid == null) return;
            if (++settle < 20) return;
            string name = index < 32 ? $"Dungeon_{(OverworldBiome)(index / 4)}_{(DungeonEnvironmentKind)((index / 2) % 2)}_{(index % 2 == 0 ? "Regular" : "Throne")}" : index < 40 ? "Town_" + (OverworldBiome)(index - 32) : index==40?"Overworld":index<49?"Overworld_"+(OverworldBiome)(index-41):"Overworld_"+new[]{"Coast","Bridge","Settlement","Mountains","Road"}[index-49];
            var camera = playground.ViewCamera;
            if(index>=40) FrameWorld(camera);
            Save(camera, name + "_Overview");
            var pos = camera.transform.position; float zoom = camera.orthographicSize;
            camera.orthographicSize = index < 32 ? 7 : index < 40 ? 9 : 16;
            Save(camera, name + "_Gameplay");
            camera.orthographicSize = index < 32 ? 2.5f : index < 40 ? 3 : 5;
            Save(camera, name + "_Detail");
            if(index>=32&&index<40&&CoreLayoutCache.TryGetTown(playground.TownCreator,out var town))
            {
                var door=town.Footprints.First().Door;float size=playground.TownCreator.twcAsset.cellSize;
                var center=new Vector3(door.X+.5f,door.Y+1.2f,0)*size;
                camera.transform.position=center+new Vector3(0,-12,-16);camera.transform.LookAt(center,Vector3.up);camera.orthographicSize=4;
                Save(camera,name+"_Facade");
            }
            camera.transform.position = pos; camera.orthographicSize = zoom;
            var rs = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var mats = rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            long bytes = mats.SelectMany(m => m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t => t != null).Distinct().Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            long tris = rs.Sum(r => { var f = r.GetComponent<MeshFilter>(); return f != null && f.sharedMesh != null ? (long)f.sharedMesh.triangles.Length / 3 : 0; });
            stats.AppendLine($"{name},{rs.Length},{mats.Length},{tris},{bytes},{UnityStats.batches},{UnityStats.setPassCalls},{playground.Seed}");
            File.WriteAllText(destination + "/Stats.csv", stats.ToString());
            if (++index <= Last) Next(); else Finish();
        }
        catch (Exception e) { Debug.LogException(e); Finish(); }
    }
    static void FrameWorld(Camera camera)
    {
        var grid=playground.Overworld.CurrentGrid;float size=playground.Overworld.Template.cellSize;
        Vector3 center=new Vector3(grid.Width*.5f,grid.Height*.5f,0)*size;
        if(index>40)
        {
            var points=Enumerable.Range(0,grid.Width*grid.Height).Select(i=>new GridPoint(i%grid.Width,i/grid.Width));
            if(index<49)points=points.Where(p=>OverworldCosmetics.Biome(grid,p.X,p.Y)==(OverworldBiome)(index-41));
            else if(index==51)points=grid.TownFootprints.Select(t=>t.Entrance);
            else
            {
                string layer=index==49?SmartEnvironmentMasks.Coast:index==50?OverworldLayers.Bridges:index==52?OverworldLayers.Mountains:OverworldLayers.Roads;
                if(index==49){var mask=SmartEnvironmentMasks.World(grid,layer);points=points.Where(p=>mask[p.X,p.Y]);}
                else points=points.Where(p=>grid.Layers[layer][p.X,p.Y]);
            }
            var candidates=points.OrderBy(p=>Mathf.Abs(p.X-grid.Width*.5f)+Mathf.Abs(p.Y-grid.Height*.5f)).ToArray();
            if(candidates.Length==0)throw new InvalidOperationException("Capture seed lacks "+index);
            center=new Vector3(candidates[0].X+.5f,candidates[0].Y+.5f,0)*size;
        }
        camera.orthographicSize=index==40?Mathf.Max(grid.Width/1.6f,grid.Height)*size*.55f:25;
        float distance=index==40?camera.orthographicSize*3:40;
        camera.farClipPlane=Mathf.Max(500,distance*3);
        camera.transform.position=center+new Vector3(0,-12,-35).normalized*distance;camera.transform.LookAt(center,Vector3.up);
    }
    static void Save(Camera camera, string name)
    {
        var rt = RenderTexture.GetTemporary(1280,800,24); var previous = RenderTexture.active; var target = camera.targetTexture;
        var image = new Texture2D(1280,800,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply(); File.WriteAllBytes(destination+"/"+name+".png",image.EncodeToPNG()); }
        finally { camera.targetTexture=target; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void Finish()
    {
        EditorApplication.update -= Tick; playground = null;
        EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("Painted environment capture finished: " + destination + " (" + index + "/"+(Last+1)+")");
    }
}



