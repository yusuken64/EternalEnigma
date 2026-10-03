using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class PaintedEnvironmentTests
    {
        [Test]
        public void PaintedTexturesHaveMipmapsFilteringAndPaddedAtlasUvs()
        {
            var kit=EnvironmentKit.Load();
            foreach(var palette in kit.Palettes)
            {
                foreach(var texture in new[]{palette.Buildings.mainTexture,palette.Ground.mainTexture})
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                    Assert.That(importer.mipmapEnabled,Is.True,texture.name);
                    Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Trilinear),texture.name);
                }
            }
            foreach(string id in new[]{"Tree","Rock","Wall","Gate","Mountain"})
            {
                var mesh=kit.Mesh(id);
                Assert.That(mesh.uv.Distinct().Count(),Is.GreaterThan(4),id+" must sample painted surface areas");
                foreach(var uv in mesh.uv)
                {
                    Assert.That(uv.x*4-Mathf.Floor(uv.x*4),Is.InRange(.064f,.936f),id);
                    Assert.That(uv.y*4-Mathf.Floor(uv.y*4),Is.InRange(.064f,.936f),id);
                }
            }
        }
        [Test]
        public void AdjacentRotatedSurfaceModulesShareUvCoordinatesAndMaterial()
        {
            var root=new GameObject("Painted seam test");
            try
            {
                var kit=EnvironmentKit.Load();var batch=new EnvironmentBatch(root.transform);
                batch.Add(kit.Mesh("Paving"),kit.Paving,Vector3.zero,Vector3.one*2.5f);
                batch.Add(kit.Mesh("Paving"),kit.Paving,new Vector3(2.5f,0,0),Vector3.one*2.5f,90);
                batch.Finish();
                var renderer=root.GetComponentInChildren<MeshRenderer>();
                // Unity may recreate a managed wrapper for the same native asset after reimport.
                Assert.That(renderer.sharedMaterial==kit.Paving,Is.True);
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.triangles.Length,Is.EqualTo(12));
                var v=mesh.vertices;var uv=mesh.uv;int pairs=0;
                for(int i=0;i<v.Length;i++)for(int j=i+1;j<v.Length;j++)if((v[i]-v[j]).sqrMagnitude<.00001f)
                { Assert.That((uv[i]-uv[j]).sqrMagnitude,Is.LessThan(.00001f));pairs++; }
                Assert.That(pairs,Is.GreaterThanOrEqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void TiledSourceImagesHaveMatchingOppositeBorders()
        {
            foreach(string path in Directory.GetFiles("Assets/Art/PaintedEnvironment","*.png").Where(p=>!p.EndsWith("DungeonProps.png")&&!p.EndsWith("BiomeDecorations.png")))
            {
                var texture=new Texture2D(2,2);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(path));Assert.That(texture.width,Is.EqualTo(1024),path);
                    for(int i=0;i<1024;i+=7)
                    {
                        Assert.That(texture.GetPixel(i,0),Is.EqualTo(texture.GetPixel(i,1023)),path+" horizontal seam");
                        Assert.That(texture.GetPixel(0,i),Is.EqualTo(texture.GetPixel(1023,i)),path+" vertical seam");
                    }
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }
    }
}
