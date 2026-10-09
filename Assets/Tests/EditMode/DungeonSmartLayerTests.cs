using System;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class DungeonSmartLayerTests
    {
        static DungeonBoundaryPreset Kit => AssetDatabase.LoadAssetAtPath<DungeonBoundaryPreset>("Assets/Art/DungeonThemes/PolyartSmartTiles/Boundary.asset");
        static TileWorldCreatorAsset Template(bool throne) => AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/"+(throne?"DungeonThroneAsset":"DungeonAsset")+".asset");

        [TestCase(false),TestCase(true)]
        public void AuthoredLayersRoundTripAndThemesRespectParticipation(bool throne)
        {
            var original=Template(throne);var clone=DungeonPresentation.CloneTemplate(original);
            try
            {
                Assert.That(clone.mapBuildLayers.OfType<DungeonThemeTileLayer>().Count(),Is.EqualTo(3));
                CollectionAssert.AreEqual(original.mapBlueprintLayers.Select(l=>l.guid),clone.mapBlueprintLayers.Select(l=>l.guid));
                CollectionAssert.AreEqual(original.mapBuildLayers.Select(l=>l.guid),clone.mapBuildLayers.Select(l=>l.guid));
                var boundary=clone.mapBuildLayers.OfType<DungeonBoundaryLayer>().Single();
                Assert.That(boundary.SmartPreset,Is.SameAs(Kit));
                Assert.That(boundary.WallHeight,Is.EqualTo(Resources.Load<DungeonPickupPresentation>("DungeonThemes/PickupPresentation").HeroHeight*1.25f).Within(.00001f));
                var floor=clone.mapBuildLayers.OfType<DungeonThemeTileLayer>().Single(l=>l.Role==DungeonThemeRole.Floor);
                Assert.That(floor.IgnoreLayers,Does.Contain(clone.mapBlueprintLayers.Single(l=>l.layerName=="Carpet").guid.ToString()));
                floor.UseThemePreset=false;floor.active=false;floor.Offset=new Vector3(.1f,.2f,.03f);
                var preset=floor.Preset;var guid=floor.assignedGenerationLayerGuid;var ignore=floor.IgnoreLayers.ToArray();
                var extra=new TWCBuildLayer {guid=Guid.NewGuid(),layerName="Unrelated authored layer",active=true};clone.mapBuildLayers.Add(extra);
                var layers=clone.mapBuildLayers.ToArray();
                var catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
                foreach(var theme in catalog.Themes)
                {
                    catalog.Apply(clone,new DungeonVisualSelection {Biome=theme.Biome,Environment=theme.Environment,UseBiomePresentation=true},throne);
                    CollectionAssert.AreEqual(layers,clone.mapBuildLayers);
                    Assert.That(extra.active,Is.True);Assert.That(floor.active,Is.False);Assert.That(floor.Preset,Is.SameAs(preset));
                    Assert.That(floor.Offset,Is.EqualTo(new Vector3(.1f,.2f,.03f)));Assert.That(floor.assignedGenerationLayerGuid,Is.EqualTo(guid));
                    CollectionAssert.AreEqual(ignore,floor.IgnoreLayers);
                    var themedKit=throne?theme.ThroneSmartBoundary:theme.RegularSmartBoundary;
                    Assert.That(themedKit,Is.Not.Null,theme.Name);
                    Assert.That(boundary.SmartPreset,Is.SameAs(themedKit));
                    Assert.That(boundary.GroundPreset,Is.SameAs(theme.Floor));
                    Assert.That(themedKit.AuthoredHeight,Is.EqualTo(Kit.AuthoredHeight));
                    Assert.That(themedKit.Tiles,Is.SameAs(Kit.Tiles),"Theme adapters must share the tested connection geometry.");
                    if(themedKit!=Kit)
                        Assert.That(themedKit.MaterialOverride,Is.SameAs(theme.RegularBoundary.edgeTile.GetComponentInChildren<MeshRenderer>().sharedMaterial));
                }
                boundary.UseThemePreset=false;boundary.SmartPreset=Kit;boundary.WallHeight=3.7f;
                catalog.Apply(clone,new DungeonVisualSelection {Biome=OverworldBiome.Volcanic,Environment=DungeonEnvironmentKind.Outdoor},throne);
                Assert.That(boundary.SmartPreset,Is.SameAs(Kit));Assert.That(boundary.WallHeight,Is.EqualTo(3.7f));
                var duplicate=(DungeonThemeTileLayer)floor.Clone();
                Assert.That(duplicate.guid,Is.Not.EqualTo(floor.guid));Assert.That(duplicate.IgnoreLayers,Is.Not.SameAs(floor.IgnoreLayers));
                CollectionAssert.AreEqual(duplicate.IgnoreLayers,floor.IgnoreLayers);
            }
            finally {DungeonPresentation.ReleaseTemplate(clone);}
        }

        [Test]
        public void EveryNeighborCombinationHasRotationSafeConnectionsAndDiagonalFills()
        {
            int[] canonical={0,1,5,3,7,15};
            for(int pattern=0;pattern<256;pattern++)
            {
                var mask=new bool[3,3];mask[1,1]=true;
                int[] dx={0,1,0,-1,1,1,-1,-1},dy={1,0,-1,0,1,-1,-1,1};
                for(int i=0;i<8;i++)mask[1+dx[i],1+dy[i]]=(pattern&(1<<i))!=0;
                var (cardinal,diagonal)=DungeonBoundaryLayer.Neighbors(mask,1,1);
                Assert.That(cardinal,Is.EqualTo(pattern&15));Assert.That(diagonal,Is.EqualTo(pattern>>4));
                var (piece,turns)=DungeonBoundaryLayer.Classify(cardinal);
                Assert.That(DungeonBoundaryLayer.RotateMask(canonical[piece],turns),Is.EqualTo(cardinal));
            }
            Assert.That(DungeonBoundaryLayer.Neighbors(new bool[,]{{true}},0,0),Is.EqualTo((0,0)),"Map edges are disconnected, not wrapped.");
            var pieces=new[]{Kit.Tiles.singleTile,Kit.Tiles.deadEndTile,Kit.Tiles.straightTile,Kit.Tiles.cornerTile,Kit.Tiles.threeWayTile,Kit.Tiles.fourWayTile,Kit.SolidFill,Kit.QuadrantFill,Kit.ConcaveCorner}.Concat(Kit.StraightVariants);
            foreach(var prefab in pieces)
            {
                Assert.That(prefab,Is.Not.Null);Assert.That(prefab.GetComponentsInChildren<Collider>(),Is.Empty);
                var bounds=prefab.GetComponent<MeshFilter>().sharedMesh.bounds;
                Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(-1.00001f));Assert.That(bounds.max.x,Is.LessThanOrEqualTo(1.00001f));
                Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(-1.00001f));Assert.That(bounds.max.y,Is.LessThanOrEqualTo(1.00001f));
                Assert.That(bounds.min.z,Is.GreaterThanOrEqualTo(-Kit.AuthoredHeight-.00001f));Assert.That(bounds.max.z,Is.LessThanOrEqualTo(.00001f));
            }
            Assert.That(Kit.StraightVariants.Length,Is.EqualTo(2));
        }

        [Test]
        public void IndependentRebuildsReleaseMeshesHonorExclusionsAndKeepRandomState()
        {
            var host=new GameObject("Smart layer test");var root=new GameObject("Smart layer output");
            var creator=host.AddComponent<TileWorldCreator>();var asset=ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
            creator.worldObject=root;creator.twcAsset=asset;asset.cellSize=2;
            var mask=new bool[7,7];
            // Thick mass, inside corner, isolated cell and a thin strip ending at the map edge.
            for(int y=1;y<5;y++)for(int x=1;x<5;x++)mask[x,y]=true;
            mask[4,4]=false;mask[6,6]=true;for(int x=0;x<5;x++)mask[x,0]=true;
            var blueprint=new TileWorldCreatorAsset.BlueprintLayerData("Boundary",true){map=mask};
            var excluded=new TileWorldCreatorAsset.BlueprintLayerData("Exclude",true){map=new bool[7,7]};excluded.map[1,1]=true;
            asset.mapBlueprintLayers=new(){blueprint,excluded};
            var layer=new DungeonBoundaryLayer {guid=Guid.NewGuid(),assignedGenerationLayerGuid=blueprint.guid,layerName="Boundary",SmartPreset=Kit,IgnoreLayers=new[]{excluded.guid.ToString()}};
            asset.mapBuildLayers=new(){layer};
            var untouched=new GameObject("Unrelated output");untouched.transform.SetParent(root.transform);
            try
            {
                creator.SetCustomRandomSeed(913);var before=UnityEngine.Random.state;
                creator.ExecuteBuildLayer(layer.guid,true);
                Assert.That(UnityEngine.Random.state,Is.EqualTo(before));
                var old=root.GetComponentsInChildren<EnvironmentMeshOwner>().SelectMany(o=>o.Meshes).ToArray();
                Assert.That(old,Is.Not.Empty);
                Assert.That(root.GetComponentsInChildren<BiomeDecorationSurfaceSet>().Sum(s=>s.Faces.Count),Is.GreaterThan(0));
                var vertices=old.SelectMany(m=>m.vertices).ToArray();
                foreach(var vertex in vertices)
                {
                    // Border vertices can belong to either incident closed cell.
                    bool inside=false;
                    foreach(float dx in new[]{-.0001f,.0001f})foreach(float dy in new[]{-.0001f,.0001f})
                    {int x=Mathf.FloorToInt((vertex.x+dx)/2),y=Mathf.FloorToInt((vertex.y+dy)/2);inside|=DungeonThemeTileLayer.At(mask,x,y)&&!DungeonThemeTileLayer.At(excluded.map,x,y);}
                    Assert.That(inside,Is.True,"Boundary protrudes into a walkable/excluded cell: "+vertex);
                }
                creator.ExecuteBuildLayer(layer.guid,false);
                Assert.That(old.All(m=>m==null),Is.True);Assert.That(untouched,Is.Not.Null);
                var next=root.GetComponentsInChildren<EnvironmentMeshOwner>().SelectMany(o=>o.Meshes).ToArray();
                CollectionAssert.AreEqual(vertices,next.SelectMany(m=>m.vertices).ToArray(),"Variant selection must be stable.");
                root.SetActive(false);root.SetActive(true);
                layer.active=false;creator.ExecuteAllBuildLayers(true);
                Assert.That(next.All(m=>m==null),Is.True);Assert.That(root.GetComponentsInChildren<LayerIdentifier>(),Is.Empty);
                Assert.That(untouched,Is.Not.Null);Assert.That(mask[1,1],Is.True,"Exclusions never mutate blueprint output.");
            }
            finally {DungeonPresentation.ClearOutput(root);Object.DestroyImmediate(root);Object.DestroyImmediate(host);Object.DestroyImmediate(asset);}
        }

        [Test]
        public void TallBoundaryCutsForegroundAndReleasesItsMaskOnIndependentRebuild()
        {
            var host=new GameObject("Cutaway test");var root=new GameObject("Legacy preview root");
            root.transform.position=Vector3.back*1.51f;root.transform.localScale=new Vector3(1,1,3.35f);
            var creator=host.AddComponent<TileWorldCreator>();var asset=ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
            creator.worldObject=root;creator.twcAsset=asset;asset.cellSize=2;
            var walls=new TileWorldCreatorAsset.BlueprintLayerData("Walls",true){map=new bool[7,7]};
            var floors=new TileWorldCreatorAsset.BlueprintLayerData("Floor",true){map=new bool[7,7]};
            for(int x=0;x<7;x++){walls.map[x,0]=true;walls.map[x,3]=true;floors.map[x,1]=true;floors.map[x,2]=true;}
            asset.mapBlueprintLayers=new(){walls,floors};
            var boundary=new DungeonBoundaryLayer{guid=Guid.NewGuid(),assignedGenerationLayerGuid=walls.guid,SmartPreset=Kit};
            var floor=new DungeonThemeTileLayer{guid=Guid.NewGuid(),assignedGenerationLayerGuid=floors.guid,Role=DungeonThemeRole.Floor};
            asset.mapBuildLayers=new(){boundary,floor};
            try
            {
                creator.ExecuteBuildLayer(boundary.guid,true);
                var cutaway=root.GetComponentInChildren<DungeonBoundaryCutaway>();Assert.That(cutaway,Is.Not.Null);
                Assert.That(cutaway.Cuts(new Vector3(5,1,-3),new Vector3(0,12,14)),Is.True);
                Assert.That(cutaway.Cuts(new Vector3(5,7,-3),new Vector3(0,12,14)),Is.False);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Min(r=>r.bounds.min.z),Is.EqualTo(DungeonPresentation.GroundPlaneZ-boundary.WallHeight).Within(.001f),"Legacy root must not multiply authored wall height.");
                var owner=root.GetComponentInChildren<EnvironmentMeshOwner>();
                Assert.That(owner.Materials.All(m=>m.shader.isSupported),Is.True);
                var texture=cutaway.FloorMask;var materials=owner.Materials.ToArray();
                Assert.That(materials,Is.Not.Empty);Assert.That(root.GetComponentInChildren<BiomeDecorationSurfaceSet>().Faces,Is.Not.Empty);
                AssertForegroundPixel(root,materials,texture);
                creator.ExecuteBuildLayer(boundary.guid,true);
                Assert.That(texture==null,Is.True);Assert.That(materials.All(m=>m==null),Is.True);
                Assert.That(floors.map[2,1],Is.True,"Cutaway must not alter gameplay floor masks.");
                System.Array.Clear(walls.map,0,walls.map.Length);creator.ExecuteBuildLayer(boundary.guid,true);
                Assert.That(root.GetComponentInChildren<DungeonBoundaryCutaway>(),Is.Null,"An empty boundary has no cutaway resources.");
            }
            finally {DungeonPresentation.ClearOutput(root);Object.DestroyImmediate(root);Object.DestroyImmediate(host);Object.DestroyImmediate(asset);}
        }

        static void AssertForegroundPixel(GameObject root,Material[] walls,Texture2D floorMask)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
            var marker=GameObject.CreatePrimitive(PrimitiveType.Quad);marker.layer=30;
            marker.transform.position=new Vector3(5,3,-.02f);marker.transform.localScale=Vector3.one*.5f;
            var markerMaterial=new Material(Shader.Find("Unlit/Color")){color=Color.magenta};marker.GetComponent<Renderer>().sharedMaterial=markerMaterial;
            var host=new GameObject("Cutaway GPU test");var camera=host.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=7;camera.aspect=1;camera.cullingMask=1<<30;
            camera.transform.position=new Vector3(5,-9,-14);camera.transform.LookAt(new Vector3(5,3,0));
            var target=RenderTexture.GetTemporary(128,128,24);var old=RenderTexture.active;var pixel=new Texture2D(1,1);
            bool asynchronous=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            Color Sample(){camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixel.ReadPixels(new Rect(64,64,1,1),0,0);pixel.Apply();return pixel.GetPixel(0,0);}
            try
            {
                var visible=Sample();Assert.That(visible.r,Is.GreaterThan(.9f));Assert.That(visible.b,Is.GreaterThan(.9f));Assert.That(visible.g,Is.LessThan(.1f));
                foreach(var material in walls)material.SetTexture("_FloorMask",Texture2D.blackTexture);
                var blocked=Sample();Assert.That(Vector4.Distance(visible,blocked),Is.GreaterThan(.3f),"The marker is geometrically behind the wall; the real shader must reveal it.");
            }
            finally
            {
                foreach(var material in walls)material.SetTexture("_FloorMask",floorMask);
                ShaderUtil.allowAsyncCompilation=asynchronous;
                camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixel);Object.DestroyImmediate(host);Object.DestroyImmediate(marker);Object.DestroyImmediate(markerMaterial);
            }
        }

        [Test]
        public void DungeonPickupsHaveIndividualReferenceRatiosAndGroundWithoutMovingRoots()
        {
            var table=Resources.Load<DungeonPickupPresentation>("DungeonThemes/PickupPresentation");
            Assert.That(table.Items.Length,Is.GreaterThan(130));
            Assert.That(table.HeroRigPrefab,Is.EqualTo("Assets/Prefabs/Dungeon/Ally.prefab"));
            Assert.That(table.HeroHeight,Is.GreaterThan(3),"Use the assembled dungeon hero, not the smaller town model.");
            var authoring=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("DungeonPickupAuthoring")).First(t=>t!=null);
            var cameraObject=new GameObject("Pickup measurement camera");var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=table.OrthographicSize;camera.aspect=1.6f;
            camera.transform.position=table.CameraOffset;camera.transform.LookAt(Vector3.zero);
            try
            {
            foreach(var entry in table.Items)
            {
                if(entry.Size==DungeonPickupSize.Chest)Assert.That(entry.WorldHeightRatio,Is.EqualTo(.5f).Within(.01f),entry.Name);
                else Assert.That(entry.ProjectedRatio,Is.InRange(entry.Size==DungeonPickupSize.Elongated?.5f:1f/3,entry.Size==DungeonPickupSize.Elongated?2f/3:.5f),entry.Name);
                var obj=Object.Instantiate(entry.Prefab);obj.transform.position=new Vector3(12,18,0);
                obj.transform.localScale=Vector3.Scale(obj.transform.localScale,entry.ParentScale);
                try
                {
                    var points=(Vector3[])authoring.GetMethod("Points").Invoke(null,new object[]{obj});
                    var projected=(Vector2[])authoring.GetMethod("Project").Invoke(null,new object[]{camera,points});
                    float diameter=(float)authoring.GetMethod("Diameter").Invoke(null,new object[]{projected});
                    Assert.That(diameter/table.HeroScreenHeight,Is.EqualTo(entry.ProjectedRatio).Within(.001f),entry.Name+" prefab changed after calibration");
                    var before=obj.transform.position;var rootScale=obj.transform.localScale;
                    DungeonPresentation.GroundFloorObject(obj.transform);
                    Assert.That(obj.transform.position,Is.EqualTo(before),entry.Name);Assert.That(obj.transform.localScale,Is.EqualTo(rootScale),entry.Name);
                    Assert.That(obj.GetComponentInChildren<DungeonPickupFootprint>().GroundZ,Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.0001f),entry.Name);
                    var renderers=obj.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    Assert.That(Mathf.Max(bounds.size.x,bounds.size.y),Is.LessThanOrEqualTo(1.641f),entry.Name);
                }
                finally {Object.DestroyImmediate(obj);}
            }
            }
            finally {Object.DestroyImmediate(cameraObject);}
        }
    }
}
