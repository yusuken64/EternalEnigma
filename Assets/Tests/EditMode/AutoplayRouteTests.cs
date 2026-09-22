using NUnit.Framework;
using UnityEngine;
using System;
using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.Tests
{
    public sealed class AutoplayRouteTests
    {
        [Test]
        public void WaterCrossingsAreNotGateOpeningObjectives()
        {
            var options = new OverworldLaunchOptions(OverworldLaunchMode.Sandbox, 42);
            var snapshot = new CampaignContext(options).Capture();
            snapshot.Permanent = (Capability[])Enum.GetValues(typeof(Capability));
            var context = new CampaignContext(options, snapshot);
            var approaches = AutoplayRunner.GateApproaches(context).ToHashSet();
            Assert.That(approaches, Is.Not.Empty, "Eligible land gates remain objectives.");
            foreach (var cell in approaches)
                Assert.That(context.Gates.Nearby(cell).Any(r => context.Gates.NeedsOpening(r) &&
                    (r.ShortcutKind == ShortcutKind.Keyed ? context.Gates.HasKey(r) : r.CanTraverse(context.Held, context.Resolved))),
                    Is.True, "Every gate objective must allow an actual gate interaction at " + cell);
            var waterOnly = context.Grid.Locks.SelectMany(g => g.Cells).Where(context.Grid.RequiresBoat)
                .SelectMany(cell => new[] { new GridPoint(cell.X+1,cell.Y), new GridPoint(cell.X-1,cell.Y),
                    new GridPoint(cell.X,cell.Y+1), new GridPoint(cell.X,cell.Y-1) })
                .Where(cell => !context.Gates.Nearby(cell).Any()).ToArray();
            Assert.That(waterOnly, Is.Not.Empty, "Seed includes water crossings.");
            Assert.That(waterOnly.Any(approaches.Contains), Is.False);
        }

        [Test]
        public void FollowsSharedAStarRouteAndReplansAroundNewObstruction()
        {
            var blocked = new bool[4,3]; int plans = 0;
            AStar.Node[,] Grid()
            {
                plans++;
                var grid = new AStar.Node[4,3];
                for(int x=0;x<4;x++) for(int y=0;y<3;y++) grid[x,y] = new AStar.Node(x,y,!blocked[x,y],0);
                return grid;
            }
            bool Walkable(Vector3Int p) => GridMovement.Contains(4,3,p) && !blocked[p.x,p.y];
            bool CanStep(Vector3Int a,Vector3Int b) => GridMovement.CanStep(a,b,Walkable);
            var route = new AutoplayRoute(); var goal = new Vector3Int(3,0);
            Assert.That(route.Next(Vector3Int.zero,goal,Grid,CanStep),Is.EqualTo(new Vector3Int(1,0)));
            Assert.That(route.Next(new Vector3Int(1,0),goal,Grid,CanStep),Is.EqualTo(new Vector3Int(2,0)));
            Assert.That(plans,Is.EqualTo(1),"Following a valid route should reuse its remaining steps.");
            blocked[2,0] = true;
            var position = new Vector3Int(1,0);
            for (int step=0;step<12 && position!=goal;step++)
            {
                var next = route.Next(position,goal,Grid,CanStep);
                Assert.That(next.HasValue,Is.True); Assert.That(CanStep(position,next.Value),Is.True);
                position = next.Value;
            }
            Assert.That(position,Is.EqualTo(goal)); Assert.That(plans,Is.EqualTo(2));
            Assert.That(route.Next(position,Vector3Int.zero,Grid,CanStep).HasValue,Is.True);
            Assert.That(plans,Is.EqualTo(3),"A changed objective requires a new route.");
        }

        [Test]
        public void UsesTheScenesDiagonalRulesAndDoesNotInventUnreachablePaths()
        {
            AStar.Node[,] Grid() => new[,] { { new AStar.Node(0,0,true,0), new AStar.Node(0,1,false,0) },
                { new AStar.Node(1,0,false,0), new AStar.Node(1,1,true,0) } };
            var goal = new Vector3Int(1,1);
            Assert.That(new AutoplayRoute().Next(Vector3Int.zero,goal,Grid,(a,b) => true),Is.Null);
            Assert.That(new AutoplayRoute().Next(Vector3Int.zero,goal,Grid,(a,b) => true,DiagonalMovement.AllowCornerCutting),Is.EqualTo(goal));
        }
    }
}
