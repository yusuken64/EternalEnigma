using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GridMovementTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void DiagonalPolicyPreservesEachMode(bool horizontalOpen, bool verticalOpen)
    {
        var map = new bool[2, 2];
        map[0, 0] = map[1, 1] = true;
        map[1, 0] = horizontalOpen;
        map[0, 1] = verticalOpen;
        bool Walkable(Vector3Int cell) => GridMovement.IsWalkable(map, cell);
        var to = new Vector3Int(1, 1);
        Assert.That(GridMovement.CanStep(Vector3Int.zero, to, Walkable), Is.EqualTo(horizontalOpen && verticalOpen));
        Assert.That(GridMovement.CanStep(Vector3Int.zero, to, Walkable, DiagonalMovement.AllowCornerCutting), Is.True);
    }

    [Test]
    public void BoundsMissingTerrainAndInvalidStepsAreRejected()
    {
        var map = new bool[3, 2];
        map[0, 0] = map[1, 0] = map[2, 0] = true;
        bool Walkable(Vector3Int cell) => GridMovement.IsWalkable(map, cell);
        Assert.That(GridMovement.IsWalkable(null, Vector3Int.zero), Is.False);
        foreach (var cell in new[] { new Vector3Int(-1, 0), new Vector3Int(3, 0), new Vector3Int(0, 2), new Vector3Int(0, 0, 1) })
            Assert.That(Walkable(cell), Is.False);
        Assert.That(GridMovement.CanStep(Vector3Int.zero, Vector3Int.zero, Walkable), Is.False);
        Assert.That(GridMovement.CanStep(Vector3Int.zero, new Vector3Int(2, 0), Walkable), Is.False);
        Assert.That(GridMovement.GetNeighbors(Vector3Int.zero, Walkable).ToArray(), Is.EqualTo(new[] { Vector3Int.right }));
    }

    [Test]
    public void OverworldAdapterPreservesCornerCuttingAndRejectsUninitializedMap()
    {
        var owner = new GameObject();
        owner.SetActive(false);
        try
        {
            var map = owner.AddComponent<WalkableMap>();
            Assert.That(map.CanWalkTo(Vector3Int.zero, Vector3Int.right), Is.False);
            var terrain = new bool[2, 2];
            terrain[0, 0] = terrain[1, 1] = true;
            typeof(WalkableMap).GetField("_walkableMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(map, terrain);
            Assert.That(map.CanWalkTo(Vector3Int.zero, new Vector3Int(1, 1)), Is.True);
            Assert.That(map.CanWalkTo(Vector3Int.zero, Vector3Int.right), Is.False);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void SearchesUseTheSuppliedGridAndAgreeOnBlockedCorners()
    {
        var astar = new AStar.Node[2, 2];
        var bfs = new BFS.Node[2, 2];
        astar[0, 0] = new AStar.Node(0, 0, true, 0);
        astar[1, 1] = new AStar.Node(1, 1, true, 0);
        bfs[0, 0] = new BFS.Node(0, 0);
        bfs[1, 1] = new BFS.Node(1, 1);
        Assert.That(AStar.FindPath(astar, astar[0, 0], astar[1, 1]), Is.Null);
        Assert.That(BFS.FindPath(bfs, bfs[0, 0], n => n == bfs[1, 1]), Is.Empty);
        Assert.That(AStar.FindPath(astar, astar[0, 0], astar[1, 1], DiagonalMovement.AllowCornerCutting).Count, Is.EqualTo(1));
        Assert.That(BFS.FindPath(bfs, bfs[0, 0], n => n == bfs[1, 1], DiagonalMovement.AllowCornerCutting).Last(), Is.SameAs(bfs[1, 1]));
        Assert.That(BFS.FindPath(bfs, null, n => true), Is.Empty);
    }

    [Test]
    public void ReusingSearchGridsResetsPreviousSearchStateAndAvoidsExpensiveTiles()
    {
        var astar = new AStar.Node[3, 2];
        var bfs = new BFS.Node[3, 2];
        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 2; y++)
        {
            astar[x, y] = new AStar.Node(x, y, true, x == 1 && y == 0 ? 50 : 0);
            bfs[x, y] = new BFS.Node(x, y);
        }
        for (int i = 0; i < 2; i++)
        {
            var start = i == 0 ? 0 : 2;
            var end = 2 - start;
            var path = AStar.FindPath(astar, astar[start, 0], astar[end, 0]);
            Assert.That(path.Last(), Is.SameAs(astar[end, 0]));
            Assert.That(path.Contains(astar[1, 0]), Is.False);
            Assert.That(BFS.FindPath(bfs, bfs[start, 0], n => n == bfs[end, 0]).Last(), Is.SameAs(bfs[end, 0]));
        }
    }

    [Test]
    public void FacingOffsetsAndCellScaleMatchExistingControls()
    {
        var facings = new[] { Facing.Up, Facing.Down, Facing.Left, Facing.Right, Facing.UpLeft, Facing.UpRight, Facing.DownLeft, Facing.DownRight };
        var offsets = new[] { new Vector3Int(0, 1), new Vector3Int(0, -1), new Vector3Int(-1, 0), new Vector3Int(1, 0), new Vector3Int(-1, 1), new Vector3Int(1, 1), new Vector3Int(-1, -1), new Vector3Int(1, -1) };
        for (int i = 0; i < facings.Length; i++)
        {
            Assert.That(Dungeon.GetFacingOffset(facings[i]), Is.EqualTo(offsets[i]));
            Assert.That(TileWorldDungeon.GetFacingOffset(facings[i]), Is.EqualTo(offsets[i]));
        }
        Assert.That(GridMovement.CellToWorld(new Vector3Int(3, 4), 2), Is.EqualTo(new Vector3(6, 8)));
    }
}
