using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public class TownInteriorTests
{
    [Theory]
    [MemberData(nameof(FurnishedNotches))]
    public void RearNotchesRetainEssentialFurnishingsAndAccessibleFloor(int half, int depth, int notchWidth, int notchDepth, bool left)
    {
        var room = BuildingFootprint.Notched(new GridPoint(20, 20), half, depth, left, notchWidth, notchDepth).TryBuildRoom()!;
        Assert.NotNull(room);
        var mirrored = new HashSet<bool>();
        foreach (var kind in new[] { TownInteriorKind.Residential, TownInteriorKind.Inn, TownInteriorKind.Trainer, TownInteriorKind.Shop })
            for (int seed = 0; seed < 24; seed++)
            {
                var spec = new TownInteriorSpec(kind);
                var interior = TownInteriorGenerator.Generate(seed, 0, room, spec);
                mirrored.Add(interior.Mirrored);
                var assets = interior.Props.Select(p => p.Asset).ToArray();
                if (kind is TownInteriorKind.Residential or TownInteriorKind.Inn)
                {
                    Assert.Equal(kind == TownInteriorKind.Inn ? 2 : 1, assets.Count(a => a == "Bed"));
                    foreach (string asset in new[] { "Bedside", "Cupboard", "Hearth", "Table", "Chair" }) Assert.Contains(asset, assets);
                    if (kind == TownInteriorKind.Inn) { Assert.Contains("Luggage", assets); Assert.Equal(2, interior.Counters.Count); }
                }
                else if (kind == TownInteriorKind.Trainer)
                    foreach (string asset in new[] { "Dummy", "Target", "Rack", "Bookcase", "Lectern", "ArcanePedestal" }) Assert.Contains(asset, assets);
                else
                {
                    foreach (string asset in new[] { "Shelf", "Crate", "Barrel", "Potions" }) Assert.Contains(asset, assets);
                    Assert.True(interior.Counters.Count >= 3);
                }
                Assert.All(interior.Props, prop => Assert.Contains(prop.Cell, room.Floor));
                var occupied = interior.Occupied.ToHashSet();
                var free = room.Floor.Where(c => !occupied.Contains(c)).ToHashSet();
                var entry = new GridPoint(room.Door.X, room.Door.Y + 1);
                var reachable = GridSearch.VisitOrder(entry, c => GridSteps.CardinalNeighbors(c, free.Contains)).ToHashSet();
                Assert.Equal(free.Count, reachable.Count);
                Assert.All(interior.Reserved, c => Assert.Contains(c, reachable));
                foreach (var prop in interior.Props.Where(p => p.Blocks))
                    Assert.Contains(GridSteps.CardinalNeighbors(prop.Cell, reachable.Contains), _ => true);
            }
        Assert.Equal(2, mirrored.Count);
    }

    public static IEnumerable<object[]> FurnishedNotches()
    {
        for (int half = 4; half <= 5; half++)
            for (int depth = 9; depth <= 12; depth++)
                for (int width = 2; width < half; width++)
                    for (int notchDepth = 2; notchDepth <= Math.Min(4, depth - 7); notchDepth++)
                        foreach (bool left in new[] { false, true })
                        {
                            var room = BuildingFootprint.Notched(new GridPoint(20, 20), half, depth, left, width, notchDepth).TryBuildRoom();
                            if (room == null) continue;
                            var floor = room.Floor.ToHashSet();
                            if (floor.Any(origin => Enumerable.Range(0, 49).All(i => floor.Contains(new GridPoint(origin.X + i % 7, origin.Y + i / 7)))))
                                yield return new object[] { half, depth, width, notchDepth, left };
                        }
    }

    [Fact]
    public void HundredSeedsPreserveDeterminismCirculationAndInteraction()
    {
        var variants=new HashSet<(TownInteriorKind,int,bool)>();
        for(int seed=0;seed<100;seed++)
        {
            var options=TownLayout.Create(seed,TownServiceCatalog.All,2,3,CampaignContext.ResidentialTownBuildings).Options;
            var a=TownPlanGenerator.Generate(options);var b=TownPlanGenerator.Generate(options);
            string Fingerprint(TownPlan p)=>string.Join(";",p.Interiors.SelectMany(i=>i.Props.Select(v=>$"{i.Door}:{i.Spec.Kind}:{v.Asset}:{v.Cell}:{v.QuarterTurns}:{v.Elevation}")));
            Assert.Equal(Fingerprint(a),Fingerprint(b));Assert.Equal(TownServiceCatalog.All.Count+CampaignContext.ResidentialTownBuildings,a.Interiors.Count);
            var npcs=TownNpcPlacement.Generate(a);Assert.Equal(2,npcs.Count);Assert.Equal(npcs.Select(p=>p.Cell),TownNpcPlacement.Generate(b).Select(p=>p.Cell));
            var blocked=npcs.Select(p=>p.Cell).Concat(a.Interiors.Where(i=>i.Spec.HasVendor).Select(i=>a.ShopRoomAt(i.Door)!.VendorAnchor)).ToHashSet();
            var reachable=GridSearch.VisitOrder(a.PartySpawn,p=>GridSteps.CardinalNeighbors(p,c=>a.IsWalkable(c)&&!blocked.Contains(c))).ToHashSet();
            foreach(var interior in a.Interiors)
            {
                variants.Add((interior.Spec.Kind,interior.Arrangement,interior.Mirrored));
                var room=a.ShopRoomAt(interior.Door)!;
                Assert.All(interior.Reserved,c=>Assert.True(a.IsWalkable(c)));
                Assert.All(interior.Occupied,c=>{Assert.Contains(c,room.Floor);Assert.False(a.IsWalkable(c));});
                Assert.All(interior.Carpet,c=>Assert.True(a.IsWalkable(c)));
                Assert.Contains(new GridPoint(room.VendorAnchor.X,room.VendorAnchor.Y-1),reachable);
                Assert.All(room.Floor.Where(c=>a.IsWalkable(c)&&!blocked.Contains(c)),c=>Assert.Contains(c,reachable));
                foreach(var prop in interior.Props.Where(p=>p.Elevation>0))Assert.Contains(prop.Cell,interior.Counters);
            }
            foreach(var npc in npcs)Assert.Contains(GridSteps.CardinalNeighbors(npc.Cell,reachable.Contains),_=>true);
            for(int x=0;x<a.Width;x++)for(int y=0;y<a.Height;y++)
                Assert.Equal(!(a.Layers[TownLayers.Houses][x,y]||a.Layers[TownLayers.Trees][x,y]||a.Layers[TownLayers.ShopWalls][x,y]||a.Layers[TownLayers.Furniture][x,y]),a.IsWalkable(new GridPoint(x,y)));
        }
        foreach(var kind in new[]{TownInteriorKind.Residential,TownInteriorKind.Shop,TownInteriorKind.Inn,TownInteriorKind.Trainer})
            Assert.Equal(6,variants.Count(v=>v.Item1==kind));
    }
    [Fact]
    public void MetadataParticipatesInCacheIdentityAndLegacyRemainsEmpty()
    {
        var a=new TownPlanOptions(1,52,52,new[]{true},detailed:true,interiors:new[]{new TownInteriorSpec(TownInteriorKind.Shop,TownShopTheme.Bakery)});
        var b=new TownPlanOptions(1,52,52,new[]{true},detailed:true,interiors:new[]{new TownInteriorSpec(TownInteriorKind.Shop,TownShopTheme.Equipment)});
        Assert.NotEqual(a,b);Assert.NotEqual(a.GetHashCode(),b.GetHashCode());
        Assert.Empty(TownPlanGenerator.Generate(new TownPlanOptions(42)).Interiors);
    }
}
