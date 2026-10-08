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

Campaign towns bind `TownLayout.SlotServices` to bakery, consumables, items, inn and one
class-aware trainer. Service-sized detailed layouts include seven residential slots. The
default standalone town also enables furnishings; explicit legacy configurations can keep
`FurnishInteriors` disabled.

## Furnished interiors (generation version 2)

`TownInteriorSpec` carries the interior kind and shop theme through Core options, cache
equality and every Unity blueprint generator. Residential rooms never imply a vendor.
Campaign service IDs, stock, inn functions, training and save schema are unchanged.
Furnished towns grow to at least 52 cells and at most 64 cells per side. Rooms provide at
least 5×5 usable floor (7×7 for inns/trainers); required service rooms fail generation rather
than disappearing. Explicit legacy layouts retain their existing geometry.

`TownInteriorGenerator` has an independent seed stream, three arrangements per kind and
mirrored variants. It reserves the entrance, central aisle, vendor anchor and adjacent
interaction cells before placing props. A flood fill validates every free floor cell; it
retries arrangements and then a compact recipe. The immutable result publishes props,
carpet/counter masks, occupied cells and reservations. `Furniture` includes counter cells;
Core `Walkable` and Unity's published mask both exclude this occupancy. Furniture has no
physics colliders. Carpets remain walkable and sit slightly above the paving.

`TownInteriorRendering` builds native TWC four-tile carpet and counter presets through
`EnvironmentSmartTileLayer`. Separate layer GUIDs keep their generated chunks independent.
Counter cabinets and tops support straight, L, U and island masks. Goods use the counter's
authored .72-cell support height. Eight furniture materials share the same geometry and
palette atlas. Bounded wall decorations use verified interior-facing timber panels,
exclude doors and occupied floor, and share the existing effect budget without point lights.
Rebuilding releases owned meshes, effects and ambient actors.

Seven `TownNpcDefinition` assets preserve identity, portrait, greeting and prefab. The cat
and shepherd dog use deterministic, reachable public anchors; service characters use the
existing `ShopVendor` contract (bear innkeeper, sheep baker, bunny consumables, guinea pig
trainer and hooded equipment merchant). Greetings turn toward the player and open the
existing message dialog. `Town.IsOccupied` is shared by player and party movement. Ambient
NPCs have no combat components, equipment, recruitment or persistent state.

Each normal furnished town selects blue, red and yellow spherical birds. Two prefer
supported sign perches on the exterior enclosure; one uses an interior shelf/cupboard.
Perches stay outside movement and service interaction space, are separated deterministically,
and are omitted if unavailable. Fixed roots, varied animation phases, no colliders, no
interaction and no audio keep them ambient. Character and bird colors stay fixed in all biomes.

See the [authoring instructions](../ArtSource/TownInteriors/README.md) and
[local visual gallery](Art/Previews/TownInteriors/index.html). Verification covers 100 seeds
in Core and Unity, geometry/skinning/dependencies, owned-mesh cleanup, all eight biome
captures, four interior types, vendor behavior, greetings, movement and revisit flows.

## Gameplay rules

- Purchases, training, donations, recruitment, dismissal and equipment changes update captured
  live state. Explicit home/inn save actions write the checkpoint; `SaveSystem.Capture` itself
  does not write storage.
- Shops keep stock per town/building and refresh after a committed dungeon return. `RestockCycle`
  is a gameplay counter, not a schema version.
- Class heroes train with skill points derived from highest level and learned ranks. The shared
  trainer shows their class kit; `LearnableSkills` can filter it. Classless configured heroes
  retain gold-priced single-rank training.
- The inn provides free HP/SP restoration and a separate Save action that records an inn checkpoint.
- Successful returns preserve remaining bag/equipment stock and carried HP/SP in memory. In a
  campaign, defeat loads the selected slot's last explicit save and quit discards unsaved progress;
  neither applies a new penalty. Standalone configured runs retain `DungeonReturnService` rules
  (by default, losing items/equipment and retaining gold on defeat).
- Retreat retains loot without completing the dungeon. Starting supplies are granted only at new game.
- The last party member cannot be dismissed. Removing the controlled hero selects another;
  dismissed equipment goes back into the bag.
- Each authored hero has fixed classes. New Journey picks that hero rather than changing classes.
  Class weapon restrictions apply in town and dungeon; accessories are unrestricted.
- Campaign entrances list the current town's interior dungeon nodes. Standalone entrances use
  configured donation-gated tiers; default thresholds are 0/1,000/3,000/6,000 gold.

Inventory/equipment persist only as `ItemSaveData`; no name-only compatibility list or format
migration remains. Item identity still uses names, so definitions sharing a name are ambiguous.
Skills/ranks, level/EXP, attributes, highest level and carried vitals live in each
`TownAllyData` record.

## Inventory and recovery

The shared Inventory/Equipment/Skills/Stats window uses the controls in [Dungeon controls](DungeonControls.md).
Town supports equipment changes, material selling, positive HP/SP recovery, and compatible
inventory inspection. `TownUtilityService` validates the complete effect list, living
beneficiaries, item-instance ownership, and SP costs before committing once through
`Town.SaveProgress`. Cancelling a picker changes nothing. Successful actions keep the
window open and display their result.

Recovery skills use learned rank, class healing/consumable passives, and persisted
`HighestLevel`. Saved -1 vitals mean full health/SP. Recovery clamps to calculated maxima;
full targets, insufficient SP, revival, food, damage, movement, status changes, buffs,
permanent stat changes, and mixed unsupported effects cannot be used. Inventory utilities
honor their filters and equipped-item eligibility. Normal transactions capture live state without
rewriting the selected slot's checkpoint; there is no separate dungeon rollback snapshot.

## Maintenance and checks

Edit committed TWC assets through Unity's inspector; their Odin data should not be hand-edited.
The completed layer-rewrite commands have been removed. **Core Layers > Verify Assets** remains.
Rebuild/import the Core DLL after source changes using **Core > Build and Import DLL**.
**Art > Rebuild Town Preview** refreshes the saved visual preview.

`node Tools/unity-mcp.mjs harness Town` covers configuration, shops, training, menus, equipment
and returns. `EditMode`, `Campaign` and `Classes` provide additional coverage. See
[test harness](../Assets/Tests/README.md) for isolation and result files.
