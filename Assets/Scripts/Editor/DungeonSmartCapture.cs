using System;
using System.IO;
using System.Linq;
using System.Text;
using EternalEnigma.Core.World;
using TWC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DungeonSmartCapture
{
    const string Folder = DungeonSmartAuthoring.Evidence;
    static Scene scene, original;
    static Camera camera;
    static GameObject hero, floor;
    static Material lightFloor,darkFloor;
    static DungeonPickupPresentation table;
    static int index;
    static bool biomeCaptureRunning;

    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Capture Pickups")]
    public static void Pickups()
    {
        if(scene.IsValid() || biomeCaptureRunning)throw new InvalidOperationException("Capture already running.");
        Directory.CreateDirectory(Folder+"/Pickups");
        table=AssetDatabase.LoadAssetAtPath<DungeonPickupPresentation>(DungeonPickupAuthoring.TablePath);
        original=SceneManager.GetActiveScene();scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        camera=DungeonPickupAuthoring.GameplayCamera(scene,Vector3.zero,out _);
        Lighting(camera);
        hero=DungeonPickupAuthoring.SpawnHero(scene);PlaceHero(hero,new Vector2(-1.4f,0));
        floor=GameObject.CreatePrimitive(PrimitiveType.Quad);Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.position=new Vector3(0,0,.002f);floor.transform.localScale=Vector3.one*50;
        var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
        Material Ground(OverworldBiome biome)=>new Material(catalog.Get(new DungeonVisualSelection {Biome=biome,Environment=DungeonEnvironmentKind.Interior}).Floor.fillTile.GetComponentInChildren<MeshRenderer>().sharedMaterial);
        lightFloor=Ground(OverworldBiome.Grassland);darkFloor=Ground(OverworldBiome.Volcanic);
        // Match the authored floor's world UV density on this inspection plane.
        lightFloor.mainTextureScale=darkFloor.mainTextureScale=Vector2.one*20;
        index=0;EditorApplication.update+=PickupTick;
        File.WriteAllText(Folder+"/CaptureProgress.txt","0 / "+table.Items.Length);
    }
    static void PickupTick()
    {
        try
        {
            if(index>=table.Items.Length)
            {
                var html=new StringBuilder("<!doctype html><meta charset='utf-8'><title>Dungeon pickup review</title><style>body{background:#181c20;color:#e9e9e9;font:16px system-ui;margin:24px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}article{background:#252b31;padding:12px}img{width:100%}h2{font-size:15px;margin:0 0 5px}p{margin:4px 0 18px}</style><h1>Dungeon pickups beside the unchanged hero</h1><p>Left: light floor. Right: dark floor. Each pair uses the same ground depth and a copy of the production camera after CameraController.Start. Images are crops from 2560 × 1600 frames, with no camera zoom. Ratios use the projected mesh silhouette's longest diameter divided by hero screen height. Chests target half hero world height.</p><main>");
                for(int i=0;i<table.Items.Length;i++) {var e=table.Items[i];html.Append($"<article><h2>{System.Net.WebUtility.HtmlEncode(e.Name)}</h2><p>{e.Size} · silhouette {e.ProjectedRatio:F3} H · world height {e.WorldHeightRatio:F3} H</p><img src='Pickups/{i:000}.png'></article>");}
                File.WriteAllText(Folder+"/PickupReview.html",html+"</main>");
                File.WriteAllText(Folder+"/CaptureProgress.txt","Complete: "+index+" pickup comparisons");
                Cleanup();Debug.Log("Dungeon pickup capture complete: "+index);return;
            }
            var entry=table.Items[index];var obj=Object.Instantiate(entry.Prefab);SceneManager.MoveGameObjectToScene(obj,scene);
            try
            {
                obj.transform.localScale=Vector3.Scale(obj.transform.localScale,entry.ParentScale);
                obj.transform.position=new Vector3(1.4f-entry.CellCenter.x,-entry.CellCenter.y,0);DungeonPresentation.GroundFloorObject(obj.transform);
                var sheet=new Texture2D(1536,500,TextureFormat.RGB24,false);
                try
                {
                    for(int side=0;side<2;side++)
                    {
                        floor.GetComponent<MeshRenderer>().sharedMaterial=side==0?lightFloor:darkFloor;
                        var frame=Render(camera,2560,1600);
                        try
                        {
                            // Both subjects have ground Y=0. This is a pixel crop, not a camera change.
                            var center=camera.WorldToViewportPoint(new Vector3(0,0,-1.4f));
                            int x=Mathf.RoundToInt(center.x*2560)-384,y=Mathf.RoundToInt(center.y*1600)-250;
                            sheet.SetPixels(side*768,0,768,500,frame.GetPixels(x,y,768,500));
                            if(index==0)File.WriteAllBytes(Folder+"/Pickups/GameplayFrame_"+(side==0?"Light":"Dark")+".png",frame.EncodeToPNG());
                        }
                        finally {Object.DestroyImmediate(frame);}
                    }
                    sheet.Apply();File.WriteAllBytes(Folder+$"/Pickups/{index:000}.png",sheet.EncodeToPNG());
                }
                finally {Object.DestroyImmediate(sheet);}
            }
            finally {Object.DestroyImmediate(obj);}
            index++;File.WriteAllText(Folder+"/CaptureProgress.txt",index+" / "+table.Items.Length);
        }
        catch(Exception e){Cleanup();Debug.LogException(e);}
    }
    static void Cleanup()
    {
        EditorApplication.update-=PickupTick;
        if(lightFloor!=null)Object.DestroyImmediate(lightFloor);if(darkFloor!=null)Object.DestroyImmediate(darkFloor);
        if(original.IsValid())SceneManager.SetActiveScene(original);
        if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);
        scene=default;camera=null;hero=null;floor=null;
    }
    public static void PlaceHero(GameObject obj,Vector2 center)
    {
        var bounds=DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(obj));
        obj.transform.position+=new Vector3(center.x-bounds.center.x,center.y-bounds.center.y,-bounds.max.z);
    }
    public static void Lighting(Camera camera)
    {
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=ScenePresentation.FillAmbient(new Color(.5775f,.649f,.4995f));
        var light=new GameObject("Dungeon review light").AddComponent<Light>();light.type=LightType.Directional;
        light.color=new Color(.87f,.892f,.846f);light.intensity=.8f;
        light.transform.rotation=Quaternion.Euler(30,-20,0);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.075f,.08f);
    }
    public static Texture2D Render(Camera camera,int width,int height)
    {
        // Camera.scene isolates preview scenes only; an additive review needs its own render layer.
        camera.cullingMask=1<<31;
        foreach(var root in camera.gameObject.scene.GetRootGameObjects())
            foreach(var transform in root.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=31;
        foreach(var light in camera.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>()))light.cullingMask=1<<31;
        var rt=RenderTexture.GetTemporary(width,height,24);var old=camera.targetTexture;var active=RenderTexture.active;
        var outsideLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.gameObject.scene!=camera.gameObject.scene&&l.enabled).ToArray();
        foreach(var light in outsideLights)light.enabled=false;
        var result=new Texture2D(width,height,TextureFormat.RGB24,false);
        bool asynchronous=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;result.ReadPixels(new Rect(0,0,width,height),0,0);result.Apply();return result;}
        catch {Object.DestroyImmediate(result);throw;}
        finally {ShaderUtil.allowAsyncCompilation=asynchronous;camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);foreach(var light in outsideLights)if(light!=null)light.enabled=true;}
    }

    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Capture Kit and Rooms")]
    public static void Rooms()
    {
        if(scene.IsValid() || biomeCaptureRunning)throw new InvalidOperationException("Wait for the current capture.");
        Directory.CreateDirectory(Folder);
        var previous=SceneManager.GetActiveScene();var review=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(review);
        try
        {
            var camera=DungeonPickupAuthoring.GameplayCamera(review,Vector3.zero,out _);Lighting(camera);
            var kit=AssetDatabase.LoadAssetAtPath<DungeonBoundaryPreset>(DungeonSmartAuthoring.Folder+"/Boundary.asset");
            var kitRoot=new GameObject("Kit lineup");
            var pieces=new[]{kit.Tiles.singleTile,kit.Tiles.deadEndTile,kit.Tiles.straightTile,kit.StraightVariants[1],kit.Tiles.cornerTile,kit.Tiles.threeWayTile,kit.Tiles.fourWayTile};
            for(int i=0;i<pieces.Length;i++){var obj=Object.Instantiate(pieces[i],kitRoot.transform);obj.transform.position=new Vector3((i-3)*2.7f,2,0);}
            var hero=DungeonPickupAuthoring.SpawnHero(review);PlaceHero(hero,new Vector2(0,-1));
            var ground=GameObject.CreatePrimitive(PrimitiveType.Quad);ground.transform.localScale=Vector3.one*45;ground.transform.position=Vector3.forward*.002f;
            var groundMaterial=new Material(Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Themes[0].Floor.fillTile.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            groundMaterial.mainTextureScale=Vector2.one*18;
            ground.GetComponent<MeshRenderer>().sharedMaterial=groundMaterial;
            Save(camera,"Kit_Gameplay.png");Object.DestroyImmediate(kitRoot);Object.DestroyImmediate(ground);Object.DestroyImmediate(groundMaterial);
            foreach(bool throne in new[]{false,true})
                CaptureRoom(camera,hero,new DungeonVisualSelection{Biome=OverworldBiome.Grassland,Environment=DungeonEnvironmentKind.Interior,UseBiomePresentation=true},
                    throne,throne?"Throne_Gameplay":"Regular_Gameplay");
        }
        finally {SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(review,true);}
        Debug.Log("Captured dungeon kit and both Grassland rooms with unchanged hero/camera projection.");
    }

    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Capture All Biomes")]
    public static void AllBiomes()
    {
        if(EditorApplication.isPlaying || scene.IsValid() || biomeCaptureRunning)throw new InvalidOperationException("Finish Play Mode and other captures first.");
        Directory.CreateDirectory(Folder+"/Biomes");
        var choices=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Themes.Select(t=>new DungeonVisualSelection{
            Biome=t.Biome,Environment=t.Environment,UseBiomePresentation=true}).ToArray();
        var previous=SceneManager.GetActiveScene();var review=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(review);biomeCaptureRunning=true;
        Camera reviewCamera=null;GameObject reviewHero=null;int next=0;
        void Finish()
        {
            EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Finish;
            if(previous.IsValid())SceneManager.SetActiveScene(previous);
            if(review.IsValid())EditorSceneManager.CloseScene(review,true);
            biomeCaptureRunning=false;
        }
        void Tick()
        {
            try
            {
                if(next==choices.Length*2)
                {File.WriteAllText(Folder+"/Biomes/Progress.txt","Complete: "+next+" biome/layout captures");Finish();Debug.Log("Captured all dungeon biomes with smart boundaries.");return;}
                var selection=choices[next/2];bool throne=next%2==1;
                CaptureRoom(reviewCamera,reviewHero,selection,throne,$"Biomes/{selection.Biome}_{selection.Environment}_{(throne?"Throne":"Regular")}");
                next++;File.WriteAllText(Folder+"/Biomes/Progress.txt",next+" / "+choices.Length*2);
            }
            catch(Exception e){Finish();Debug.LogException(e);}
        }
        try
        {
            reviewCamera=DungeonPickupAuthoring.GameplayCamera(review,Vector3.zero,out _);Lighting(reviewCamera);
            reviewHero=DungeonPickupAuthoring.SpawnHero(review);
            File.WriteAllText(Folder+"/Biomes/Progress.txt","0 / "+choices.Length*2);
            AssemblyReloadEvents.beforeAssemblyReload+=Finish;EditorApplication.update+=Tick;
        }
        catch {Finish();throw;}
    }

    static void CaptureRoom(Camera camera,GameObject hero,DungeonVisualSelection selection,bool throne,string name)
    {
        var host=new GameObject("Room capture creator");var creator=host.AddComponent<TileWorldCreator>();
        var asset=DungeonPresentation.CloneTemplate(DungeonSmartAuthoring.Template(throne));creator.twcAsset=asset;
        var world=new GameObject("Room capture");creator.worldObject=world;
        try
        {
            var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");var theme=catalog.Get(selection);catalog.Apply(asset,selection,throne);
            RenderSettings.ambientLight=ScenePresentation.FillAmbient(theme.Ambient);
            var light=camera.gameObject.scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Light>()).Single();
            light.color=theme.LightColor;light.intensity=theme.LightIntensity;
            creator.SetCustomRandomSeed(12345);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(asset);creator.ExecuteAllBuildLayers(true);
            CoreLayoutCache.TryGetDungeon(creator,out var layout);
            DungeonPresentation.Decorate(creator,layout,theme);BiomeDecorations.Dungeon(creator,layout,selection,true);
            var center=new Vector2((layout.Start.X+.5f)*2,(layout.Start.Y+.5f)*2);PlaceHero(hero,center);
            var target=new Vector3(layout.Start.X*2,layout.Start.Y*2,0);
            camera.transform.position=target+new Vector3(0,-12,-14);camera.transform.LookAt(target);
            var examples=AssetDatabase.LoadAssetAtPath<DungeonPickupPresentation>(DungeonPickupAuthoring.TablePath).Items;
            var names=new[]{"Potion","Arrows","Wooden Wand","Bread","Category: TreasureChest"};int next=0;
            for(int y=layout.Start.Y-2;y<=layout.Start.Y+2;y++)for(int x=layout.Start.X-2;x<=layout.Start.X+2;x++)
            {
                if(next>=names.Length||x==layout.Start.X&&y==layout.Start.Y||!layout.Layers[DungeonLayers.Floor].At(new GridPoint(x,y)))continue;
                string itemName=names[next++];var entry=examples.Single(e=>e.Name==itemName);
                var obj=Object.Instantiate(entry.Prefab,world.transform);
                obj.transform.position=new Vector3(x*2,y*2,0);DungeonPresentation.GroundFloorObject(obj.transform);
            }
            Save(camera,name+".png");
            File.WriteAllText(Folder+"/"+name+".txt",$"seed=12345 start={layout.Start} wall_faces={world.GetComponentsInChildren<BiomeDecorationSurfaceSet>().Sum(s=>s.Faces.Count)} mounts={world.GetComponentsInChildren<BiomeDecorationFog>().Length} meshes={world.GetComponentsInChildren<MeshFilter>().Length}");
        }
        finally {DungeonPresentation.ClearOutput(world);Object.DestroyImmediate(world);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(asset);}
    }
    static void Save(Camera camera,string name)
    {
        var image=Render(camera,1600,1000);
        try {File.WriteAllBytes(Folder+"/"+name,image.EncodeToPNG());}
        finally {Object.DestroyImmediate(image);}
    }
}
