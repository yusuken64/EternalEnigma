# Dungeon themes and playground explorer

## Explore

Open `Assets/Scenes/EnvironmentPlayground.unity`, enter Play Mode, and select **Dungeon themes** (or press **5**).

- Select any of the eight biome buttons.
- Toggle **Interior / Outdoor** and **Regular floor / Throne room**.
- Enter a seed, select **Next seed**, or rebuild the current selection.
- **Frame dungeon** restores the overview; WASD and the mouse wheel pan and zoom.
- The other playground tabs hide the dungeon and restore playground lighting. The explorer never loads Common or reads/writes campaign saves.

In edit mode, select the `Dungeon theme explorer` object and use **Show / rebuild dungeon**. The menu **Tools → Eternal Enigma → Art → Preview Dungeon Themes** opens the same view. The controls are authored in the playground scene; `DungeonThemeExplorerAuthoring` can reinstall them without rebuilding the gallery.

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

`DungeonSaveData.VisualSelectionVersion` distinguishes old saves from a resolved selection. The biome and environment are persisted together and retained across floors. Old saves resolve their campaign biome and Interior. Existing interrupted-run recovery still returns to town; this change does not introduce mid-floor combat-state saving.

## Presentation and ownership

The catalog is `Assets/Resources/DungeonThemes/Catalog.asset`. Each entry contains regular/throne boundary presets, floor/accent presets, materials, decoration choices, and lighting. Grassland Interior bypasses presentation replacement and uses the original templates, models, materials, and lighting.

The other themes replace only build presentation on cloned TWC assets. `DungeonThemeTileLayer` consumes TWC's existing edge, outer-corner, inner-corner, and fill classifications and exclusions. Blueprint stacks, masks, generation dimensions, seeds, placements, navigation, sight, and minimap data remain unchanged.

Output belongs to the existing dungeon output root. Floor changes clear both creators' previous output, including baked clusters. Combined meshes and per-clone preview textures have explicit owners. Two small vendor fixes stop tile/object editor coroutines when their creator has been destroyed and release temporary material submeshes after combining.

Decorations use a local integer hash, the existing biome tree picker, and shared materials. There are at most 16 placements in each 32×32-cell chunk. Rotated, centered mesh bounds fit entirely inside blocked cells, including thin throne-room boundaries. Props have no colliders. Water/lava are opaque cosmetic surfaces outside walkways.

New themes use their authored vertical scale; Grassland retains the legacy 3.35 depth scale. With the existing gameplay output position, all new terrain and props stay behind the fog plane at Z = -3.35. No emissive lights or transparent effects bypass fog.

## Art

Blender MCP produced the crypt quarter-tiles, root mesh, and shared 64×64 masonry, moss, ice, and ember textures. Exported mesh data is retained in `Assets/Art/DungeonThemes/Source/Modules.json`; Unity MCP ran the installer that authored Unity meshes, materials, prefabs, and presets.

- Crypt edge/outer/inner: 8 triangles; fill: 2.
- Root prop: 48 triangles; root-covered tiles: at most 56.
- Reused terrain tiles remain below 300 triangles; selected props are checked against the 120-triangle limit.
- Existing Grassland dungeon assets are preserved and are exempt from the new-asset triangle limit.

Use **Tools → Eternal Enigma → Dungeon Themes → Install Catalog** to regenerate derived assets from these sources. The catalog and preset assets can also be edited directly. **Capture All Themes** writes all 32 fixed-seed previews.

## Verification

- CoreIntegration Edit Mode suite: **26 passed**, including five dungeon-theme tests.
- Dungeon transition and playground explorer Play Mode tests: **2 passed**.
- Existing environment playground regression tests: **2 passed**, including production town rendering and all previous playground views.
- The restored authored Grassland screenshot is byte-identical to the pre-integration baseline. Original dungeon templates and tile/preset assets have no changes.
- All 16 themes were generated in both regular and throne layouts at seed 12345 and visually reviewed, including corners, corridors, and outer boundaries.
- Tests compare every gameplay mask and all start/stairs/enemy/item/trap/gold placements across themes, verify unchanged source preset references, migration and JSON round-tripping, and resolution without overworld generation.
- Repeated builds check mesh disposal, one cosmetics root, decoration counts, transformed vertex clearance, and unchanged gameplay random state during presentation builds.
- In-game transitions cover Interior → Outdoor → Interior, regular → throne, persistent selection, and renderer bounds behind fog.
- The playground test builds all 32 combinations, checks seed entry, tab visibility, and absence of Common/save access.

Review images: [regular layouts](Art/Previews/DungeonThemes/ContactSheet_Regular.png), [throne layouts](Art/Previews/DungeonThemes/ContactSheet_Throne.png), and [playground explorer](Art/Previews/DungeonThemes/PlaygroundExplorer.png). Individual theme and gameplay captures are in the same directory. This is functional and visual verification, not a target-device frame-time benchmark.
