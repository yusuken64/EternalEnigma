using System.Linq;
using EternalEnigma.Core.Generation;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using TWC;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class EnvironmentKitTests
    {
        [Test]
        public void ImportedModelsMeetBudgetsAndShareEightPalettes()
        {
            var kit = EnvironmentKit.Load();
            Assert.That(kit.Models.Length, Is.EqualTo(68));
            Assert.That(kit.Palettes.Length, Is.EqualTo(8));
            foreach (var model in kit.Models)
            {
                Assert.That(model.Mesh.isReadable, Is.True, model.Id);
                Assert.That(model.Mesh.subMeshCount, Is.EqualTo(1), model.Id);
                Assert.That(model.Triangles, Is.EqualTo(model.Mesh.GetIndexCount(0) / 3), model.Id);
                Assert.That(model.Triangles, Is.LessThanOrEqualTo(300), model.Id);
                Assert.That(model.Mesh.bounds.max.z, Is.LessThan(.01f), model.Id);
            }
            Assert.That(kit.Palettes.Select(p => p.Props).Distinct().Count(), Is.EqualTo(8));
            // The active WebGL target loads the documented 1024 override.
            Assert.That(kit.Palettes.All(p => p.Props.mainTexture.width is 1024 or 2048 && p.Ground != null), Is.True);
            Assert.That(kit.Palettes.All(p => p.Buildings.mainTexture.width is 1024 or 2048), Is.True);
            foreach(var palette in kit.Palettes)
                Assert.That(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(palette.Buildings.mainTexture))).GetPlatformTextureSettings("WebGL").maxTextureSize,Is.EqualTo(1024));
            Assert.That(kit.Palettes.All(p => p.Props.mainTexture == p.Buildings.mainTexture), Is.True,
                "Compatible building/prop families share the same atlas in memory");
            Assert.That(kit.Mesh("SmartHouseEdge").uv.Distinct().Count(),Is.GreaterThan(8),"Facades need projected texture UVs, not point palette UVs");
        }

        [Test]
        public void TreePickerUsesBiomeSpecificSinglesAndGroupsWithoutGameplayRandom()
        {
            var kit=EnvironmentKit.Load();var picker=kit.TreeModels;var state=Random.state;
            foreach(EternalEnigma.Core.World.OverworldBiome biome in System.Enum.GetValues(typeof(EternalEnigma.Core.World.OverworldBiome)))
            {
                var choices=picker.Models.Where(c=>c.Matches(biome)).ToArray();
                var allowed=choices.Select(c=>TreeModelPicker.Id(c.Prefab)).ToArray();
                var results=Enumerable.Range(0,512).Select(i=>picker.Pick(biome,OverworldCosmetics.Hash(42,i,0))).Distinct().ToArray();
                CollectionAssert.IsSubsetOf(results,allowed);Assert.That(results.Length,Is.GreaterThanOrEqualTo(2),biome.ToString());
                foreach(var id in results)
                {
                    var model=DioramaCatalog.Load().Get(id);Assert.That(model,Is.Not.Null,id);
                    Assert.That(model.Tree,Is.True,id);Assert.That(kit.Triangles(id),Is.LessThanOrEqualTo(1100));
                    Assert.That(model.Width*model.Scale,Is.LessThanOrEqualTo(3.8f),id);
                    Assert.That(model.Height*model.Scale/DioramaScale.HeroHeight,Is.InRange(2.2f,3.11f),id);
                }
            }
            Assert.That(picker.Models.Any(c=>TreeModelPicker.Id(c.Prefab)=="SnowPine"&&c.Matches(EternalEnigma.Core.World.OverworldBiome.Tundra)),Is.True);
            Assert.That(picker.Models.Any(c=>TreeModelPicker.Id(c.Prefab)=="CharredTree"&&c.Matches(EternalEnigma.Core.World.OverworldBiome.Volcanic)),Is.True);
            Assert.That(Random.state,Is.EqualTo(state));
        }

        [Test]
        public void SavedTownContainsCurrentSmartHousePreviewInsteadOfLegacyClusters()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Town.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var creator=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TileWorldCreator>()).Single();
                Assert.That(creator.worldObject.GetComponentsInChildren<ClusterIdentifier>(),Is.Empty,"Legacy baked TWC clusters must be removed from the scene");
                var renderers=creator.worldObject.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers.Any(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="EternalEnigma/Diorama Vertex Lit"),Is.True);
                // TMP regenerates sign-label meshes on enable; only environment geometry is baked.
                Assert.That(creator.worldObject.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent("TextMeshPro")==null)
                    .All(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/Art/EnvironmentKit/TownPreview/")),Is.True);
            }
            finally {UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
        }

        [TestCase(42)]
        [TestCase(123)]
        public void CosmeticPlanIsRepeatableBudgetedAndAvoidsProtectedCells(int seed)
        {
            var campaign = CampaignGenerator.Generate(seed);
            var grid = OverworldGridGenerator.Generate(campaign);
            var kit = EnvironmentKit.Load();
            var state = Random.state;
            var plan = OverworldCosmetics.Plan(grid, kit);
            CollectionAssert.AreEqual(plan, OverworldCosmetics.Plan(grid, kit));
            Assert.That(Random.state, Is.EqualTo(state));
            Assert.That(plan.Count, Is.GreaterThan(0));
            var protect = OverworldCosmetics.Protected(grid);
            Assert.That(plan.All(p => !protect[p.X, p.Y]), Is.True);
            Assert.That(plan.Sum(p => kit.Triangles(p.Model)), Is.LessThanOrEqualTo(OverworldCosmetics.TriangleBudget));
            Assert.That(plan.GroupBy(p => (p.X / 32, p.Y / 32)).All(g => g.Count() <= OverworldCosmetics.PropsPerChunk), Is.True);
            foreach (var region in grid.RegionBiomes)
                Assert.That(OverworldGridGenerator.BiomeForRegion(campaign, region.Key), Is.EqualTo(region.Value));
        }

        [Test]
        public void WallGeometryConnectsForAllSixteenNeighborPatterns()
        {
            var set = AssetDatabase.LoadAssetAtPath<TileWorldCreator6TilesPreset>("Assets/Art/EnvironmentKit/SmartTiles/Wall.asset");
            for(int mask=0;mask<16;mask++)
            {
                var n=new NeighboursLocation {north=(mask&1)!=0,east=(mask&2)!=0,south=(mask&4)!=0,west=(mask&8)!=0};
                var (prefab,angle)=EnvironmentSmartTileLayer.SelectWall(set,n);
                var vertices=prefab.GetComponentInChildren<MeshFilter>().sharedMesh.vertices.Select(v=>Quaternion.Euler(0,0,angle)*v).ToArray();
                int geometry=(vertices.Any(v=>v.y>.499f)?1:0)|(vertices.Any(v=>v.x>.499f)?2:0)|
                    (vertices.Any(v=>v.y<-.499f)?4:0)|(vertices.Any(v=>v.x<-.499f)?8:0);
                Assert.That(geometry,Is.EqualTo(mask),$"Wall connection mismatch for {mask}: {prefab.name} at {angle}");
            }
        }

        [TestCase(SmartEnvironmentMasks.Mountains)]
        [TestCase(SmartEnvironmentMasks.Summits)]
        public void MountainRulePiecesMeetAtMatchingHeights(string layer)
        {
            var template=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Art/EnvironmentKit/SmartTiles/RuleExamples.asset");
            var asset=Object.Instantiate(template); var host=new GameObject("Seam validation");var creator=host.AddComponent<TileWorldCreator>();creator.twcAsset=asset;
            try
            {
                creator.ExecuteAllBlueprintLayers();
                var build=asset.mapBuildLayers.OfType<EnvironmentSmartTileLayer>().First(l=>l.layerName==layer);
                var map=creator.GetGeneratedBlueprintMap(build.assignedGenerationLayerGuid.ToString());
                var tiles=map.clusters.Values.SelectMany(c=>c.Values).ToDictionary(t=>new Vector2Int((int)t.position.x,(int)t.position.z));
                foreach(var pair in tiles) foreach(var offset in new[]{Vector2Int.right,Vector2Int.up})
                {
                    if(!tiles.TryGetValue(pair.Key+offset,out var neighbor)) continue;
                    var a=Geometry(pair.Value);var b=Geometry(neighbor);
                    foreach(float along in new[]{-.5f,-.25f,0,.25f,.5f})
                    {
                        var pa=offset.x==1?new Vector2(.5f,along):new Vector2(along,.5f);
                        var pb=offset.x==1?new Vector2(-.5f,along):new Vector2(along,-.5f);
                        Assert.That(Height(a,pa),Is.EqualTo(Height(b,pb)).Within(.001f),$"Cliff seam at {pair.Key}, {offset}, {along}");
                    }
                }
                (Vector3[],int[]) Geometry(TileData t)
                {
                    var set=build.QuarterTiles;
                    var prefab=t.tileType switch {TileData.TileType.edge=>set.edgeTile,TileData.TileType.exteriorCorner=>set.exteriorCornerTile,TileData.TileType.interiorCorner=>set.interiorCornerTile,_=>set.fillTile};
                    var mesh=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                    return(mesh.vertices.Select(v=>Quaternion.Euler(0,0,-t.yRotation)*v).ToArray(),mesh.triangles);
                }
            }
            finally {Object.DestroyImmediate(host);Object.DestroyImmediate(asset);}
        }
        [Test]
        public void SummitNoiseIsRepeatableClippedAndFormsSmallDensePatches()
        {
            var host=new GameObject("Summit noise validation");var creator=host.AddComponent<TileWorldCreator>();
            try
            {
                creator.currentSeed=42; var input=new bool[64,64];
                for(int y=8;y<56;y++) for(int x=8;x<56;x++) input[x,y]=true;
                var state=Random.state;var noise=new MountainTopNoise();var mask=noise.Execute(input,creator);
                CollectionAssert.AreEqual(mask,noise.Execute(input,creator));Assert.That(Random.state,Is.EqualTo(state));
                int count=0, joined=0;
                for(int y=0;y<64;y++) for(int x=0;x<64;x++) if(mask[x,y])
                {
                    count++;Assert.That(input[x,y],Is.True);
                    if(x<63&&mask[x+1,y]) joined++;
                    Assert.That(x<61&&mask[x+1,y]&&mask[x+2,y]&&mask[x+3,y],Is.False,"Patches must remain small");
                    Assert.That(y<61&&mask[x,y+1]&&mask[x,y+2]&&mask[x,y+3],Is.False,"Patches must remain small");
                }
                Assert.That(count,Is.InRange(500,1200));Assert.That(joined,Is.GreaterThan(150));
            }
            finally {Object.DestroyImmediate(host);}
        }
        private static float Height((Vector3[] v,int[] triangles) mesh,Vector2 p)
        {
            float result=float.NegativeInfinity;
            for(int i=0;i<mesh.triangles.Length;i+=3)
            {
                var a=mesh.v[mesh.triangles[i]];var b=mesh.v[mesh.triangles[i+1]];var c=mesh.v[mesh.triangles[i+2]];
                float d=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                if(Mathf.Abs(d)<.000001f)continue;
                float u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/d;
                float v=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/d;
                if(u>=-.0001f && v>=-.0001f && u+v<=1.0001f) result=Mathf.Max(result,-(u*a.z+v*b.z+(1-u-v)*c.z));
            }
            Assert.That(float.IsNegativeInfinity(result),Is.False,"Missing terrain surface at "+p);
            return result;
        }
    }
}

