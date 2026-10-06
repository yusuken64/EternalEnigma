# TWC generators & modifiers: reference and layout cookbook

A detailed companion to [TileWorldCreator.md](TileWorldCreator.md). The behavior described here
comes from the **installed source** in `Assets/TileWorldCreator/Code/Actions`, not only from the
official docs. Where the two disagree, this file follows the code.

## Execution rules every recipe depends on

1. **Each blueprint layer starts from an empty mask.** Its actions run top to bottom, and each one
   receives the previous action's output.
2. **Generators add to the mask; they don't replace it.** Every generator except `Pathfinding` ORs
   its result into the incoming mask, so stacking `Circle` + `Circle` + `BSPDungeon` gives their
   union. `Pathfinding` **replaces** the mask with the path (or returns the input unchanged if it
   finds no start or target cells).
3. **Cross-layer references read that layer's last generated output.** Always reference layers
   **above** the current one. A reference to a layer below reads the previous run's (stale) result.
4. **Seeds repeat.** Every seeded generator calls `Random.InitState(currentSeed)`, and by default
   every layer and every action gets the **same** seed. Consequences:
   - Two layers with an identical `BSPDungeon`/`Maze` produce the **same dungeon**. This is how
     you extract different parts (rooms, corridors, start, end) of a single dungeon into separate layers.
   - Two `RandomNoise`/`CellularAutomata` actions with the same settings produce **identical**
     noise. For independent noise, enable the layer's **random seed override** with a distinct
     custom seed.
   - `Select (random)` and `Pick` seed from `twcAsset.randomSeed`, **not** `currentSeed`. They
     ignore per-layer overrides and stay fixed between runs unless the asset seed changes.
     `SetCustomRandomSeed()` changes it.
5. **Neighbor counts use the 3x3 block including the center cell, and out-of-bounds cells count as
   empty.** `Shrink`, `Smooth`, `Select` and `PlayerPosition` therefore treat the map edge like
   empty space. A full map still loses its outer ring when you `Shrink` it.
6. An action that outputs an empty mask is flagged `resultFailed` in the editor. Check this first
   when a layer looks empty.

Coordinates: masks are `[x, y]`. With `XZ` orientation, map `y` is world `z`. "North"/"up"
below means `+y`.

---

## Generators

### Cellular Automata
`numberOfSteps` (2), `deathLimit` (4), `birthLimit` (4)

Fills 50% of cells at random, then runs N simulation steps. A live cell survives if its 3x3
count ≥ `deathLimit`. A dead cell is born if its count > `birthLimit`.

- More steps give smoother, blobbier shapes. 3–5 steps make good caves and islands.
- Raising `deathLimit` gives less land and more holes. Raising `birthLimit` gives slower growth.
- Map edges count as empty, so land naturally pulls away from the border, which gives free shorelines.
- **Caves:** use CA, then `Invert` (walls become floor) or use CA directly as the floor. Neither
  guarantees connectivity (see [Custom actions](#gaps--custom-actions-worth-writing)).

### BSP Dungeon
`minBSPLeafWidth/Height` (10), `corridorWidth` (1), `onlyCorridors`,
`outputOnlyPlayerStartPosition`, `outputOnlyPlayerEndPosition`

Recursively splits the map into leaves (alternating horizontal and vertical, split at 25–75%)
until a leaf is smaller than the minimum. It puts a room in each leaf (50%…(size−1) of the leaf;
rooms under 2x2 are dropped) and joins sibling and parent leaves with L-shaped corridors. It
always clears a **2-cell border**.

- Smaller leaf minimums give more, smaller rooms. Use 6–8 for crowded crypts and 12–16 for halls.
- `onlyCorridors` outputs **only the corridor cells outside rooms**.
- `outputOnlyPlayerStartPosition` outputs a single cell at the center of a random room.
  `...EndPosition` outputs the center of the room **farthest** from it.
- Because of seed rule 4, the same BSP settings in several layers give consistent
  Rooms / Corridors / Start / End layers.

### L-System
`rule.results` (list of strings), `iterations` (3), `length` (4), `shortenLength`,
`randomIgnoreRuleModifier`, `chanceToIgnoreRule` (0.3), `rootSentence` (`[F]--F`)

A turtle starts at the **map center facing +y** and draws the expanded sentence:

| Char | Meaning |
|---|---|
| `F` | Draw forward `length` cells |
| `+` / `-` | Turn right / left by 90° |
| `[` / `]` | Push / pop position, direction and length (branches) |

Each `F` is kept, and a **random entry** from `results` is appended after it (recursively, up to
`iterations` levels). `shortenLength` subtracts 2 from the step length after each draw (min 1),
which tapers the branches. `randomIgnoreRuleModifier` skips expansion with the configured chance
at deeper levels for irregular growth. A cleanup pass removes cells inside thick clumps, which keeps
roads about 1 cell wide (a good match for 6-Tiles). `rootSentence` isn't shown in the custom GUI.
Its default `[F]--F` grows two arms in opposite directions from the center.

Starter rules for roads:
- `F[+F]F[-F]F` gives a dense branching village.
- `F[+F][-F]` gives a crossroads hub.
- `FF+F` gives a winding single street.
- Combine `F[+F]F` and `F[-F]F` as two results to get asymmetric variety.

### Maze
`corridorWidth` (1), `onlyOutputPlayerStartPos`, `onlyOutputPlayerEndPos`

Carves a perfect maze (`true` = passage) from a random start cell. The end cell is the passage
cell farthest from the start (straight-line distance). The start and end outputs are single cells
and follow seed rule 4.

### Random Noise
`weight` (0–1)

Per-cell random noise (Perlin sampled at random offsets, so it is effectively white noise). A
cell is set if its sample > `weight`, so **a higher weight means sparser** output. On its own it is
salt-and-pepper. Shape it with:
- `Expand` → `Smooth` for organic blobs (forests, ponds, rubble patches)
- `Smooth` only for sparse clumps
- a very high weight (0.85+) to get sparse scatter points for props

### Circle
`randomPosition`, `positionX/Y`, `radius`

A filled Euclidean disc. Stack several in one layer to get a union of circles: random lakes,
clearings, or a figure-eight plaza.

### Checkerboard (actually a **grid of lines**)
`spacing` (2), `horizontal`, `vertical`, `fillIntersections`

Sets every row where `y % spacing == 0` and/or every column where `x % spacing == 0`. With
`fillIntersections` off, cells are **toggled**, so intersections cancel out and existing cells on
the lines flip. This is the go-to **street grid**: `spacing = blockSize + 1`.

### Dot Grid
`spacing` (2)

Sets cells where `x % s == 0 && y % s == 0`. Use it for pillars, lamp posts, fence posts, crop rows
and lot anchors. Combine it with `Overlap With` to get regular spacing inside any area.

### Texture
`originalTexture`, grayscale range min/max

The texture is resized to the map size, and a cell is set if its pixel's grayscale is in range.
**The texture must have Read/Write enabled.** Texture row 0 is the bottom (`y = 0`). At runtime:
`((TWC.Actions.Texture)twc.GetAction(layer, i)).SetTexture(tex)` / `SetGrayscaleRange(min, max)`.
Use several layers with different ranges on one heightmap to get elevation bands. A
hand-drawn texture can also define a town or dungeon silhouette.

### Pathfinding
`navigationLayer`, `startLayer`, `targetLayer`, `pathfindingOption`,
`outputOnlyStartPosition`, `outputOnlyEndPosition`

4-directional A* (no diagonals) over cells that are `true` in the navigation layer.
**It replaces** the current mask.

| Option | Result |
|---|---|
| `RandomStartRandomTarget` | One path between a random start cell and a random target cell. |
| `RandomStartMultipleTargets` | A path from one random start to **every** target cell. Use it for a hub-and-spoke network. |
| `MultipleStartsRandomTarget` | A path from **every** start cell to one random target. Use it when everything leads to a central point. |

Keep the start and target layers to a few cells (`Pick`, `PlayerPosition`, `Circle` radius 0).
Thousands of targets mean thousands of A* runs. Start and target cells should lie on the
navigation layer. The `Multipath` field exists but is unused.

### Paint
A hand-painted mask that is saved in the asset. It is **required** for `ModifyMap`/`FillMap`/`CopyMap`
at runtime. A good authoring pattern: generate a layout, then use `CopyMap` to bake it into a
Paint layer and hand-fix it.

---

## Modifiers

### Boolean set operations

| Modifier | Exact operation | Notes |
|---|---|---|
| `Add` (layer) | `M = M ∪ L` | Usually the first action of a derived layer. |
| `Subtract` (layer) | `M = M − L` | |
| `Subtract From` (layer) | `M = L − M` | Turns "what I computed" into "everything else in L". |
| `Overlap With` (layer) | `M = M ∩ L` | **Use this for clipping.** |
| `Overlap` (layer1, layer2) | `M = M ∪ (L1 ∩ L2)` | Does **not** intersect with the current mask. Put it on an empty layer to get a plain `L1 ∩ L2`. |
| `Invert` | `M = ¬M` | |

### Morphology

| Modifier | Behavior |
|---|---|
| `Expand` | Dilate by 1 cell in 8 directions (squares off corners). Stack it for bigger radii. |
| `Shrink` | Erode by 1 cell in 8 directions. The map edge counts as empty. |
| `Smooth` (`smoothCount`) | Each pass **removes** cells with ≤3 occupied neighbors. It only erodes thin spurs and specks and **never fills holes**. To fill small holes, use `Invert → Smooth → Invert`. |
| `Offset` (x, y) | Shift the mask. **Buggy:** a shift smaller than the shape's own extent loses the overlapping cells (a 3-wide row shifted by 1 becomes 1 cell), and cells that would leave the map stay in place. Use it only for shifts larger than the shape, or write a fixed version. |

Useful compositions:
- **Outline / wall ring outside an area:** `Add A, Expand, Subtract A`
- **Inner border ring:** `Add A, Select(border)` (or `Add A, Subtract (A Shrunk)`)
- **Opening** (removes thin bridges): `Shrink, Expand`
- **Closing** (fills narrow gaps): `Expand, Shrink`
- **Distance band** (cells 2–3 away from A): layer `A2 = Add A, Expand×2`, then
  `Add A, Expand×3, Subtract A2`

### Selection
The output **replaces** the mask with the selected subset.

| `Select` type | Cells kept (occupied cells only) |
|---|---|
| `border` | Any cell with at least one empty 8-neighbor. |
| `edges` | Straight-edge cells (one full side missing). |
| `corners` | Exterior (convex) corners. |
| `interiorCorners` | Exactly one empty neighbor, which is a concave notch. |
| `fill` | All 8 neighbors occupied (deep interior). |
| `random` | Each cell with probability `randomSelectionWeight`. |
| `rule` | Exact 3x3 match: checked cells must be occupied and unchecked cells must be **empty**. Several rules are ORed. |

**`Select By Rule`** is the more flexible version. Each of the 8 neighbors is `Occupied`,
`Unoccupied` or `DontCare`, and the center must be occupied. Each rule has optional
**90/180/270° rotations**, and rules are ORed. **Out-of-bounds neighbors always pass.** Because
it uses DontCare and rotations, it is the main tool for finding contextual cells:

| Goal (applied to the given mask) | Rule (N/E/S/W = orthogonal, others DontCare unless stated) |
|---|---|
| Corridor dead ends (on a corridor mask) | Exactly one orthogonal neighbor `Occupied`, the other 3 `Unoccupied`, rotations on. |
| Corridor bends | N `Occupied`, E `Occupied`, S `Unoccupied`, W `Unoccupied`, rotations on. |
| T-junctions / crossroads | 3 or 4 orthogonal `Occupied`. |
| Cells facing open space to the south (on a lot/block mask) | S `Unoccupied`, rest DontCare, rotations **off**. Make one layer per direction. |
| 1-wide chokepoints / doorways (on a floor mask) | N & S `Unoccupied`, E & W `Occupied`, plus the 90° rotation. |

Check in the editor which UI grid cell maps to which world direction before you rely on it.

### Picking and placement

| Modifier | Behavior |
|---|---|
| `Pick` (`PickCount`) | Keeps N random occupied cells. Uses the asset seed (rule 4). |
| `PlayerPosition` (quadrant) | Splits the map 3x3 and returns **one** cell in the chosen ninth: the center if it is deep interior (≥7 occupied neighbors), otherwise the first such cell found. It can return **nothing** if the ninth has no interior cell, so feed it a solid area. |
| `Remove Neighbours` (`radius`) | Meant to space cells apart. **Unreliable:** it only checks an asymmetric set of diagonal offsets and never removes orthogonal neighbors. Don't rely on it for spacing. Use `Overlap With DotGrid` for regular spacing, or write a Poisson-disc action. |

### Project actions (EternalEnigma)
- `Core Dungeon Layer` / `Core Town Layer` (generators) emit a named layer from Core's
  seeded `DungeonFloor` / `TownPlan` (see [CoreTwcBridge.md](CoreTwcBridge.md)). These are the
  authoritative layouts. TWC actions below them should only **dress** that output.
- `Mountain top patch noise` (modifier) adds seeded patches clipped to a plateau.
- `CampaignLayerAction` imports overworld masks.

---

## Dungeon layouts

Notation: `Layer = actions`, then `→ build layer`. Create the layers **in the order listed**.
Every layer a recipe references is listed above the layer that uses it. Shared helper layers go
first:
```
Dots       = DotGrid(spacing 3)                  (regular spacing)
Blobs      = RandomNoise(0.55), Expand, Smooth 2 (seed override ON; organic patches)
```

### 1. Classic rooms and corridors (BSP)
```
Floor      = BSPDungeon(leaf 8, corridor 1)                 → 4-Tiles Dungeon preset
                                                               (the edges/corners become walls)
Corridors  = BSPDungeon(same, onlyCorridors)
Rooms      = BSPDungeon(same), Subtract Corridors
Start      = BSPDungeon(same, outputOnlyPlayerStartPosition) → Objects: player/stairs up
Exit       = BSPDungeon(same, outputOnlyPlayerEndPosition)   → Objects: stairs down / boss
```
These layers depend on all BSP layers sharing the seed, so **don't** enable seed overrides on them.

Dressing layers built from these:
```
RoomCore   = Add Rooms, Shrink                         (cells not touching a wall)
RoomEdge   = Add Rooms, Select(border)
DoorZone   = Add Corridors, Expand                     (corridor cells + 1)
Doors      = Add DoorZone, Overlap With RoomEdge       → Objects: door prefab
WallProps  = Add RoomEdge, Subtract DoorZone           → Objects: torches, banners
Pillars    = Add RoomCore, Shrink, Overlap With Dots   → Objects: pillars
DeadEnds   = Add Corridors, Select By Rule(dead end)   → Objects: chests, secret switches
SafeZone   = Add Start, Expand ×4
Enemies    = Add RoomCore, Subtract SafeZone, Subtract Exit, Select(random 0.06)
             → Objects: spawners, random rotation
Treasure   = Add RoomCore, Select(corners), Pick 3     → Objects: chests
```

### 2. Natural caves (cellular automata)
```
Cave       = CellularAutomata(steps 4, death 4, birth 4), Smooth 1   → 4-Tiles cave/rock preset
CaveFilled = Add Cave, Invert, Smooth 1, Invert          (fills pinholes)
Pools      = Add CaveFilled, Shrink ×2, Overlap With Blobs → 4-Tiles water
Stalagmite = Add CaveFilled, Select(interiorCorners)      → Objects
Start/Exit = Add CaveFilled, PlayerPosition(BottomLeft / TopRight)
Route      = Pathfinding(nav CaveFilled, start Start, target Exit) (debug: verifies the route)
```
TWC has no "keep largest region" action, so isolated pockets can occur. If `Route` comes out
empty, the cave is disconnected: reseed, or add the custom action below.

### 3. Hybrid: BSP structure with ragged, cave-like rooms
```
Corridors  = BSPDungeon(leaf 10, onlyCorridors)
Rooms      = BSPDungeon(same), Subtract Corridors
Rough      = CellularAutomata(steps 3)                      (seed override ON)
RoomCore   = Add Rooms, Shrink ×2                           (guarantees the center survives)
Ragged     = Add Rooms, Expand, Overlap With Rough, Add RoomCore
Floor      = Add Ragged, Add Corridors, Smooth 1            → 4-Tiles
```

### 4. Labyrinth with rooms
```
Maze       = Maze(corridorWidth 2)
Halls      = Circle(random, r 3), Circle(random, r 4), Circle(random, r 3)   (seed override ON)
Floor      = Add Maze, Add Halls                            → 4-Tiles
Start/End  = Maze(same, onlyOutputPlayerStartPos / EndPos)
```

### 5. Multi-level look (pits, platforms, ramps)
```
Floor      = (any of the above, with RoomCore)
Pit        = Add RoomCore, Shrink, Overlap With Blobs     → 4-Tiles chasm/void
Platform   = Add RoomCore, Shrink ×2, Subtract Pit        → 4-Tiles raised (Y offset)
                                                            (or a Paint layer for authored ones)
Ramps      = Add Platform, Select(edges), Pick 2           → 6-Tiles ramp, Orientation = Platform
```
Put `Pit` and `Platform` in the Floor build layer's **Ignore layers** so the floor doesn't render under them.
For more ramp setups, see demo `09_Ramps`.

---

## Town layouts

### 1. Grid town (Checkerboard)
```
TownArea   = Circle(fixed center, r 14)   (or a Texture/Paint silhouette)
Streets    = Checkerboard(spacing 6), Overlap With TownArea        → 6-Tiles Roads
Plaza      = Circle(center, r 3)                                   → 4-Tiles paving
Blocks     = Add TownArea, Subtract Streets, Subtract Plaza
Buildings  = Add Blocks, Shrink                                    → 4-Tiles House preset
Sidewalk   = Add Blocks, Subtract Buildings                        → 4-Tiles pavement
Yards      = Add Buildings, Select(fill), Overlap With Dots        → Objects: chimneys, wells
```
`spacing 6` gives 5x5 blocks. `Shrink` leaves a 1-cell sidewalk and a 3x3 building core. For
smaller, individual houses instead of block-filling buildings, use the
[facing pattern](#building-facing-pattern).

### 2. Organic village (L-System)
```
Land       = CellularAutomata(steps 5), Smooth 1                   (or the overworld mask)
Roads      = LSystem(results ["F[+F]F[-F]F"], iterations 2, length 5),
             Overlap With Land                                     → 6-Tiles Roads
Frontage   = Add Roads, Expand, Subtract Roads, Overlap With Land  (cells next to road)
Lots       = Add Frontage, Overlap With Dots
Houses     = Add Lots, Select(random 0.6)                          → Objects: house prefabs
RoadBuffer = Add Roads, Expand ×3
Fields     = Add Land, Subtract RoadBuffer, Overlap With Blobs     → 4-Tiles farmland
Rows       = Checkerboard(spacing 2, horizontal only)
Crops      = Add Fields, Shrink, Overlap With Rows                 → Objects: crop rows
Trees      = RandomNoise(0.5), Expand, Smooth 2                    (own seed override, not Blobs')
Forest     = Add Trees, Overlap With Land, Subtract RoadBuffer,
             Subtract Fields, Select(random 0.3)                   → Objects: trees + Childs
Well       = Add Roads, Select By Rule(crossroads), Pick 1         → Objects: well/statue
```
Demo `02 Village L-System` uses this approach.

### 3. Points of interest linked by paths (Pathfinding)
```
Land, Water, Cliffs = (terrain layers, see TileWorldCreator.md)
Walkable   = Add Land, Subtract Water, Subtract Cliffs
Center     = Circle(center, r 0)            (or PlayerPosition(MiddleCenter) on Walkable)
POIs       = Add Walkable, Select(fill), Overlap With Dots(8), Pick 5
Roads      = Pathfinding(nav Walkable, start Center, target POIs,
                         RandomStartMultipleTargets)           → 6-Tiles Roads
                         (add Expand afterwards for 2-wide roads, built as 4-Tiles)
```
This works for hamlets, farm networks and overworld roads between towns. If you need roads to
cross water, use `Land` (water included) as the navigation layer and add
`Bridges = Overlap(Roads, Water)` on an empty layer below `Roads` → Objects: bridge.

### 4. Walled town
```
Inside     = (TownArea from recipe 1, or a Paint layer)
Roads      = (any road layer; must extend past Inside)
Wall       = Add Inside, Expand, Subtract Inside                   → 6-Tiles Walls
Gates      = Add Wall, Overlap With Roads                          → Objects: gate (rotated)
WallFinal  = Add Wall, Subtract Gates                              (assign this to the build)
Towers     = Add WallFinal, Overlap With Dots(6)                   → Objects: tower
Moat       = Add Wall, Expand ×2, Subtract Wall, Subtract Inside   → 4-Tiles water
```
Gates are wherever the roads cross the wall ring, so roads must leave the town.

### Building facing pattern
Single-prefab houses must face a street. Work on the **Blocks** mask, where an empty neighbor
means street (or the outside). Use `Select By Rule` with rotations **off**:
```
FaceS = Add Blocks, Select By Rule(S Unoccupied)                    → Objects rot 180
FaceN = Add Blocks, Select By Rule(N Unoccupied), Subtract FaceS    → Objects rot 0
FaceE = Add Blocks, Select By Rule(E Unoccupied), Subtract FaceS, Subtract FaceN → rot 90
FaceW = Add Blocks, Select By Rule(W Unoccupied), Subtract FaceS, Subtract FaceN,
        Subtract FaceE                                              → rot 270
```
The `Subtract`s give corner lots exactly one house. To thin the houses out, `Overlap With Dots`
before the rule step. Check the rotation offsets against your prefab's forward axis.

### Districts
Split the town with `Texture` ranges or `Circle`s into `Market`, `Residential`, `Noble`
and so on, and `Overlap With` each district before placing its props. Assign each district's
build layer its own **tiles presets with weights** for variety, and use **Ignore layers** so the
shared ground doesn't double up under district tiles.

---

## Gaps & custom actions worth writing

Use the template at `Code/Actions/Modifiers/_MyCustomAction.cs` (inherit from
`TWCBlueprintAction` and implement `ITWCAction`). The installed actions leave these gaps:

| Need | Why |
|---|---|
| **Keep largest region** / **remove regions < N** | CA caves and noise produce disconnected pockets. Flood fill (4-connected) and keep the largest region. |
| **Connect regions** | Run A* or straight L-corridors between region centroids. This guarantees caves can be traversed. |
| **Poisson-disc spacing** | Replaces the unreliable `Remove Neighbours` for houses, trees and enemies. |
| **Correct Offset** | Read from a copy, write to a fresh mask, and drop cells that leave the map. |
| **Seeded per-action RNG** | Seed from `twc.currentSeed ^ hash(layer/action name)` so stacked noise actions differ without per-layer overrides. |

Minimal largest-region modifier body:
```csharp
public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
{
    int w = map.GetLength(0), h = map.GetLength(1);
    var seen = new bool[w, h];
    List<Vector2Int> best = new List<Vector2Int>();
    var stack = new Stack<Vector2Int>();
    for (int x = 0; x < w; x++)
    for (int y = 0; y < h; y++)
    {
        if (!map[x, y] || seen[x, y]) continue;
        var region = new List<Vector2Int>();
        stack.Push(new Vector2Int(x, y)); seen[x, y] = true;
        while (stack.Count > 0)
        {
            var p = stack.Pop(); region.Add(p);
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var n = p + d;
                if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h || seen[n.x, n.y] || !map[n.x, n.y]) continue;
                seen[n.x, n.y] = true; stack.Push(n);
            }
        }
        if (region.Count > best.Count) best = region;
    }
    var result = new bool[w, h];
    foreach (var p in best) result[p.x, p.y] = true;
    return result;
}
```

## Debugging a stack

- Give every layer a distinct **preview color** and enable `Merge preview textures` to see the composition.
- Toggle individual actions off (each action has an `active` flag) to bisect a stack.
- An empty layer means an action has `resultFailed` set. Common causes: a missing layer
  reference, a reference to a layer **below**, `PlayerPosition` finding no interior cell, or
  `Pathfinding` start/target cells that aren't on the navigation layer.
- If two "random" layers look identical, that's seed rule 4. Enable a per-layer seed override.
- If execute does nothing, Ctrl-click it to force a rebuild.
