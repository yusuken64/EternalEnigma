# Town interior authoring

`TownInteriors.blend` contains the isolated editable authoring scene, 54 models, ten rigs,
their actions, and the shared painted palette. Original purchased artwork and other live
Blender scenes are not modified. References come from the project's licensed Bamao pack;
the generated assets and reference board should remain within this project's license scope.

Run `author.py` through Blender MCP with `EE_PROJECT_ROOT` set to the checkout root in the
execution globals. It builds a fresh isolated scene, exports FBX and lossless mesh/weight
JSON under `Assets/Art/TownInteriors/Models`, and writes both manifests. Source coordinates
are XY ground, Z up, front -Y. Unity uses XY ground, negative Z up. Actor roots stay fixed.
NPCs use one material and 4,874–5,688 triangles; the three birds use 1,020–1,184 triangles.
The full palette has 32 colors with four broad highlight/shadow bands.

In Unity run **Tools > Eternal Enigma > Art > Import Town Interiors**. This preserves asset
GUIDs while updating meshes, prefabs, animation clips/controllers, seven NPC definitions,
eight furniture materials and two native TWC four-tile presets. Character materials are
shared across biomes. Existing building definitions keep their service IDs, stock catalogs
and dialog bindings; only presentation and interior metadata are assigned.

Run **Tools > Eternal Enigma > Art > Verify Town Interiors** to capture turnarounds, eight
idle samples per actor, four room types, the full prop sheet, native TWC rules and all eight
biomes. Then run `python Tools/Art/town_preview_gallery.py`. Open
[`index.html`](../../Docs/Art/Previews/TownInteriors/index.html) for the local animated review.
`Tools/Art/town_reference_board.ps1` rebuilds the reference board from local licensed assets.

Verification menus: **Tests > Run Town Interiors EditMode** (including 100 seeds) and
**Tests > Run Town Interiors PlayMode** (services, party, greetings and save/revisit behavior).
The broader **Tests > Run Town** also exercises older save/transition expectations; its
initial failures are recorded in the review folder rather than hidden. Core tests live
in `Core/EternalEnigma.Core/EternalEnigma.Core.Tests/Generation/TownInteriorTests.cs`.
