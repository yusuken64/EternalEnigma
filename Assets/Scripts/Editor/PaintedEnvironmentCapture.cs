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
    static int last=53, first;
    static double deadline;
    static bool reloadLocked;
    static readonly StringBuilder stats = new StringBuilder();
    [MenuItem("Tools/Eternal Enigma/Painted Environment/Capture Before")]
    public static void Before() => Begin("Before");
    [MenuItem("Tools/Eternal Enigma/Painted Environment/Capture After")]
    public static void After() => Begin("After");
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Before")]
    public static void DioramaBefore() => Begin("Before", 0, "Docs/Art/Previews/Diorama");
    [MenuItem("Tools/Eternal Enigma/Diorama/Resume Before")]
    public static void ResumeDioramaBefore() => Begin("Before",
        File.ReadAllLines("Docs/Art/Previews/Diorama/Before/Stats.csv").Length - 1, "Docs/Art/Previews/Diorama");
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture After")]
    public static void DioramaAfter() => Begin("After", 0, "Docs/Art/Previews/Diorama");
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Ground")]
    public static void DioramaGround() => Begin("Ground", 32, "Docs/Art/Previews/Diorama", 40, false);
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Vegetation")]
    public static void DioramaVegetation() => Begin("Vegetation", 32, "Docs/Art/Previews/Diorama", 40, false);
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Houses")]
    public static void DioramaHouses() => Begin("Houses", 32, "Docs/Art/Previews/Diorama", 39, false);
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Final Towns")]
    public static void FinalTowns() => Begin("FinalTowns", 32, "Docs/Art/Previews/Diorama", 39, false);
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Final Signs")]
    public static void FinalSigns() => Begin("FinalSigns", 51, "Docs/Art/Previews/Diorama", 53, false);
    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Terrain and Settlements")]
    public static void DioramaTerrain() => Begin("Terrain", 40, "Docs/Art/Previews/Diorama", 53, false);
    [MenuItem("Tools/Eternal Enigma/Painted Environment/Resume Before Features")]
    public static void ResumeBeforeFeatures() => Begin("Before",50);
    static void Begin(string stage,int start=0,string folder="Docs/Art/Previews/PaintedEnvironment",int end=53,bool resume=true)
    {
        if (EditorApplication.isPlaying || playground != null) throw new InvalidOperationException("Capture requires idle edit mode.");
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save your scene edits before capturing.");
        setup = EditorSceneManager.GetSceneManagerSetup();
        destination = folder + "/" + stage;
        Directory.CreateDirectory(destination);
        // Editor auto-refresh can otherwise discard Tick halfway through a long capture.
        EditorApplication.LockReloadAssemblies(); AssetDatabase.DisallowAutoRefresh(); reloadLocked = true;
        try
        {
        EditorSceneManager.OpenScene("Assets/Scenes/EnvironmentPlayground.unity");
        playground = UnityEngine.Object.FindFirstObjectByType<EnvironmentPlayground>();
        playground.Seed = 12345;
        index = start;first=start;last=end;
        stats.Clear().AppendLine("view,renderers,materials,triangles,textureBytes,batches,setPassCalls,seed");
        if(start>0 && resume)stats.Clear().Append(string.Join("\n",File.ReadAllLines(destination+"/Stats.csv").Take(start+1))).AppendLine();
        Next(); EditorApplication.update += Tick;
        }
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
                CaptureTownTree(camera,name,town,size);
                CaptureTownAccents(camera,name,town,size);
            }
            if(index==45||index==53)CaptureWorldTree(camera,name);
            camera.transform.position = pos; camera.orthographicSize = zoom;
            var rs = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var mats = rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            long bytes = mats.SelectMany(m => m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t => t != null).Distinct().Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            long tris = rs.Sum(r => { var f = r.GetComponent<MeshFilter>(); return f != null && f.sharedMesh != null ? (long)f.sharedMesh.triangles.Length / 3 : 0; });
            stats.AppendLine($"{name},{rs.Length},{mats.Length},{tris},{bytes},{UnityStats.batches},{UnityStats.setPassCalls},{playground.Seed}");
            File.WriteAllText(destination + "/Stats.csv", stats.ToString());
            if (++index <= last) Next(); else Finish();
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
        foreach(var label in UnityEngine.Object.FindObjectsByType<BiomeSignLabel>(FindObjectsSortMode.None))
        {label.Face(camera);label.GetComponent<TMPro.TMP_Text>().ForceMeshUpdate();}
        var rt = RenderTexture.GetTemporary(1280,800,24); var previous = RenderTexture.active; var target = camera.targetTexture;
        var image = new Texture2D(1280,800,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply(); File.WriteAllBytes(destination+"/"+name+".png",image.EncodeToPNG()); }
        finally { camera.targetTexture=target; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void CaptureTownTree(Camera camera,string name,TownPlan town,float size)
    {
        var catalog=DioramaCatalog.Load();var picker=EnvironmentKit.Load().TreeModels;if(catalog==null)return;
        var biome=playground.TownBiome;
        for(int y=1;y<town.Height-1;y++)for(int x=2;x<town.Width-2;x++)
        {
            if(!town.Layers[TownLayers.Trees][x,y])continue;
            uint hash=OverworldCosmetics.Hash(playground.TownCreator.currentSeed^15401,x,y);
            var model=catalog.Get(picker.Pick(biome,hash));if(model==null)continue;
            bool Protected(int a,int b)=>a>=0&&b>=0&&a<town.Width&&b<town.Height&&(town.Layers[TownLayers.Roads][a,b]||town.IsReserved(new GridPoint(a,b))||town.Layers[TownLayers.Buildings][a,b]);
            if(!DioramaPlacement.TreePosition(model,x,y,size,DioramaPlacement.Variation(hash),Protected,out var tree))continue;
            int heroX=town.Layers[TownLayers.Roads][x-2,y]?x-2:town.Layers[TownLayers.Roads][x+2,y]?x+2:-1;if(heroX<0)continue;
            var hero=DioramaFitAuthoring.Spawn(DioramaFitAuthoring.HeroPath,camera.gameObject.scene);
            try
            {
                hero.GetComponent<TownAlly>()?.SetFacing(Facing.Down);
                var bounds=DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(hero));
                hero.transform.position=new Vector3((heroX+.5f)*size-bounds.center.x,tree.y-bounds.center.y,-bounds.max.z);
                var target=new Vector3((tree.x+(heroX+.5f)*size)*.5f,tree.y,-DioramaScale.HeroHeight);
                camera.transform.position=target+new Vector3(0,-14,-20);camera.transform.LookAt(target,Vector3.up);camera.orthographicSize=4.2f;
                Save(camera,name+"_HeroTree");
            }
            finally {UnityEngine.Object.DestroyImmediate(hero);}
            return;
        }
    }
    static void CaptureTownAccents(Camera camera,string name,TownPlan town,float size)
    {
        foreach(var theme in new[]{TownShopTheme.Bakery,TownShopTheme.Consumables})
        {
            var interior=town.Interiors.FirstOrDefault(i=>i.Spec.Kind==TownInteriorKind.Shop&&i.Spec.Theme==theme);if(interior==null)continue;
            var roof=playground.TownCreator.worldObject.GetComponentsInChildren<TownRoofVisual>().FirstOrDefault(r=>r.Door.Equals(interior.Door));
            if(roof!=null)roof.gameObject.SetActive(false);
            try
            {
                var target=new Vector3(interior.Door.X+.5f,interior.Door.Y+3.5f,-.35f)*size;
                camera.transform.position=target+new Vector3(0,-14,-20);camera.transform.LookAt(target,Vector3.up);camera.orthographicSize=5.5f;
                Save(camera,name+"_"+theme);
            }
            finally {if(roof!=null)roof.gameObject.SetActive(true);}
        }
        if(index!=32)return;
        foreach(string id in new[]{"hen","duck","butterfly","bee","ladybug"})
        {
            var animal=playground.TownCreator.worldObject.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==id);if(animal==null)continue;
            var target=animal.position+Vector3.back*.3f;
            camera.transform.position=target+new Vector3(0,-14,-20);camera.transform.LookAt(target,Vector3.up);camera.orthographicSize=2;
            Save(camera,name+"_"+id);
        }
    }
    static void Finish()
    {
        EditorApplication.update -= Tick; playground = null;
        try { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        finally
        {
            if (reloadLocked) { reloadLocked = false; AssetDatabase.AllowAutoRefresh(); EditorApplication.UnlockReloadAssemblies(); }
        }
        File.WriteAllText(destination+"/CaptureState.txt", index > last ? $"Completed: {last-first+1}/{last-first+1} views" : $"Incomplete: {index-first}/{last-first+1} views; next index {index}");
        Debug.Log("Painted environment capture finished: " + destination + " (" + index + "/"+(last+1)+")");
    }
    static void CaptureWorldTree(Camera camera,string name)
    {
        var grid=playground.Overworld.CurrentGrid;float size=playground.Overworld.Template.cellSize;
        var catalog=DioramaCatalog.Load();var protect=OverworldCosmetics.Protected(grid);
        for(int y=3;y<grid.Height-3;y++)for(int x=3;x<grid.Width-3;x++)
        {
            if((x+y)%2!=0||protect[x,y]||!grid.Layers[OverworldLayers.Trees][x,y])continue;
            var biome=OverworldCosmetics.Biome(grid,x,y);if(index==45&&biome!=OverworldBiome.Forest)continue;
            uint hash=OverworldCosmetics.Hash(grid.CampaignSeed^15401,x,y);var model=catalog.Get(EnvironmentKit.Load().TreeModels.Pick(biome,hash));
            if(!DioramaPlacement.TreePosition(model,x,y,size,DioramaPlacement.Variation(hash),(a,b)=>a>=0&&b>=0&&a<grid.Width&&b<grid.Height&&protect[a,b],out var tree))continue;
            int heroX=Enumerable.Range(x-3,7).Where(a=>grid.Layers[OverworldLayers.Roads][a,y]).DefaultIfEmpty(-1).First();if(heroX<0)continue;
            var hero=DioramaFitAuthoring.Spawn(DioramaFitAuthoring.HeroPath,camera.gameObject.scene);
            try
            {
                hero.GetComponent<TownAlly>()?.SetFacing(Facing.Down);
                var bounds=DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(hero));
                hero.transform.position=new Vector3((heroX+.5f)*size-bounds.center.x,tree.y-bounds.center.y,-bounds.max.z);
                var target=new Vector3((tree.x+(heroX+.5f)*size)*.5f,tree.y,-DioramaScale.HeroHeight);
                camera.transform.position=target+new Vector3(0,-14,-20);camera.transform.LookAt(target,Vector3.up);camera.orthographicSize=5;
                Save(camera,name+"_HeroTree");
            }
            finally {UnityEngine.Object.DestroyImmediate(hero);}
            return;
        }
    }
}



