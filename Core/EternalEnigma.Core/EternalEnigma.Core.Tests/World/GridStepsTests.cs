using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.World;

public sealed class GridStepsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DiagonalPolicyPreservesEachMode(bool horizontalOpen, bool verticalOpen)
    {
        var map = new bool[2, 2];
        map[0, 0] = map[1, 1] = true;
        map[1, 0] = horizontalOpen;
        map[0, 1] = verticalOpen;

        bool Walkable(GridPoint cell) => cell.X >= 0 && cell.X < 2 && cell.Y >= 0 && cell.Y < 2 && map[cell.X, cell.Y];
        var to = new GridPoint(1, 1);

        // RequireOpenSides succeeds only if both sides are open
        Assert.Equal(horizontalOpen && verticalOpen,
            GridSteps.CanStep(new GridPoint(0, 0), to, Walkable, DiagonalRule.RequireOpenSides));

        // AllowCornerCutting always succeeds (both corners are walkable)
        Assert.True(GridSteps.CanStep(new GridPoint(0, 0), to, Walkable, DiagonalRule.AllowCornerCutting));
    }

    [Fact]
    public void ZeroLengthStepReturnsFalseForBothRules()
    {
        bool Walkable(GridPoint cell) => true;

        Assert.False(GridSteps.CanStep(new GridPoint(0, 0), new GridPoint(0, 0), Walkable, DiagonalRule.RequireOpenSides));
        Assert.False(GridSteps.CanStep(new GridPoint(0, 0), new GridPoint(0, 0), Walkable, DiagonalRule.AllowCornerCutting));
    }

    [Fact]
    public void TwoCellStepReturnsFalseForBothRules()
    {
        bool Walkable(GridPoint cell) => true;

        // Step from (0,0) to (2,0) is 2 cells
        Assert.False(GridSteps.CanStep(new GridPoint(0, 0), new GridPoint(2, 0), Walkable, DiagonalRule.RequireOpenSides));
        Assert.False(GridSteps.CanStep(new GridPoint(0, 0), new GridPoint(2, 0), Walkable, DiagonalRule.AllowCornerCutting));
    }

    [Fact]
    public void EightWayHasEightEntriesInDocumentedOrder()
    {
        Assert.Equal(8, GridSteps.EightWay.Length);

        // Documented order: DownLeft, Down, DownRight, Left, Right, UpLeft, Up, UpRight
        var expected = new[]
        {
            new GridPoint(-1, -1), // DownLeft
            new GridPoint(0, -1),  // Down
            new GridPoint(1, -1),  // DownRight
            new GridPoint(-1, 0),  // Left
            new GridPoint(1, 0),   // Right
            new GridPoint(-1, 1),  // UpLeft
            new GridPoint(0, 1),   // Up
            new GridPoint(1, 1)    // UpRight
        };

        Assert.Equal(expected, GridSteps.EightWay);
    }

    [Fact]
    public void CardinalIsUpLeftRightDown()
    {
        // Documentation says: Up, Left, Right, Down
        var expected = new[]
        {
            new GridPoint(0, 1),   // Up
            new GridPoint(-1, 0),  // Left
            new GridPoint(1, 0),   // Right
            new GridPoint(0, -1)   // Down
        };

        Assert.Equal(expected, GridSteps.Cardinal);
    }

    [Fact]
    public void VisitOrderOn3x3OpenGridFromCenterVisits9CellsWithCenterFirst()
    {
        // 3x3 grid from (0,0) to (2,2), center is (1,1)
        bool Walkable(GridPoint cell) => cell.X >= 0 && cell.X <= 2 && cell.Y >= 0 && cell.Y <= 2;

        var visited = GridSearch.VisitOrder(new GridPoint(1, 1), cell =>
            GridSteps.Neighbors(cell, Walkable, DiagonalRule.AllowCornerCutting));

        Assert.Equal(9, visited.Count);
        Assert.Equal(new GridPoint(1, 1), visited[0]); // Center first
    }

    [Fact]
    public void NearestFindsDiagonalCellWithAllowCornerCutting()
    {
        var map = new bool[3, 3];
        map[0, 0] = map[1, 1] = map[2, 2] = true;
        bool Walkable(GridPoint cell) => cell.X >= 0 && cell.X < 3 && cell.Y >= 0 && cell.Y < 3 && map[cell.X, cell.Y];

        var nearest = GridSearch.Nearest(new GridPoint(0, 0),
            cell => GridSteps.Neighbors(cell, Walkable, DiagonalRule.AllowCornerCutting),
            c => c.Equals(new GridPoint(2, 2)));

        Assert.Equal(new GridPoint(2, 2), nearest);
    }

    [Fact]
    public void NearestReturnsNullWithRequireOpenSidesWhenBothOrthogonalNeighboursBlocked()
    {
        var map = new bool[3, 3];
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                map[x, y] = true;

        // Block the orthogonal neighbors of (0,0) going to (1,1)
        map[1, 0] = false;
        map[0, 1] = false;

        bool Walkable(GridPoint cell) => cell.X >= 0 && cell.X < 3 && cell.Y >= 0 && cell.Y < 3 && map[cell.X, cell.Y];

        var nearest = GridSearch.Nearest(new GridPoint(0, 0),
            cell => GridSteps.Neighbors(cell, Walkable, DiagonalRule.RequireOpenSides),
            c => c.Equals(new GridPoint(1, 1)));

        Assert.Null(nearest);
    }

    [Fact]
    public void PathFrom0To0Returns0()
    {
        var map = new bool[3, 1];
        map[0, 0] = map[1, 0] = map[2, 0] = true;
        bool Walkable(GridPoint cell) => cell.X >= 0 && cell.X < 3 && cell.Y >= 0 && cell.Y < 1 && map[cell.X, cell.Y];

        var path = GridSearch.Path(new GridPoint(0, 0), new GridPoint(0, 0),
            cell => GridSteps.Neighbors(cell, Walkable, DiagonalRule.AllowCornerCutting));

        Assert.Single(path);
        Assert.Equal(new GridPoint(0, 0), path[0]);
    }

    [Fact]
    public void PathOn3x1OpenStripFrom0To2Returns3Cells()
    {
        var map = new bool[3, 1];
        map[0, 0] = map[1, 0] = map[2, 0] = true;
        bool Walkable(GridPoint cell) => cell.X >= 0 && cell.X < 3 && cell.Y >= 0 && cell.Y < 1 && map[cell.X, cell.Y];

        var path = GridSearch.Path(new GridPoint(0, 0), new GridPoint(2, 0),
            cell => GridSteps.Neighbors(cell, Walkable, DiagonalRule.AllowCornerCutting));

        Assert.Equal(3, path.Count);
        Assert.Equal(new GridPoint(0, 0), path[0]);
        Assert.Equal(new GridPoint(1, 0), path[1]);
        Assert.Equal(new GridPoint(2, 0), path[2]);
    }

    [Fact]
    public void PathToUnreachableCellReturnsEmpty()
    {
        var map = new bool[3, 1];
        map[0, 0] = map[2, 0] = true;  // Only endpoints walkable
        map[1, 0] = false;              // Middle is blocked

        bool Walkable(GridPoint cell) => cell.X >= 0 && cell.X < 3 && cell.Y >= 0 && cell.Y < 1 && map[cell.X, cell.Y];

        var path = GridSearch.Path(new GridPoint(0, 0), new GridPoint(2, 0),
            cell => GridSteps.Neighbors(cell, Walkable, DiagonalRule.AllowCornerCutting));

        Assert.Empty(path);
    }
}
