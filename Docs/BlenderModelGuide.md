# Eternal Enigma: Blender model generation guide and asset audit

Audit date: 2026-09-22. Use this document when generating heroes, equipment, pickups, terrain, and environment props for this project.

For the TWC demo review, visual-only overworld layering plan, and prioritized model requests, see [OverworldVisualDetailAndModels.md](OverworldVisualDetailAndModels.md).

## Art direction

Create **low-poly, super-deformed fantasy miniatures** matching the playable Tiny Hero Polyart characters: oversized heads, compact bodies, short limbs, chunky hands and boots, bold equipment, and readable color blocks. Here, “deformed” means deliberately exaggerated proportions, not damaged anatomy or randomly distorted geometry.

Match the existing assets in silhouette, screen size, shading, and rendering cost. Use broad angular forms with selective smooth shading. The current assets are not uniformly flat-shaded: heads, bodies, bottles, and tree crowns have smooth surfaces; hair, blades, stone edges, and silhouettes retain visible facets. Avoid realistic anatomy, dense sculpt detail, thin ornamental filigree, and noisy photorealistic textures.

## What was audited

The audit traced Unity prefab GUID references, inspected model/texture import metadata, equipment code, terrain generation, and render settings. It imported **36 FBX source files containing 64 mesh objects into Blender**, counted their triangulated geometry, and rendered neutral previews to inspect shape and shading. This is a representative source-mesh audit, not an exhaustive inventory of every purchased asset or a GPU benchmark.

Triangle counts below are Blender source counts, before Unity import processing. Material counts describe slots actually used by faces, not measured draw calls. Unity can split vertices at UV seams, hard normals, or material boundaries. Blender source dimensions also exclude Unity import scale and prefab transforms.

The Unity scene query timed out, so this audit does not claim live gameplay visual approval, current assembled-hero triangle totals, frame times, or measured memory usage. Existing hero test results in [HeroPrefabAudit.md](HeroPrefabAudit.md) are prior project evidence; those tests were not rerun for this document.

### Reference hierarchy

| Category | Primary project reference | Interpretation |
|---|---|---|
| Playable heroes | `Assets/Prefabs/Town/Allies/Ally_MC01.prefab` through `Ally_MC24.prefab`; `Assets/Art/RPGTinyHeroWavePolyart/` | Main style and rig reference. Use an assembled playable prefab, not every mesh in its source FBX. |
| Held equipment | `Assets/Art/RPGTinyHeroWavePolyart/Mesh/Weapons/`; `Assets/Prefabs/Dungeon/Items/Weapons/` | Match the hero hand scale, sockets, stance, and exact model-name wiring. |
| Dropped items | `Assets/Prefabs/Dungeon/DroppedItems/` | Mostly non-atlas Adorable Items assets; the dropped sword uses `RPGHero/Meshes/Sword.fbx`. |
| Dungeon terrain | `Assets/Prefabs/Dungeon/DungeonThroneAsset.asset`; `Assets/TileWorldCreator/Tiles/Version 2 Tiles/Dungeon/` | Modular chunky masonry, cheap floor planes, separate gate/prop meshes. |
| Town terrain | `Assets/TileWorldCreator/VillageLSystemAsset.asset`; `Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/` | House, roof, park, road, and tree references. Park/road/tree meshes were sampled; house geometry was not measured. |
| Overworld terrain | `Assets/Overworld/CampaignTerrain.asset`; `Assets/Scripts/Overworld/OverworldBiomeRenderer.cs` | Runtime chunked surfaces currently replace the visible TWC terrain renderers. Do not assume adding a TWC prefab makes it visible here. |

Other bundled packs and their PBR/high-poly variants are not the default hero style authority. The `RPGHero` dropped sword is a specific existing exception, not a reason to adopt that pack's character proportions.

### Measured source geometry

| Source mesh / group | Triangles | Used material slots per mesh | Notes |
|---|---:|---:|---|
| Tiny Hero `Body01`–`Body20` | 1,874–3,920 | 1 | All 20 body alternatives in `AllBodiesCloaks.fbx`; not simultaneously visible. |
| `Cloak01` / `Cloak02` / `Cloak03` | 350 / 180 / 222 | 1 | Additional geometry, separate from the body. |
| `Head01_Male` / `Hair01` | 706 / 798 | 1 | Face parts and accessories add further geometry. |
| Held `OHS03_Sword` / `Shield01` | 276 / 314 | 1 | Grip and socket orientation matter as much as count. |
| `Bow01`–`Bow05` in `BowsSkinnedMesh.fbx` | 404–1,036 | 1 | Skinned alternatives; preserve bow deformation when needed. |
| Dropped shield / spellbook (`book2`) | 138 / 150 | 1 | Simple, strong silhouettes. |
| Dropped sword / gold bag | 232 / 242 | 1 | Sword comes from RPGHero; bag is Adorable Items. |
| Dropped bow / ring / skull | 266 / 266 / 270 | 1 | Low segment counts remain visible in the outlines. |
| Dropped potion / treasure chest | 324 / 347 | 2 / 1 | Potion uses two materials in its prefab. |
| Dropped bread (`waffle`) / key | 428 / 450 | 1 | Bread currently resolves to a waffle model. |
| Dungeon ground / block | 2 / 2 | 1 | Do not replace ordinary floor cells with dense geometry. |
| Dungeon edge / corner / inner corner | 674 / 811 / 599 | 3 | Layered, chipped masonry with multiple surfaces. |
| Dungeon column / torch | 143 / 134 | 1 | Repeated decoration. |
| Dungeon gate: wall + two doors | 800 + 472 + 472 = 1,744 | 1 / 2 / 2 | Separate door pivots must remain usable. |
| Dungeon treasure chest | 624 | 2 | Different asset from the 347-triangle dropped chest. |
| Park fill / edge / inner corner / outer corner | 2 / 10 / 22 / 58 | 1 / 2 / 2 / 2 | Cheap modular transitions. |
| Road fill / edge / corner variants | 2 / 14 / 82 | 1 / 2 / 2 | Both sampled road corner variants have 82 triangles. |
| Park tree / road streetlight | 1,062 / 92 | 2 / 1 | Tree has a tapered trunk and clustered rounded crowns. |

### Findings that affect generation

- **Count the active assembly.** `Ally_MC01` has active body, cloak, head, hair, mouth, eyes, and horn meshes. Its prefab also contains many inactive alternatives. Neither the whole FBX sum nor the body alone represents its visible cost. Budget the selected appearance plus equipped weapons.
- **Separate materials from colors.** Sampled Adorable Items FBXs import into Blender with ten material slots, but only one is used by most meshes; the potion uses two. Clean unused slots on new exports and verify Unity submeshes. Do not report ten draw calls simply from that source slot count.
- **Keep cheap floors cheap.** The overworld uses 32×32-cell chunks, four vertices/two triangles per ordinary cell, and five vertices/four triangles per raised cell. Layers can overlap, so two triangles per cell is not the total scene cost. Shadows are disabled on these generated surfaces.
- **Scale conventions differ.** The sampled park floor FBX has Unity `globalScale: 100`; the hero body has `globalScale: 1`. Copying raw Blender dimensions across those packs will produce incorrect sizes.
- **Current rendering is built-in.** `ProjectSettings/GraphicsSettings.asset` has no custom render pipeline. The bundled `PolyartMaskTint` shader uses albedo, three tint masks, and emission. A Blender material alone does not reproduce this Unity shader.
- **Small shared textures already work.** Tiny Hero's default albedo source is 512×512, despite a general importer maximum of 2048. The sampled potion red texture is 1024×1024. Maximum import size is not proof of actual source resolution.
- **Do not copy legacy importer settings blindly.** The sampled potion FBX is CPU-readable and configured as Generic animation even though it is a pickup. New static props should import without animation unless they need it. Evaluate Read/Write against mesh-combining/runtime code before changing it on terrain.

## Generation budgets

These are **recommended starting targets for new assets**, informed by the source measurements. They are not established engine limits or proof of performance on a particular device. A target platform and frame-time budget have not been specified. Exceeding a review ceiling needs a concrete silhouette, animation, or gameplay reason and an in-engine comparison.

Count triangulated evaluated meshes after render-time modifiers. Include all simultaneously visible parts, equipment, and accessory geometry in assembly totals.

| Asset | Working target | Review ceiling | Material / renderer goal |
|---|---:|---:|---|
| Complete normal hero, including equipped gear | 4,000–7,000 triangles | 8,000 | Shared hero palette/material; minimize active renderers while retaining required interchangeable parts. |
| Body component | 1,800–3,500 | 4,000 | 1 material; keep deformation loops. |
| Head + hair + face accessories | 1,000–2,000 | 2,500 | Shared material where compatible; this is part of the hero total. |
| Ordinary held weapon / shield | 150–500 | 800 | 1 material, normally 1 mesh. |
| Complex animated bow / signature weapon | 400–1,000 | 1,200 | 1 material; rig only what moves. |
| Ordinary pickup | 100–450 | 600 | 1 material, 1 renderer preferred. |
| Chest / interactive prop | 300–700 | 1,000 | 1–2 materials; split lid/doors only when needed. |
| Plain floor / road interior | 2–12 | 24 | 1 shared material; favor chunked rendering. |
| Ground transition / curb / corner | 10–100 | 150 | 1–2 shared materials. |
| Dungeon wall module | 400–800 | 1,000 | Target 1–2; existing reference uses 3. |
| Repeated rock / small decoration | 50–250 | 400 | 1 shared material. |
| Tree | 400–1,000 | 1,200 | 1–2 shared materials; opaque crown geometry preferred. |
| Large multi-part gate | 1,000–1,800 | 2,000 | Count both doors and frame together. |

Default texture proposals: 256–512 for a simple individual asset, 512–1024 for a shared set/atlas. Use 2048 only when screen-space inspection proves it necessary. Reuse existing palettes and materials where UV compatibility permits. Do not create one material per color, brick, leaf, or costume panel.

For repeated detailed trees or props, consider LOD1 around 50% and LOD2 around 20–25% of LOD0 triangles, preserving the silhouette. These are optional proposals, not a claim that the current assets have LODs. Tiny floor planes do not need extra LOD assets. Choose transitions in Unity at actual camera size; avoid visible popping and preserve shadows where needed.

Low polygon count alone is insufficient: inspect active renderer count, submeshes, shadow passes, skinning, texture memory, transparent overdraw, and batching. Reuse meshes/materials. Do not combine the entire world into one mesh and lose useful chunk culling.

## Shape and surface rules

### Heroes

1. Use approximately **2–2.5 head heights** as an initial super-deformed blockout, then match the actual reference assembly. This ratio is an art target, not a measured rig requirement. Match feet, shoulders, head, and hand positions before detailing.
2. Give the head roughly comparable visual mass to the torso and legs together. Keep a compact torso, short legs, broad boots, and substantial hands. Avoid realistic long thighs, narrow wrists, and small heads.
3. Build hair as a handful of chunky locks with deliberate sharp tips. Facial features must read from the gameplay camera; use simple eyes/mouth and restrained geometry.
4. Emphasize one primary silhouette cue per class: helmet, hair, hat, cape, shoulders, or weapon. Keep secondary detail subordinate. Preserve separation between arms, torso, and held items.
5. Smooth skin and soft cloth selectively; use crisp edges on armor, hair clumps, and hard props. Do not subdivide the final mesh merely to remove intentional facets.
6. Keep enough topology at shoulders, elbows, hips, knees, and wrists for existing animations. Remove invisible geometry only when costume swapping and animation cannot reveal it.

### Items and equipment

- Exaggerate the identity: broad blade, substantial shield rim, thick book cover, large bottle stopper, readable key teeth. Use restrained bevels only where they change the silhouette or highlight.
- Start round sections around 6–12 sides; add segments only when the outline visibly suffers. Use opaque color blocks for most bottles/gems; transparency is an intentional exception with a rendering cost.
- Make held and dropped versions of the same item visually related. Their pivots, scale, and pose can differ. Do not force a pickup's ground origin into a hand socket.
- Put the held-item origin at the grip/contact point; match an existing weapon's local axes. Put dropped-item origins at their ground support center. Test swords, shields, and bows in their actual stance.
- Treat a chest lid and gate doors as separately pivoted rigid parts if animated. Do not spend bones on static coins, straps, or seams.

### Terrain and environment

- Use large quiet surfaces, chunky chipped corners, tapered trunks, grouped foliage crowns, and broad stone courses. The visual vocabulary is miniature/toy-like, not realistic erosion or millions of separate leaves.
- Reserve geometric detail for the outline and important intersections. Suggest interior surface detail with a modest texture or palette variation. Never subdivide every floor tile to imitate a sculpt.
- Keep join boundaries exact even when the interior is irregular. Test straight, inner corner, outer corner, fill, and any end/junction variants used by the chosen tileset. Test rotations and mixed neighbors.
- Use a hero beside the prop to judge scale. Keep trees, walls, and roofs from hiding actors, pickups, entrances, or the next traversable tile from the actual camera.
- Preserve the distinction between walkable floor, blocked terrain, water, bridge, and closed gates. Decorative gaps must not imply traversable routes that the game disallows.

## Coordinates, scale, and pivots

The inspected town, dungeon, and overworld terrain assets use **cell size 2**. Town and overworld use an XY gameplay plane; the overworld renderer places visual centers half a tile from actor roots and raises barriers toward negative Z. This differs from an ordinary Unity XZ-ground scene.

Do not encode the actor's half-cell placement into every mesh. Keep logical root placement in the game/prefab wrapper and mesh origins consistent with their asset category. Existing pickup child transforms also include pose, offset, and scale adjustments; raw FBX bounds are not their gameplay footprint.

For Blender authoring:

1. Use meters and a documented unit convention for new source assets. Include a two-unit cell reference and a correctly scaled imported hero reference in a non-export collection.
2. For a new full-cell ground module, target a final two-unit footprint **after** Unity import and placement. Some TWC source modules are normalized and scaled by the generator: inspect the target preset before selecting raw export dimensions. Do not double-scale a tile.
3. Use a foot-ground origin for heroes, a grip origin for held equipment, a ground-center origin for pickups/props, and the existing tileset's anchor for terrain. Match wall/gate hinges to existing connections.
4. Apply mesh rotation/scale before rigging where possible. Do not blindly apply transforms to an already bound reference armature. Avoid negative-scale exports.
5. A conventional FBX starting configuration is forward `-Z`, up `Y`, scale 1, selected objects only, no leaf bones. This is a starting preset, not a guarantee of this game's final orientation. Match the target Unity prefab wrapper and XY placement, then round-trip one asset before exporting a set.
6. Verify facing, handedness, grip orientation, ground contact, and bounds in Unity. Do not “fix” an export error by accumulating unexplained 100× scales and compensating rotations.

## Rig and gameplay integration

- Base hero replacements on the existing Tiny Hero skeleton/bind pose. `AllBodiesCloaks.fbx` uses Humanoid import and an existing avatar source. A differently proportioned new rig requires explicit retargeting validation; Humanoid alone is not a guarantee.
- Preserve socket names **`weapon_r` and `weapon_l`**, their parent relationships, and their local orientation. Keep useful accessory bones when the reference animation or attachments use them.
- `Assets/Scripts/Town/HeroAnimator.cs` activates registered hand objects by exact `EquipmentItemDefinition.WeaponModelName`. New equipment needs both correct object names and registration in the corresponding hand list. An exported model file alone is insufficient.
- Bows in the current catalog are left-hand equipment. Preserve their skinned deformation where applicable; do not replace an animated bow with a rigid mesh without testing its action.
- Use at most four normalized bone influences per vertex as a new-asset target; test lower quality settings that reduce skin weights. Avoid tiny weights and unnecessary auxiliary bones.
- Keep locomotion consistent with the existing grid movement/root-motion setup. Do not introduce root translation into clips as an unreviewed side effect.
- Validate idle, forward movement, attack, hit, and death for all applicable stances: unarmed, single sword, sword/shield, dual swords, two-hand sword, spear, wand, and bow. Inspect feet, shoulder deformation, hand grips, cape intersections, and weapon reach.
- Preserve the town-to-dungeon model handoff described in [HeroPrefabAudit.md](HeroPrefabAudit.md). Use `node Tools/unity-mcp.mjs harness Heroes` when integrating hero/weapon changes, not merely when editing this guide.

Terrain movement is governed by game grids, not arbitrary new mesh colliders. Follow [OverworldGrid.md](OverworldGrid.md): decoration must preserve reserved routes, locations, gate footprints, and boat-only water behavior. Prefer simple colliders only where gameplay actually requires them; never add dense mesh colliders to every decorative tile by default.

## Blender delivery and Unity import

- Deliver an editable `.blend`, an FBX containing only intended runtime objects, required textures, and a short asset manifest. Keep references, cameras, lights, and construction helpers outside the export selection.
- Suggested names: `EE_Hero_<Name>`, `EE_Weapon_<Name>`, `EE_Prop_<Name>`, `EE_Terrain_<Set>_<Piece>`. Keep gameplay-required names such as sockets and registered equipment objects intact; wrapper names can carry the prefix.
- Separate source collections for model, rig, collision, LODs, and non-export references. Use deliberate UV islands with atlas padding. Remove unused materials and hidden construction duplicates.
- Record LOD0/LOD counts, active assembly count, material slots, texture dimensions, skeleton/socket requirements, pivot, intended footprint, and export axis/scale in the manifest.
- Export intentional normals; check the resulting Unity appearance. The existing hero body recalculates normals on import, while other sources import them. Match the result rather than indiscriminately copying one pack's setting.
- Use no animation/rig for genuinely static assets. Disable mesh Read/Write when no runtime mesh access needs it; verify TWC combining first. Do not globally change existing importers as part of asset generation.
- Recreate or assign project-compatible Unity materials. Keep base color primarily in albedo/palette, use restrained highlights, and avoid mandatory normal/roughness/displacement stacks for tiny assets. Blender procedural nodes are not a runtime material delivery format.
- Put new assets in a dedicated project-owned folder, for example `Assets/Art/EternalEnigma/<Category>/`; do not overwrite vendor sources or existing prefab GUIDs. Create or update game-facing prefabs deliberately.

## Reusable generation brief

Fill in the bracketed fields and provide the listed local reference assets to the Blender-generation tool. Do not leave scale, category, or attachment behavior to guesswork.

```text
Create [asset name / category] for Eternal Enigma using Docs/BlenderModelGuide.md.
Reference asset: [specific playable hero / equipment / tile prefab and its mesh].
Purpose and view: [held, dropped, modular floor, obstacle, etc.; actual gameplay camera].
Appearance: low-poly super-deformed fantasy miniature; oversized/chunky forms,
clean color blocks, angular silhouette, selectively smooth soft surfaces.
Distinctive silhouette: [one primary cue]. Palette: [existing material / colors].
Triangle target: [number]; review ceiling: [number], after triangulation/modifiers.
Assembly allowance: [hero + selected accessories + held equipment, if applicable].
Materials: [normally one shared material]; textures: [atlas and resolution].
Final size/footprint: [measured reference size; account for the 2-unit game cell].
Origin/axes: [feet / grip / ground / tile anchor; match named reference wrapper].
Rig/sockets: [none or existing skeleton; weapon_r / weapon_l requirements].
Variants/LODs: [only required variants and their budgets].
No dense subdivision, microdetail, photoreal textures, unnecessary transparency,
per-color material proliferation, or altered tile seams.
Deliver editable .blend, clean FBX, textures, and measured asset manifest.
Show front, side, three-quarter, wireframe, and gameplay-scale comparison views.
Report any requirement that cannot be satisfied; do not silently change the rig,
dimensions, materials, budget, or gameplay attachment convention.
```

Example constraints to insert:

- **Hero:** use `Ally_MC01` as the assembled scale reference; target 6,000 triangles including selected costume and equipment; preserve the existing skeleton and weapon sockets; make a broad hat the primary silhouette cue.
- **Potion:** use `DroppedItems/Potion.prefab`; target 300 triangles and one opaque palette material; emphasize a stout bottle and oversized cork; match the current gameplay footprint through the prefab wrapper.
- **Dungeon wall:** use `A_edge_tile.fbx` plus its TWC prefab/preset; target 650 triangles and two shared materials; preserve joins and final cell fit; use a few chunky chips along the silhouette.
- **Tree:** use the Park `Tree.prefab`; target 800 triangles and two materials; tapered trunk, three to five broad crown masses, and no individual leaf cards; compare actor visibility at the game camera.

## Acceptance checklist

- [ ] Side-by-side comparison with an existing assembled hero and relevant item/tile at actual gameplay scale; recognizable in grayscale and silhouette.
- [ ] Correct exaggerated proportions, coherent palette, intentional normals, no excessive tiny detail or accidental smoothing artifacts.
- [ ] Triangles measured after modifiers/triangulation; Unity mesh/submesh and active-renderer counts checked separately; complete hero/equipment total recorded.
- [ ] Correct pivot, scale, axes, grounded feet/base, hand grip, and bounds after a Blender → FBX → Unity round trip.
- [ ] No unwanted helpers, duplicate meshes, unused material slots, broken textures, or missing Unity shaders.
- [ ] Relevant animations and sockets tested; no major clipping, detached parts, changed root motion, or broken town/dungeon handoff.
- [ ] Tile seams tested in a mixed 3×3 arrangement, including rotations/corners; floor continuity, walkability, gates, and visibility preserved.
- [ ] Repetition tested at expected density; inspect draw calls, shadows, skinning, texture memory, and frame time in the target build. Record hardware, resolution, and quality level; do not certify performance from triangle count alone.
- [ ] Source, export, material/texture dependencies, manifest, and comparison views delivered.

This audit changes documentation only. It does not replace art, alter import settings, or certify existing assets against the proposed budgets.
