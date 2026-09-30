# Town services: Unity execution plan

Status: **Core done, Unity not started.** This is the work to run in the Unity
editor. Nothing below has been run in Unity; every claim about Unity behaviour is
read from source and must be verified.

## Goal

Replace the single generic shop and single trainer with split services, in every
town, in a much larger town:

- Bakery, Consumables shop, Items shop (walk-in interiors with a vendor).
- One trainer per class (10), each teaching only that class's skills.
- Towns sized from their building count and with a centred entrance corridor.

Every town offers every service for now. Variety per town, per-biome stock and
a different trainer split are later iterations (see "Later").

## What Core already provides

All in `Core/EternalEnigma.Core`, covered by `TownLayoutTests` and the existing
town tests. The Core suite passes except three overworld tests that fail on a
clean `main` too (`OverworldBiomeTests.BiomesAreSampledFromTheWholePoolWithAGrasslandStart`,
`OverworldGridTests.SeedSweepFitsDefaultDimensionsAndPreservesTopology`,
`OverworldGridTests.TerritoriesHaveBroadInteriorsSeparatedDestinationsAndShortPasses`).

| Piece | Where | Notes |
|---|---|---|
| `TownService`, `TownServiceKind`, `TownServiceCatalog` | `Progression/TownService.cs` | Ids: `bakery`, `consumables`, `items`, `trainer-<classId>`. Class ids are lowercase and match the `ClassDefinition` assets (`warrior`, `guardian`, `archer`, `elementalist`, `healer`, `bard`, `occultist`, `rogue`, `commander`, `scout`). `HasInterior` is true for **every** service: shops and trainers are both used inside their building, not at the door. |
| `CampaignLocation.Services` | `Progression/Campaign.cs` | Filled for towns only. `CampaignGenerator.Version` is now **8**; old saves are rejected by the existing version check. |
| `CampaignValidator` rules | `Validation/CampaignValidator.cs` | `towns.services`, `towns.trainers`. |
| `TownLayout.Create(seed, services, otherBuildings, allyCount)` | `Generation/TownLayout.cs` | Returns `Options` (a ready `TownPlanOptions`) and `SlotServices`. |
| `TownLayout.OptionsFor(campaignSeed, location)` | same | Falls back to legacy 15x15 when a location has no services. |
| `CampaignContext.TownLayout(townId)` / `Town(townId)` | `Progression/CampaignContext.cs` | `AuthoredTownBuildings = 2` (dungeon entrance + statue). |
| `TownPlanOptions.SizeFor`, `MaxBuildings = 24`, `SpineX` | `Generation/TownPlanOptions.cs` | 15 buildings gives a **33x33** town. `SpineX` defaults to 10 so legacy towns are unchanged; layouts use `width / 2`. |
| `TownPlan.SpineX`, `IsReserved(cell)` | `World/TownPlan.cs` | Static `IsReservedCorridor(cell, height)` keeps its old meaning (spine 10). |
| `TownPlanOptions.Detailed` | `Generation/TownPlanOptions.cs` | Set by `TownLayout`. Off (the default) keeps the original simple layout byte for byte, so the legacy 15x15 town is unchanged. |
| `BuildingFootprint`, `TownPlan.Footprints` | `World/BuildingFootprint.cs` | Per building (slot order): width 3, 5 or 7, depth 4 to 6, optional notched corner (L shape). Shop rooms are derived from the footprint, so rooms vary too. `ShopRoom.VendorAnchor` is now stored and is the back-centre floor cell. |
| Road classes | `TownLayers.MainRoads`, `Alleys` | `Roads` is still the union. Main roads are 3 cells wide (the spine and a cross avenue). Arteries are single-cell and run from every door to the network (`Roads` minus `MainRoads` minus `Alleys`). Alleys are single-cell lanes behind most buildings. |
| Prop cells | `TownLayers.Props` | Non-blocking cells for biome decoration. Densest in a 2-cell frame around the town edge, lighter along road verges, sparse elsewhere. Never on roads, buildings, doors, allies, the dungeon entrance or the entrance corridor. |

### Contract the Unity side must follow

- `TownLayout.SlotServices[i]` is the service for **building slot `i`**, in the
  plan's raster order. `null` means "an authored non-service building" (entrance
  or statue). The entrance and statue can take any `null` slot, but the entrance
  must stay a single building with `DialogId == "entrance"`.
- `layout.Options` is the complete truth for generation: width, height, shop flags,
  spine, spawn and exit. Do not re-derive any of them from `TownConfiguration`.
- Shop rooms can fail to carve; the generator retries whole attempts until the
  validator passes, so every service will have a room. A town has 13 rooms plus the
  entrance and statue, which have none and keep their walk-on-the-door behaviour.
- Interaction happens **inside**: the door tile must not open a dialog for a
  service building. The player walks in, faces the NPC at the room's vendor anchor
  and presses interact.

## Phase 0: Import Core and get a clean baseline

1. **Tools > Eternal Enigma > Core > Build and Import DLL** (needs the .NET 10 SDK, outside Play Mode).
2. Confirm the project compiles. Unity code that touches changed Core APIs:
   `CoreTownLayerGenerator` (uses the `TownPlanOptions` ctor, still source compatible)
   and `Assets/Tests/EditMode/CoreLayerGeneratorTests.cs:177` (same ctor).
3. Run the baseline and record failures before changing anything:
   `node Tools/unity-mcp.mjs harness EditMode`, `Campaign`, `Overworld`, `Town`.
   `Docs/BiomeDungeons.md` already records six failing `SkillMovementTests`; do not
   treat those as regressions.
4. Expect any test that loads an existing campaign save to reject it (generator
   version 7 to 8). The project does not migrate saves.

## Phase 1 (spike): can the TWC map change size at runtime?

This gates everything else, so do it first and stop if it fails.

`CoreTownLayerGenerator.Options` reads `twc.twcAsset.mapWidth/mapHeight`, and
`Execute` throws `Core layer is AxB but the TWC asset is CxD` on a mismatch.
`VillageLSystemAsset.asset` is 15x15. `Town.Start` already instantiates the asset
(`twc.twcAsset = Instantiate(twc.twcAsset)`), so a per-town clone can be resized.

1. In a scratch scene, instantiate the asset, set `mapWidth`/`mapHeight` to 33 on
   the clone, run `ExecuteAllBlueprintLayers()` then the build layers.
2. Check that blueprint maps are reallocated to the new size, that build layers
   (ground, roads, houses, trees, shop walls, environment layers) cover all 33x33
   cells, and that `WalkableMap.CellToWorld` and the camera bounds still line up.
3. Decide:
   - **Works:** size per town from `layout.Options.Width/Height`.
   - **Does not work:** author a 33x33 asset and make campaign towns a fixed size.
     Then change `TownPlanOptions.SizeFor` (Core) to return that constant for
     campaign layouts, and keep a test that asserts the asset size equals it.

## Phase 2: Generation adapter

Files: `Assets/Scripts/Generation/CoreTownLayerGenerator.cs`, `Assets/Scripts/Town/Town.cs`,
`Assets/Scripts/Overworld/CampaignTravelService.cs`.

1. `CoreTownLayerGenerator`: add `[NonSerialized] public TownPlanOptions OptionsOverride`.
   `Options(twc)` returns it when set. Copy it in `Clone()`. Keep the existing
   fields for the legacy (non-campaign) path.
2. Add `Configure(TileWorldCreatorAsset asset, TownPlanOptions options)` that sets
   `OptionsOverride` on every `CoreTownLayerGenerator` and, if Phase 1 allows it,
   sets `asset.mapWidth/mapHeight` from the options.
3. `Town.Start`: when `Common.Instance.CampaignContext != null`, call
   `Context.TownLayout(Configuration.Id)` and use the overload from step 2. Store
   the layout on `Town` (for example `public TownLayout Layout`). Otherwise keep
   `Configure(asset, Configuration)`.
4. `CampaignTravelService.PrepareTown` hardcodes `PartySpawn = (10, 2, 0)`. Set it
   from `layout.Options.PartySpawn` instead. `Town.GenerateAllies` and
   `TownPlayer` read `Configuration.PartySpawn`, so this is what keeps the party
   on the corridor.
5. `CoreLayoutCache.GetTown` keys on `TownPlanOptions.Equals`, which now includes
   `SpineX`. No change needed.

## Phase 2b: New layers, footprints and props (detailed towns)

The three detail layers (`MainRoads`, `Alleys`, `Props`) are **not** in `TownLayers.All`,
so the existing TWC asset ignores them until you author them. Legacy towns never
produce them.

1. **Layers.** Add `MainRoads`, `Alleys` and `Props` blueprint layers to the town TWC
   asset, each with one `CoreTownLayerGenerator`. `CoreLayerNames.Town` and the
   `CoreLayerAuthoring` rewrite/verify menu need the new names (see
   `Assets/Scripts/Editor/CoreLayerAuthoring.cs`). Never hand-edit the asset YAML.
   `Roads` stays the union, so existing road build layers keep working.
2. **Footprints.** `Houses` (and so `Roofs`) are no longer uniform 3x4 blocks:
   widths 3, 5, 7, depths 4 to 6, some L-shaped. Verify in the editor that the
   smart house footprints, roofs and connected shop walls look right on wide and
   notched shapes, and that `TownGateVisuals` still suits the entrance building when
   it is 5 or 7 wide. Shop rooms can now be up to 5 cells wide inside, so check camera
   framing while walking in and that the vendor still faces the door
   (`vendor.SetFacing(Facing.Down)`).
3. **Road styles.** Give each class its own look: wide main roads, narrower
   arteries, plain alleys. The current town uses the "modern road kit" for all of
   `Roads` (`Docs/Art/EnvironmentAudit.md`), so start by overlaying a main-road
   variant on `MainRoads` and a plain variant on `Alleys`.
4. **Props from the overworld biome decoration.** `Props` only says *where*; the
   biome supplies *what*. `TownBiomeStyle.Current` already returns the town's biome
   (from the campaign region) and `EnvironmentKit` owns the biome tree picker and
   surfaces (`BiomeModel`). Follow the decoration rules already written for the
   overworld (`Docs/OverworldVisualDetailAndModels.md`,
   `Docs/TWCBiomeStylingIntegrationPlan.md`, `Docs/Art/EnvironmentPlayground.md`):
   - pick models with a coordinate hash of town seed, cell and art version, so it
     never consumes gameplay RNG and is stable across rebuilds;
   - no colliders; props sit on walkable cells but do not change `Walkable`;
   - own a dedicated decoration root for the Town scene, and destroy it with the
     scene (the overworld cache does not apply to towns);
   - respect the cosmetic budgets (48 props per 32-cell chunk, triangle cap);
   - vary by zone: the edge frame is where larger silhouettes (rocks, bushes, the
     biome's trees) belong, the road verges and field cells get small accents.
   A 35x35 town has a few hundred `Props` cells, so check the prop count against
   the budget on seed 42 before adding density.
5. **Explorer and CLI** already render `=` main road, `:` artery, `-` alley and `p`
   prop. The CLI `--town` export writes every layer in `Plan.Layers`, so the new
   ones appear in the JSON and `GridPreview` will need colours for them.

## Phase 3: Building and vendor assets

Existing assets in `Assets/Resources/Towns/Buildings/`: `Entrance`, `Statue`,
`Shop` (dialog `shop`), `Ballista` (dialog `trainer`). `DefaultTown.asset` lists
those four.

1. Create `TownBuildingDefinition` assets whose **`Id` equals the Core service id**:
   `bakery`, `consumables`, `items`, and `trainer-warrior` ... `trainer-scout`.
   - Shops: `DialogId = shop`, distinct `DisplayName`, `VendorPrefab` (a distinct
     vendor look is the point of the flavor), and a `ShopCatalog` (Phase 6).
   - Trainers: `DialogId = trainer`, `DisplayName` like "Warrior Trainer", and a
     `VendorPrefab` for the trainer NPC who stands inside (the field is named for
     shops but works for any interior NPC; rename later if it bothers you).
   - Reuse the existing shop and trainer prefabs at first; art comes later.
2. Keep `Entrance` and `Statue` as they are. Retire the generic `Shop` and
   `Ballista` from campaign towns (keep them for the legacy non-campaign town).
3. Add a lookup, for example `TownBuildingCatalog.Find(string id)`, backed by
   `Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings")`, and validate
   that every `TownServiceCatalog.All` id resolves.
4. `TownConfiguration.Validate` currently requires unique building ids; the
   lookup must not reintroduce duplicates.

## Phase 4: Spawning buildings in slot order

File: `Assets/Scripts/Town/TownBuildingManager.cs`, called from `Town.GenerateInteractableBuildings`.

1. Replace `Spawn(configuration, positions, map)` with
   `Spawn(IReadOnlyList<TownBuildingDefinition> definitions, positions, map)`.
   Keep the old overload for legacy towns, delegating to the new one.
2. In campaign mode build the list from the layout:
   - slot with a service: the definition whose `Id == service.Id`;
   - `null` slot: the next of the authored non-service definitions (entrance first,
     then statue), taken from `Configuration.Buildings` entries whose dialog is
     `entrance` or `statue`.
3. The entrance block (`definition.DialogId == "entrance"`) already special-cases
   the gate visuals; keep it working for any slot.
4. **Trainers get an interior NPC too.** `Town.GenerateShopInteriors`
   (`Town.cs:188`) skips any building with `ShopCatalog.Count == 0`, so trainers
   would stay door-triggered. Change the condition to "this building is a service",
   for example by checking the definition id against `TownServiceCatalog.All` or by
   adding a `bool HasInterior` to `TownBuildingDefinition`. Trainers then get a
   `ShopVendor` at the vendor anchor, and `building.HasInterior` becomes true.
   - No change is needed to the interaction path: `TownPlayer` already opens
     `townMenu.OpenBuilding(vendor.Building, ...)` when the player presses interact
     facing a vendor (`TownPlayer.cs:194`), and stepping on a door only triggers
     buildings with `!HasInterior` (`TownPlayer.cs:266`). Verify the trainer dialog
     opens from the NPC and that stepping on a trainer's door opens nothing.
   - Log an error in the editor if a service ends up without an interior (a room
     that was not carved), or a shop service has an empty catalog, so a gap is
     obvious instead of silently becoming a door-triggered building.
5. `CoreTownLayerGenerator.Configure` (legacy path) still derives shop flags from
   `ShopCatalog`; leave it alone.

## Phase 5: Per-class trainers

Today `TrainerOffers.Build(ally, configuration)` lists the hero's whole kit and
`configuration.LearnableSkills` is a town-wide allowlist. `BallistaDialog` calls
it with only the configuration.

1. Add `string TrainerClassId` to `TownBuildingDefinition` (empty for non-trainers).
2. Change `TrainerOffers.Build` to take the building (or a class id) and keep only
   offers whose `Source` class `Id` equals it. Keep the existing rank, tier and
   level rules untouched.
3. Hero does not have that class (primary or secondary): show an empty list with a
   clear message ("This trainer teaches <Class>"), do not crash, do not fall back
   to the whole kit.
4. Combination heroes: a secondary-class skill appears at the secondary class's
   trainer, capped at rank 3 by the existing rules. Verify.
5. `BallistaDialog.PrepareTown(context)` has `context.Building`; pass its
   definition through. It is reached from the trainer NPC inside the building
   (Phase 4 step 4), with the controlled ally as the hero. `HandleSkillChanged` also calls `TrainerOffers.Build` and
   must use the same filter so indices stay aligned with `SkillGridItems`.
6. Legacy towns: empty `TrainerClassId` keeps today's behaviour.

## Phase 6: Shop catalogs

Author catalogs as data, not per town. Suggested starting split, to be tuned:

- **Bakery:** food and hunger items only.
- **Consumables:** healing, cure and other dungeon consumables.
- **Items:** weapons, armor, accessories, arrows, materials buyer.

Open choices to decide while authoring:

- Whether each shop buys only its own goods (`TownServices` has a sell path for
  materials, `Classes.md` says materials are sold at shops).
- Whether stock varies by tier later. Stock state is saved per `(town id, building
  id)`, so it already separates per building.

Unity tests and docs that assume one shop: `TownGameplayTests`, `ControllerFlowTests`,
`Docs/Town.md` ("Shops use the building's authored item/price/quantity...").

## Phase 7: Autoplay and tooling

- `AutoplayRunner` (around line 277) picks the first unvisited building whose
  catalog has a `WantsOffer` item. With three shops it should still work; verify
  it does not spend its time budget walking a 33x33 town and that stall detection
  (same position, same enemy HP) does not misfire on the longer walks. Check the
  trainer logic, since it now has ten buildings.
- `Tools/unity-mcp.mjs harness Autoplay` and a manual normal-mode run on seed 42.
- Explorer and CLI already use `TownLayout.OptionsFor`. The Explorer renders
  building slot indices above 9 as `B`; consider labelling slots with their
  service id if the layout is hard to read.

## Phase 8: Tests

Add or update in Unity:

- EditMode: every `TownServiceCatalog.All` id resolves to a `TownBuildingDefinition`;
  shop services have non-empty catalogs; trainer definitions carry the matching
  class id.
- PlayMode (Town harness): a campaign town loads at the layout size with the
  spawn on the centred corridor; every service building spawns at its slot; every
  service (shops and trainers) has an NPC inside a carved room; stepping on a
  service door opens no dialog; interacting with the NPC does; each trainer lists
  only its class.
- `TownTrainerRankTests` and `TownGameplayTests`: update for per-class trainers
  and three shops; keep at least one legacy-town test.
- Run, in order: `EditMode`, `Campaign`, `Overworld`, `Town`, `Classes`, then
  `Autoplay`.

## Phase 9: Docs

Update `Docs/Town.md` (authoring a town, shop interiors, shipped rules),
`Docs/CampaignFlow.md` (town services and size), `Docs/Classes.md` (trainer
section) and `Core/README.md` if the architecture notes mention town options.

## Risks and things to watch

- **Map size (Phase 1)** is the biggest unknown: build layers, biome styling,
  camera limits, minimap and any baked navigation may assume 15x15.
- **Performance:** a 33x33 town is about 4.8x the cells. Watch generation time
  and the number of spawned prefabs (15 buildings, 10 trainers' dialogs share one
  view, vendors, walls).
- **Dialog reuse:** `TownMenu.BuildingDialogs` maps dialog ids to shared views, so
  one `trainer` view serves ten buildings. Make sure dialog state does not leak
  between buildings (the class filter must be set on every open).
- **Saved shop stock:** keyed by town and building id. Changing building ids after
  release would orphan stock.
- **Retry cost:** shop-room carving can fail and trigger a full regeneration
  attempt. The Core sweep test (100 seeds) passes, but if Unity logs retries,
  raise `CellsPerBuilding` in `TownPlanOptions` rather than adding special cases.

## Later (not in this pass)

- Per-town variation through `TownServiceCatalog.ServicesFor(tier, stage)`, with
  guarantee rules in `CampaignValidator` (every stage keeps a path to every class
  and to bread and healing).
- A different trainer split (by class group or by tier).
- Stock by biome and tier; per-shop buy rules.
- Distinct art for each shop and trainer building.
