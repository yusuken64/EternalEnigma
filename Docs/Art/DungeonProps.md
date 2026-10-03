# Dungeon props and town enclosure

Ten project-owned models were created in Blender through Blender MCP: Chest,
Crate, Urn, CrystalOre, HazardPool, Herbs, Mushrooms, Caltrops, SpikeTrap and BumpTrap. The editable
source is `ArtSource/DungeonProps/DungeonProps.blend`; FBX exports live in
`Assets/Art/EternalEnigma/Props/Dungeon`. No downloaded assets are required.

Edit the retained Blender source and export its meshes, then use Unity's
**Tools > Eternal Enigma > Art > Import Dungeon Props**. The importer preserves
asset GUIDs, merges all material submeshes, and converts models to XY ground with
negative-Z height. Run **Painted Environment > Integrate** after this legacy importer
to restore the painted atlas and projected UVs. The shared material retains the modeled wood, iron,
stone, crystal and foliage colors with painted surface detail. Runtime models fit within their cells without
adding physics colliders; the existing grid rules still control interaction,
blocking, damage, drops and hazards.

Containers use chests or urns; breakables use crates, urns or crystal outcrops by
biome. Gathering nodes and player-placed caltrops also use these imported meshes.
The damage and bump trap prefabs use a bolted spike plate and a spring-loaded
pressure plate. Their `VisualObject` bindings retain hidden/revealed behavior.
`DungeonPropModels` is the runtime mapping and placement entry point.

The shared town TWC environment layer adds four cells of ground around the map,
an inner wall enclosure, and biome trees beyond it. These exterior cells are
outside the walkable grid. The southern approach stays visually open and leads
to the plan's exit. Town and dungeon gates have models, direction markers and
labels; dungeon gate markers use the entrance building's actual interaction cell.
Use **Art > Rebuild Town Preview** to update the saved Town scene after edits.

Validation: **Tests > Run Town And Prop Visuals** checks imported bounds, collider
absence, expanded scenery and the accessible exit, and renders
`Previews/DungeonProps.png` and `Previews/TownEnclosure.png`.
**Tests > Run Scenery Gameplay** exercises damage, loot, hazards and transitions.
