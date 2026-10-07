using System.Linq;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class OverworldSixTerrainTests
    {
        [Test]
        public void AuthoredCliffsAndConcaveAdaptersMeetAlongEveryBorderSample()
        {
            var source=DioramaCatalog.Load().CliffGrid;
            var set=AssetDatabase.LoadAssetAtPath<TileWorldCreator6TilesPreset>("Assets/Art/Diorama/Cliffs/CliffSixTerrain.asset");
            var meshes=new System.Collections.Generic.Dictionary<Vector2Int,Vector3[]>();
            bool At(int x,int y)=>x>=0&&y>=0&&x<9&&y<9&&OverworldCosmetics.Hash(51,x,y)%5!=0;
            for(int y=0;y<9;y++)for(int x=0;x<9;x++)if(At(x,y))
            {
                int n=(At(x,y+1)?1:0)|(At(x+1,y)?2:0)|(At(x,y-1)?4:0)|(At(x-1,y)?8:0);
                int d=(At(x+1,y+1)?1:0)|(At(x+1,y-1)?2:0)|(At(x-1,y-1)?4:0)|(At(x-1,y+1)?8:0);
                bool concave=((n&3)==3&&(d&1)==0)||((n&6)==6&&(d&2)==0)||((n&12)==12&&(d&4)==0)||((n&9)==9&&(d&8)==0);
                if(concave){var mesh=DioramaCliffGeometry.Adapt(source,n,d);meshes[new(x,y)]=mesh.vertices;Object.DestroyImmediate(mesh);}
                else {var selected=EnvironmentSmartTileLayer.SelectSixTerrainPrefab(set,n);meshes[new(x,y)]=selected.prefab.GetComponentInChildren<MeshFilter>().sharedMesh.vertices.Select(v=>Quaternion.Euler(0,0,selected.angle)*v).ToArray();}
            }
            foreach(var cell in meshes)foreach(var direction in new[]{Vector2Int.up,Vector2Int.right})
                if(meshes.TryGetValue(cell.Key+direction,out var neighbor))for(int i=0;i<=8;i++)
                {
                    float t=i/8f-.5f;
                    var a=direction.x==1?new Vector2(.5f,t):new Vector2(t,.5f);
                    var b=direction.x==1?new Vector2(-.5f,t):new Vector2(t,-.5f);
                    float Height(Vector3[] vertices,Vector2 p)=>vertices.First(v=>Vector2.Distance(v,p)<.001f).z;
                    Assert.That(Height(cell.Value,a),Is.EqualTo(Height(neighbor,b)).Within(.001f),$"{cell.Key} to {direction} at {t}");
                }
        }
        [Test]
        public void MountainCornersRespondToMissingDiagonalAndFaceTheCamera()
        {
            var connected = EnvironmentSmartTileLayer.SixTerrainMesh(15, 15, false, .42f);
            var hollow = EnvironmentSmartTileLayer.SixTerrainMesh(15, 14, false, .42f);
            try
            {
                Assert.That(connected.triangles.Length / 3, Is.EqualTo(8));
                Assert.That(connected.vertices.Where(v => v.x == .5f && v.y == .5f).All(v => v.z == -.42f), Is.True);
                Assert.That(hollow.vertices.Where(v => v.x == .5f && v.y == .5f).All(v => v.z == 0), Is.True);
                Assert.That(hollow.vertices.Where(v => v.x == 0 && v.y == 0).All(v => v.z == -.42f), Is.True);
                Assert.That(connected.normals.All(n => n.z < 0), Is.True);
            }
            finally { Object.DestroyImmediate(connected); Object.DestroyImmediate(hollow); }
        }

        [Test]
        public void WaterDrawsOnlyShoreEdgesAndDiagonalInnerCorners()
        {
            var open = EnvironmentSmartTileLayer.SixTerrainMesh(15, 15, true, .42f);
            var corner = EnvironmentSmartTileLayer.SixTerrainMesh(15, 14, true, .42f);
            var edge = EnvironmentSmartTileLayer.SixTerrainMesh(14, 15, true, .42f);
            try
            {
                Assert.That(open.triangles, Is.Empty);
                Assert.That(corner.triangles.Length / 3, Is.EqualTo(2));
                Assert.That(edge.triangles.Length / 3, Is.EqualTo(2));
                Assert.That(corner.normals.All(n => n.z < 0), Is.True);
                Assert.That(edge.uv.Select(v => v.y), Is.EqualTo(new[] { 0f, 0f, 1f, 1f }), "Shore UV is distance into water, independent of world direction.");
            }
            finally { Object.DestroyImmediate(open); Object.DestroyImmediate(corner); Object.DestroyImmediate(edge); }
        }

        [Test]
        public void CampaignTerrainUsesSixPiecePrefabsOnEveryMountainAndWaterLayer()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Overworld/CampaignTerrain.asset");
            var layers = asset.mapBuildLayers.OfType<EnvironmentSmartTileLayer>()
                .Where(l => l.layerName.StartsWith("Smart/Mountains") ||
                    l.layerName == SmartEnvironmentMasks.Summits || l.layerName == SmartEnvironmentMasks.Coast).ToArray();
            Assert.That(layers.Length, Is.EqualTo(5));
            foreach (var layer in layers)
            {
                Assert.That(layer.active, Is.True, layer.layerName);
                Assert.That(layer.QuarterTiles, Is.Null, layer.layerName);
                Assert.That(layer.WallTiles, Is.Not.Null, layer.layerName);
                if (layer.layerName != SmartEnvironmentMasks.Coast)
                {
                    Assert.That(layer.DioramaCliffs,Is.True);Assert.That(layer.HeightScale,Is.EqualTo(1));
                    Assert.That(DioramaCatalog.Load().Cliffs.Length,Is.EqualTo(8));
                    Assert.That(DioramaCatalog.Load().Cliffs.All(m=>m.shader.name=="EternalEnigma/Diorama Cliff"),Is.True);
                }
                else Assert.That(layer.SurfaceMaterial, Is.Not.Null, layer.layerName);
                Assert.That(asset.mapBlueprintLayers.Any(b => b.guid == layer.assignedGenerationLayerGuid), Is.True, layer.layerName);
                var set = layer.WallTiles;
                foreach (var piece in new[] { set.singleTile, set.deadEndTile, set.straightTile,
                    set.cornerTile, set.threeWayTile, set.fourWayTile })
                {
                    Assert.That(piece, Is.Not.Null, layer.layerName);
                    var filter = piece.GetComponentInChildren<MeshFilter>();
                    Assert.That(filter?.sharedMesh, Is.Not.Null, piece.name);
                    Assert.That(Vector3.Dot(filter.transform.TransformDirection(Vector3.back), Vector3.up), Is.GreaterThan(.999f), "TWC prefab ground is XZ and height is +Y.");
                }
            }
        }

        [Test]
        public void AllSixteenCardinalMasksRotateTheirPrefabConnectionsCorrectly()
        {
            var set = AssetDatabase.LoadAssetAtPath<TileWorldCreator6TilesPreset>("Assets/Art/Diorama/Cliffs/CliffSixTerrain.asset");
            Vector2[] sides = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int mask = 0; mask < 16; mask++)
            {
                var selected = EnvironmentSmartTileLayer.SelectSixTerrainPrefab(set, mask);
                var vertices = selected.prefab.GetComponentInChildren<MeshFilter>().sharedMesh.vertices
                    .Select(v => Quaternion.Euler(0, 0, selected.angle) * v).ToArray();
                for (int side = 0; side < 4; side++)
                {
                    var midpoint = sides[side] * .5f;
                    var height = vertices.First(v => Vector2.Distance(new Vector2(v.x,v.y), midpoint) < .001f).z;
                    Assert.That(height, Is.EqualTo((mask & (1 << side)) != 0 ? -.42f : 0).Within(.001f), $"Mask {mask}, side {side}");
                }
            }
        }
    }
}
