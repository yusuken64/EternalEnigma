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
                Assert.That(layer.SurfaceMaterial, Is.Not.Null, layer.layerName);
                if (layer.layerName != SmartEnvironmentMasks.Coast)
                    Assert.That(layer.SurfaceMaterial.GetTag("EnvironmentProjection", false, ""), Is.EqualTo("Box"));
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
            var set = AssetDatabase.LoadAssetAtPath<TileWorldCreator6TilesPreset>("Assets/Art/EnvironmentKit/SmartTiles/SixTerrain/MountainSixTerrain.asset");
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
