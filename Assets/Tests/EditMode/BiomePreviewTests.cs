using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class BiomePreviewTests
    {
        [Test]
        public void ExportAllBiomesAndRolesWithCosts()
        {
            const string directory="Docs/Art/Previews/BiomeLayouts";
            Directory.CreateDirectory(directory);
            var report=new StringBuilder("Biome,Role,Seed,Width,Height,Walkable,CoreMilliseconds,GeometryMilliseconds,Triangles,Renderers\n");
            var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
            var ambient=RenderSettings.ambientLight;
            try
            {
                foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
                foreach(DungeonFloorRole role in Enum.GetValues(typeof(DungeonFloorRole)))
                {
                    var options=DungeonLayoutProfile.Options(42,biome,4,role);
                    if(role==DungeonFloorRole.Regular) options=new DungeonFloorOptions(42,56,56,false,10,5,5,0,3,1,biome,4,role);
                    var watch=Stopwatch.StartNew();var expected=DungeonFloorGenerator.Generate(options);watch.Stop();double coreMs=watch.Elapsed.TotalMilliseconds;
                    var host=new GameObject("Biome preview controller");var root=new GameObject("Biome preview");
                    var creator=host.AddComponent<TileWorldCreator>();creator.worldObject=root;
                    var asset=DungeonPresentation.CloneTemplate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/"+(role==DungeonFloorRole.Regular?"DungeonAsset":"DungeonThroneAsset")+".asset"));
                    var cameraObject=new GameObject("Biome capture camera");var camera=cameraObject.AddComponent<Camera>();
                    var lightObject=new GameObject("Biome capture light");var light=lightObject.AddComponent<Light>();
                    RenderTexture target=null;Texture2D image=null;
                    try
                    {
                        var selection=new DungeonVisualSelection {Biome=biome,UseBiomePresentation=true};var theme=catalog.Get(selection);
                        catalog.Apply(asset,selection,role!=DungeonFloorRole.Regular);CoreDungeonLayerGenerator.Configure(asset,options);
                        creator.twcAsset=asset;creator.SetCustomRandomSeed(options.Seed);
                        watch.Restart();creator.ExecuteAllBlueprintLayers();CoreLayoutCache.ClearResultFlags(asset);creator.ExecuteAllBuildLayers(true);
                        Assert.That(CoreLayoutCache.TryGetDungeon(creator,out var floor),Is.True);
                        Assert.That(floor.Layers[DungeonLayers.Floor].ToArray(),Is.EqualTo(expected.Layers[DungeonLayers.Floor].ToArray()));
                        DungeonPresentation.Decorate(creator,floor,theme);
                        var dungeon=root.AddComponent<TileWorldDungeon>();dungeon.Interactables=new();dungeon.Setup(creator,floor);
                        foreach(var definition in floor.Scenery) DungeonProp.Create(dungeon,definition,theme);
                        watch.Stop();double geometryMs=watch.Elapsed.TotalMilliseconds;
                        foreach(var transform in root.GetComponentsInChildren<Transform>()) transform.gameObject.layer=31;
                        light.type=LightType.Directional;light.color=theme.LightColor;light.intensity=theme.LightIntensity;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(25,-30,0);
                        RenderSettings.ambientLight=theme.Ambient;
                        camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.04f,.05f);camera.orthographic=true;
                        float size=asset.cellSize;var center=new Vector3(floor.Width*size*.5f,floor.Height*size*.5f,0);
                        camera.transform.position=center+new Vector3(0,-12,-160);camera.transform.LookAt(center,Vector3.up);camera.orthographicSize=Math.Max(floor.Width,floor.Height)*size*.55f;
                        target=new RenderTexture(1024,1024,24);camera.targetTexture=target;camera.Render();
                        var previous=RenderTexture.active;RenderTexture.active=target;image=new Texture2D(1024,1024,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();RenderTexture.active=previous;
                        File.WriteAllBytes(directory+"/"+biome+"_"+role+".png",image.EncodeToPNG());
                        int triangles=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null).Sum(f=>f.sharedMesh.triangles.Length/3);
                        report.AppendLine(FormattableString.Invariant($"{biome},{role},42,{floor.Width},{floor.Height},{floor.Layers[DungeonLayers.Floor].ToArray().Cast<bool>().Count(v=>v)},{coreMs:F3},{geometryMs:F3},{triangles},{root.GetComponentsInChildren<Renderer>().Length}"));
                    }
                    finally
                    {
                        camera.targetTexture=null;if(target!=null)Object.DestroyImmediate(target);if(image!=null)Object.DestroyImmediate(image);
                        Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lightObject);
                        DungeonPresentation.ClearOutput(root);Object.DestroyImmediate(root);Object.DestroyImmediate(host);DungeonPresentation.ReleaseTemplate(asset);
                    }
                }
                File.WriteAllText(directory+"/costs.csv",report.ToString());
            }
            finally {RenderSettings.ambientLight=ambient;}
        }
    }
}
