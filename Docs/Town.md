# Town configuration and gameplay

The shared scene is `Assets/Scenes/Town.unity`. Enter it through:

```csharp
TownSceneLoader.Load(configuration);
```

`TownSceneLoader.Configure(configuration)` is also available for callers that own
their scene transition/loading process. The caller must configure before loading.
Campaign navigation uses `CampaignTravelService` to clone a location-specific
configuration and stable seed. Legacy saves retain `TownSceneLoader` and their
existing town flow. See [campaign flow](CampaignFlow.md). Direct editor play uses the saved town
configuration, falling back to the default for older saves without a town ID.

## Authoring a town

Create a **Game > Town > Configuration** asset. The shipped configuration is
`Assets/Resources/Towns/DefaultTown.asset`.

- `Id` identifies the town's persistent shop stock. For save restoration after an
  application restart, place the asset at `Resources/Towns/<Id>.asset` (or have the
  invoking scene supply it).
- `Buildings` lists building definitions in placement order. The map's named
  `BuildingLayer` must supply at least that many positions. Empty lists are valid.
- `AllyCatalog` resolves saved party members, independently of who can be recruited
  locally. Include every ally who can arrive in this town. IDs must be unique;
  display names can repeat. Legacy saves without IDs resolve the first matching name.
- `Recruits` is the local candidate pool with an authored cost per ally. Candidates
  already in the party are excluded. Available `AllyLayer` positions determine how
  many candidates spawn. Catalog/prefab assets are never removed from or modified
  when restoring the party.
- `StartingParty`, `PartySpawn` and `MaxPartySize` define new-game party rules and
  placement. Party progress comes from the save when continuing.
- In campaign mode the entrance lists dungeon nodes whose `ParentTownId` matches
  this town; victory and interrupted runs return inside that town.
- Outside campaign mode, `DungeonTiers` defines available dungeon ranges. Each tier has a cumulative
  `RequiredDonation`; `LearnableSkills` defines the trainer's offerings.
- `LoseItemsOnDefeat` and `KeepGoldOnDefeat` configure the return rules.

## Adding buildings and menus

Create a **Game > Town > Building** asset with a unique ID, display name, a prefab
containing `TownBuilding`, and a dialog ID. Add it to the configuration's list.
There is no fixed four-building switch or prefab field list.

A definition can instead provide its own `DialogPrefab`. This takes precedence
over the scene binding, so a caller can introduce an entirely new building and
menu using only configuration/prefab assets, without editing the Town scene.

`TownMenu.BuildingDialogs` maps dialog IDs to views in the scene. Existing IDs are
`entrance`, `shop`, `statue`, and `trainer`. Multiple building definitions can use
the same view; shop state is separate for each town/building ID pair.

For a new interaction, add a `Dialog` subclass and register a view under a new
dialog ID. Override `PrepareTown(TownInteractionContext)` to bind the building,
party, configuration and services. Use `CloseDialog()` for every close path.
The generic building arranges interaction and return movement. Put state changes
in services rather than a dialog's close callback.

## Shop interiors

**Status**: the code below is real and implemented. It has not yet been run
inside Unity — no EditMode/PlayMode/Town harness has exercised it, and the
shipped `.asset` files have not yet had the rewrite menu run against them (see
the end of this section). Treat the Unity-side wiring as implemented-but-unverified.

The core `EternalEnigma.Core.World.TownPlan` (produced by
`EternalEnigma.Core.Generation.TownPlanGenerator`) now owns every layer of a
town, not just the overworld-facing ones: `Roads`, `Houses`, `Trees`, `Parks`,
`Roofs`, `Buildings`, `Allies`, `Dungeon`, `ShopFloor`, `ShopWalls` and the
derived `Walkable` mask (`Walkable == !(Houses | Trees | ShopWalls)`). A single
TWC blueprint action, `CoreTownLayerGenerator`, generates all of them: each
blueprint layer in the Town TWC asset carries one `CoreTownLayerGenerator`
configured with that layer's name, and its `Execute` pulls the matching layer
out of a `TownPlan` cached per-`TileWorldCreator` by `CoreLayoutCache.GetTown`.

`Town.Start` (`Assets/Scripts/Town/Town.cs`) clones the asset instance
(`Instantiate(twc.twcAsset)`, `hideFlags = HideFlags.DontSave`), calls
`CoreTownLayerGenerator.Configure(asset, Configuration)` to push the
authored `TownConfiguration.Buildings` count/shop flags and party spawn onto
every `CoreTownLayerGenerator` on the clone, sets the seed from
`Common.Instance.GameSaveData.TownSaveData.TownSeed`, and runs
`ExecuteAllBlueprintLayers()`. In campaign mode that seed is
`CampaignContext.LocationSeed(townId)` — a deterministic hash of the campaign
seed and the town's location id, so returning to the same town in the same
campaign regenerates the same plan. Once blueprint generation completes,
`CoreLayoutCache.ClearResultFlags` clears the "empty layer" failure flags TWC
would otherwise raise on an all-false `Carpet`/`ShopFloor` mask, and build
layers run; `Town.FinishGeneration` then reads the cached `TownPlan` back out
via `CoreLayoutCache.TryGetTown` and throws if it is missing (meaning the
asset's blueprint layers are not wired to `CoreTownLayerGenerator` yet).

Building slots are `TownPlan.BuildingSlots`, one `GridPoint` per door marker in
the `Buildings` layer, always in raster order (y-outer, x-inner) — the same
order `TownConfiguration.Buildings` is authored in, so index *i* in one list is
building *i* in the other. `AllyCount` on `CoreTownLayerGenerator` is an
authored knob on the TWC action itself, independent of anything in
`TownConfiguration`; it is not derived from the recruit pool.

Any building whose slot index has a `1` in the remapped shop-flag string
(`CoreTownLayerGenerator.ShopFlags`, one character per building in
configuration order) gets a carved interior instead of opening its dialog the
moment the player steps on its tile: `TownPlanGenerator` calls
`ShopInteriors.ComputeRooms` after placing doors, which carves a room north of
each shop door into the `ShopFloor`/`ShopWalls` layers and clears `Houses`
inside that footprint, skipping any room that would go out of bounds, overlap
another room, or land on an ally cell. `TownPlan.ShopRooms` exposes each
carved room, `TownPlan.ShopRoomAt(door)` looks one up by its door cell, and
`TownPlan.TryGetVendorAnchor(door, out anchor)` returns the room's vendor
anchor only if that cell is actually on `ShopFloor`.

`Town.GenerateShopInteriors()` (called from `FinishGeneration` after
`GenerateInteractableBuildings()`) instantiates a wall cube per `ShopWalls`
cell and, for each `TownBuilding` with a non-empty `ShopCatalog`, spawns a
`ShopVendor` at `TryGetVendorAnchor`'s anchor (falling back to
`ShopVendor.CreateDefault` when the definition's `VendorPrefab` is unset) and
only then sets `building.HasInterior = true`. The player walks through the
door like any other floor tile — it does not open a dialog by itself — and
faces the vendor and presses interact to open the shop, exactly like talking
to a party member. A shop whose room could not be carved (out of bounds,
overlapping, etc.) keeps `HasInterior == false` and falls back to the
original single-tile, walk-on-triggers-dialog-and-bounce-back flow, exactly
like a non-shop building.

The southern entrance corridor — `x` in 8..12, `y` from 0 up to `height / 2` —
is reserved (`TownPlan.IsReservedCorridor`) and never receives a building,
tree, park or ally, keeping the party's route to the exit clear.

The Odin-serialized `.asset` files that TWC actually reads
(`Assets/TileWorldCreator/VillageLSystemAsset.asset`, and the dungeon
equivalents referenced from `Docs/DungeonFloor.md`) are rewritten to use
`CoreTownLayerGenerator`/`CoreDungeonLayerGenerator` through
`Assets/Scripts/Editor/CoreLayerAuthoring.cs`'s menu items:
**Tools/Eternal Enigma/Core Layers/Rewrite Town Asset** rewrites every core
town layer's action stack to a single `CoreTownLayerGenerator`, preserving
blueprint layer GUIDs so build layers keep referencing the right source layer,
and adds any of `ShopFloor`, `ShopWalls`, `Walkable` that the asset is
missing. **Tools/Eternal Enigma/Core Layers/Verify Assets** checks that every
core layer has exactly one action of the right type, that every core action in
the asset shares the same options, and that build layers still reference a
valid blueprint layer GUID. Never hand-edit the `.asset` YAML; always go
through these menu items, then re-import the core DLL if core code changed
(see `Core/README.md`'s "Import into Unity" section).

Town and dungeon share `DialogController`, `Dialog`, inventory and skill views,
and the item action prefab. The controller owns modal focus, Back, input switching,
selection restoration, and exactly-once close callbacks. Town equipment changes
are immediate; dungeon actions continue through the turn system. Town consumables
and skills display details and explain that their use belongs in the dungeon.

## Shipped gameplay rules

- Purchases, training, donations, recruitment, dismissal and equipment changes
  commit and save immediately. Cancel never buys or learns anything.
- Shops use the building's authored item/price/quantity/stack-count catalog. Stock
  survives exiting to the main menu and continuing, and restocks after a completed
  dungeon return, on either victory or defeat.
- Donation thresholds are **0 / 1,000 / 3,000 / 6,000 gold** for the four default
  tiers. Locked tiers explain the requirement; the entrance rechecks eligibility.
  Completed tiers are marked after a victory.
- Victory carries back remaining bag items and equipped items with their remaining
  stack quantities. Defeat clears carried items and equipment while retaining
  earned gold. Party membership, learned skills, skill ranks and each hero's highest level reached remain.
  Leaving a dungeon through settings or the game-over Quit button uses defeat rules.
- Starting supplies are granted on a new game, not on every dungeon entrance.
- The final party member cannot be dismissed. Dismissing the controlled ally
  selects a remaining ally; dismissed equipment goes back into the bag.
- Each hero has a fixed class (or primary/secondary combination) set on its `TownAlly` prefab; the protagonist's class is chosen at new game and stored in the save (`TownAllyData.PrimaryClassId`/`SecondaryClassId`). Classes never change.
- Equipping a weapon or off-hand item outside the hero's class is refused with a message; accessories are unrestricted and unequipping is always allowed. Classless heroes can equip anything.
- The recruit/party dialog shows `Name - Class`, and the trainer header shows the selected hero's class.
- Heroes learn their class skills at the trainer. `LearnableSkills` is empty in the shipped town; a non-empty list acts as an allowlist. Each hero starts with their class's tier-1 mastery.

Saves migrate the former world/seed JSON keys. Item snapshots preserve stack
counts and per-ally equipment; the old name list remains for legacy compatibility.
Only migration code/tests retain the old scene terminology.

## Verification

```text
node Tools/unity-mcp.mjs harness Town
node Tools/unity-mcp.mjs harness EditMode
node Tools/unity-mcp.mjs harness PlayMode
node Tools/unity-mcp.mjs harness Classes
```

`TownGameplayTests` covers caller configuration, multiple shops, stock persistence,
purchase failures, training before dialog close, donation thresholds, shared
equipment actions, party safeguards, and victory/defeat inventory round trips.
