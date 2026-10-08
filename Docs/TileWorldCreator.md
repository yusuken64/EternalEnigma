# TileWorldCreator (TWC) usage guide

Summary of the official TWC v3 docs (<https://doorfortyfour.github.io/TileWorldCreator/#/>),
checked against the copy in `Assets/TileWorldCreator` (v3.1+, with extra community modifiers).
It focuses on generating terrain and towns. For how EternalEnigma feeds Core masks into TWC,
see [CoreTwcBridge.md](CoreTwcBridge.md). For town layout, see [Town.md](Town.md).
**For per-action parameters, source-verified behavior and dungeon/town layout recipes, see
[TileWorldCreatorActions.md](TileWorldCreatorActions.md).**

## Mental model

TWC has two pipelines. Both run **top to bottom**.

1. **Blueprint layers.** These are boolean masks (`bool[x,y]`, `true` = occupied). Each layer
   has an **action stack**. Actions are either **generators**, which create a mask from scratch,
   or **modifiers**, which transform the current mask or combine it with other layers.
2. **Build layers.** These instantiate prefabs from a blueprint layer. Types:
   - **3D 4-Tiles**: area fills such as ground, islands, cliffs, plateaus and building footprints.
   - **3D 6-Tiles**: path-like networks such as roads, rivers, fences, walls and pipes.
   - **Objects**: single prefabs per cell, such as trees, props, enemies, the player and houses.

Masks are the source of truth. Prefabs only present them. Layers reference other layers by
**name or GUID**, so renaming a blueprint layer can break the modifiers and build layers that
point at it.

## Setup

1. `Create > TileWorldCreator > New TileWorldCreator Asset` (a ScriptableObject that holds the stacks).
2. Add a GameObject with the `TileWorldCreator` component and assign the asset.
3. Set `Map width/height`, `Cell size` and `Map orientation` (`XZ` for 3D ground, `XY` for 2D).
4. Click `+` in Blueprint layers, name the layer and pick a preview color. Then add actions.
5. Click `+` in Build layers, pick a type, assign a blueprint layer and a tiles preset. Then execute.

Component settings that matter:

| Setting | Notes |
|---|---|
| `World name` | Name of the root object that tiles get parented under. It **must be unique** when a scene has several TWC components (for example overworld and town). |
| `Custom random seed` | Gives deterministic output. You can also call `SetCustomRandomSeed(int)` / `DisableCustomRandomSeed()`. |
| `Cell size` | Matches Unity units since v3.1. |
| `Use custom cluster cell size` | Smaller clusters mean faster partial rebuilds but slower full instantiation. |
| `Merge preview textures` | Overlays layer thumbnails so you can preview a stack. |

Map position comes from the TWC GameObject's transform.

**Render pipeline:** bundled preset materials use the Built-in RP. Under URP, run
`Edit > Render Pipeline > Universal Render Pipeline > Upgrade Project Materials` if materials show pink.

**Force rebuild:** hold **Left-Ctrl** while clicking any execute button. This also fixes the case
where clicking execute does nothing.

## Generators (create a mask)

| Generator | Best for |
|---|---|
| `CellularAutomata` | Organic landmasses, islands, caves, lakes. This is the default "terrain" generator. |
| `RandomNoise` | Scatter. Add `Expand` then `Smooth` to get blobs (forests, hills, ponds). |
| `BSPDungeon` | Rooms and corridors. Also usable as rectangular town blocks. |
| `LSystem` | Road networks from string rules. The village demo uses it. |
| `Maze` | Mazes and labyrinth districts. |
| `Circle` | Plazas, clearings, a town center, crater/mountain cores. Uses a fixed or random position. |
| `Texture` | Mask from a `Texture2D`. Can be set by script at runtime. Use it for hand-authored or imported heightmap bands. |
| `DotGrid` / `Checkerboard` | Dot lattice (posts, pillars, lot anchors) / grid of **lines** (street grids, crop rows). |
| `Pathfinding` | Path between a start layer and an end layer across a navigation layer. Use it for roads that connect points of interest. |
| `Paint` | Hand-painted mask (Ctrl + left or right mouse in the Scene view). **Required on any layer you edit at runtime.** |

Custom generators and modifiers (EternalEnigma's Core adapters are examples) use the template at
`Code/Actions/Modifiers/_MyCustomAction.cs`:

- Inherit from `TWCBlueprintAction` and implement `ITWCAction`.
- Tag with `[ActionCategory(Category = ...Generators | ...Modifiers)]` and `[ActionName(Name = "...")]`.
- Implement `bool[,] Execute(bool[,] map, TileWorldCreator twc)` and `Clone()`.
- Optionally implement `DrawGUI`/`GetGUIHeight` using `TWCGUILayout`.

## Modifiers (transform or combine masks)

| Modifier | Effect |
|---|---|
| `Add` | Union with another layer (also the way to start a layer as a copy of another). |
| `Subtract` / `Subtract From` | `this - other` / `other - this`. |
| `Overlap With` | `this ∩ other`. Use it for clipping. |
| `Overlap` | Adds `L1 ∩ L2` to this layer (it does not intersect with this layer). |
| `Invert` | NOT. Turns land into water or open space into walls. |
| `Expand` / `Shrink` | Grow or erode borders by one cell. Stack it to go further. |
| `Smooth` | Removes spurs and specks (erodes only and never fills holes). |
| `Offset` | Shifts the mask (buggy for small shifts; see the Actions doc). |
| `Select` | Keeps `border`, `edges`, `corners`, `interiorCorners`, `fill`, `random` (weighted) or `rule` cells. |
| `Select By Rule` | 3x3 neighbor rules (Occupied / Unoccupied / DontCare) with optional 90/180/270 rotations. Use it to find spots such as cliff tops next to a drop. |
| `Pick` | Keeps N random occupied cells (spawn points, landmarks). |
| `Remove Neighbours` | Intended to space cells apart, but unreliable in this version. Prefer `Overlap With DotGrid`. |
| `PlayerPosition` | Outputs one fill cell in one of 9 quadrants (TopLeft…BottomRight) for spawns and exits. |

You can chain `Select`s to isolate very specific cells.

## Build layers

### 4-Tiles vs 6-Tiles

- **4-Tiles**: `Edge`, `Exterior Corner`, `Interior Corner` and `Fill`. TWC subdivides the mask
  2x2, so each tile is **half a cell**. Enable `Scale tile by cell size`. Use it for regions
  where each cell has two or more neighbors.
- **6-Tiles**: `Single`, `Straight`, `Corner`, `Three way`, `Four way` and `Dead end`. There is no
  subdivision, so it can handle cells with one or zero neighbors. Use it for roads, rivers, walls
  and fences.
  - `Orientation Layer` rotates lone tiles toward another layer. For example, a ramp faces the
    plateau (demo `09_Ramps`). `Use only orientation layer` ignores the tile's own layer when it
    picks a rotation.

### Common options

- **Tiles presets**: you can assign several presets with **random weights** to add variety.
- **Ignore layers**: skips cells that another blueprint layer covers. This is how you stack
  materials. For example, the `Cliffs` build ignores the `Inner` (grass) layer, so the
  border gets cliff tiles and the interior gets grass (demo `11 Mix Tile-sets`).
- **Merge tiles**: combines meshes per cluster. **Enable it for large maps.** Only changed
  clusters rebuild after edits.
- **Collision**: `None`, `Mesh Collider` (merged mesh) or `Tile Collider` (box-based, with
  height and border offset).
- `Global position/scaling offset` moves or scales every tile in the layer.

### Objects layer

- Places one prefab per occupied cell. Set `Use subdivided map` to get 2x2 per cell, matching tile density.
- Has position, rotation and scale offsets, and **random** position, rotation and scale ranges.
- `Childs` scatters child prefabs within a radius, for example grass and rocks around trees.
- `Merge` combines meshes into clusters.

### Tiles presets and custom tiles

Create presets with `Create > TileWorldCreator > New 3D 4-Tiles preset` or `New 3D 6-Tiles preset`. A preset has
`Rotation offset` and `Scaling offset` to fix DCC export orientation. Custom tiles must:

- be square in X and Z, ideally 1x1 units (height doesn't matter)
- have a **centered pivot**
- for 2D, use quads with textures (Unity Tilemap/Sprites are not supported)

The bundled v3 presets are `Roads`, `House`, `Park`, `Walls`, `Platformer` and `Block`. The v2
presets are `SciFi`, `CliffIsland`, `Dungeon`, `2D Island` and `Prototype`. The official "city"
screenshot combines roads, house and park.

## Conventions

- **Name layers by role.** The docs use `Base` → `Inner` / `Island`. Derived layers (such as
  `Base` → `Inner` → `Peaks`) come **below** the layers they depend on.
- Each derived layer usually starts with `Add <source>`, then adds modifiers.
- Each surface gets **one blueprint layer and one build layer**, and the build layer **ignores
  the layers drawn on top of it**.
- Keep masks disjoint where possible (`Subtract` overlaps). Otherwise rely on Ignore layers.
- Execution order: blueprint stack → `OnBlueprintLayersComplete` → build stack → `OnBuildLayersComplete`.

## Recipe: terrain

These recipes are built from the documented primitives. Layer order matters.

```
Blueprint layers                          Build layer (type, preset, ignore)
------------------------------------------------------------------------------
Land       CellularAutomata, Smooth       Shore   4-Tiles  CliffIsland   ignore: Grass
Grass      Add Land, Shrink               Grass   4-Tiles  grass         ignore: Hills
Hills      Add Grass, Shrink x2,          Hills   4-Tiles  cliff/plateau ignore: Peaks
           Overlap With HillNoise*
Peaks      Add Hills, Shrink, Smooth      Peaks   4-Tiles  rock/snow
Water      Add Land, Invert               Water   4-Tiles  water (or a plane under the map)
River      Pathfinding(start,end,nav=Land) River  6-Tiles  river   (+ Subtract from Grass)
Ramps      Select By Rule on Hills edge,  Ramps   6-Tiles  ramp    orientation: Hills
           Pick N
Forest     RandomNoise, Expand, Smooth,   Trees   Objects  tree prefab, random rot/scale,
           Overlap With Grass,                    Childs = bushes/rocks
           Subtract Hills, Select(random 0.3)
Spawn      Add Grass, PlayerPosition      Player  Objects  spawn marker
```
\* `HillNoise` is a helper layer (`RandomNoise → Expand → Smooth`, seed override on) that must sit
**above** `Hills`, because layers can only reference layers above them.

Tips:
- Height comes from stacking 4-Tile layers, each a `Shrink` of the one below, with the
  build layers raised using `Global position offset` Y. Each extra shrink is a cliff step.
- Use the `Texture` generator plus thresholds per layer when you want authored macro shapes
  instead of cellular automata.
- Use `Select(border)` or `Select(edges)` to get beaches and shorelines for prop scatter.

## Recipe: town / village

The official demos are `02 Village` (roads around houses) and `02 Village L-System` (L-System
roads). `12 Anno-like road editor` shows interactive road painting with fast rebuilds.

```
Blueprint layers                          Build layer
------------------------------------------------------------------------------
Ground     Invert (empty → full) or Add Land Ground  4-Tiles  grass/cobble
Roads      LSystem  (or Paint, or         Roads   6-Tiles  Roads preset
           Pathfinding between gates)
Plaza      Circle (fixed center)          Plaza   4-Tiles  park/paving
Lots       Add Roads, Expand, Subtract    Houses  4-Tiles  House preset   (blocky buildings)
           Roads, Subtract Plaza          --or--  Objects  house prefabs, rotation offsets
Parks      Select(random) on Lots or      Park    4-Tiles  Park preset
           RandomNoise ∩ Lots
Walls      Add TownArea, Select(border)   Walls   6-Tiles  Walls preset (fences/town wall)
Props      Add Roads, Expand, Subtract    Lamps   Objects  lamp/market stall prefabs
           Roads, Overlap With DotGrid(4)
Gate/Spawn PlayerPosition / Pick 1        Spawn   Objects  marker
```

Tips:
- `Expand` the road mask, then `Subtract` the road itself. The result is the frontage lots
  that face the road, which is where houses go.
- `Overlap With` a `DotGrid` layer spaces buildings and props evenly. Don't rely on `Remove Neighbours`.
- `Ignore layers` on `Ground` for `Roads`/`Houses`/`Plaza` prevents doubled floor tiles.
- For houses as single prefabs that must face the road, run `Select By Rule` on the *blocks*
  mask (rotations **off**), using one layer per facing direction with a matching `Rotation offset`.
  See "Building facing pattern" in the Actions doc.

## Runtime API (`using TWC;`)

```csharp
public TileWorldCreator twc;

void OnEnable()  { twc.OnBlueprintLayersComplete += Build; }
void OnDisable() { twc.OnBlueprintLayersComplete -= Build; }
void Build(TileWorldCreator t) => t.ExecuteAllBuildLayers(false); // false = only changed clusters

void Generate(int seed)
{
    twc.SetCustomRandomSeed(seed);
    twc.ExecuteAllBlueprintLayers();          // fires OnBlueprintLayersComplete
}
```

| Method | Purpose |
|---|---|
| `ExecuteAllBlueprintLayers()` / `ExecuteBlueprintLayer(name)` | Regenerate masks. |
| `ExecuteAllBuildLayers(bool force)` / `ExecuteAllBuildLayers(priorityLayer, force)` | Instantiate. The priority layer rebuilds first, which keeps editors responsive. |
| `ExecuteBuildLayer(name or guid, bool force)` | Rebuild one build layer. |
| `GetMapOutputFromBlueprintLayer(name or guid)` | Final `bool[,]` of a layer, for gameplay queries. |
| `GetGeneratedBlueprintMap(guid)` | `WorldMap` with `worldPartitionTiles` data. |
| `GetTileData(layer, Vector3 pos)` | Tile data at a world position. |
| `ModifyMap(layer, x, y, bool)` | Edit a **Paint** layer cell (since v3.1, no need to halve coordinates). |
| `FillMap(layer, bool)` / `CopyMap(layer)` | Clear or fill a paint layer, or copy the layer's last output into it. |
| `GetAction(layer, guid or index)` | Access an action to tweak parameters by script (for example `Texture`). |
| `SaveBlueprintStack(path)` / `LoadBlueprintStack(path)` / `LoadBlueprintStackAndExecute(path)` | Saves **only the stack and settings** (small JSON), not the tiles. Regenerate after loading. |

Events: `OnBlueprintLayersComplete(TileWorldCreator)` and `OnBuildLayersComplete(TileWorldCreator)`.

## Gotchas

- **v3.1 size change**: 4-Tile maps now come out half their pre-3.1 size. Either set
  cell size to 2, or double the map size and enable `Scale tile by cell size`.
- v2 maps are not compatible with v3. v2 tiles still work.
- AOT platforms (IL2CPP) need API level .NET 4.x.
- Runtime editing only works on layers whose stack contains a `Paint` generator.
- Saved files store the recipe, not the result. The same seed and stack give the same map.

## Vendor demo references

These names refer to vendor package examples. The project removed the demo assets
after migrating production dependencies; they are not required in the checkout.
Use the committed Environment Playground for current integration examples, or the
[retained sample audit](Art/TWCSampleAudit.md) for the inspected vendor stacks.

`01 Runtime editor` (fill/clear/save/load) · `02 Village` / `02 Village L-System` · `03 Rooms` ·
`04 Platformer` (Objects-only build + selection rules) · `06 Cliff Island` · `07 Dungeon Game`
(start/end positions) · `09 Ramps` (orientation layer) · `10 Generate by Texture` ·
`11 Mix Tile-sets` (ignore layers) · `12 Anno-like road editor` · `13 Pathfinding`.
