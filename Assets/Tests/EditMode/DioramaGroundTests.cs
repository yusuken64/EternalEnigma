using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class DioramaGroundTests
    {
        [Test]
        public void AllBiomesHaveDistinctSourcesAndTenTexturesAreSeamless()
        {
            var style=PaintedGroundStyle.Load();Assert.That(style,Is.Not.Null);
            Assert.That(Enum.GetValues(typeof(OverworldBiome)).Cast<OverworldBiome>().Select(PaintedGroundStyle.Surface).Distinct().Count(),Is.EqualTo(8));
            Assert.That(style.DryGround.shader.isSupported,Is.True);
            Assert.That(ShaderUtil.ShaderHasError(style.DryGround.shader),Is.False);
            foreach(string name in PaintedGroundStyle.TextureNames.Concat(new[]{"Water"}))
            {
                string path="Assets/Art/Diorama/Ground/"+name+".png";
                var texture=new Texture2D(2,2);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(path));Assert.That(texture.width,Is.EqualTo(1024),name);Assert.That(texture.height,Is.EqualTo(1024),name);
                    for(int i=0;i<1024;i+=13)
                    {Assert.That(texture.GetPixel(i,0),Is.EqualTo(texture.GetPixel(i,1023)),name);Assert.That(texture.GetPixel(0,i),Is.EqualTo(texture.GetPixel(1023,i)),name);}
                }
                finally {Object.DestroyImmediate(texture);}
            }
        }
        [Test]
        public void HalfCellLatticeHasMatchingChunkSeamsAndDeterministicWeights()
        {
            var cells=new GroundSurface[34,3];for(int y=0;y<3;y++)for(int x=0;x<34;x++)cells[x,y]=y==1?GroundSurface.Dirt:GroundSurface.Grass;
            var root=new GameObject("Ground test");
            try
            {
                var a=PaintedGroundMesh.Build(root.transform,cells,PaintedGroundStyle.Load(),2,123,-4,-4);
                var b=PaintedGroundMesh.Build(root.transform,cells,PaintedGroundStyle.Load(),2,123,-4,-4);
                var am=a.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();var bm=b.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
                Assert.That(am.Length,Is.EqualTo(2));Assert.That(a.CellCount,Is.EqualTo(102));
                for(int i=0;i<am.Length;i++) {Assert.That(am[i].vertices,Is.EqualTo(bm[i].vertices));Assert.That(am[i].colors,Is.EqualTo(bm[i].colors));Assert.That(am[i].triangles,Is.EqualTo(bm[i].triangles));}
                var seamA=am[0].vertices.Select((v,i)=>(v,i)).Where(p=>Mathf.Approximately(p.v.x,56)).OrderBy(p=>p.v.y).ToArray();
                var seamB=am[1].vertices.Select((v,i)=>(v,i)).Where(p=>Mathf.Approximately(p.v.x,56)).OrderBy(p=>p.v.y).ToArray();
                Assert.That(seamA.Length,Is.EqualTo(7));Assert.That(seamA.Select(p=>p.v),Is.EqualTo(seamB.Select(p=>p.v)));
                Assert.That(seamA.Select(p=>am[0].colors[p.i]),Is.EqualTo(seamB.Select(p=>am[1].colors[p.i])));
                Assert.That(am[0].bounds.min.x,Is.EqualTo(-8));Assert.That(am[1].bounds.max.x,Is.EqualTo(60));
                var blend=PaintedGroundMesh.Weights(cells,10,1,123);Assert.That(blend.Sum(),Is.EqualTo(1).Within(.00001f));
                Assert.That(blend[(int)GroundSurface.Dirt],Is.InRange(.15f,.85f));Assert.That(blend[(int)GroundSurface.Grass],Is.GreaterThan(0));
                Assert.That(root.GetComponentsInChildren<Collider>(),Is.Empty);
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void TwcGroundOwnsExactlyOneSurfacePerCellWithWaterBridgeAndRoadPriority()
        {
            var host=new GameObject("Ground priority test");var creator=host.AddComponent<TileWorldCreator>();
            var asset=ScriptableObject.CreateInstance<TileWorldCreatorAsset>();GameObject world=null;
            try
            {
                asset.mapWidth=4;asset.mapHeight=1;asset.cellSize=2;creator.twcAsset=asset;
                var layer=new OverworldGroundLayer {Style=PaintedGroundStyle.Load(),guid=Guid.NewGuid(),layerName="Ground"};
                foreach(var input in layer.Inputs)
                {
                    var mask=new bool[4,1];
                    if(input.CoreLayer==OverworldLayers.Landscape(OverworldBiome.Grassland)||input.CoreLayer==OverworldLayers.Roads) for(int x=0;x<4;x++)mask[x,0]=true;
                    if(input.CoreLayer==OverworldLayers.TownFootprints)mask[1,0]=true;
                    if(input.CoreLayer==OverworldLayers.Water){mask[2,0]=true;mask[3,0]=true;}
                    if(input.CoreLayer==OverworldLayers.Bridges)mask[3,0]=true;
                    asset.mapBlueprintLayers.Add(new TileWorldCreatorAsset.BlueprintLayerData(input.BlueprintLayer,true){map=mask});
                }
                world=creator.worldObject;layer.Execute(creator,true);
                var output=world.GetComponentInChildren<PaintedGroundOutput>();Assert.That(output.CellCount,Is.EqualTo(4));
                foreach(var surface in new[]{GroundSurface.Dirt,GroundSurface.Cobble,GroundSurface.Water,GroundSurface.Bridge})Assert.That(output.SurfaceCells[(int)surface],Is.EqualTo(1),surface.ToString());
                Assert.That(output.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(3));
                Assert.That(world.GetComponentsInChildren<Collider>(),Is.Empty);
                layer.Execute(creator,true);Assert.That(world.GetComponentsInChildren<PaintedGroundOutput>().Length,Is.EqualTo(1));
            }
            finally {Object.DestroyImmediate(host);if(world!=null)Object.DestroyImmediate(world);Object.DestroyImmediate(asset);}
        }
        [Test]
        public void ArtIdentityChangesWhenGroundConfigurationChanges()
        {
            var obj=new GameObject("Cache identity test");var map=obj.AddComponent<CampaignOverworld>();
            var style=PaintedGroundStyle.Load();int revision=style.Revision;
            try {string before=DioramaArtIdentity.For(map);style.Revision++;Assert.That(DioramaArtIdentity.For(map),Is.Not.EqualTo(before));}
            finally {style.Revision=revision;Object.DestroyImmediate(obj);}
        }
    }
}
