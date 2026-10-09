# Dungeon smart layers and pickup presentation

The regular and throne TWC templates contain their floor, accent, and boundary build layers. All eight dungeon biomes use the dungeon-owned Polyart smart boundary modules in both Interior and Outdoor presentations, covering all 32 biome/environment/layout combinations.

## Authoring

Expand **Dungeon**, **Floor**, or **Carpet** in the TWC build inspector. Each participating layer exposes its theme role, blueprint binding, exclusions, offset, and **Use theme preset** switch. Turn that switch off to keep a layer-specific preset. A boundary also exposes its six-piece kit, paving beneath the wall, wall height, and foreground cutaway. Leave the six-piece kit empty to use the four-piece boundary.

Wall height and foreground cutaway configure the six-piece kit. Four-piece presets remain available as an explicit authoring fallback and retain their authored dimensions.

Theme application preserves the build stack, active flags, and explicit overrides. The migration retained blueprint and build GUIDs. The two known legacy Columns/Torchlights layers are committed as inactive, matching their already-retired production behavior; theme application does not disable additional layers. Floor still excludes Carpet, and regular Carpet uses floor paving. Throne Carpet keeps its accent and small depth offset.

Executing one layer replaces only its owned output. Meshes, generated materials, and attachment faces are released before rebuilding. Disabling a layer and rebuilding clears its old output, including after hiding/restoring the world. Builds use integer coordinate hashing for straight variants and do not consume Unity's random stream.

## Stone kit

`Assets/Art/DungeonThemes/PolyartSmartTiles/Boundary.asset` wraps a native TWC six-tile preset, two compatible straight variants, a closed solid fill, a quadrant fill, and a concave cap. Canonical connections are N, NS, NE, NES, and NESW. Neighbor bits rotate clockwise; diagonal occupancy determines which inside quadrants are filled. An eight-neighbor interior uses the solid fill. Missing neighbors at map edges stay disconnected.

The fifteen adapters in `PolyartSmartTiles/Themes` share these meshes and connection rules. Each adapter uses its existing dungeon boundary material, preserving the biome's texture, tint, and continuous Box UV projection. Grassland Interior retains the source Polyart atlas. Floors, throne accents, pools, decorations, music, and lighting retain their authored theme settings. **Apply Kit to All Biomes** fills missing catalog bindings; **Install Kit and Layers** also refreshes the adapters. Both retain explicitly assigned replacement kits.

The authored grid is 2 units. The maximum wall height is **4.234727**, or 1.25 times the unchanged assembled dungeon hero's measured 3.3877816-unit height. This follows the user's clarified choice of the actual dungeon hero over the earlier 1.508-unit estimate from the town reference. Source-stone courses and capitals retain their Polyart atlas UVs; additional courses preserve stone proportions at the taller height. Closed backing geometry and matching end profiles prevent gaps; full-cell paving beneath the narrower walls preserves visible floor coverage. Geometry stays within blocked cells. The dungeon-owned material reads the original atlas without changing vendor materials.

The boundary's generated Standard-lit cutaway material removes fragments whose camera ray crosses the existing floor mask. Back and side walls retain their full height, while foreground walls reveal actors and pickups. Cosmetics on cutaway cells disappear as whole props; their selection, seed, placement, and generation callbacks remain intact. Decoration faces that would lose their supporting rectangle are omitted. The mask, material instances, and meshes are owned by their build layer and released on rebuild. In every biome, fog rises above the tall kit with a compensating XY shift that preserves its existing screen projection. Padding covers wall faces at map edges. No camera asset or controller is changed.

Editable sources are in `ArtSource/DungeonSmartTiles/DungeonSmartTiles.blend`; project-owned copies of Wall01–04 are in its `Inputs` directory. `build_kit.py` extracts source masonry islands, reshapes them, adds connectors/caps/fills, and exports XY/-Z mesh data. With that blend open, run:

```python
path = bpy.path.abspath('//build_kit.py')
exec(compile(open(path, encoding='utf-8').read(), path, 'exec'), {'__file__': path})
```

Then use **Tools > Eternal Enigma > Dungeon Smart Layers > Install Kit and Layers**. This updates the dungeon-owned meshes/prefabs and is idempotent for migrated templates. It does not edit the vendor FBX files, existing four-piece presets, heroes, cameras, towns, or overworld assets.

## Pickups

`Assets/Resources/DungeonThemes/PickupPresentation.asset` is the individual adjustment table for 141 visuals: all item mappings, eleven fallbacks, currency, small key, chest disguise, and bag. Each row contains a pose, thickness adjustment, target, baked scale, and measured projected ratio. **Calibrate Pickups** updates only the visual children of dungeon pickup prefabs. Shared key/chest/bag props have separate dungeon adapters. Existing item icons and held equipment are untouched.

Calibration copies the authored gameplay camera and invokes the existing camera controller's startup/follow behavior: offset `(0,-12,-14)`, orthographic size approximately 6.9979. It measures the longest projected mesh-silhouette diameter rather than a rotated world bounding box. The unchanged Rowan/MC03 model is placed into the original dungeon Ally rig through its production model-replacement method, then sampled in its existing idle pose. The town prefab alone is smaller and is not the measurement reference. The live gameplay regression independently measures the spawned dungeon hero.

Hero screen height measures the mesh from head to feet, excluding selection sprites and UI.

Ordinary pickups measure **0.366–0.43** of hero screen height in the reference pose; elongated equipment measures **0.57–0.58**. The upper targets leave room for the live hero's animation and facing variation, and the Play Mode check enforces the requested one-third-to-half and half-to-two-thirds ranges against the actual spawned hero. Chest height is 0.50 of hero world height, with a broader footprint. The reference crops show its vertical screen size beside the hero. Every footprint remains within 1.64 units of the two-unit cell. Thin equipment uses individual thickness and pose adjustments; the closest fitting angle is saved in each item's row. Source weapon meshes remain shared and unchanged. The chest-disguise adapter compensates for its existing enemy parent's scale in its visual child, keeping its world size equal to ordinary dungeon treasure.

`DungeonPickupFootprint` stores the actual mesh support point. Runtime grounding moves visual children to the ground plane using that point, preserving logical roots, cell coordinates, and interactions. Small-key placement only reduces oversized art; it no longer enlarges the calibrated key to a universal footprint.

## Review and regression commands

- **Capture Kit and Rooms** renders the kit and representative Grassland regular/throne rooms.
- **Capture All Biomes** renders all 32 Interior/Outdoor regular/throne combinations beside the unchanged hero, with each theme's materials and lighting.
- **Capture Pickups** creates every light/dark floor comparison and `PickupReview.html`. Images are crops of the production projection at the same ground depth, with rendering isolated to the review scene.
- **Test Edit Mode** checks serialization, roles/overrides, all 256 neighbor patterns, exclusions, map edges, independent rebuilds, resource ownership, masks/placements/randomness, actual pickup projection, and grounding. A GPU render check verifies that the foreground cutaway reveals an otherwise occluded object.
- **Test Play Mode** exercises dungeon theme transitions, previews, fog, wall decorations, live hero/item projected ratios, loose-item pickup, currency, drop/throw behavior, keys, and the chest disguise.

Evidence is in [DungeonSmartLayers](Art/Verification/DungeonSmartLayers/). The [pickup review](Art/Verification/DungeonSmartLayers/PickupReview.html) and [adjustment table](Art/Verification/DungeonSmartLayers/PickupAdjustments.csv) contain all 141 comparisons and measurements. These checks establish presentation behavior; they are not a target-device performance benchmark.
