using EternalEnigma.ConsoleExplorer;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;
using CampaignDefinition = EternalEnigma.Core.Progression.Campaign;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class ExplorerInteriorTests
{
    private static ExplorerSession Create()
    {
        var campaign = new CampaignDefinition(1, 1, "town", "dungeon", Array.Empty<ActivatedCapability>(),
            new[] { new CampaignRegion("region", "test", 0) },
            new[]
            {
                new CampaignLocation("town", "region", 0, LocationKind.Town),
                new CampaignLocation("dungeon", "region", 0, LocationKind.StoryDungeon),
            },
            new[] { new CampaignRoute("path", "town", "dungeon", Requirement.Open, LockForm.None) },
            Array.Empty<CapabilitySource>(),
            Array.Empty<CampaignCompanion>());
        var ground = new bool[15, 1];
        for (int x = 0; x < 15; x++) ground[x, 0] = true;
        var layers = new Dictionary<string, GridLayer>
        {
            [OverworldLayers.Ground] = new(ground), [OverworldLayers.Roads] = new(ground),
            [OverworldLayers.Water] = new(new bool[15, 1]), [OverworldLayers.Trees] = new(new bool[15, 1])
        };
        var grid = new OverworldGrid(campaign, layers,
            new Dictionary<string, GridPoint> { ["town"] = new(0, 0), ["dungeon"] = new(1, 0) },
            new Dictionary<string, IReadOnlyList<GridPoint>>(), Array.Empty<GridLock>());
        return new ExplorerSession(campaign, grid);
    }

    [Fact]
    public void EnteringTownGeneratesInteriorAtPartySpawn()
    {
        var session = Create();
        Assert.False(session.InInterior);
        Assert.True(session.EnterLocation());
        Assert.True(session.InInterior);
        Assert.NotNull(session.Town);
        Assert.Null(session.Dungeon);
        Assert.Equal(session.Town!.Plan.PartySpawn, session.InteriorPosition);
    }

    [Fact]
    public void WalkingToTownExitReturnsToOverworld()
    {
        var session = Create();
        session.EnterLocation();
        var exit = session.Town!.Plan.Exit;
        while (!session.InteriorPosition.Equals(exit))
        {
            int dy = Math.Sign(exit.Y - session.InteriorPosition.Y);
            int dx = Math.Sign(exit.X - session.InteriorPosition.X);
            Assert.True(session.Move(dx, dy));
        }
        Assert.False(session.InInterior);
        Assert.Equal("town", session.Location!.Id);
    }

    [Fact]
    public void EnteringDungeonGeneratesInteriorAtStartAndCanLeave()
    {
        var session = Create();
        Assert.True(session.Move(1, 0)); // step onto "dungeon"
        Assert.Equal("dungeon", session.Location!.Id);
        Assert.True(session.EnterLocation());
        Assert.NotNull(session.Dungeon);
        Assert.Equal(session.Dungeon!.Current.Start, session.InteriorPosition);

        Assert.False(session.EnterLocation()); // already inside
        Assert.True(session.LeaveLocation());
        Assert.False(session.InInterior);
        Assert.Null(session.Dungeon);
    }

    [Fact]
    public void ReenteringSameLocationReusesGeneratedInterior()
    {
        var session = Create();
        session.EnterLocation();
        var firstPlan = session.Town;
        session.LeaveLocation();
        session.EnterLocation();
        Assert.Same(firstPlan!.Plan, session.Town!.Plan);
    }

    [Fact]
    public void InteriorInteractClearsPlacementOnce()
    {
        var session = Create();
        session.Move(1, 0);
        session.EnterLocation();
        var dungeon = session.Dungeon!.Current;

        // Nothing to interact with at the entrance itself.
        Assert.False(session.InteriorInteract());

        var target = dungeon.Gold.Concat(dungeon.Items).Concat(dungeon.Enemies).Concat(dungeon.Traps)
            .Select(p => p.Cell).First();

        foreach (var step in FindPath(dungeon, dungeon.Start, target).Skip(1))
        {
            var before = session.InteriorPosition;
            Assert.True(session.Move(step.X - before.X, step.Y - before.Y));
        }

        Assert.Equal(target, session.InteriorPosition);
        Assert.False(session.IsCleared(target));
        Assert.True(session.InteriorInteract());
        Assert.True(session.IsCleared(target));
        Assert.False(session.InteriorInteract()); // already cleared
    }

    [Fact]
    public void NoClipIgnoresWallsInsideTown()
    {
        var session = Create();
        session.EnterLocation();
        var town = session.Town!.Plan;
        var wallCell = FindBlockedCellOutsideCorridor(town);

        session.ToggleNoClip();
        Assert.True(session.NoClip);
        while (!session.InteriorPosition.Equals(wallCell))
        {
            Assert.True(session.InInterior);
            int dx = Math.Sign(wallCell.X - session.InteriorPosition.X);
            int dy = Math.Sign(wallCell.Y - session.InteriorPosition.Y);
            Assert.True(session.Move(dx, dy));
        }
        Assert.False(town.IsWalkable(wallCell));
        Assert.True(session.InInterior);
    }

    [Fact]
    public void NoClipIgnoresWallsInsideDungeon()
    {
        var session = Create();
        session.Move(1, 0);
        session.EnterLocation();
        var dungeon = session.Dungeon!.Current;
        var wallCell = FindBlockedCell(dungeon);

        session.ToggleNoClip();
        while (!session.InteriorPosition.Equals(wallCell))
        {
            int dx = Math.Sign(wallCell.X - session.InteriorPosition.X);
            int dy = Math.Sign(wallCell.Y - session.InteriorPosition.Y);
            Assert.True(session.Move(dx, dy));
        }
        Assert.False(dungeon.IsWalkable(wallCell));
        Assert.True(session.InInterior);
    }

    [Fact]
    public void NoClipCanBeToggledWhileInsideAndPersistsAcrossLeaving()
    {
        var session = Create();
        session.EnterLocation();
        session.ToggleNoClip();
        Assert.True(session.NoClip);
        Assert.True(session.LeaveLocation());
        Assert.True(session.NoClip); // no-clip state persists across leaving, mirroring overworld behavior
    }

    private static GridPoint FindBlockedCellOutsideCorridor(TownPlan town)
    {
        for (int y = 0; y < town.Height; y++)
            for (int x = 0; x < town.Width; x++)
            {
                var p = new GridPoint(x, y);
                if (!town.IsWalkable(p) && (x < TownPlan.CorridorMinX || x > TownPlan.CorridorMaxX))
                    return p;
            }
        throw new InvalidOperationException("No blocked town cell found outside the corridor.");
    }

    private static GridPoint FindBlockedCell(DungeonFloor dungeon)
    {
        for (int y = 0; y < dungeon.Height; y++)
            for (int x = 0; x < dungeon.Width; x++)
            {
                var p = new GridPoint(x, y);
                if (!dungeon.IsWalkable(p)) return p;
            }
        throw new InvalidOperationException("No blocked dungeon cell found.");
    }

    private static List<GridPoint> FindPath(DungeonFloor dungeon, GridPoint from, GridPoint to)
    {
        var previous = new Dictionary<GridPoint, GridPoint>();
        var visited = new HashSet<GridPoint> { from };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Equals(to)) break;
            foreach (var next in dungeon.Neighbors(current))
            {
                if (visited.Add(next))
                {
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
        }

        var path = new List<GridPoint> { to };
        var node = to;
        while (!node.Equals(from))
        {
            node = previous[node];
            path.Add(node);
        }
        path.Reverse();
        return path;
    }
}
