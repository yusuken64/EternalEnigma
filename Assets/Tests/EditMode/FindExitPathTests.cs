using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;

namespace EternalEnigma.Tests
{
    public sealed class FindExitPathTests
    {
        static readonly GridPoint[] Cardinal = { new(1, 0), new(0, 1), new(-1, 0), new(0, -1) };

        static IReadOnlyList<GridPoint> Route(GridPoint origin, GridPoint exit, params GridPoint[] cells)
        {
            var open = cells.ToHashSet();
            return FindExitAction.FindRoute(origin, exit, p => Cardinal
                .Select(d => new GridPoint(p.X + d.X, p.Y + d.Y)).Where(open.Contains));
        }

        [Test] public void FindsShortestRouteAroundWallRatherThanDrawingStraightThroughIt()
        {
            var route = Route(new(0, 0), new(2, 0), new(0, 0), new(0, 1), new(1, 1), new(2, 1), new(2, 0),
                new(0, -1), new(0, -2), new(1, -2), new(2, -2), new(2, -1));
            Assert.That(route, Is.EqualTo(new GridPoint[] { new(0, 1), new(1, 1), new(2, 1), new(2, 0) }));
        }

        [Test] public void LongRouteStopsAtFifteenStepsAndRecastContinuesFromNewPosition()
        {
            var cells = Enumerable.Range(0, 41).Select(x => new GridPoint(x, 0)).ToArray();
            var first = Route(new(0, 0), new(40, 0), cells);
            Assert.That(first, Is.EqualTo(cells.Skip(1).Take(15)));
            var second = Route(first.Last(), new(40, 0), cells);
            Assert.That(second, Is.EqualTo(cells.Skip(16).Take(15)));
            var third = Route(second.Last(), new(40, 0), cells);
            Assert.That(third, Is.EqualTo(cells.Skip(31)));
        }

        [Test] public void ShortRouteIncludesExitAndNeverIncludesOrigin()
        {
            var route = Route(new(0, 0), new(2, 0), new(0, 0), new(1, 0), new(2, 0));
            Assert.That(route, Is.EqualTo(new GridPoint[] { new(1, 0), new(2, 0) }));
        }

        [Test] public void UnreachableExitUsesPathToClosestReachableTile()
        {
            var route = Route(new(0, 0), new(4, 0), new(0, 0), new(0, 1), new(1, 1), new(2, 1), new(4, 0));
            Assert.That(route, Is.EqualTo(new GridPoint[] { new(0, 1), new(1, 1), new(2, 1) }));
        }

        [Test] public void EquallyCloseFallbackPrefersFewerSteps()
        {
            var route = Route(new(0, 0), new(3, 0), new(0, 0), new(1, 0), new(0, 1), new(0, 2), new(1, 2));
            Assert.That(route, Is.EqualTo(new[] { new GridPoint(1, 0) }));
        }

        [Test] public void UnreachableExitStillTakesAvailableStepWhenOriginIsClosest()
        {
            var route = Route(new(0, 0), new(2, 0), new(0, 0), new(-1, 0), new(-2, 0));
            Assert.That(route, Is.EqualTo(new[] { new GridPoint(-1, 0) }));
        }

        [Test] public void AtExitOrCompletelyBlockedProducesNoCoins()
        {
            Assert.That(Route(new(0, 0), new(0, 0), new(0, 0), new(1, 0)), Is.Empty);
            Assert.That(Route(new(0, 0), new(3, 0), new(0, 0), new(3, 0)), Is.Empty);
        }
    }
}
