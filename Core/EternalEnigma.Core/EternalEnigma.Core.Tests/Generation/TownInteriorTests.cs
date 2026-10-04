using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public class TownInteriorTests
{
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
