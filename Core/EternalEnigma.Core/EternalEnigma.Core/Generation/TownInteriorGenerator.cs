using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Independent furnishing stream. Every accepted recipe preserves all free floor circulation.</summary>
public static class TownInteriorGenerator
{
    public static TownInterior Generate(int seed, int slot, ShopRoom room, TownInteriorSpec spec)
    {
        var random = new SeedStream(seed, 1800u + (uint)slot);
        int first = random.Range(3); bool mirror = random.Range(2) == 1;
        for (int retry = 0; retry < 4; retry++)
        {
            var result = Recipe(room, spec, (first + retry) % 3, mirror, retry == 3);
            var occupied = new HashSet<GridPoint>(result.Occupied);
            var free = new HashSet<GridPoint>(room.Floor.Where(c => !occupied.Contains(c)));
            var entry = new GridPoint(room.Door.X, room.Door.Y + 1);
            var seen = GridSearch.VisitOrder(entry, c => GridSteps.CardinalNeighbors(c, free.Contains));
            if (seen.Count() == free.Count && result.Reserved.All(free.Contains)) return result;
        }
        throw new InvalidOperationException($"No accessible furnishing recipe for {spec.Kind} at {room.Door}.");
    }

    private static TownInterior Recipe(ShopRoom room, TownInteriorSpec spec, int arrangement, bool mirror, bool compact)
    {
        var floor = new HashSet<GridPoint>(room.Floor);
        int left = room.Floor.Min(c => c.X), right = room.Floor.Max(c => c.X);
        int bottom = room.Door.Y + 2, top = room.Floor.Max(c => c.Y);
        int minimum = spec.Kind is TownInteriorKind.Inn or TownInteriorKind.Trainer ? 7 : 5;
        if (right - left + 1 < minimum || top - bottom + 1 < minimum)
            throw new InvalidOperationException($"{spec.Kind} requires {minimum}x{minimum} usable floor at {room.Door}.");
        var reserved = new HashSet<GridPoint>();
        // Keep a broad central aisle, including the vendor/home anchor and its adjacent interaction cell.
        foreach (var c in floor) if (Math.Abs(c.X - room.Door.X) <= 1) reserved.Add(c);
        var occupied = new HashSet<GridPoint>(); var counters = new List<GridPoint>();
        var props = new List<TownPropPlacement>();
        GridPoint At(int x, int y) => new(mirror ? left + right - x : x, y);
        void Add(string asset, int x, int y, int turns = 0)
        {
            var c = At(x,y);
            if (!floor.Contains(c) || reserved.Contains(c) || !occupied.Add(c)) return;
            props.Add(new TownPropPlacement(asset,c,mirror ? (4-turns)%4 : turns));
        }
        void Counter(int x, int y)
        {
            var c = At(x,y);
            if (!floor.Contains(c) || reserved.Contains(c) || !occupied.Add(c)) return;
            counters.Add(c);
        }
        int shift = compact ? 0 : arrangement;
        if (spec.Kind == TownInteriorKind.Residential || spec.Kind == TownInteriorKind.Inn)
        {
            Add("Bed",left,top); Add("Bedside",left+1,top);
            Add("Cupboard",right,top); Add("Hearth",right,bottom);
            Add("Table",left,bottom+shift); Add("Chair",left,bottom+shift+1,2);
            if (spec.Kind == TownInteriorKind.Inn)
            { Add("Bed",left,top-2); Add("Luggage",left+1,top-2); Counter(right,top-2); Counter(right-1,top-2); }
            if (!compact) { Add("Chest",right,top-2); Add("Plant",left,top-2); }
        }
        else if (spec.Kind == TownInteriorKind.Trainer)
        {
            Add("Dummy",left,bottom+shift); Add("Target",left,top);
            Add("Rack",left+1,top); Add("Bookcase",right,top);
            Add("Lectern",right,bottom); Add("ArcanePedestal",right,top-2);
            if (!compact) Add("Bench",left,bottom+shift+2);
        }
        else if (spec.Kind == TownInteriorKind.Shop)
        {
            for (int y=bottom+1; y<=top-1; y++) Counter(left,y);
            if (arrangement >= 1) Counter(left+1,top-1);
            if (arrangement == 2) Counter(left+1,bottom+1);
            Add("Shelf",right,top); Add("Crate",right,bottom); Add("Barrel",right,bottom+2);
            string goods = spec.Theme == TownShopTheme.Bakery ? "Basket" : spec.Theme == TownShopTheme.Equipment ? "Equipment" : "Potions";
            foreach (var c in counters.Where((_,i)=>i%2==0)) props.Add(new TownPropPlacement(goods,c,elevation:.72f,blocks:false));
            if (!compact) Add("Sack",right,top-2);
        }
        var carpet = floor.Where(c => c.Y >= bottom && c.Y < top && Math.Abs(c.X-room.Door.X)<=1 && !occupied.Contains(c)).ToArray();
        return new TownInterior(room.Door,spec,arrangement,mirror,props,occupied.OrderBy(c=>c.Y).ThenBy(c=>c.X),carpet,counters,reserved.OrderBy(c=>c.Y).ThenBy(c=>c.X));
    }
}
