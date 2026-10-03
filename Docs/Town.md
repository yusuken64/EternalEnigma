# Town configuration and gameplay

`Assets/Scenes/Town.unity` is the shared town scene. `TownSceneLoader.Load(configuration)`
configures and loads it; callers owning a transition can configure first, then load the scene.
Campaign travel clones `Resources/Towns/DefaultTown`, assigns a location ID and deterministic
seed, and prepares the active party. Standalone/test towns use the same current save schema.

## Configuration and buildings

Create **Game > Town > Configuration** assets. `Id` namespaces shop stock; the default
configuration and definitions live under `Assets/Resources/Towns`. `Buildings` is an ordered
list, `AllyCatalog` resolves hero identities, `Recruits` defines paid candidates, and
`StartingParty`, `PartySpawn` and `MaxPartySize` define starting behavior.

Create **Game > Town > Building** assets with an ID, display name, prefab and dialog ID.
`TownMenu.BuildingDialogs` connects scene views. An optional `DialogPrefab` supports a custom
view. `ShopCatalog` owns prices, quantities and stack sizes; `VendorPrefab` controls the NPC.
`HasInterior` is true for shops or definitions with `ServiceInterior`, including catalog-less inns.
Stepping into an interior does not open a dialog: face its vendor and interact. Non-interior
buildings keep the door-trigger flow. Missing rooms cannot provide interior vendors.

`Town.Start` clones the TWC template and configures `CoreTownLayerGenerator` from the building
list and spawn. `CoreLayoutCache` supplies a shared `TownPlan`; its walkability, rooms, slots
and deterministic rolls drive gameplay. Smart environment layers render houses, walls, roads,
plants and the exterior enclosure. The loading transition remains closed until generation and
party/camera setup finish.

Core also has detailed service-sized towns through `TownLayout`, but Unity does not yet bind
`SlotServices` or all detailed road/prop layers. The current service vocabulary is bakery,
consumables, items, inn and one trainer, not ten class-specific trainers. See the remaining
[integration plan](TownServicesUnityPlan.md).

## Gameplay rules

- Purchases, training, donations, recruitment, dismissal and equipment changes save immediately.
- Shops keep stock per town/building and refresh after a committed dungeon return. `RestockCycle`
  is a gameplay counter, not a schema version.
- Class heroes train with skill points derived from highest level and learned ranks. The shared
  trainer shows their class kit; `LearnableSkills` can filter it. Classless configured heroes
  retain gold-priced single-rank training.
- The inn provides free HP/SP restoration and a separate Save action that records an inn checkpoint.
- Successful returns preserve remaining bag/equipment stock and carried HP/SP. The defeat recovery
  button restores the inn checkpoint when available. Without it, configured defeat rules apply;
  the default return loses items/equipment and retains earned gold. Quit/abandon uses return rules.
- Retreat retains loot without completing the dungeon. Starting supplies are granted only at new game.
- The last party member cannot be dismissed. Removing the controlled hero selects another;
  dismissed equipment goes back into the bag.
- Each authored hero has fixed classes. New Journey picks that hero rather than changing classes.
  Class weapon restrictions apply in town and dungeon; accessories are unrestricted.
- Campaign entrances list the current town's interior dungeon nodes. Standalone entrances use
  configured donation-gated tiers; default thresholds are 0/1,000/3,000/6,000 gold.

Inventory/equipment persist only as `ItemSaveData`; no name-only compatibility list or format
migration remains. Item identity still uses names, so definitions sharing a name are ambiguous.
Skills/ranks, highest level and carried vitals live in each `TownAllyData` record.

## Maintenance and checks

Edit committed TWC assets through Unity's inspector; their Odin data should not be hand-edited.
The completed layer-rewrite commands have been removed. **Core Layers > Verify Assets** remains.
Rebuild/import the Core DLL after source changes using **Core > Build and Import DLL**.
**Art > Rebuild Town Preview** refreshes the saved visual preview.

`node Tools/unity-mcp.mjs harness Town` covers configuration, shops, training, menus, equipment
and returns. `EditMode`, `Campaign` and `Classes` provide additional coverage. See
[test harness](../Assets/Tests/README.md) for isolation and result files.
