# Dungeon themes and playground explorer

## Explore

Open `Assets/Scenes/EnvironmentPlayground.unity`, enter Play Mode, and select **Dungeon themes** (or press **5**).

- Select any of the eight biome buttons.
- Toggle **Interior / Outdoor** and **Regular floor / Throne room**.
- Enter a seed, select **Next seed**, or rebuild the current selection.
- **Frame dungeon** restores the overview; WASD and the mouse wheel pan and zoom.
- The other playground tabs hide the dungeon and restore playground lighting. The explorer never loads Common or reads/writes campaign saves.

In edit mode, select the `Dungeon theme explorer` object and use **Show / rebuild dungeon**. The menu **Tools → Eternal Enigma → Art → Preview Dungeon Themes** opens the same view. The controls are authored in the playground scene. The completed explorer installer has been removed; the inspector and preview command remain.

`DungeonScene` also has an editor-only selector on **DungeonGenerator**. Choose biome, environment, seed, and layout, then **Build theme preview**. **Restore authored Grassland preview** restores the original baked output. Preview geometry and cloned assets are not saved over the scene's authored preview.

## Runtime selection

`DungeonVisualSelection.Resolve` looks up the pending dungeon/location ID in the campaign and calls `BiomeForRegion`. It never accesses `Context.Grid`, `Position`, or the lazy `Location` property. Existing campaign travel calls resolve the biome and use Interior; standalone launches default to Grassland Interior.

Future encounters can explicitly request an outdoor presentation:

```csharp
common.Travel.EnterLocation(new DungeonEncounterVisualSettings
{
    Environment = DungeonEnvironmentKind.Outdoor,
    OverrideBiome = true,
    Biome = OverworldBiome.Water
});
```

`EnterTownDungeon` accepts the same optional settings. No current encounter has been reassigned. The generator's encounter settings also support standalone/new unsaved runs.

`CampaignTravelService` resolves biome/environment at run creation and stores the selection
directly in `DungeonSaveData.VisualSelection`. Floors reuse it. There is no selection-format
version or migration. Standalone runs default to Grassland Interior. Interrupted-run recovery
returns to the entrance; it does not restore mid-floor combat.

## Presentation and ownership

The catalog is `Assets/Resources/DungeonThemes/Catalog.asset`. Each entry contains regular/throne boundary presets, floor/accent presets, materials, decoration choices, and lighting. Starter-layout Grassland Interior uses the original presentation. Biome-layout Grassland enables the themed presentation like other biome profiles.

The other themes replace only build presentation on cloned TWC assets. `DungeonThemeTileLayer` consumes TWC's existing edge, outer-corner, inner-corner, and fill classifications and exclusions. Blueprint stacks, masks, generation dimensions, seeds, placements, navigation, sight, and minimap data remain unchanged.

Output belongs to the existing dungeon output root. Floor changes clear both creators' previous output, including baked clusters. Combined meshes and per-clone preview textures have explicit owners. Two small vendor fixes stop tile/object editor coroutines when their creator has been destroyed and release temporary material submeshes after combining.

Decorations use a local integer hash, the existing biome tree picker, and shared materials. There are at most 16 placements in each 32×32-cell chunk. Rotated, centered mesh bounds fit entirely inside blocked cells, including thin throne-room boundaries. Props have no colliders. Water/lava are opaque cosmetic surfaces outside walkways.

Themed map roots use unit scale at the origin; ground is offset to Z=0.05 beneath
unit feet. The original starter presentation retains its depth compensation. Fog and
visibility remain controlled by gameplay, including skipped offscreen effect playback.

## Art

Blender MCP produced the crypt quarter-tiles and root mesh. The [painted environment pass](Art/PaintedEnvironment.md) replaces the former 64×64 surface patterns with 1024×1024 painted stone and terrain, separates quiet floor slabs from wall masonry, and shares compatible 2048×2048 atlases with towns and the overworld. Exported mesh data and updated crypt UVs are retained in `Assets/Art/DungeonThemes/Source/Modules.json`; existing catalog and preset references remain stable.

- Crypt edge/outer/inner: 8 triangles; fill: 2.
- Root prop: 48 triangles; root-covered tiles: at most 56.
- Reused terrain tiles remain below 300 triangles; selected props are checked against the 120-triangle limit.
- Existing Grassland dungeon assets are preserved and are exempt from the new-asset triangle limit.

Edit the committed catalog and presets directly; the completed installer is removed. **Capture All Themes** remains available.

## Verification

`DungeonThemeTests` checks transformed ground, unchanged gameplay masks, explicit selection
round-trips and lazy biome lookup. Transition/explorer tests cover repeated builds, output
ownership, tab/seed controls and isolated playground state. Historical screenshots below
are reference captures, not a fresh target-device performance certification.

Review images: [regular layouts](Art/Previews/DungeonThemes/ContactSheet_Regular.png), [throne layouts](Art/Previews/DungeonThemes/ContactSheet_Throne.png), and [playground explorer](Art/Previews/DungeonThemes/PlaygroundExplorer.png). Individual theme and gameplay captures are in the same directory. This is functional and visual verification, not a target-device frame-time benchmark.
