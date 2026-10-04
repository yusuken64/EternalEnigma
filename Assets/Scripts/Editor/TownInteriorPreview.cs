using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TWC;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class TownInteriorPreview
{
    const string Folder="Docs/Art/Previews/TownInteriors";
    [MenuItem("Tools/Eternal Enigma/Art/Verify Town Interiors")]
    public static void Verify()
    {
        Directory.CreateDirectory(Folder);var catalog=TownInteriorCatalog.Load();
        var report=new List<string>{"biome,rooms,props,birds,wall_mounts,npcs,walkability,cleanup"};
        foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
        {
            var host=new GameObject("Interior verification");var creator=host.AddComponent<TileWorldCreator>();
            var template=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset"));
            creator.twcAsset=template;creator.worldObject=new GameObject("Furnished town");
            var style=host.AddComponent<TownBiomeStyle>();style.OverrideBiome=true;style.Biome=biome;
            try
            {
                var options=TownLayout.Create(42,TownServiceCatalog.All,2,3,CampaignContext.ResidentialTownBuildings).Options;
                CoreTownLayerGenerator.Configure(template,options);creator.SetCustomRandomSeed(42);creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(template);creator.ExecuteAllBuildLayers(true);
                CoreLayoutCache.TryGetTown(creator,out var plan);TownInteriorRendering.Build(creator);
                if(biome==OverworldBiome.Grassland)File.WriteAllLines(Folder+"/SurfaceDiagnostics.txt",
                    template.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().Select(l=>$"{l.layerName}: elevation={l.Elevation} heightScale={l.HeightScale}")
                    .Concat(creator.worldObject.GetComponentsInChildren<BiomeDecorationSurfaceSet>().SelectMany(s=>s.Faces).GroupBy(f=>f.Surface).Select(g=>$"{g.Key}: count={g.Count()} width={g.Min(f=>f.Width)}..{g.Max(f=>f.Width)} height={g.Min(f=>f.Height)}..{g.Max(f=>f.Height)}")));
                if(biome==OverworldBiome.Grassland)
                    File.AppendAllLines(Folder+"/SurfaceDiagnostics.txt",creator.worldObject.GetComponentsInChildren<BiomeDecorationSurfaceSet>().SelectMany(s=>s.Faces.Where(f=>f.Surface==DecorationSurface.BuiltWall).Take(8).Select(f=>$"{s.name} p={s.transform.TransformPoint(f.Center)} n={s.transform.TransformDirection(f.Normal)} local={f.Center} size={template.cellSize}")));
                var output=creator.worldObject.transform.Find("Town interiors");
                if(output==null)throw new InvalidOperationException("No rendered interiors");
                int before=output.GetComponentsInChildren<Renderer>().Length;
                TownInteriorRendering.Build(creator);output=creator.worldObject.transform.Find("Town interiors");
                if(output.GetComponentsInChildren<Renderer>().Length!=before)throw new InvalidOperationException("Regeneration changed counts");
                int birds=output.GetComponentsInChildren<TownAmbientAnimation>().Length;
                if(birds!=3)throw new InvalidOperationException($"Expected all three birds, got {birds}");
                foreach(var layer in TownLayers.InteriorLayers.Append(TownLayers.Walkable))
                {
                    var unity=creator.GetMapOutputFromBlueprintLayer(layer);var core=plan.Layers[layer];
                    for(int x=0;x<plan.Width;x++)for(int y=0;y<plan.Height;y++)if(unity[x,y]!=core[x,y])throw new InvalidOperationException("Core/Unity disagreement "+layer);
                }
                // Include production NPC prefabs in the gameplay-scale previews.
                foreach(var interior in plan.Interiors.Where(i=>i.Spec.HasVendor))
                {
                    string id=interior.Spec.Kind==TownInteriorKind.Inn?"Bear":interior.Spec.Kind==TownInteriorKind.Trainer?"GuineaPig":interior.Spec.Theme==TownShopTheme.Bakery?"Sheep":interior.Spec.Theme==TownShopTheme.Consumables?"Bunny":"Merchant";
                    var p=plan.ShopRoomAt(interior.Door).VendorAnchor;var npc=Object.Instantiate(catalog.Get(id).Prefab,creator.worldObject.transform);
                    npc.transform.localPosition=new Vector3(p.X+.5f,p.Y+.5f,0)*template.cellSize;npc.transform.localScale=Vector3.one*template.cellSize*.65f;
                    catalog.Get(id).Prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.First(c=>c.name.EndsWith("Idle",StringComparison.Ordinal)).SampleAnimation(npc,0);
                }
                var room=plan.Interiors.First(i=>i.Spec.Kind==TownInteriorKind.Shop);
                var center=new Vector3(room.Door.X+.5f,room.Door.Y+5,0)*template.cellSize;
                Capture(creator.worldObject,center,template.cellSize*6,Folder+"/Gameplay_"+biome+".png");
                if(biome==OverworldBiome.Grassland)
                foreach(var kind in new[]{TownInteriorKind.Residential,TownInteriorKind.Shop,TownInteriorKind.Inn,TownInteriorKind.Trainer})
                {
                    var selected=plan.Interiors.First(i=>i.Spec.Kind==kind);center=new Vector3(selected.Door.X+.5f,selected.Door.Y+5,0)*template.cellSize;
                    Capture(creator.worldObject,center,template.cellSize*5.7f,Folder+"/Interior_"+kind+".png");
                }
                var audit=output.GetComponent<TownInteriorOutput>();
                if(audit.WallMountCount==0)throw new InvalidOperationException("No supported interior wall decorations");
                if(audit.Birds.Count(b=>!b.Interior)!=2)throw new InvalidOperationException("Expected two exterior perches");
                if(biome==OverworldBiome.Grassland)
                    foreach(var bird in audit.Birds)Capture(creator.worldObject,bird.Position+Vector3.back*.2f*template.cellSize,template.cellSize*1.5f,Folder+"/Perch_"+bird.Variant+".png",800,800);
                report.Add($"{biome},{plan.Interiors.Count},{plan.Interiors.Sum(i=>i.Props.Count)},{birds},{audit.WallMountCount},{TownNpcPlacement.Generate(plan).Count},pass,pass");
                if(biome==OverworldBiome.Grassland)File.AppendAllText(Folder+"/SurfaceDiagnostics.txt",$"\nSupporting faces: {audit.SupportingFaces}, interior facing: {audit.InteriorFaces}, mounted: {audit.WallMountCount}\n");
            }
            finally {DungeonPresentation.ClearOutput(creator.worldObject);Object.DestroyImmediate(creator.worldObject);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(template);}
        }
        File.WriteAllLines(Folder+"/UnityVerification.csv",report);
        Gallery(catalog);RuleGallery(catalog);Debug.Log(string.Join("\n",report));
    }
    static void RuleGallery(TownInteriorCatalog catalog)
    {
        var host=new GameObject("Native TWC rule gallery");var creator=host.AddComponent<TileWorldCreator>();
        var template=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset"));
        template.mapBlueprintLayers.Clear();template.mapBuildLayers.Clear();template.mapWidth=28;template.mapHeight=16;template.cellSize=1;
        creator.twcAsset=template;creator.worldObject=new GameObject("Straight L U island");
        try
        {
            foreach(bool counter in new[]{false,true})
            {
                var mask=new TWC.Actions.Paint();var layer=new TileWorldCreatorAsset.BlueprintLayerData(counter?"Counter":"Carpet",true);
                layer.stack.Add(new TileWorldCreatorAsset.BlueprintLayerData.ActionStack("Review shapes",mask));template.mapBlueprintLayers.Add(layer);
                int row=counter?9:1;
                for(int shape=0;shape<4;shape++)for(int x=0;x<5;x++)for(int y=0;y<5;y++)
                    if(shape==0?y==0:shape==1?x==0||y==0:shape==2?x==0||x==4||y==0:true)
                        mask.ModifyMap(1+shape*7+x,row+y,true,creator);
                template.mapBuildLayers.Add(new EnvironmentSmartTileLayer {guid=Guid.NewGuid(),layerName=layer.layerName,assignedGenerationLayerGuid=layer.guid,
                    Kit=EnvironmentKit.Load(),QuarterTiles=counter?catalog.Counter:catalog.Carpet,SurfaceMaterial=catalog.BiomeMaterials[0],HeightScale=counter?2:1});
            }
            creator.ExecuteAllBlueprintLayers();creator.ExecuteAllBuildLayers(true);
            var types=template.mapBlueprintLayers.SelectMany(l=>creator.GetGeneratedBlueprintMap(l.guid.ToString()).clusters.Values.SelectMany(c=>c.Values)).Select(t=>t.tileType).Distinct().ToArray();
            if(types.Length!=4)throw new InvalidOperationException("Gallery must exercise all four native TWC tile rules");
            Capture(creator.worldObject,new Vector3(13.5f,7,0),11,Folder+"/TWCRuleGallery.png",1600,1000);
        }
        finally {DungeonPresentation.ClearOutput(creator.worldObject);Object.DestroyImmediate(creator.worldObject);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(template);}
    }
    static void Gallery(TownInteriorCatalog catalog)
    {
        foreach(var entry in catalog.Assets.Where(a=>a.Prefab.GetComponent<Animator>()!=null))
        {
            var root=new GameObject("Turnaround "+entry.Id);
            try
            {
                for(int i=0;i<4;i++)
                {
                    var copy=Object.Instantiate(entry.Prefab,root.transform);copy.transform.localPosition=new Vector3(i*(entry.Id.StartsWith("Bird")?.9f:2),0,0);copy.transform.localRotation=Quaternion.Euler(0,0,i*90);
                    entry.Prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.First(c=>c.name.EndsWith("Idle",StringComparison.Ordinal)).SampleAnimation(copy,0);
                }
                bool bird=entry.Id.StartsWith("Bird");
                Capture(root,new Vector3(bird?1.35f:3,0,bird?-.28f:-.85f),bird?1.2f:2.8f,Folder+"/Turnaround_"+entry.Id+".png",1400,700,true);
                var actor=root.transform.GetChild(0).gameObject;foreach(Transform child in root.transform)child.gameObject.SetActive(child.gameObject==actor);actor.transform.localPosition=Vector3.zero;
                var clip=entry.Prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.First(c=>c.name.EndsWith("Idle",StringComparison.Ordinal));
                for(int frame=0;frame<8;frame++){clip.SampleAnimation(actor,frame*.5f);Capture(root,new Vector3(0,0,entry.Id.StartsWith("Bird")?-.28f:-.85f),entry.Id.StartsWith("Bird")?.55f:1.25f,Folder+"/Animation_"+entry.Id+"_"+frame+".png",480,480,true);}
                if(!bird)
                {
                    var greeting=entry.Prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.First(c=>c.name.EndsWith("Greeting",StringComparison.Ordinal));
                    for(int frame=0;frame<8;frame++){greeting.SampleAnimation(actor,frame*.5f);Capture(root,new Vector3(0,0,-.85f),1.25f,Folder+"/Greeting_"+entry.Id+"_"+frame+".png",480,480,true);}
                }
            }
            finally{Object.DestroyImmediate(root);}
        }
        var props=new GameObject("Prop contact sheet");
        try
        {
            int i=0;foreach(var entry in catalog.Assets.Where(a=>a.Prefab.GetComponent<Animator>()==null))
            {var copy=Object.Instantiate(entry.Prefab,props.transform);copy.transform.localPosition=new Vector3(i%8*1.5f,i/8*1.9f,0);i++;}
            Capture(props,new Vector3(5.3f,4.4f,-.3f),7,Folder+"/PropContactSheet.png");
        }finally{Object.DestroyImmediate(props);}
    }
    // Matches ScenePresentation's fixed indoor sun direction and ambient palette; no extra point lights.
    public static void Capture(GameObject root,Vector3 center,float zoom,string path,int width=1400,int height=1000,bool frontal=false)
    {
        var cameraObject=new GameObject("Interior capture camera");var camera=cameraObject.AddComponent<Camera>();
        var lightObject=new GameObject("Interior capture daylight");var key=lightObject.AddComponent<Light>();key.type=LightType.Directional;key.intensity=1;key.cullingMask=1<<30;key.shadows=LightShadows.None;
        float angle=35*Mathf.Deg2Rad,elevation=55*Mathf.Deg2Rad;
        key.transform.rotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(angle)*Mathf.Cos(elevation),Mathf.Sin(angle)*Mathf.Cos(elevation),Mathf.Sin(elevation)),Vector3.up);
        var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;var active=RenderTexture.active;
        var target=RenderTexture.GetTemporary(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        var transforms=root.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        try
        {
            foreach(var t in transforms)t.gameObject.layer=30;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.64f,.67f);
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.orthographic=true;camera.orthographicSize=zoom;
            camera.transform.position=center+(frontal?new Vector3(0,-28,-9):new Vector3(0,-23,-28));camera.transform.LookAt(center,Vector3.up);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally {for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;RenderTexture.active=active;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lightObject);}
    }
}
