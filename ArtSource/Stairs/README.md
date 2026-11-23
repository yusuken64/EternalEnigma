# Stone stairs

Original project-owned Blender model for `Assets/Prefabs/Dungeon/Interactables/Stairs.prefab`.
Five broad stone treads, chamfered edges, stepped side walls, and a warm landing inset.

- Source: `EE_Prop_StoneStairs.blend`; source measurements: `geometry.json`.
- Runtime: `Assets/Art/EternalEnigma/Props/Stairs/EE_Prop_StoneStairs.fbx`.
- One mesh, 968 triangles, one submesh and one Standard material.
- 128 x 16 opaque palette texture; padded color swatches sampled at their centers.
- Static prop: no rig, animations, LODs, or generated mesh collider; mesh Read/Write disabled.
- Blender coordinates: X width, Y run, Z height, in meters. Origin at the center of the footprint, with the base at Z = -0.2.
- FBX: selected mesh only, forward -Z/up Y, unit scale applied, baked axis transform.
- The visual model rotates by (-90, 0, 0) under the stair body so height maps to game -Z. At spawn, the stair body aligns the mesh footprint and colliders to the center of the interaction tile using that floor's cell size.
- Existing prefab GUID, root interaction component, and three collider objects are retained. Original cube MeshFilters and MeshRenderers are removed.

Edit the retained `.blend` source and export the FBX with the settings above. The completed
construction script and obsolete installer instructions are removed. Preserve the existing
prefab interaction component, colliders and GUID. `unity-validation.json` records the original
import measurements; revalidate bounds and interaction after editing.

`preview.png` shows the Blender studio render. The dungeon grid alignment Play Mode test checks the rendered footprint, ground height and colliders on two consecutive floors.
