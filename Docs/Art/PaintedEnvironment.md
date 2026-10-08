# Painted environment surfaces

This records the painted-surface pass and its 2026-10-03 verification. Later
production ground, foliage, facades and item bindings are documented in
[diorama style](DioramaStyle.md). The later retained full suites passed with no
failures; open failures below describe the original audit, not current status.
Use the current diorama authoring workflow when reproducing production scenes.

Dungeon, town, and overworld materials now use a shared painted family: quiet flagstone
floors, stronger recessed wall masonry, timber, shingles, plaster, foliage, and eight
biome ground surfaces. The main-menu scene, its materials, and vendor originals remain
unchanged. Biome identity is carried by the existing palette colors and dedicated ground
art; no lighting, layout, navigation, collision, save format, or public renderer interface
changes are required.

## Assets and reproduction

- `ArtSource/PaintedEnvironment` retains newly generated masters, original semantic UVs,
  palette colors, and [the full prompt set](../../ArtSource/PaintedEnvironment/Prompts.json).
  Art was generated with the built-in imagegen tool. It returned 1254-pixel masters;
  Unity resamples new artwork into 1024-square repeating surfaces and 2048-square atlases.
  The old 64/256-pixel texture patterns are not enlarged into the new surfaces.
- `Assets/Art/PaintedEnvironment` contains the shared floor, wall, bridge, water, ground,
  dungeon-prop and biome-decoration textures. Existing building atlas paths and texture
  GUIDs remain stable. Buildings and environment props share each biome's building atlas
  in memory, while retaining existing shared material objects.
- Atlas regions retain the original 4×4 semantic layout. Packing extrudes edges by 24 pixels;
  building UVs sit at least 28 pixels inside their cells and prop UVs at least 33 pixels.
  Surface packing matches opposite borders with a narrow periodic blend. All new sampled
  textures use mipmaps, trilinear filtering, anisotropy 4, and high-quality compression.
- Unity menu **Tools / Eternal Enigma / Painted Environment / Integrate** rebuilds texture
  packing, assignments, and UVs. **Update Ground** and **Update Assignments** allow narrower
  iterations. Run integration after the older prop importers, which still author their
  original palettes. UV snapshots make integration repeatable rather than cumulative.
- `update_source_uvs.py` runs through Blender in background mode for the Environment,
  DungeonProps, FantasyTraps, and BiomeDecorations source blends. It retains an
  `OriginalSemanticUV` layer, updates the render UVs and painted material bindings, and
  exports the existing FBX paths. It does not add polygons or alter object transforms.
  `Modules.json` retains the matching crypt module UVs.

## Mapping and scale

Palette-only environment meshes now project stone, wood, metal and foliage surfaces into
their original semantic atlas cells. Building facades keep their region mapping. Runtime
dungeon scenery and fallback overworld settlements also sample surface areas rather than
single colors. Geometry silhouettes and material-slot counts are retained.

Only materials tagged `EnvironmentProjection` opt into continuous projection in the
existing batch combiner. Floors/roads use XY projection; dungeon boundaries use the
dominant face plane. Projection happens after combining, so rotated modules and adjacent
chunks share coordinates. The mapping unit is 2.5 world units; floor and ground material
scales are 0.5, while wall masonry uses 1. Overworld floor UVs continue across grid cells
with a 0.5 material scale. Atlas geometry retains its authored UVs.

Ocean and shore surface images are updated together. Their shaders, secondary noise
textures, speed settings, foam/shore masks and animation remain in place.

## Review and checks

Fixed seed **12345**, the same camera protocol, and unchanged lighting are used by
`PaintedEnvironmentCapture`. Campaigns contain six of eight biomes; missing biome views
use the first subsequent seed containing that biome. Each CSV records the seed, which
is identical between paired views. It captures all 32 dungeon combinations, eight towns with
facade views, all eight overworld biomes, and road, bridge, settlement, mountain and coast
views. Overview, gameplay-scale, and close views are stored in
`Previews/PaintedEnvironment/Before` and `After`. The capture scene is never saved.

The before baseline uses HEAD art with the same capture/runtime code as the after pass,
isolating the texture, material, and committed mesh UV changes.
`baseline_assets.py stage` temporarily stages HEAD art while preserving exact working
bytes under `Temp`; always run `restore` after the before capture, including on failure.
`contact_sheets.ps1` produces review sheets from the matched captures.

New EditMode checks cover atlas inset UVs, import settings, surface-border equality, and
continuous mapping across adjacent rotated modules. Existing tests cover theme selection,
biome rendering, mesh budgets, visibility, repeated generation/cleanup, transitions, and
travel. The saved-town check excludes TextMesh Pro sign meshes, which are rebuilt on enable.

`verify_meshes.py` compares the 144 updated runtime meshes against HEAD. Vertex counts,
positions, normals, tangents, and index buffers are byte-identical. Only UVs change.
Capture CSVs retain renderer/material/triangle counts and referenced texture memory.
Editor batch counters are indicative; they are not a target-device frame-time benchmark.

Visual review covers chipped masonry, quieter outdoor floors, building facades and biome
color continuity. The existing raised forest cells and block-shaped coast silhouettes remain
visible at close zoom; this pass does not smooth or redesign that geometry. Texture-border
tests address UV/texture seams, not those intentional mesh edges.

## Verification results (2026-10-03)

- Headless core: **343 passed**, zero failures.
- Final targeted EditMode art suite: **40 passed**, zero failures, including mipmaps,
  texture borders, padded atlas UVs, rotated-module seams, shared materials, mesh budgets,
  all dungeon themes, biome layout previews and town/prop validation.
- Source audit: **149 objects** retain positions, polygons and transforms across the four
  Blender collections. Runtime audit: **144 meshes** retain positions, normals, tangents
  and index buffers byte for byte.
- PlayMode coverage initially passed 12 of 18 checks. The two affected fixtures were
  corrected (the old 256-pixel atlas expectation and inherited full-control preference),
  and both passed the focused rerun. This includes all 32 dungeon transitions, visibility,
  repeated generation/cleanup, ocean animation, town rendering, scenery hazards and travel.
- Four `OverworldSceneTests` failed in this run: `AuthoredSceneBuildsCampaignAndMovesHeroWithSealedGates`
  expects 8 but observes 2; the three `RequiredReturnTripSeed*` tests expect capability
  enum names while the UI returns prose gate descriptions. No gameplay or message behavior
  was changed to satisfy these assertions.
- An initial full EditMode run passed 199 of 209 tests. Besides the saved-town sign-mesh
  assertion corrected here, failures involve ally emergency selection, boss flags, the
  imported campaign fingerprint, edit-mode material instantiation, stealth and targeting.
  The full suite is therefore not certified green by this art pass.

Raw test XML/JSON and geometry audit reports are retained under
[`Verification`](Verification/). The targeted art result and final memory comparison below
are the acceptance checks for this change; editor counters are not a GPU performance claim.

All **54 paired views** match in seed, renderer count, shared-material count, triangles,
batch count and SetPass count. Referenced texture memory measured by the Unity editor:

| View | Before | After |
| --- | ---: | ---: |
| Grassland interior dungeon | 0.035 MiB | 37.34 MiB |
| Grassland town | 2.92 MiB | 31.34 MiB |
| Overworld (seed 12345) | 6.14 MiB | 92.82 MiB |

These totals count distinct textures referenced by active mesh-renderer materials, not
total process memory or a target-device GPU allocation. The increase is substantial and
comes from the requested 1024/2048 textures and mip chains. Shared biome atlases avoid
duplicating building and prop images. No target-device frame-time benchmark was run.

Review sheets: [dungeons](Previews/PaintedEnvironment/After_Dungeon_ContactSheet.png),
[towns](Previews/PaintedEnvironment/After_Town_ContactSheet.png), and
[overworld](Previews/PaintedEnvironment/After_Overworld_ContactSheet.png).
The full before/after sets include close views and town facade inspection.
Run `compare_captures.py` to reproduce the CSV comparison.
