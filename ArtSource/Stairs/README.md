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
- Unity visual wrapper: local rotation (-90, 0, 0), unit scale, zero position under the existing (1.25, 1.25, 0) offset. This maps height towards game -Z.
- Existing prefab GUID, root interaction component, and three collider objects are retained. Original cube MeshFilters and MeshRenderers are removed.

Regenerate by running `Tools/Art/generate_stairs.py` inside Blender. Then run Unity menu
`Tools > Eternal Enigma > Art > Install Stone Stairs`, or invoke
`-batchmode -nographics -executeMethod InstallStoneStairs.Install -quit` for this project.
The explicit importer validates mesh budget, scale, ground contact, collider count,
and interaction component before saving. `unity-validation.json` records the Unity measurements.

`preview.png` is a Blender studio render, not a gameplay screenshot. Live gameplay
camera, fog, and interaction behavior have not been play-tested as part of this visual replacement.
