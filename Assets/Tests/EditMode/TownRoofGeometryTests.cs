using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class TownRoofGeometryTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void NotchedRoofKeepsOneContinuousPitchAcrossTheRecess(bool rightNotch)
        {
            var body = NotchedBody(rightNotch);
            var profile = TownHouseTiles.RoofProfile(body, 1);
            Assert.That(profile.center, Is.EqualTo(5.5f));
            Assert.That(profile.halfWidth, Is.EqualTo(5.5f));
            int x = rightNotch ? 7 : 3;
            var lower = TownHouseTiles.RoofTile(x, x + 1, profile.center, profile.halfWidth, false, rise: profile.rise);
            var upper = TownHouseTiles.RoofTile(x - (rightNotch ? 0 : .1f), x + 1 + (rightNotch ? .1f : 0),
                profile.center, profile.halfWidth, false, rise: profile.rise, leftEdge: !rightNotch, rightEdge: rightNotch);
            try
            {
                for (float sample = .1f; sample < 1; sample += .2f)
                {
                    float below = SurfaceHeight(lower, x + sample, 1);
                    float above = SurfaceHeight(upper, x + sample, 0);
                    Assert.That(above, Is.EqualTo(below).Within(.0001f), "The narrower rear row must meet the full-width front row.");
                    Assert.That(above, Is.EqualTo(TownHouseTiles.RoofHeight(x + sample,
                        profile.center, profile.halfWidth, profile.rise)).Within(.0001f));
                }
            }
            finally { Object.DestroyImmediate(lower); Object.DestroyImmediate(upper); }
        }

        [TestCase(-1, 0)]
        [TestCase(1, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 1)]
        public void EveryExposedDirectionClosesFromTheWallToTheRoof(int dx, int dy)
        {
            var mesh = TownHouseTiles.RoofEdgeTile(3, dx, dy, 5.5f, 5.5f, .78f);
            try
            {
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    Assert.That(vertices[i].z, Is.EqualTo(-DioramaScale.Eaves / 2).Within(.0001f));
                    Assert.That(vertices[i + 3].z, Is.EqualTo(-DioramaScale.Eaves / 2).Within(.0001f));
                    foreach (var top in new[] { vertices[i + 1], vertices[i + 2] })
                        Assert.That(top.z, Is.EqualTo(TownHouseTiles.RoofHeight(top.x, 5.5f, 5.5f, .78f)).Within(.0001f));
                }
                foreach (var normal in mesh.normals)
                    Assert.That(Vector3.Dot(normal, new Vector3(dx, dy, 0)), Is.GreaterThan(.999f));
                Assert.That(mesh.bounds.size.z, Is.GreaterThan(.3f), "Recessed walls need full-height roof closures.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ShingleAndUnderlayTrianglesSplitExactlyAtTheRidge()
        {
            var mesh = TownHouseTiles.RoofTile(4.97f, 6.07f, 5.5f, 5.5f, true, true, true);
            try
            {
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                Assert.That(vertices.Any(v => Mathf.Abs(v.x - 5.5f) < .0001f), Is.True);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var xs = new[] { vertices[indices[i]].x, vertices[indices[i + 1]].x, vertices[indices[i + 2]].x };
                    Assert.That(xs.Min() < 5.4999f && xs.Max() > 5.5001f, Is.False,
                        "A triangle spanning both slopes would flatten the ridge.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RoofDetailsRemainSupportedWhenThePreferredPointFallsInTheNotch(bool rightNotch)
        {
            var body = NotchedBody(rightNotch);
            var preferred = new Vector2(rightNotch ? 9.5f : 1.5f, 10.5f);
            Assert.That(body.Contains(new GridPoint(Mathf.FloorToInt(preferred.x), Mathf.FloorToInt(preferred.y))), Is.False);
            var anchor = TownHouseTiles.RoofDetailAnchor(body, preferred);
            Assert.That(body.Contains(new GridPoint(Mathf.FloorToInt(anchor.x), Mathf.FloorToInt(anchor.y))), Is.True);
            Assert.That(anchor.x % 1, Is.EqualTo(.5f));
            Assert.That(anchor.y % 1, Is.EqualTo(.5f));
            Assert.That(TownHouseTiles.RoofDetailAnchor(body.Reverse(), preferred), Is.EqualTo(anchor),
                "Detail placement must be independent of footprint enumeration order.");
        }

        [Test]
        public void RoofStylesVaryRidgeHeightWithoutChangingTheWallHeight()
        {
            var body = NotchedBody(false);
            var rises = new HashSet<float>();
            for (uint style = 0; style < 3; style++)
            {
                var profile = TownHouseTiles.RoofProfile(body, style);
                rises.Add(profile.rise);
                Assert.That(TownHouseTiles.RoofHeight(0, profile.center, profile.halfWidth, profile.rise),
                    Is.EqualTo(-(DioramaScale.Eaves / 2 + .07f)).Within(.0001f));
                Assert.That(TownHouseTiles.RoofProfile(body.Reverse(), style), Is.EqualTo(profile));
            }
            Assert.That(rises.Count, Is.EqualTo(3));
        }

        private static HashSet<GridPoint> NotchedBody(bool right)
        {
            var body = new HashSet<GridPoint>();
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 11; x++)
                    if (y < 8 || (right ? x < 8 : x >= 3)) body.Add(new GridPoint(x, y));
            return body;
        }

        private static float SurfaceHeight(Mesh mesh, float x, float y)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            float result = float.PositiveInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                float area = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(area) < .000001f) continue;
                float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / area;
                float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / area;
                if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f)
                    result = Mathf.Min(result, u * a.z + v * b.z + (1 - u - v) * c.z);
            }
            Assert.That(float.IsPositiveInfinity(result), Is.False, "The roof must cover the sampled point.");
            return result;
        }
    }
}
