using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class ReturnAndShortcutTests
{
    [Theory]
    [InlineData(0)] [InlineData(42)] [InlineData(-1)]
    public void RequiredReturnHasAnEarlyEntranceAndCannotBeSkipped(int seed)
    {
        var c = CampaignGenerator.Generate(seed);
        var objective = Assert.Single(c.ReturnObjectives, o => o.Required);
        Assert.Equal(3, c.ReturnObjectives.Count(o => !o.Required));
        var before = CampaignExplorer.Explore(c, excludedCapability: objective.EnablingCapability);
        foreach (string gateId in objective.GateIds)
        {
            var gate = c.Routes.Single(r => r.Id == gateId);
            Assert.Contains(gate.From, before.ReachableLocations);
            Assert.DoesNotContain(gate.To, before.ReachableLocations);
        }
        Assert.DoesNotContain(c.FinalLocationId, before.ReachableLocations);
        var withoutReward = CampaignExplorer.Explore(c, excludedCapability: objective.RewardCapability);
        Assert.DoesNotContain(c.FinalLocationId, withoutReward.ReachableLocations);
        var full = CampaignExplorer.Explore(c);
        var enabler = full.Witnesses.First(w => c.Sources.Single(s => s.Id == w.SourceId).Capability == objective.EnablingCapability);
        Assert.DoesNotContain(c.Sources.Single(s => s.Id == enabler.SourceId).LocationId, objective.DestinationIds);
        foreach (var source in c.Sources.Where(s => s.Capability == objective.RewardCapability))
        {
            Assert.Contains(source.LocationId, objective.DestinationIds);
            var witness = full.Witnesses.Single(w => w.SourceId == source.Id);
            Assert.Contains(witness.RouteIds, objective.GateIds.Contains);
        }
        var omitOptional = c.ReturnObjectives.Where(o => !o.Required).SelectMany(o => o.DestinationIds).ToHashSet();
        Assert.Contains(c.FinalLocationId, CampaignExplorer.Explore(c, excludedLocations: omitOptional).ReachableLocations);
    }

    [Fact]
    public void ValidatorRejectsAlternateProviderAndBoundaryBypasses()
    {
        var c = CampaignGenerator.Generate(42);
        var objective = c.ReturnObjectives.Single(o => o.Required);
        var source = c.Sources.First(s => s.Capability == objective.RewardCapability);
        var moved = c.Sources.Select(s => s == source ? new CapabilitySource(s.Id, c.StartLocationId, s.Capability, s.CompanionId, prerequisites: s.Prerequisites) : s);
        Assert.False(CampaignValidator.Validate(CampaignGeneratorTests.With(c, sources: moved)).IsValid);
        var boundary = c.Routes.Single(r => r.IsProgressionBoundary && r.Requirement.Alternatives.Count == 1 && r.Requirement.Alternatives[0].Contains(objective.RewardCapability!.Value));
        var bypass = new CampaignRoute(boundary.Id, boundary.From, boundary.To,
            new Requirement(CapabilitySet.Of(objective.RewardCapability!.Value), CapabilitySet.Of(Capability.Engineering)), boundary.Form, true, true);
        Assert.Contains(CampaignValidator.Validate(CampaignGeneratorTests.With(c, routes: c.Routes.Select(r => r == boundary ? bypass : r))).Errors,
            e => e.StartsWith("return.bypass:", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRejectsAnEnablerAvailableDuringTheInitialVisit()
    {
        var c = CampaignGenerator.Generate(42);
        var objective = c.ReturnObjectives.Single(o => o.Required);
        var source = c.Sources.First(s => s.Capability == objective.EnablingCapability);
        var moved = c.Sources.Select(s => s == source ? new CapabilitySource(s.Id, c.StartLocationId, s.Capability, s.CompanionId, prerequisites: s.Prerequisites) : s);
        Assert.Contains(CampaignValidator.Validate(CampaignGeneratorTests.With(c, sources: moved)).Errors,
            e => e.StartsWith("return.acquisitionStage:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)] [InlineData(42)] [InlineData(-1)]
    public void ShortcutsRequireLaterBiomeKeysAndSaveTravel(int seed)
    {
        var c = CampaignGenerator.Generate(seed); var grid = OverworldGridGenerator.Generate(c);
        var all = CapabilitySet.From(c.Manifest.Select(m => m.Id));
        var resolved = c.Routes.Select(r => r.Id).ToHashSet();
        foreach (var route in c.Routes.Where(r => r.ShortcutKind != ShortcutKind.None))
        {
            var state = new HashSet<string>();
            var cell = grid.Locations[route.From];
            Assert.True(route.IsWarp);
            Assert.DoesNotContain(grid.Locks, g => g.RouteId == route.Id);
            Assert.Equal(2, grid.Routes[route.Id].Count);
            var far = grid.Locations[route.To];
            Assert.False(grid.IsGround(new GridPoint((cell.X + far.X) / 2, (cell.Y + far.Y) / 2)));
            Assert.Equal(ShortcutKind.Keyed, route.ShortcutKind);
            Assert.False(route.CanTraverse(all, state));
            Assert.False(route.TryUnlock(route.From, state));
            Assert.False(route.TryUnlock(route.To, state));
            Assert.False(route.TryCollectKey(route.From, state));
            Assert.False(route.TryCollectKey(route.To, state));
            Assert.False(grid.TryWarp(route.Id, cell, all, state, out _));
            Assert.False(grid.TryWarp(route.Id, grid.Locations[route.To], all, state, out _));
            Assert.True(route.TryCollectKey(route.KeyLocationId!, state));
            Assert.False(route.TryCollectKey(route.KeyLocationId!, state));
            Assert.True(route.CanTraverse(CapabilitySet.Empty, state));
            Assert.True(grid.IsWalkable(cell, CapabilitySet.Empty, state));
            Assert.True(grid.TryWarp(route.Id, cell, CapabilitySet.Empty, state, out var destination));
            Assert.Equal(grid.Locations[route.To], destination);
            Assert.False(grid.CanStep(cell, destination, all, state));
            Assert.True(grid.TryWarp(route.Id, destination, CapabilitySet.Empty, state, out var back));
            Assert.Equal(cell, back);
            Assert.False(grid.TryWarp(route.Id, grid.PlayerStart, all, state, out _));
            Assert.False(grid.TryWarp(route.Id, cell, all, new HashSet<string>(), out _));
            Assert.False(grid.RequiresBoat(cell));
            int before = OverworldGridValidator.Distance(grid, grid.Locations[route.From], grid.Locations[route.To], all, resolved, route.Id);
            int after = OverworldGridValidator.Distance(grid, grid.Locations[route.From], grid.Locations[route.To], all, resolved);
            Assert.True(after * 4 <= before * 3, $"{route.Id}: {before} -> {after}");
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(42)] [InlineData(-1)]
    public void SixBiomesFormASpineAndEveryUnlockedPairIsWithinTwoHops(int seed)
    {
        var c = CampaignGenerator.Generate(seed);
        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F" }, c.Regions.Select(r => r.Label));
        var locations = c.Locations.ToDictionary(l => l.Id);
        var order = c.Regions.ToDictionary(r => r.Id, r => r.ProgressionOrder);
        var edges = c.Routes.Select(r => (route: r, a: order[locations[r.From].RegionId], b: order[locations[r.To].RegionId])).Where(e => e.a != e.b).ToArray();
        Assert.Equal(new[] { (0, 1), (1, 2), (2, 3), (3, 4), (4, 5) },
            edges.Where(e => e.route.ShortcutKind == ShortcutKind.None).Select(e => (e.a, e.b)).OrderBy(e => e.a));
        Assert.Equal(new[] { (1, 3), (1, 4), (1, 5) },
            edges.Where(e => e.route.ShortcutKind == ShortcutKind.Keyed).Select(e => (e.a, e.b)).OrderBy(e => e.b));
        for (int start = 0; start < 6; start++)
        {
            var distance = new Dictionary<int, int> { [start] = 0 }; var pending = new Queue<int>(); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                int at = pending.Dequeue();
                foreach (var edge in edges.Where(e => e.a == at || e.b == at))
                {
                    int next = edge.a == at ? edge.b : edge.a;
                    if (distance.TryAdd(next, distance[at] + 1)) pending.Enqueue(next);
                }
            }
            Assert.Equal(6, distance.Count); Assert.All(distance.Values, d => Assert.InRange(d, 0, 2));
        }
        var shortcutIds = c.Routes.Where(r => r.ShortcutKind == ShortcutKind.Keyed).Select(r => r.Id).ToHashSet();
        var normal = CampaignExplorer.Explore(c, excludedRoutes: shortcutIds);
        Assert.Contains(c.FinalLocationId, normal.ReachableLocations);
        foreach (var route in c.Routes.Where(r => r.ShortcutKind == ShortcutKind.Keyed))
        {
            Assert.Contains(route.KeyLocationId!, normal.ReachableLocations);
            int later = locations[route.To].Stage;
            var boundary = c.Routes.Single(r => r.IsProgressionBoundary && locations[r.To].Stage == later);
            var before = CampaignExplorer.Explore(c, excludedRoutes: new HashSet<string> { boundary.Id });
            Assert.DoesNotContain(route.KeyLocationId!, before.ReachableLocations);
            Assert.DoesNotContain(route.To, before.ReachableLocations);
        }
    }

    [Fact]
    public void KeyCanOnlyBeCollectedAtItsSourceAndPersistsForTheSession()
    {
        var route = new CampaignRoute("passage", "start", "later", Requirement.Open, LockForm.None,
            shortcutKind: ShortcutKind.Keyed, keyId: "D key", keyLocationId: "key-site");
        var c = new Campaign(0, 0, "start", "later", Array.Empty<ActivatedCapability>(), Array.Empty<CampaignRegion>(),
            new[] { "start", "later", "key-site" }.Select(id => new CampaignLocation(id, "r", 0, LocationKind.Town)),
            new[] { route, new CampaignRoute("normal", "start", "key-site", Requirement.Open, LockForm.None) },
            Array.Empty<CapabilitySource>(), Array.Empty<CampaignCompanion>());
        var session = new CampaignSession(c);
        Assert.False(session.TryCollectShortcutKey(route.Id)); Assert.False(session.TryMove(route.Id));
        Assert.True(session.TryMove("normal")); Assert.True(session.TryCollectShortcutKey(route.Id));
        Assert.False(session.TryCollectShortcutKey(route.Id)); Assert.True(session.TryMove("normal"));
        Assert.True(session.TryMove(route.Id)); Assert.True(session.TryMove(route.Id));
        session.ReturnAfterDefeat(); Assert.True(session.TryMove(route.Id));
        Assert.False(new CampaignSession(c).TryMove(route.Id));
    }

    [Fact]
    public void ValidatorRejectsEarlyOrMissingKeys()
    {
        var c = CampaignGenerator.Generate(42); var route = c.Routes.First(r => r.ShortcutKind == ShortcutKind.Keyed);
        var early = new CampaignRoute(route.Id, route.From, route.To, route.Requirement, route.Form,
            shortcutKind: ShortcutKind.Keyed, keyId: route.KeyId, keyLocationId: c.StartLocationId);
        Assert.NotEqual(CampaignFingerprint.Compute(c), CampaignFingerprint.Compute(CampaignGeneratorTests.With(c, routes: c.Routes.Select(r => r == route ? early : r))));
        Assert.Contains(CampaignValidator.Validate(CampaignGeneratorTests.With(c, routes: c.Routes.Select(r => r == route ? early : r))).Errors,
            e => e.StartsWith("shortcut.keyStage:", StringComparison.Ordinal));
        var missing = new CampaignRoute(route.Id, route.From, route.To, route.Requirement, route.Form,
            shortcutKind: ShortcutKind.Keyed, keyId: route.KeyId, keyLocationId: "missing");
        Assert.Contains(CampaignValidator.Validate(CampaignGeneratorTests.With(c, routes: c.Routes.Select(r => r == route ? missing : r))).Errors,
            e => e.StartsWith("shortcut.key:", StringComparison.Ordinal));
    }

    [Fact]
    public void FarSideUnlocksComposeAndRemainBidirectional()
    {
        var a = new CampaignRoute("a", "start", "middle", Requirement.Open, LockForm.None, shortcutKind: ShortcutKind.FarSide, unlockingEndpoint: "start");
        var b = new CampaignRoute("b", "middle", "end", Requirement.Open, LockForm.None, shortcutKind: ShortcutKind.FarSide, unlockingEndpoint: "middle");
        var c = new Campaign(0, 0, "start", "end", Array.Empty<ActivatedCapability>(), Array.Empty<CampaignRegion>(),
            new[] { "start", "middle", "end" }.Select(id => new CampaignLocation(id, "r", 0, LocationKind.Town)),
            new[] { a, b }, Array.Empty<CapabilitySource>(), Array.Empty<CampaignCompanion>());
        var session = new CampaignSession(c);
        Assert.False(session.TryMove("a")); Assert.False(session.TryOpenShortcut("b"));
        Assert.True(session.TryOpenShortcut("a")); Assert.True(session.TryMove("a"));
        Assert.False(session.TryMove("b")); Assert.True(session.TryOpenShortcut("b")); Assert.True(session.TryMove("b"));
        Assert.True(session.TryMove("b")); Assert.True(session.TryMove("a"));
    }

    [Theory]
    [InlineData(0)] [InlineData(42)] [InlineData(-1)]
    public void GridMatchesGraphWithReachableUnlockActions(int seed)
    {
        var c = CampaignGenerator.Generate(seed); var grid = OverworldGridGenerator.Generate(c);
        var capabilities = c.Manifest.Select(m => m.Id).ToArray();
        for (int sample = 0; sample < 16; sample++)
        {
            int bits = sample * ((1 << capabilities.Length) - 1) / 15;
            var held = CapabilitySet.From(capabilities.Where((_, i) => (bits & (1 << i)) != 0));
            var resolved = new HashSet<string>(); var reachable = new HashSet<string> { c.StartLocationId };
            bool changed;
            do
            {
                changed = false;
                foreach (string at in reachable.ToArray())
                    foreach (var shortcut in c.Routes) changed |= shortcut.TryCollectKey(at, resolved);
                foreach (string at in reachable.ToArray())
                foreach (var route in c.Routes.Where(r => r.Other(at) != null))
                {
                    changed |= route.TryUnlock(at, resolved);
                    if (!route.CanTraverse(held, resolved)) continue;
                    if (route.Latches) changed |= resolved.Add(route.Id);
                    changed |= reachable.Add(route.Other(at)!);
                }
            } while (changed);
            var tiles = new HashSet<GridPoint> { grid.PlayerStart }; var pending = new Queue<GridPoint>(); pending.Enqueue(grid.PlayerStart);
            while (pending.Count > 0)
            {
                var at = pending.Dequeue();
                foreach (var next in OverworldMovement.Neighbors(at).Where(p => grid.CanStep(at, p, held, resolved))
                    .Concat(grid.WarpDestinations(at, held, resolved)))
                    if (tiles.Add(next)) pending.Enqueue(next);
            }
            foreach (var location in c.Locations) Assert.Equal(reachable.Contains(location.Id), tiles.Contains(grid.Locations[location.Id]));
        }
    }
}
