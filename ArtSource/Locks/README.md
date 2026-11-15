# Overworld lock models

`Locks.blend` contains 49 models created through Blender MCP in the isolated
`EE Lock Authoring` scene. The original Blender scene is preserved. The models use
the existing diorama kit's bevelled mesh helpers and material roles.

To regenerate, execute `build.py` in Blender through MCP with `EE_PROJECT_ROOT` set
to the repository root. It exports `Meshes.json.gz` in the game's XY / negative-Z
coordinate system and saves a copy of the Blender source. The script changes only
its own authoring scene and reports exceptions to the MCP output.

In Unity, run **Tools > Eternal Enigma > Locks > Import Blender Lock Models**.
The importer preserves mesh GUIDs, uses the existing biome palettes, and registers
the prefabs in `DioramaCatalog`. It also reuses the existing dog, crate and barrel
assets. The dog is baked as scenery, without NPC behavior. Castle gates, trees,
rocks, doors, fences, signs, reeds and flags reuse existing catalog assets.

Gate prefabs separate `Frame`, an optional low `Side frame`, and `Closed barrier`.
East-west gates use the low frame to expose the closure to the camera. Gameplay hides the barrier
after unlocking and restores it when a lab station is reset. Natural rock uses the
existing cliff material; built stone uses the painted masonry atlas. No vendor
mesh or material is edited.

Run **Locks > Capture Model Contact Sheets** and **Tests > Run Lock Models** after
changing geometry. **Tests > Run Lock Test Lab** also checks production targeting,
unlocking, resetting and save isolation for every station.
