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

Any building with a non-empty `ShopCatalog` gets a small procedurally carved
interior instead of opening its dialog the moment the player steps on its tile.
`ShopInteriorCarver` runs as part of the Town's TileWorldCreator generation: it
reads the `Buildings` layer's raster-ordered markers (the same order
`TownConfiguration.Buildings` is authored in) and, for each one whose building
has a shop catalog, carves a small room north of that marker into two new
layers (`ShopFloor`, `ShopWalls`) and clears the `Houses`/`Trees` layers within
that footprint so scenery never overlaps the room. A room that would go out of
bounds, overlap another room, or land on an ally spawn point is silently
skipped for that building, which then keeps today's walk-onto-tile behavior.

A `ShopVendor` is spawned at the back of a successfully carved room (falling
back to a placeholder capsule when the definition's `VendorPrefab` is unset).
The player walks through the door like any other floor tile - it no longer
opens a dialog by itself - and faces the vendor and presses interact to open
the shop, exactly like talking to a party member. `TownBuilding.HasInterior`
is only ever true once a room and vendor actually exist for that instance, so
generation never has to special-case a shop whose room could not be carved.

Non-shop buildings (no `ShopCatalog` entries) are completely unaffected - they
keep the original single-tile, walk-on-triggers-dialog-and-bounce-back flow.

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
  earned gold. Party membership and learned skills remain.
  Leaving a dungeon through settings or the game-over Quit button uses defeat rules.
- Starting supplies are granted on a new game, not on every dungeon entrance.
- The final party member cannot be dismissed. Dismissing the controlled ally
  selects a remaining ally; dismissed equipment goes back into the bag.
- Each hero has a fixed class (or primary/secondary combination) set on its `TownAlly` prefab; the protagonist's class is chosen at new game and stored in the save (`TownAllyData.PrimaryClassId`/`SecondaryClassId`). Classes never change.
- Equipping a weapon or off-hand item outside the hero's class is refused with a message; accessories are unrestricted and unequipping is always allowed. Classless heroes can equip anything.
- The recruit/party dialog shows `Name - Class`, and the trainer header shows the selected hero's class.

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
