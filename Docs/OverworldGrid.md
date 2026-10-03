# Campaign overworld grid

`OverworldGridGenerator.Generate(campaign, options)` produces an immutable grid of `[x,y]`
boolean layers and location, route, gate and warp metadata. `ToArray()` returns copies.
The default canvas is 256x256; `OverworldGridOptions` defines supported dimensions/settings.
Generator metadata is currently 12; the source constants remain authoritative.

## Geography and movement

Seeded territory/noise construction forms a continuous landmass with six ordered regions.
Grassland starts the campaign; other biome identities are sampled without replacement.
Gameplay biome masks and `Landscape/<biome>` masks serve different purposes: the latter include
nonwalkable background. Trees/rock block movement; navigable water requires Boat. Roads,
destinations and gates reserve clear approaches. Grid generation retries deterministically
when placement/validation constraints fail, rather than silently returning an invalid map.

Walking routes have contiguous grid paths; warps have two landing coordinates without a
carved connecting road. Interior dungeon nodes share their parent town tile and have no
separate overworld marker. Town exits are scene gates; departure checks also enforce them
in sandbox/explorer movement. Story-0 opens town-0's exit; repeatable-0 awards the outer-area key.

`OverworldGates` tracks keys, permanent resolutions and open physical passages. Stand cardinally
adjacent and interact to open a physical gate. Key acquisition and gate opening are separate;
keys are not consumed. Area gates still require current capabilities. Warps use an explicit
travel action after unlocking. Party changes require a town. The overworld has no combat.

## Unity

`CampaignOverworld` imports Core masks into a cloned TileWorldCreator template.
`OverworldBiomeRenderer` draws broad terrain. Project TWC layers draw smart terrain features,
roads, houses, walls, coastline/ocean and bounded cosmetics. Markers and gate state are separate.
Core remains movement authority; art does not introduce collision rules.

`Common` owns the campaign context and an in-memory terrain cache. Grid generation is lazy;
town/interior travel does not build it. Terrain is reused after returning from towns/dungeons,
while dynamic objects reflect current progression. Saves store progression/seed, not meshes.

Normal travel enters towns/dungeons through `CampaignTravelService`. **Tools > Eternal Enigma >
Launch Overworld Sandbox** opens isolated testing with simulated completion controls. WASD,
arrows, stick/D-pad move; Enter/controller confirm interacts. Sandbox diagnostics and normal
HUD controls differ. Both use the same grid and gate rules.

For a custom scene, add TileWorldCreator plus CampaignOverworld, assign a template and semantic
layer bindings, then use **Generate And Build Overworld**. Edit presets/materials through Unity.
See [current rendering](OverworldVisualDetailAndModels.md) and [remaining styling work](TWCBiomeStylingIntegrationPlan.md).

## Validation and export

`OverworldGridValidator` compares closed-gate physical components with the logical graph,
checks route steps, location placement, gate contact/sealing, diagonals and shortcut value.
Core tests exercise bypasses, determinism and representative capability/resolution states.
These checks do not certify arbitrary prefab colliders or target-device performance.

```powershell
dotnet run --project Core/EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --grid --output Temp/OverworldPreview
node Tools/unity-mcp.mjs harness Overworld
```

CLI layers are rows of 0/1 strings (row index y, character index x); SVG flips y for display.
Exports are diagnostics. Unity tests cover scene generation, movement, camera, gates, cache
reuse and adapter isolation. See [campaign flow](CampaignFlow.md) for persistence.
