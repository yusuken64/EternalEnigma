# Remaining TWC biome styling integration

Status: partial presentation integration. The environment kit, smart tiles, native derived
mountain layers, ocean/shore layers and constrained cosmetics are implemented. The proposed
general style-profile/source-binding architecture is not implemented, so this plan remains.

## Current ownership

Core owns campaign/grid topology and movement. `CampaignOverworld` imports masks into a cloned
TWC template. `OverworldBiomeRenderer` owns broad biome terrain; project TWC build layers own
smart mountains, roads, houses, walls, cosmetics and surrounding ocean. Rendering uses separate
gameplay and landscape masks. Committed presets/materials/prefabs are editable in Unity.

The adapter contains special handling for existing presentation layers. There is no general
`OverworldStyleProfile` with validated semantic source bindings, layer roles, placement policy
and dependency-aware cache identity. Existing implementation must not be described as the
complete architecture below.

## Remaining work

1. Define one composed style profile/template with stable semantic-to-blueprint-GUID bindings.
   Replace only imported source stacks and preserve artist-owned derivations.
2. Validate layer references/order, required masks, dimensions and ownership; distinguish valid
   empty masks from execution errors. Keep movement independent of rendered geometry.
3. Move broad surface mesh emission into a configurable TWC build action if full TWC ownership
   is adopted. Assign each surface exactly one rendering owner during the transition.
4. Preserve route/location/gate/crossing exclusions after every scatter/transform. Bound both
   individual footprints and aggregate triangle/instance costs.
5. Include style/template dependencies in terrain cache invalidation and isolate visual random
   streams. Rebuild/dispose complete owned outputs without touching imported shared assets.
6. Validate same-seed determinism, asset isolation, failure cleanup, changing styles, gate state,
   protected approaches and target-device cost in the production overworld and playground.

See [current rendering](../Docs/OverworldVisualDetailAndModels.md), [playground](../Docs/Art/EnvironmentPlayground.md)
and [model guide](../Docs/BlenderModelGuide.md). Completed construction scripts are removed; the sources,
presets, templates and runtime builders remain.
