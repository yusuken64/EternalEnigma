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
- `DungeonTiers` defines available dungeon ranges. Each tier has a cumulative
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

Saves migrate the former world/seed JSON keys. Item snapshots preserve stack
counts and per-ally equipment; the old name list remains for legacy compatibility.
Only migration code/tests retain the old scene terminology.

## Verification

```text
node Tools/unity-mcp.mjs harness Town
node Tools/unity-mcp.mjs harness EditMode
node Tools/unity-mcp.mjs harness PlayMode
```

`TownGameplayTests` covers caller configuration, multiple shops, stock persistence,
purchase failures, training before dialog close, donation thresholds, shared
equipment actions, party safeguards, and victory/defeat inventory round trips.
