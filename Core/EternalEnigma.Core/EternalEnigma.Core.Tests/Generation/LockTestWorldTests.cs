using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class LockTestWorldTests
{
    [Fact]
    public void GalleryCoversEverySkinAndNamedKeyWithoutUnresolvedFiction()
    {
        var world = new LockTestWorld();
        var context = world.CreateContext();
        Assert.True(context.IsSandbox);
        Assert.Equal(CapabilityCatalog.All.Count, context.Held.Count);
        Assert.Equal((LockSkinCatalog.Skins.Count + 19) * 2 + 8, world.Stations.Count);
        Assert.All(LockSkinCatalog.Skins, skin => {
            var stations = world.Stations.Where(s => s.Route.SkinId == skin.Id && !s.Route.Id.StartsWith("test-")).ToArray();
            Assert.Equal(2, stations.Length);
            Assert.Single(stations, station => station.Horizontal);
            Assert.Single(stations, station => !station.Horizontal);
        });
        Assert.Equal(24, context.Keys.Count);
        Assert.Equal(24, world.Campaign.Routes.Where(route => route.KeyId != null).Select(route => route.KeyLabel).Distinct().Count());
        Assert.All(world.Stations, station => {
            Assert.DoesNotContain("{", station.Route.LockText!);
            Assert.True(context.Gates.IsWalkable(station.Approach, context.Held));
            Assert.True(context.Gates.IsWalkable(station.FarSide, context.Held));
            Assert.Equal(station.Horizontal, station.Approach.Y == station.FarSide.Y);
        });
    }

    [Fact]
    public void EveryStationOpensThroughProductionGateActionsAndCanBeReset()
    {
        var world = new LockTestWorld();
        var context = world.CreateContext();
        foreach (var station in world.Stations)
        {
            var route = station.Route;
            if (route.ShortcutKind == ShortcutKind.FarSide)
            {
                Assert.True(route.TryUnlock(route.To, context.Resolved));
                context.Resolved.Remove(route.Id);
                continue;
            }
            context.Position = station.Approach;
            using var session = context.Gates.BeginInteraction(route.Id, () => context.Position, () => context.Held);
            Assert.NotNull(session);
            var actions = route.KeyId != null ? new LockAction[] { new KeyLockAction(route.KeyId) } :
                route.Requirement.Alternatives[0].Values.Select(capability => (LockAction)new CapabilityLockAction(capability)).ToArray();
            for (int i = 0; i < actions.Length; i++)
                Assert.Equal(i == actions.Length - 1 ? LockOutcome.Opened : LockOutcome.Progress, session!.Attempt(actions[i]));
            Assert.False(context.Gates.NeedsOpening(route));
            context.Opened.Remove(route.Id); context.Resolved.Remove(route.Id);
            Assert.True(context.Gates.NeedsOpening(route));
        }
        Assert.Equal(24, context.Keys.Count);
        Assert.Equal(CapabilityCatalog.All.Count, context.Held.Count);
    }
}
