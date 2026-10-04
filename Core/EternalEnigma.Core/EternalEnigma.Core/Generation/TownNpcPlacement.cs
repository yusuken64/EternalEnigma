using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

public static class TownNpcPlacement
{
    /// <summary>Public, clear, connected anchors; removing both actors preserves every previously reachable free cell.</summary>
    public static IReadOnlyList<Placement> Generate(TownPlan plan)
    {
        if (plan.Interiors.Count == 0) return Array.Empty<Placement>();
        var reachable = new HashSet<GridPoint>(GridSearch.VisitOrder(plan.PartySpawn, plan.CardinalNeighbors));
        var candidates = new List<GridPoint>();
        var reserved=plan.BuildingSlots.Concat(plan.AllySlots.Select(p=>p.Cell)).Append(plan.Exit).Append(plan.DungeonEntrance);
        foreach(var c in reachable.OrderBy(c=>c.Y).ThenBy(c=>c.X))
        {
            if (plan.IsReserved(c) || plan.Layers[TownLayers.ShopFloor].At(c) || plan.Layers[TownLayers.Roads].At(c) ||
                plan.Layers[TownLayers.Props].At(c) || reserved.Any(p=>Math.Max(Math.Abs(c.X-p.X),Math.Abs(c.Y-p.Y))<=2)) continue;
            var neighbors=plan.CardinalNeighbors(c).ToArray();
            if(neighbors.Length==4 && neighbors.Any(n=>plan.Layers[TownLayers.Roads].At(n))) candidates.Add(c);
        }
        var chosen=new List<Placement>(); var blocked=new HashSet<GridPoint>();
        foreach(var c in new SeedStream(plan.Seed,1951).Shuffle(candidates))
        {
            if(chosen.Any(p=>Math.Max(Math.Abs(p.Cell.X-c.X),Math.Abs(p.Cell.Y-c.Y))<4))continue;
            blocked.Add(c);
            var seen=GridSearch.VisitOrder(plan.PartySpawn,p=>GridSteps.CardinalNeighbors(p,q=>plan.IsWalkable(q)&&!blocked.Contains(q)));
            if(seen.Count()!=reachable.Count-blocked.Count){blocked.Remove(c);continue;}
            chosen.Add(new Placement(c,chosen.Count));if(chosen.Count==2)break;
        }
        return chosen.AsReadOnly();
    }
}
