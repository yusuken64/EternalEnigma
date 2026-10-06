using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Version 1 indoor masks. Streams are independent of presentation and Unity random state.</summary>
internal static class BiomeDungeonGenerator
{
    // Mean walkable cells of legacy 32x32 seeds 0..255; locked to layout version 1.
    internal const double ReferenceArea = 322.13671875;
    internal static double Density(int area) => Math.Max(1, Math.Min(2, Math.Sqrt(area / ReferenceArea)));

    internal static DungeonFloor Generate(DungeonFloorOptions o)
    {
        var rng = new SeedStream(o.Seed, 1100 + (uint)o.Biome);
        var place = new SeedStream(o.Seed, 1200);
        var sceneryRandom = new SeedStream(o.Seed, 1300);
        int w = o.Width, h = o.Height;
        var mask = new bool[w, h];
        var rooms = new List<GridRect>();
        bool organic = o.Biome == OverworldBiome.Forest || o.Biome == OverworldBiome.Mountain || o.Biome == OverworldBiome.Marsh;
        int spacing = o.Biome == OverworldBiome.Volcanic ? 12 : 10;
        int nx = Math.Max(2, (w - 4) / spacing), ny = Math.Max(2, (h - 4) / spacing);
        void Paint(int x, int y) { if (x > 0 && y > 0 && x < w - 1 && y < h - 1) mask[x, y] = true; }
        for (int iy = 0; iy < ny; iy++) for (int ix = 0; ix < nx; ix++)
        {
            int left = 2 + ix * (w - 4) / nx, right = 2 + (ix + 1) * (w - 4) / nx;
            int bottom = 2 + iy * (h - 4) / ny, top = 2 + (iy + 1) * (h - 4) / ny;
            int rw = Math.Max(3, right - left - 2 - rng.Range(2));
            int rh = Math.Max(3, top - bottom - 2 - rng.Range(2));
            int x = left + rng.Range(2), y = bottom + rng.Range(2);
            var room = new GridRect(x, y, rw, rh);
            if (organic)
            {
                // Small rectangular anchors remain wholly walkable. Lobes extend beyond anchors.
                int cx = room.Center.X, cy = room.Center.Y;
                rooms.Add(new GridRect(cx - 1, cy - 1, 3, 3));
                for (int dx = -rw / 2; dx <= rw / 2; dx++) for (int dy = -rh / 2; dy <= rh / 2; dy++)
                    if (dx * dx * 4.0 / (rw * rw) + dy * dy * 4.0 / (rh * rh) < 1.05 + rng.Range(20) / 100.0)
                        Paint(cx + dx, cy + dy);
                foreach (var p in rooms.Last().Cells()) Paint(p.X, p.Y);
            }
            else if (o.Biome == OverworldBiome.Grassland && (ix+iy*nx)%4==3 && rw>=7 && rh>=7)
            {
                // Ruined courtyards: a two-tile gallery around an enclosed recess.
                rooms.Add(new GridRect(x,y,3,3));
                foreach(var p in room.Cells())
                    if(p.X<x+2 || p.Y<y+2 || p.X>=x+rw-2 || p.Y>=y+rh-2) Paint(p.X,p.Y);
                foreach(var p in rooms.Last().Cells()) Paint(p.X,p.Y);
            }
            else if (o.Biome == OverworldBiome.Volcanic || o.Biome == OverworldBiome.Tundra || o.Biome == OverworldBiome.Water)
            {
                int cx = room.Center.X, cy = room.Center.Y;
                rooms.Add(new GridRect(cx-1,cy-1,3,3));
                foreach (var p in room.Cells())
                {
                    int dx=Math.Abs(p.X-cx), dy=Math.Abs(p.Y-cy);
                    bool carve = o.Biome == OverworldBiome.Volcanic ? dx+dy <= Math.Max(rw,rh)/2 :
                        o.Biome == OverworldBiome.Tundra ? dx+dy < (rw+rh)/2-2 : !(dx==rw/2 && dy==rh/2);
                    if(carve) Paint(p.X,p.Y);
                }
                foreach(var p in rooms.Last().Cells()) Paint(p.X,p.Y);
            }
            else
            {
                rooms.Add(room);
                foreach (var p in room.Cells()) Paint(p.X, p.Y);
            }
        }
        int minWidth = organic || o.Biome == OverworldBiome.Volcanic ? 1 : 2;
        int maxWidth = o.Biome == OverworldBiome.Desert ? 4 : o.Biome == OverworldBiome.Mountain ? 2 : 3;
        void Connect(GridPoint a, GridPoint b)
        {
            int width = minWidth + rng.Range(maxWidth - minWidth + 1);
            void Stamp(int x, int y) { for (int dx = 0; dx < width; dx++) for (int dy = 0; dy < width; dy++) Paint(x + dx, y + dy); }
            int x = a.X, y = a.Y; Stamp(x, y);
            bool horizontal = rng.Range(2) == 0;
            while (x != b.X || y != b.Y)
            {
                bool moveX = x != b.X && (y == b.Y || (organic ? rng.Range(2) == 0 : horizontal));
                if (moveX) x += Math.Sign(b.X - x); else y += Math.Sign(b.Y - y);
                Stamp(x, y);
            }
        }
        // A connected spanning tree, with biome-specific extra connections.
        for (int i = 1; i < rooms.Count; i++)
        {
            int parent = i % nx == 0 ? i - nx : i - 1;
            if (organic && i >= nx && rng.Range(2) == 0) parent = i - nx;
            if(o.Biome==OverworldBiome.Marsh)
                parent=Enumerable.Range(Math.Max(0,i-4),Math.Min(i,4)).OrderBy(j=>Math.Abs(rooms[j].Center.X-rooms[i].Center.X)+Math.Abs(rooms[j].Center.Y-rooms[i].Center.Y)).First();
            Connect(rooms[parent].Center, rooms[i].Center);
        }
        for (int i = nx; i < rooms.Count; i++)
            if (o.Biome == OverworldBiome.Tundra || o.Biome == OverworldBiome.Grassland ||
                (o.Biome == OverworldBiome.Water && i % 2 == 0) || rng.Range(4) == 0)
                Connect(rooms[i - nx].Center, rooms[i].Center);
        if (o.Biome == OverworldBiome.Desert)
        {
            int axis = w / 2;
            for(int y=rooms[0].Center.Y;y<=rooms[(ny-1)*nx].Center.Y;y++) for(int dx=-1;dx<=1;dx++) Paint(axis+dx,y);
            foreach(var room in rooms) Connect(room.Center,new GridPoint(axis,room.Center.Y));
        }

        var layer = new GridLayer(mask);
        var cells = new List<GridPoint>();
        for (int x = 1; x < w - 1; x++) for (int y = 1; y < h - 1; y++) if (mask[x, y]) cells.Add(new GridPoint(x, y));
        var start = (o.Role == DungeonFloorRole.Exit ? rooms.Last() : rooms[0]).Center;
        var distances = GridSearch.Distances(start, p => Neighbors(layer, p));
        var stairs = cells.OrderByDescending(p => distances[p]).First();
        var reserved = new HashSet<GridPoint>(GatheringPlacement.RequiredPath(layer, start, stairs));
        // Protect orthogonal support cells of every diagonal along the safe path as well.
        foreach (var p in reserved.ToArray()) for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            if (layer.At(new GridPoint(p.X + dx, p.Y + dy))) reserved.Add(new GridPoint(p.X + dx, p.Y + dy));
        var occupied = new HashSet<GridPoint> { start, stairs };
        foreach (var p in cells.Where(p => Math.Max(Math.Abs(p.X - start.X), Math.Abs(p.Y - start.Y)) <= 3)) occupied.Add(p);
        // Stream 1500 belongs to soft locks; floors that get none keep their placements.
        var lockRandom = new SeedStream(o.Seed, 1500);
        (GridPoint Door, HashSet<GridPoint> Cells)? vault = null;
        if (o.Role == DungeonFloorRole.Regular && lockRandom.Range(100) < LockChance)
        {
            vault = FindVault(layer, cells, start, stairs, reserved, occupied, lockRandom);
            // Looping biomes rarely have a one-door side room, so carve one into solid wall. Stairs are already fixed.
            if (vault == null && CarveVault(mask, cells, start, lockRandom))
            {
                layer = new GridLayer(mask);
                cells.Clear();
                for (int x = 1; x < w - 1; x++) for (int y = 1; y < h - 1; y++) if (mask[x, y]) cells.Add(new GridPoint(x, y));
                vault = FindVault(layer, cells, start, stairs, reserved, occupied, lockRandom);
            }
        }
        if (vault != null) occupied.Add(vault.Value.Door);
        double scale = Density(cells.Count);
        int Count(int n, int cap = 64) => Math.Min(cap, (int)Math.Round(n * scale, MidpointRounding.AwayFromZero));
        List<Placement> Place(int n, bool keepExitRouteClear = false)
        {
            var result = new List<Placement>();
            foreach (var p in place.Shuffle(cells.Where(p => !occupied.Contains(p) &&
                         (!keepExitRouteClear || !reserved.Contains(p)))).Take(n))
            { result.Add(new Placement(p, place.Range(int.MaxValue))); occupied.Add(p); }
            return result;
        }
        bool regular = o.Role == DungeonFloorRole.Regular;
        // A dormant/disguised enemy on this route can leave the party unable to reach the exit.
        var enemies = Place(regular ? Count(o.EnemyCount) : 0, keepExitRouteClear: true);
        var props = new List<DungeonScenery>();
        var blocked = new HashSet<GridPoint>();
        int goldBudget = regular ? Count(o.GoldCount) : 0, itemBudget = regular ? Count(o.ItemCount) : 0;
        DungeonScenery? chest = null;
        if (vault != null)
        {
            // The vault's chest is paid from the floor budget, so total loot is unchanged.
            foreach (var p in lockRandom.Shuffle(vault.Value.Cells.Where(p => !occupied.Contains(p))).OrderBy(p => GridSight.IsRoom(layer, p) ? 0 : 1))
            {
                if (itemBudget == 0 && goldBudget == 0) break;
                blocked.Add(p);
                if (GridSearch.VisitOrder(start, q => Neighbors(layer, q, blocked)).Count != cells.Count - blocked.Count) { blocked.Remove(p); continue; }
                var reward = itemBudget > 0 ? SceneryReward.Item : SceneryReward.Gold;
                if (itemBudget > 0) itemBudget--; else goldBudget--;
                chest = new DungeonScenery(p, DungeonSceneryKind.Container, 0, lockRandom.Range(int.MaxValue), reward);
                props.Add(chest); occupied.Add(p);
                break;
            }
        }
        foreach (var kind in new[] { DungeonSceneryKind.Container, DungeonSceneryKind.Destructible, DungeonSceneryKind.Hazard })
        {
            int target = regular ? Count(kind == DungeonSceneryKind.Container ? 2 : kind == DungeonSceneryKind.Destructible ? 6 : 3) : 0;
            int placed = 0;
            foreach (var p in sceneryRandom.Shuffle(cells.Where(p => !occupied.Contains(p) && !reserved.Contains(p))))
            {
                if (placed >= target) break;
                if (kind != DungeonSceneryKind.Hazard)
                {
                    blocked.Add(p);
                    var reachable = GridSearch.VisitOrder(start, q => Neighbors(layer, q, blocked));
                    if (reachable.Count != cells.Count - blocked.Count) { blocked.Remove(p); continue; }
                }
                var reward = SceneryReward.None;
                if (kind != DungeonSceneryKind.Hazard)
                {
                    if (itemBudget > 0) { itemBudget--; reward = SceneryReward.Item; }
                    else if (goldBudget > 0) { goldBudget--; reward = SceneryReward.Gold; }
                }
                props.Add(new DungeonScenery(p, kind, kind == DungeonSceneryKind.Destructible ? 20 + 10 * o.Tier : 0, sceneryRandom.Range(int.MaxValue), reward));
                occupied.Add(p); placed++;
            }
        }
        var locks = new List<DungeonLock>();
        if (chest != null)
        {
            // The key hides in a prop the party can reach with the door still closed.
            var closed = new HashSet<GridPoint>(blocked) { vault!.Value.Door };
            var outside = GridSearch.VisitOrder(start, q => Neighbors(layer, q, closed)).ToHashSet();
            var holders = Enumerable.Range(0, props.Count).Where(i => props[i] != chest && props[i].Kind != DungeonSceneryKind.Hazard &&
                Orthogonal(props[i].Cell).Any(outside.Contains)).ToList();
            if (holders.Count > 0)
            {
                int holder = holders[lockRandom.Range(holders.Count)];
                props[holder] = props[holder].WithKey();
                locks.Add(new DungeonLock(vault.Value.Door, 40 + 20 * o.Tier, vault.Value.Cells.OrderBy(p => p.X).ThenBy(p => p.Y)));
            }
        }
        var gold = Place(goldBudget, keepExitRouteClear: true);
        var items = Place(itemBudget, keepExitRouteClear: true);
        var gathering = GatheringPlacement.Place(layer, start, stairs, occupied, o.Seed, regular ? Count(o.GatheringCount, 16) : 0);
        var walls = new bool[w, h]; var accents = new bool[w, h]; var columns = new bool[w, h]; var torches = new bool[w, h];
        var decor = new SeedStream(o.Seed, 1400);
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
        {
            walls[x, y] = !mask[x, y];
            if (mask[x, y]) accents[x, y] = GridSight.IsRoom(layer, new GridPoint(x, y));
            else if (x > 0 && y > 0 && x < w - 1 && y < h - 1 &&
                (mask[x - 1, y] || mask[x + 1, y] || mask[x, y - 1] || mask[x, y + 1]))
            { int roll = decor.Range(12); columns[x, y] = roll == 0; torches[x, y] = roll == 1; }
        }
        var result = new DungeonFloor(o.Seed, o.IsThroneFloor, new Dictionary<string, GridLayer>
        {
            [DungeonLayers.Floor] = layer, [DungeonLayers.Dungeon] = new GridLayer(walls),
            [DungeonLayers.Carpet] = new GridLayer(accents), [DungeonLayers.Columns] = new GridLayer(columns),
            [DungeonLayers.Torchlights] = new GridLayer(torches)
        }, rooms, start, stairs, enemies, gold, items, Array.Empty<Placement>(), gathering, props, locks);
        var validation = EternalEnigma.Core.Validation.DungeonFloorValidator.Validate(result,o);
        if(!validation.IsValid) throw new InvalidOperationException(string.Join("\n",validation.Errors));
        return result;
    }

    internal const int MinimumVault = 6;
    /// <summary>Percent of regular floors that try for a locked side room.</summary>
    internal const int LockChance = 70;

    /// <summary>A doorway off an existing floor cell into a 3-wide vault, with solid wall all around both.</summary>
    static bool CarveVault(bool[,] mask, List<GridPoint> cells, GridPoint start, SeedStream random)
    {
        int w = mask.GetLength(0), h = mask.GetLength(1);
        bool Wall(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !mask[x, y];
        bool Interior(int x, int y) => x > 0 && y > 0 && x < w - 1 && y < h - 1;
        var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (int depth in new[] { 3, 2 })
        {
            var fits = new List<(GridPoint From, int Dx, int Dy)>();
            foreach (var from in cells)
            foreach (var (dx, dy) in steps)
            {
                if (Math.Max(Math.Abs(from.X + dx - start.X), Math.Abs(from.Y + dy - start.Y)) <= 3) continue;
                bool fit = true;
                // Row 1 is the doorway, rows 2..depth+1 the vault; side and back rows stay wall.
                for (int row = 1; row <= depth + 2 && fit; row++)
                for (int side = -2; side <= 2 && fit; side++)
                {
                    int x = from.X + dx * row + dy * side, y = from.Y + dy * row + dx * side;
                    fit = Wall(x, y) && (Interior(x, y) || row == depth + 2 || Math.Abs(side) == 2);
                }
                if (fit) fits.Add((from, dx, dy));
            }
            if (fits.Count == 0) continue;
            var (origin, sx, sy) = fits[random.Range(fits.Count)];
            mask[origin.X + sx, origin.Y + sy] = true;
            for (int row = 2; row <= depth + 1; row++)
            for (int side = -1; side <= 1; side++)
                mask[origin.X + sx * row + sy * side, origin.Y + sy * row + sx * side] = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// A one-tile doorway whose closing seals off a side room but not the stairs. The smallest seal per
    /// side room wins, so the door sits at the room's mouth rather than further down its corridor.
    /// </summary>
    static (GridPoint Door, HashSet<GridPoint> Cells)? FindVault(GridLayer layer, List<GridPoint> cells, GridPoint start, GridPoint stairs,
        ISet<GridPoint> reserved, ISet<GridPoint> occupied, SeedStream random)
    {
        bool Open(int x, int y) => layer.At(new GridPoint(x, y));
        var found = new List<(GridPoint Door, HashSet<GridPoint> Cells)>();
        foreach (var door in cells)
        {
            if (reserved.Contains(door) || occupied.Contains(door)) continue;
            bool across = !Open(door.X - 1, door.Y) && !Open(door.X + 1, door.Y) && Open(door.X, door.Y - 1) && Open(door.X, door.Y + 1);
            bool along = !Open(door.X, door.Y - 1) && !Open(door.X, door.Y + 1) && Open(door.X - 1, door.Y) && Open(door.X + 1, door.Y);
            if (!across && !along) continue;
            var closed = new HashSet<GridPoint> { door };
            var outside = GridSearch.VisitOrder(start, p => Neighbors(layer, p, closed)).ToHashSet();
            int size = cells.Count - 1 - outside.Count;
            if (!outside.Contains(stairs) || size < MinimumVault || size > cells.Count / 4) continue;
            var sealedCells = cells.Where(p => !p.Equals(door) && !outside.Contains(p)).ToHashSet();
            if (sealedCells.Any(p => GridSight.IsRoom(layer, p))) found.Add((door, sealedCells));
        }
        var distinct = new List<(GridPoint Door, HashSet<GridPoint> Cells)>();
        foreach (var candidate in found.OrderBy(f => f.Cells.Count))
            if (!distinct.Any(d => candidate.Cells.Overlaps(d.Cells))) distinct.Add(candidate);
        return distinct.Count == 0 ? null : distinct[random.Range(distinct.Count)];
    }

    internal static IEnumerable<GridPoint> Orthogonal(GridPoint p)
    {
        yield return new GridPoint(p.X - 1, p.Y); yield return new GridPoint(p.X + 1, p.Y);
        yield return new GridPoint(p.X, p.Y - 1); yield return new GridPoint(p.X, p.Y + 1);
    }

    internal static IEnumerable<GridPoint> Neighbors(GridLayer floor, GridPoint p, HashSet<GridPoint>? blocked = null)
    {
        bool Open(GridPoint q) => floor.At(q) && (blocked == null || !blocked.Contains(q));
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            var q = new GridPoint(p.X + dx, p.Y + dy);
            if (Open(q) && (dx == 0 || dy == 0 || (Open(new GridPoint(p.X + dx, p.Y)) && Open(new GridPoint(p.X, p.Y + dy))))) yield return q;
        }
    }
}
