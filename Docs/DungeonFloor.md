# Dungeon floors and town plans

Core `DungeonFloorGenerator.Generate(DungeonFloorOptions)` and
`TownPlanGenerator.Generate(TownPlanOptions)` return immutable, validated grid data.
Unity adapters copy named layers into TWC; gameplay reads the Core floor/walkable masks.

## Dungeon profiles

The two campaign starter dungeons use original 32x32 BSP regular floors and the fixed 12x12
throne template. Other campaign locations use [biome profiles](BiomeDungeons.md), with larger
tier-scaled dimensions and entry/regular/exit roles. Core's layout selector remains part of
generation options/cache identity; the save stores `UseBiomeLayout`, not a schema version.

Dungeon layers are Floor, Dungeon, Carpet, Columns, Torchlights, Start and Stairs.
Dungeon is the inverse floor mask; stairs are reachable. Seeded placement records contain
cell/roll pairs for enemies, gold, items, traps and gathering. Biome profiles also supply
containers, destructibles and hazards with safe placement and reward-budget constraints.
Entry/exit rooms omit normal encounters and blocking scenery.

`CampaignContext.LocationSeed` hashes campaign seed, location and floor. Tier floor ranges
are 1-5, 5-10, 10-20, 20-30 and 30-40. Revisiting the same campaign/location/floor uses the
same generation inputs. Combat progress and exact floor state are not persisted.

## Town plans

Base layers are Roads, Houses, Trees, Parks, Roofs, Buildings, Allies, Dungeon, ShopFloor,
ShopWalls and Walkable. Base walkability excludes houses, trees and shop walls;
furnished plans also exclude furniture/counter occupancy. Detailed plans additionally
provide MainRoads, Alleys and Props, with furnishing/carpet/counter data when enabled. `BuildingSlots`, `Footprints`, `ShopRooms`, vendor anchors,
PartySpawn and Exit describe placement and interaction independently of art.

`TownLayout` sizes campaign towns and assigns five service kinds, one authored entrance and
seven residential plots in stable seeded order. Unity's `CampaignTownLayout` maps that order
to authored definitions. Residential slots have furnished rooms when enabled; town-0's first
residential slot is the home, with a bed and explicit save flow.
All services have walk-in rooms. The spine, party spawn and exit are centered on the generated
map. Custom noncampaign configurations retain explicit building/spawn options.

Dungeon movement requires both diagonal side cells open. Town movement permits corner cutting.
Occupancy, turn actions and party following remain Unity gameplay responsibilities.

## TWC integration and maintenance

`CoreDungeonLayerGenerator` and `CoreTownLayerGenerator` share cached Core results per creator.
`CoreLayoutCache` keys results by complete options. Campaign town configuration sets dimensions,
spine, detailed mode, flags, spawn/exit and ally count consistently on every Core layer; missing
detail layers are added to the runtime clone. Blueprint GUIDs used by authored build layers survive.

The committed dungeon/throne/village assets are already Core-backed. Completed asset rewrite
menus are removed. **Tools > Eternal Enigma > Core Layers > Verify Assets** remains for checks.
Edit Odin assets through Unity, and rebuild/import the DLL after Core changes with
**Core > Build and Import DLL**. Saved preview rebuilding uses the same detailed town options.

## Headless inspection

```powershell
dotnet run --project Core/EternalEnigma.Core/EternalEnigma.Campaign.Explorer -- --seed 42 --town town-0
dotnet run --project Core/EternalEnigma.Core/EternalEnigma.Campaign.Cli -- --seed 42 --dungeon story-0 --floor 2 --output Temp/DungeonPreview
```

Core tests cover layouts and validation. Unity `TownLayoutIntegrationTests` compares every
layer/options against Core and `CampaignTownServiceTests` covers production services/inn restore.
Layouts are regenerated, so changes to generation can change future visits; old save schemas
are unsupported. See [town gameplay](Town.md), [Core](../Core/README.md) and [themes](DungeonThemes.md).
