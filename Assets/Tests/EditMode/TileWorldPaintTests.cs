using NUnit.Framework;
using TWC;
using TWC.Actions;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class TileWorldPaintTests
    {
        [TestCase(32, 80)] [TestCase(80, 32)] [TestCase(32, 48)] [TestCase(48, 32)]
        public void RuntimeResizePreservesPaintCoordinatesAndSourceCells(int width, int height)
        {
            var host = new GameObject("Paint resize test");
            var asset = ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
            try
            {
                var creator = host.AddComponent<TileWorldCreator>(); creator.twcAsset = asset;
                asset.mapWidth = asset.mapHeight = 32;
                var paint = new Paint(); paint.ModifyMap(3, 7, true, creator);
                asset.mapWidth = width; asset.mapHeight = height;
                var source = new bool[width, height]; source[width - 1, height - 1] = true;
                var result = paint.Execute(source, creator);
                Assert.That(result, Is.SameAs(source));
                Assert.That(result[3, 7], Is.True);
                Assert.That(result[6, 14], Is.False, "Resizing must not subdivide or move painted cells.");
                Assert.That(result[width - 1, height - 1], Is.True);
                paint.ModifyMap(width - 2, height - 2, true, creator);
                Assert.That(paint.Execute(new bool[width, height], creator)[width - 2, height - 2], Is.True);
                asset.mapWidth = asset.mapHeight = 12;
                result = paint.Execute(new bool[12, 12], creator);
                Assert.That(result[3, 7], Is.True);
                paint.FillMap(false, creator);
                Assert.That(paint.Execute(new bool[12, 12], creator)[3, 7], Is.False);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void SubdividedInputStillExpandsPaintCells()
        {
            var host = new GameObject("Subdivided paint test");
            var asset = ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
            try
            {
                var creator = host.AddComponent<TileWorldCreator>(); creator.twcAsset = asset;
                asset.mapWidth = 12; asset.mapHeight = 8;
                var paint = new Paint(); paint.ModifyMap(3, 2, true, creator);
                var result = paint.Execute(new bool[24, 16], creator);
                for (int x = 0; x < 24; x++)
                    for (int y = 0; y < 16; y++)
                        Assert.That(result[x, y], Is.EqualTo(x >= 6 && x <= 7 && y >= 4 && y <= 5));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void EmptyPaintCanBeClonedBeforeFirstEdit()
        {
            Assert.That(new Paint().Clone(), Is.TypeOf<Paint>());
        }
    }
}
