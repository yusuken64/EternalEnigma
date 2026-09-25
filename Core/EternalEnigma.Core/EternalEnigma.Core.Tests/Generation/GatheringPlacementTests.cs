using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class GatheringPlacementTests
{
    /// <summary>Creates a rectangular room with all interior cells true and border cells false.</summary>
    private static GridLayer OpenRoom(int w, int h)
    {
        var cells = new bool[w, h];
        for (int x = 1; x < w - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                cells[x, y] = true;
            }
        }
        return new GridLayer(cells);
    }

    /// <summary>Chebyshev distance (max of absolute differences).</summary>
    private static int Cheb(GridPoint a, GridPoint b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    [Fact]
    public void SameInputsGiveSameSites()
    {
        var layer = OpenRoom(20, 20);
        var start = new GridPoint(2, 2);
        var stairs = new GridPoint(17, 17);
        var occupied = new List<GridPoint>();

        var result1 = GatheringPlacement.Place(layer, start, stairs, occupied, seed: 42, count: 3);
        var result2 = GatheringPlacement.Place(layer, start, stairs, occupied, seed: 42, count: 3);

        Assert.Equal(result1.Count, result2.Count);
        for (int i = 0; i < result1.Count; i++)
        {
            Assert.Equal(result1[i].Cell, result2[i].Cell);
            Assert.Equal(result1[i].Kind, result2[i].Kind);
            Assert.Equal(result1[i].Roll, result2[i].Roll);
        }
    }

    [Fact]
    public void DifferentAttemptsCanDiffer()
    {
        var layer = OpenRoom(20, 20);
        var start = new GridPoint(2, 2);
        var stairs = new GridPoint(17, 17);
        var occupied = new List<GridPoint>();
        bool foundDifference = false;

        for (int seed = 1; seed <= 20; seed++)
        {
            var result0 = GatheringPlacement.Place(layer, start, stairs, occupied, seed: seed, count: 3, attempt: 0);
            var result1 = GatheringPlacement.Place(layer, start, stairs, occupied, seed: seed, count: 3, attempt: 1);

            if (!SequenceEqual(result0, result1))
            {
                foundDifference = true;
                break;
            }
        }

        Assert.True(foundDifference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(123)]
    public void SitesAvoidPathStartStairsAndOccupied(int seed)
    {
        var layer = OpenRoom(24, 16);
        var start = new GridPoint(2, 2);
        var stairs = new GridPoint(21, 13);
        var occupied = new List<GridPoint> { new GridPoint(10, 10), new GridPoint(5, 8) };
        var requiredPath = GatheringPlacement.RequiredPath(layer, start, stairs);

        var result = GatheringPlacement.Place(layer, start, stairs, occupied, seed: seed, count: 16);

        foreach (var site in result)
        {
            // Not in required path
            Assert.DoesNotContain(site.Cell, requiredPath);

            // Not start or stairs
            Assert.NotEqual(start, site.Cell);
            Assert.NotEqual(stairs, site.Cell);

            // Not in occupied
            Assert.DoesNotContain(site.Cell, occupied);

            // Must be a room
            Assert.True(GridSight.IsRoom(layer, site.Cell));

            // Must satisfy minimum distance from start
            Assert.True(Cheb(site.Cell, start) >= GatheringPlacement.MinStartDistance);
        }
    }

    [Fact]
    public void SitesNeverTouch()
    {
        var layer = OpenRoom(30, 30);
        var start = new GridPoint(2, 2);
        var stairs = new GridPoint(27, 27);
        var occupied = new List<GridPoint>();

        var result = GatheringPlacement.Place(layer, start, stairs, occupied, seed: 5, count: 16);

        for (int i = 0; i < result.Count; i++)
        {
            for (int j = i + 1; j < result.Count; j++)
            {
                Assert.True(Cheb(result[i].Cell, result[j].Cell) > 1);
            }
        }
    }

    [Fact]
    public void ZeroCountGivesEmpty()
    {
        var layer = OpenRoom(20, 20);
        var start = new GridPoint(2, 2);
        var stairs = new GridPoint(17, 17);
        var occupied = new List<GridPoint>();

        var result = GatheringPlacement.Place(layer, start, stairs, occupied, seed: 42, count: 0);

        Assert.Empty(result);
    }

    [Fact]
    public void InvalidArgumentsThrow()
    {
        var layer = OpenRoom(20, 20);
        var start = new GridPoint(2, 2);
        var stairs = new GridPoint(17, 17);
        var occupied = new List<GridPoint>();

        // count -1 throws
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GatheringPlacement.Place(layer, start, stairs, occupied, seed: 42, count: -1));

        // count 17 (> MaxCount of 16) throws
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GatheringPlacement.Place(layer, start, stairs, occupied, seed: 42, count: 17));

        // null floor throws
        Assert.Throws<ArgumentNullException>(() =>
            GatheringPlacement.Place(null!, start, stairs, occupied, seed: 42, count: 3));

        // null occupied throws
        Assert.Throws<ArgumentNullException>(() =>
            GatheringPlacement.Place(layer, start, stairs, null!, seed: 42, count: 3));

        // attempt -1 throws
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GatheringPlacement.Place(layer, start, stairs, occupied, seed: 42, count: 3, attempt: -1));
    }

    [Fact]
    public void TooFewCandidatesReturnsFewer()
    {
        var layer = OpenRoom(6, 6);
        var start = new GridPoint(1, 1);
        var stairs = new GridPoint(4, 4);
        var occupied = new List<GridPoint>();

        var result = GatheringPlacement.Place(layer, start, stairs, occupied, seed: 42, count: 16);

        // The 6x6 room has only a 4x4 interior (cells 1-4 in each dimension)
        // With spacing of Cheb <= 1 around each site, we can't fit 16 sites
        Assert.True(result.Count < 16);
    }

    [Fact]
    public void KindsAreDefined()
    {
        var layer = OpenRoom(20, 20);
        var start = new GridPoint(2, 2);
        var stairs = new GridPoint(17, 17);
        var occupied = new List<GridPoint>();

        for (int seed = 0; seed < 50; seed++)
        {
            var result = GatheringPlacement.Place(layer, start, stairs, occupied, seed: seed, count: 3);

            foreach (var site in result)
            {
                Assert.True(Enum.IsDefined(typeof(GatheringKind), site.Kind),
                    $"Kind {site.Kind} for seed {seed} is not a defined GatheringKind value");
            }
        }
    }

    /// <summary>Helper to compare two GatheringSite sequences for equality.</summary>
    private static bool SequenceEqual(IReadOnlyList<GatheringSite> a, IReadOnlyList<GatheringSite> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i])) return false;
        }
        return true;
    }
}
