# Environment asset audit

Measured in Unity before replacing art. Triangle counts are imported mesh index counts for active prefab renderers, including repeated instances; renderer/material slots are not GPU timings.

| Asset | Triangles | Vertices | Renderers / slots | Colliders | Bounds |
|---|---:|---:|---:|---:|---|
| Assets/Overworld/Dungeon.prefab | 80 | 88 | 1 / 1 | 0 | (1.10, 1.10, 1.30) |
| Assets/Overworld/Gate.prefab | 12 | 24 | 1 / 1 | 0 | (0.30, 1.90, 1.60) |
| Assets/Overworld/Landmark.prefab | 768 | 515 | 1 / 1 | 0 | (0.85, 0.85, 0.85) |
| Assets/Overworld/Town.prefab | 12 | 24 | 1 / 1 | 0 | (1.10, 1.10, 1.40) |
| Assets/Prefabs/Town/Ballista.prefab | 1616 | 2730 | 6 / 13 | 0 | (1.67, 1.44, 1.09) |
| Assets/Prefabs/Town/Entrance.prefab | 1852 | 2824 | 2 / 3 | 0 | (2.08, 2.19, 2.73) |
| Assets/Prefabs/Town/Shop.prefab | 398 | 638 | 2 / 4 | 0 | (1.69, 1.69, 2.54) |
| Assets/Prefabs/Town/Statue.prefab | 248 | 432 | 6 / 10 | 0 | (1.60, 1.60, 3.02) |
| Assets/Prefabs/Town/TownPlayer.prefab | 2024 | 4294 | 5 / 5 | 0 | (0.76, 0.71, 0.77) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/block_edge_mesh.prefab | 12 | 16 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/block_edgeRamp_mesh.prefab | 96 | 90 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/block_exteriorCorner_mesh.prefab | 54 | 40 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/block_fill_mesh.prefab | 2 | 4 | 1 / 1 | 0 | (1.00, 0.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/block_interiorCorner_mesh.prefab | 30 | 46 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/blockRamp_edge_mesh.prefab | 2 | 4 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/blockRamp_exteriorCorner_mesh.prefab | 2 | 6 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/Prefabs/blockRamp_interiorCorner_mesh.prefab | 2 | 6 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/house_edge_mesh.prefab | 78 | 122 | 1 / 5 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/house_edge2_mesh.prefab | 38 | 70 | 1 / 5 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/house_exteriorCorner_mesh.prefab | 154 | 234 | 1 / 5 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/house_fill_mesh.prefab | 2 | 4 | 1 / 1 | 0 | (1.00, 0.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/house_interiorCorner_mesh.prefab | 34 | 64 | 1 / 4 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/Roof/roof_edge_mesh.prefab | 2 | 4 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/Roof/roof_exteriorCorner_mesh.prefab | 2 | 6 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/Roof/roof_fill_mesh.prefab | 18 | 28 | 1 / 2 | 0 | (1.00, 0.02, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/House/Prefabs/Roof/roof_interiorCorner_mesh.prefab | 2 | 6 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Park/Prefabs/park_edge_mesh.prefab | 10 | 20 | 1 / 2 | 0 | (1.00, 0.07, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Park/Prefabs/park_exteriorCorner_mesh.prefab | 58 | 80 | 1 / 2 | 0 | (1.00, 0.07, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Park/Prefabs/park_fill_mesh.prefab | 2 | 4 | 1 / 1 | 0 | (1.00, 0.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Park/Prefabs/park_interiorCorner_mesh.prefab | 22 | 42 | 1 / 2 | 0 | (1.00, 0.07, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Park/Prefabs/Tree.prefab | 1062 | 767 | 1 / 2 | 0 | (2.39, 4.66, 2.55) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Platformer/Prefabs/bottomTile_mesh.prefab | 332 | 202 | 1 / 2 | 0 | (1.04, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Platformer/Prefabs/fillTile_mesh.prefab | 188 | 118 | 1 / 1 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Platformer/Prefabs/topTile_mesh.prefab | 476 | 278 | 1 / 2 | 0 | (1.00, 1.05, 1.05) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Platformer/Prefabs/woodTile_mesh.prefab | 284 | 262 | 1 / 2 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Road/Prefabs/road_edge_mesh.prefab | 14 | 28 | 1 / 2 | 0 | (1.03, 0.02, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Road/Prefabs/road_extCorner_mesh.prefab | 82 | 112 | 1 / 2 | 0 | (1.16, 0.02, 1.16) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Road/Prefabs/road_fill_mesh.prefab | 2 | 4 | 1 / 1 | 0 | (1.00, 0.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Road/Prefabs/road_intCorner_mesh.prefab | 82 | 136 | 1 / 2 | 0 | (1.03, 0.02, 1.03) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Road/Prefabs/streetLight_mesh.prefab | 92 | 137 | 1 / 1 | 0 | (0.31, 0.64, 0.07) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/innerWall_edge_mesh.prefab | 60 | 72 | 1 / 2 | 0 | (0.02, 0.98, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/innerWall_exteriorCorner_mesh.prefab | 28 | 48 | 1 / 2 | 0 | (0.02, 0.98, 0.02) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/innerWall_interiorCorner_mesh.prefab | 116 | 147 | 1 / 2 | 0 | (1.00, 0.98, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/wall_edge_mesh.prefab | 94 | 128 | 1 / 3 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/wall_exteriorCorner_mesh.prefab | 182 | 250 | 1 / 3 | 0 | (1.00, 1.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/wall_fill_mesh.prefab | 2 | 4 | 1 / 1 | 0 | (1.00, 0.00, 1.00) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/wall_interiorCorner_mesh.prefab | 18 | 40 | 1 / 2 | 0 | (1.05, 1.00, 1.05) |
| Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Walls/Prefabs/window_mesh.prefab | 84 | 136 | 1 / 2 | 0 | (0.07, 0.51, 0.56) |

## TWC authoring


### Assets/Overworld/CampaignTerrain.asset

Map 256 x 256; cell 2.

- Blueprint `Parks`: 
- Blueprint `Roads`: 
- Build `Parks`: InstantiateTiles; active=True; source=Parks
- Build `Roads`: InstantiateTiles; active=True; source=Roads

### Assets/TileWorldCreator/VillageLSystemAsset.asset

Map 15 x 15; cell 2.

- Blueprint `Roads`: CoreTownLayerGenerator
- Blueprint `Houses`: CoreTownLayerGenerator
- Blueprint `Roofs`: CoreTownLayerGenerator
- Blueprint `Parks`: CoreTownLayerGenerator
- Blueprint `Trees`: CoreTownLayerGenerator
- Blueprint `Dungeon`: CoreTownLayerGenerator
- Blueprint `Buildings`: CoreTownLayerGenerator
- Blueprint `Allies`: CoreTownLayerGenerator
- Blueprint `ShopFloor`: CoreTownLayerGenerator
- Blueprint `ShopWalls`: CoreTownLayerGenerator
- Blueprint `Walkable`: CoreTownLayerGenerator
- Build `Houses`: InstantiateTiles; active=True; source=Houses
- Build `Roofs`: InstantiateTiles; active=True; source=Roofs
- Build `Roads`: InstantiateTiles; active=True; source=Roads
- Build `Parks`: InstantiateTiles; active=True; source=Parks
- Build `Trees`: InstantiateObjects; active=True; source=Trees

## Material and texture specifications

| Material | Shader | Texture size |
|---|---|---|
| Assets/Overworld/BiomeBarrier.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeBridge.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeDesert.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeForest.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeGrassland.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeMarsh.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeMountain.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeRoad.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeTundra.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeVolcanic.mat | Standard | 32 x 32 |
| Assets/Overworld/BiomeWater.mat | Standard | 32 x 32 |
| Assets/Overworld/Dungeon.mat | Standard | none |
| Assets/Overworld/Gate.mat | Standard | none |
| Assets/Overworld/Landmark.mat | Standard | none |
| Assets/Overworld/Town.mat | Standard | none |

## Current runtime geometry

OverworldBiomeRenderer emits 2 triangles per floor cell, 4 per raised mountain/tree cell, in 32-cell chunks. It currently disables all TWC renderers, so cosmetic layers require a visibility exception. Overworld uses XY ground, negative Z elevation, 2-unit cells. Town roads currently use the modern road kit. Terrain navigation is owned by core masks; all new decorations must be collider-free and excluded from road, bridge, town, marker, gate and warp clearances.

