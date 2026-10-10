using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

public static class EncounterFloorGenerator
{
    public static DungeonFloorOptions Options(int campaignSeed, CampaignLocation location, int number, OverworldBiome biome)
    {
        var encounter = location.Encounter ?? throw new ArgumentException("Location has no encounter.");
        if (number < 1 || number > encounter.FloorCount) throw new ArgumentOutOfRangeException(nameof(number));
        var kind = encounter.Kind;
        var objective = kind switch {
            EncounterKind.OvertakenCity => DungeonObjectiveKind.DefeatEnemies,
            EncounterKind.Ambush => DungeonObjectiveKind.DefeatEnemies,
            EncounterKind.BossArena => DungeonObjectiveKind.DefeatBoss,
            EncounterKind.Sokoban => DungeonObjectiveKind.PositionObjects,
            EncounterKind.LightPattern => DungeonObjectiveKind.MatchLights,
            EncounterKind.Lure => DungeonObjectiveKind.TrapEnemy,
            EncounterKind.Herding => DungeonObjectiveKind.GuideEnemy,
            EncounterKind.Prison when number == encounter.FloorCount => DungeonObjectiveKind.RecoverPossessions,
            _ => DungeonObjectiveKind.ReachExit };
        int size = kind == EncounterKind.LostWoods ? 49 + 4 * location.Tier : kind == EncounterKind.OvertakenCity ? 48 : 28;
        return new DungeonFloorOptions(CampaignContext.LocationSeed(campaignSeed, location.Id, number), size, size,
            enemyCount: encounter.IsPuzzle ? 0 : 6 + 2 * location.Tier, goldCount: 3, itemCount: 3, trapCount: 0, gatheringCount: 0,
            biome: biome, tier: location.Tier, encounter: kind, objective: new DungeonObjectiveDefinition(objective), encounterFloor: number, finalEncounterFloor: number == encounter.FloorCount);
    }

    public static DungeonFloor Generate(DungeonFloorOptions options)
    {
        if (!options.Encounter.HasValue) throw new ArgumentException("Encounter options required.");
        var random = new SeedStream(options.Seed, 1701);
        var kind = options.Encounter.Value;
        int width = options.Width, height = options.Height;
        var mask = new bool[width, height];
        var rooms = new List<GridRect>();
        var objects = new List<PuzzleObjectPlacement>();
        var objectiveEnemies = new List<ObjectiveEnemyPlacement>();
        var enemies = new List<Placement>();
        var items = new List<Placement>();
        var gold = new List<Placement>();
        var stashes = new List<RecoveryStashPlacement>();
        var witness = new List<GridPoint>();
        var start = new GridPoint(3, 3); var stairs = new GridPoint(width - 4, height - 4);
        void Rect(int x, int y, int w, int h) { var room = new GridRect(x, y, w, h); rooms.Add(room); foreach (var p in room.Cells()) mask[p.X, p.Y] = true; }
        if (kind == EncounterKind.LostWoods)
        {
            // Iterative randomized DFS: at most (side/2)^2 stack entries, no recursion.
            var stack = new Stack<GridPoint>(); var initial = new GridPoint(1, 1); stack.Push(initial); mask[1, 1] = true;
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                var unvisited = PuzzleState.CardinalNeighbors(new GridPoint(0, 0)).Select(d => new GridPoint(current.X + 2 * d.X, current.Y + 2 * d.Y))
                    .Where(p => p.X > 0 && p.Y > 0 && p.X < width - 1 && p.Y < height - 1 && !mask[p.X, p.Y]).ToArray();
                if (unvisited.Length == 0) { stack.Pop(); continue; }
                var next = unvisited[random.Range(unvisited.Length)];
                mask[(current.X + next.X) / 2, (current.Y + next.Y) / 2] = true; mask[next.X, next.Y] = true; stack.Push(next);
            }
            Rect(1, 1, 4, 4); start = new GridPoint(2, 2);
        }
        else if (kind == EncounterKind.OvertakenCity)
        {
            var town = TownPlanGenerator.Generate(new TownPlanOptions(options.Seed, width, height, new bool[7], 0, spineX: width / 2, detailed: true));
            mask = town.Layers[TownLayers.Walkable].ToArray();
            for (int x = 0; x < width; x++) { mask[x, 0] = false; mask[x, height - 1] = false; }
            for (int y = 0; y < height; y++) { mask[0, y] = false; mask[width - 1, y] = false; }
            start = town.PartySpawn; Rect(start.X - 1, start.Y - 1, 3, 3);
        }
        else if (kind == EncounterKind.BoulderCave)
        {
            var cave = BiomeDungeonGenerator.Generate(new DungeonFloorOptions(options.Seed, width, height, enemyCount: 0, goldCount: 0, itemCount: 0, trapCount: 0, gatheringCount: 0,
                layoutVersion: 1, biome: OverworldBiome.Mountain, tier: options.Tier));
            mask = cave.Layers[DungeonLayers.Floor].ToArray(); rooms.AddRange(cave.Rooms); start = cave.Start;
        }
        else if (kind == EncounterKind.Bridge)
        {
            Rect(2, height / 2 - 2, width - 4, 5); Rect(2, height / 2 - 4, 7, 9); Rect(width - 9, height / 2 - 4, 7, 9);
            start = new GridPoint(4, height / 2); stairs = new GridPoint(width - 5, height / 2);
        }
        else if (kind == EncounterKind.Prison)
        {
            Rect(2, 2, width - 4, 4); Rect(width / 2 - 2, 2, 5, height - 4); Rect(2, height - 8, width - 4, 6);
            for (int x = 2; x < width - 6; x += 6) { Rect(x, 7, 4, 5); Rect(x + 1, 5, 1, 3); }
            objects.Add(new PuzzleObjectPlacement("escape-supplies", PuzzleObjectKind.BasicSupplies, new GridPoint(4, 3)));
            if (options.FinalEncounterFloor) stashes.Add(new RecoveryStashPlacement("possessions", new GridPoint(width / 2, height - 5)));
        }
        else if (kind == EncounterKind.Lure || kind == EncounterKind.Herding)
        {
            // A broad arrival room feeds the target lane; side lanes make maneuvering and party following possible.
            Rect(2, 2, 5, 5); Rect(2, 4, width - 4, 3);
            start = new GridPoint(kind == EncounterKind.Herding ? 4 : width - 7, 5);
            var target = new GridPoint(kind == EncounterKind.Herding ? width - 4 : 8, 5);
            var enemy = new GridPoint(kind == EncounterKind.Herding ? 7 : width - 4, 5);
            objects.Add(new PuzzleObjectPlacement("target", kind == EncounterKind.Herding ? PuzzleObjectKind.Destination : PuzzleObjectKind.Trap, target));
            objectiveEnemies.Add(new ObjectiveEnemyPlacement("puzzle-target", enemy, "Slime", true));
            stairs = new GridPoint(3, 3);
        }
        else
        {
            Rect(2, 2, width - 4, height - 4);
            if (kind == EncounterKind.Ambush) start = new GridPoint(width / 2, height / 2);
        }
        bool Open(GridPoint p) => p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height && mask[p.X, p.Y];
        var reachable = GridSearch.Distances(start, p => PuzzleState.CardinalNeighbors(p).Where(Open));
        // Prune street/cavern cul-de-sacs disconnected by the exterior border, not objective-dependent blockers.
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (!reachable.ContainsKey(new GridPoint(x, y))) mask[x, y] = false;
        if (kind != EncounterKind.Bridge && kind != EncounterKind.Lure && kind != EncounterKind.Herding)
            stairs = reachable.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X).First().Key;
        if (kind == EncounterKind.Sokoban)
            BuildSokoban(options, mask, objects, witness, ref start);
        if (kind == EncounterKind.LightPattern)
            BuildLights(options, mask, objects, witness, start);
        var reserved = new HashSet<GridPoint>(objects.Select(o => o.Position)) { start, stairs };
        foreach (var stash in stashes) reserved.Add(stash.Position);
        foreach (var enemy in objectiveEnemies) reserved.Add(enemy.Position);
        foreach (var p in reachable.Keys.Where(p => Distance(p, start) <= 3)) reserved.Add(p);
        var candidates = random.Shuffle(reachable.Keys.Where(p => !reserved.Contains(p)).OrderBy(p => p.Y).ThenBy(p => p.X));
        if (kind == EncounterKind.BossArena)
        {
            var center = new GridPoint(width / 2, height / 2);
            objectiveEnemies.Add(new ObjectiveEnemyPlacement("boss", center, options.BossFamily ?? BossCatalog.Select(options.Seed, options.Tier).Family, footprint: 3));
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) reserved.Add(new GridPoint(center.X + dx, center.Y + dy));
        }
        else if (!options.Objective.AllowsReset)
        {
            IEnumerable<GridPoint> placements = candidates;
            if (kind == EncounterKind.Ambush) placements = candidates.Where(p => Distance(p, start) >= 4 && Distance(p, start) <= 7).OrderBy(p => Math.Atan2(p.Y - start.Y, p.X - start.X));
            foreach (var p in placements.Take(options.EnemyCount))
            {
                if (options.Objective.Kind == DungeonObjectiveKind.DefeatEnemies)
                    objectiveEnemies.Add(new ObjectiveEnemyPlacement("enemy-" + objectiveEnemies.Count, p));
                else enemies.Add(new Placement(p, random.Range(int.MaxValue)));
                reserved.Add(p);
            }
        }
        var lootCells = candidates.Where(p => !reserved.Contains(p)).ToList();
        if (kind == EncounterKind.LostWoods) lootCells = lootCells.OrderBy(p => PuzzleState.CardinalNeighbors(p).Count(Open)).ThenByDescending(p => reachable[p]).ToList();
        foreach (var p in lootCells.Take(options.ItemCount)) { items.Add(new Placement(p, random.Range(int.MaxValue))); reserved.Add(p); }
        foreach (var p in lootCells.Where(p => !reserved.Contains(p)).Take(options.GoldCount)) gold.Add(new Placement(p, random.Range(int.MaxValue)));
        // Independent reflection stream varies entrances and board orientation without changing difficulty.
        // Target witnesses are solved after reflection, against the real directional tie-breaking policy.
        int reflection = new SeedStream(options.Seed, 1705).Range(4);
        GridPoint Reflect(GridPoint p) => new GridPoint((reflection & 1) == 0 ? p.X : width - 1 - p.X, (reflection & 2) == 0 ? p.Y : height - 1 - p.Y);
        if (reflection != 0)
        {
            var reflected = new bool[width, height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { var p = Reflect(new GridPoint(x, y)); reflected[p.X, p.Y] = mask[x, y]; }
            mask = reflected; start = Reflect(start); stairs = Reflect(stairs);
            rooms = rooms.Select(r => new GridRect((reflection & 1) == 0 ? r.X : width - r.X - r.Width, (reflection & 2) == 0 ? r.Y : height - r.Y - r.Height, r.Width, r.Height)).ToList();
            objects = objects.Select(p => new PuzzleObjectPlacement(p.Id, p.Kind, Reflect(p.Position), p.InitialState, p.TargetState)).ToList();
            objectiveEnemies = objectiveEnemies.Select(p => new ObjectiveEnemyPlacement(p.Id, Reflect(p.Position), p.Family, p.Invulnerable, p.Footprint)).ToList();
            enemies = enemies.Select(p => new Placement(Reflect(p.Cell), p.Roll)).ToList(); items = items.Select(p => new Placement(Reflect(p.Cell), p.Roll)).ToList(); gold = gold.Select(p => new Placement(Reflect(p.Cell), p.Roll)).ToList();
            stashes = stashes.Select(p => new RecoveryStashPlacement(p.Id, Reflect(p.Position))).ToList(); witness = witness.Select(Reflect).ToList();
        }
        var inverse = new bool[width, height]; for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) inverse[x, y] = !mask[x, y];
        var layers = new Dictionary<string, GridLayer>(StringComparer.Ordinal) { [DungeonLayers.Floor] = new GridLayer(mask), [DungeonLayers.Dungeon] = new GridLayer(inverse) };
        foreach (var name in new[] { DungeonLayers.Carpet, DungeonLayers.Columns, DungeonLayers.Torchlights }) layers[name] = new GridLayer(new bool[width, height]);
        DungeonFloor Make() => new DungeonFloor(options.Seed, false, layers, rooms.Where(r => r.Cells().All(Open)), start, stairs, enemies, gold, items, Array.Empty<Placement>(),
            objective: options.Objective, puzzleObjects: objects, objectiveEnemies: objectiveEnemies, recoveryStashes: stashes, solutionWitness: witness);
        if (kind == EncounterKind.Lure || kind == EncounterKind.Herding)
        {
            // The witness uses the production target policy. Search is bounded by 24,000 actor/player states.
            witness.AddRange(SolveTarget(Make(), 24000));
        }
        var result = Make();
        if (result.Objective.AllowsReset && !PuzzleState.ValidateWitness(result)) throw new InvalidOperationException("Invalid generated puzzle witness.");
        return result;
    }
    private static int Distance(GridPoint a, GridPoint b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    internal static List<GridPoint>? Path(GridPoint from, GridPoint to, Func<GridPoint, bool> open)
    {
        var previous = new Dictionary<GridPoint, GridPoint> { [from] = from }; var queue = new Queue<GridPoint>(); queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue(); if (p.Equals(to)) { var path = new List<GridPoint>(); while (!p.Equals(from)) { path.Add(p); p = previous[p]; } path.Reverse(); return path; }
            foreach (var q in PuzzleState.CardinalNeighbors(p).Where(open)) if (!previous.ContainsKey(q)) { previous[q] = p; queue.Enqueue(q); }
        }
        return null;
    }
    private static void BuildSokoban(DungeonFloorOptions options, bool[,] mask, List<PuzzleObjectPlacement> objects, List<GridPoint> witness, ref GridPoint start)
    {
        var random = new SeedStream(options.Seed, 1801); int count = 2 + options.Tier / 2;
        var goals = Enumerable.Range(0, count).Select(i => new GridPoint(6 + i * 3, 8 + random.Range(4))).ToArray();
        var boxes = goals.ToArray(); var player = new GridPoint(3, 3); var inverse = new List<List<GridPoint>>();
        bool Floor(GridPoint p) => p.X > 2 && p.Y > 2 && p.X < mask.GetLength(0) - 3 && p.Y < mask.GetLength(1) - 3 && mask[p.X, p.Y];
        for (int step = 0; step < 12 + options.Tier * 6; step++)
        {
            var choices = random.Shuffle(Enumerable.Range(0, count).SelectMany(i => PuzzleState.CardinalNeighbors(new GridPoint(0, 0)).Select(d => (i, d))));
            foreach (var (i, d) in choices)
            {
                var behind = new GridPoint(boxes[i].X - d.X, boxes[i].Y - d.Y); var retreat = new GridPoint(behind.X - d.X, behind.Y - d.Y);
                bool Free(GridPoint p) => Floor(p) && !boxes.Contains(p);
                if (!Free(behind) || !Free(retreat)) continue;
                var path = Path(player, behind, Free); if (path == null) continue;
                var reverse = new List<GridPoint> { behind };
                var walk = new[] { player }.Concat(path).ToArray();
                for (int n = walk.Length - 2; n >= 0; n--) reverse.Add(walk[n]);
                inverse.Add(reverse); boxes[i] = behind; player = retreat; break;
            }
        }
        if (boxes.All(goals.Contains))
        {
            // Verified inverse-push fallback, independent of solver effort.
            boxes = goals.ToArray();
            boxes[0] = new GridPoint(goals[0].X - 1, goals[0].Y); player = new GridPoint(goals[0].X - 2, goals[0].Y);
            inverse.Clear(); inverse.Add(new List<GridPoint> { boxes[0] });
        }
        start = player;
        for (int i = inverse.Count - 1; i >= 0; i--) witness.AddRange(inverse[i]);
        for (int i = 0; i < count; i++) { objects.Add(new PuzzleObjectPlacement("boulder-" + i, PuzzleObjectKind.Boulder, boxes[i])); objects.Add(new PuzzleObjectPlacement("goal-" + i, PuzzleObjectKind.Goal, goals[i])); }
    }
    private static void BuildLights(DungeonFloorOptions options, bool[,] mask, List<PuzzleObjectPlacement> objects, List<GridPoint> witness, GridPoint start)
    {
        var random = new SeedStream(options.Seed, 1802); int side = 3 + options.Tier / 3;
        var cells = Enumerable.Range(0, side * side).Select(i => new GridPoint(7 + i % side, 7 + i / side)).ToArray();
        var target = cells.Select(_ => random.Range(2) == 0).ToArray(); var initial = target.ToArray(); var player = start;
        bool Open(GridPoint p) => p.X > 1 && p.Y > 1 && p.X < mask.GetLength(0) - 2 && p.Y < mask.GetLength(1) - 2 && mask[p.X, p.Y];
        foreach (var destination in random.Shuffle(cells).Take(5 + options.Tier))
        {
            var path = Path(player, destination, Open)!; witness.AddRange(path);
            foreach (var p in path) { int index = Array.IndexOf(cells, p); if (index >= 0) initial[index] = !initial[index]; }
            player = destination;
        }
        foreach (var destination in cells)
        {
            if (!initial.SequenceEqual(target)) break;
            var path = Path(player, destination, Open)!; witness.AddRange(path);
            foreach (var p in path) { int i = Array.IndexOf(cells, p); if (i >= 0) initial[i] = !initial[i]; }
            player = destination;
        }
        for (int i = 0; i < cells.Length; i++) {
            objects.Add(new PuzzleObjectPlacement("light-" + i, PuzzleObjectKind.Light, cells[i], initial[i], target[i]));
            objects.Add(new PuzzleObjectPlacement("target-" + i, PuzzleObjectKind.LightTarget, new GridPoint(cells[i].X + side + 2, cells[i].Y), target[i], target[i])); }
    }
    private static IReadOnlyList<GridPoint> SolveTarget(DungeonFloor floor, int budget)
    {
        var initial = (Player: floor.Start, Enemy: floor.ObjectiveEnemies[0].Position);
        var target = floor.PuzzleObjects.First(p => p.Kind == PuzzleObjectKind.Trap || p.Kind == PuzzleObjectKind.Destination).Position;
        var previous = new Dictionary<(GridPoint Player, GridPoint Enemy), (GridPoint Player, GridPoint Enemy)> { [initial] = initial };
        var queue = new Queue<(GridPoint Player, GridPoint Enemy)>(); queue.Enqueue(initial);
        while (queue.Count > 0 && previous.Count <= budget)
        {
            var state = queue.Dequeue();
            if (state.Enemy.Equals(target)) { var result = new List<GridPoint>(); while (!state.Equals(initial)) { result.Add(state.Player); state = previous[state]; } result.Reverse(); return result; }
            foreach (var player in PuzzleState.CardinalNeighbors(state.Player).Where(p => floor.IsWalkable(p) && !p.Equals(state.Enemy)))
            {
                var enemy = PuzzleState.NextEnemyPosition(player, state.Enemy, floor.IsWalkable, floor.Objective.Kind == DungeonObjectiveKind.GuideEnemy);
                var next = (player, enemy); if (!previous.ContainsKey(next)) { previous[next] = state; queue.Enqueue(next); }
            }
        }
        // Verified corridor fallback: both policies advance along the open target lane when the
        // player keeps the initial separation. Reflection changes direction, never this invariant.
        var fallback = new PuzzleState(floor); var steps = new List<GridPoint>();
        int direction = Math.Sign(target.X - initial.Enemy.X);
        for (int i = 0; i < floor.Width * 2 && !fallback.Complete; i++)
        {
            var next = new GridPoint(fallback.Player.X + direction, fallback.Player.Y);
            if (!fallback.TryMove(next)) break;
            steps.Add(next);
        }
        if (fallback.Complete) return steps;
        throw new InvalidOperationException("Target puzzle and its verified corridor fallback could not be solved.");
    }
}
