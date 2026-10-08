# Remaining TWC style-profile architecture

Audited 2026-10-08. The general composed `OverworldStyleProfile` and semantic
binding validator remain unimplemented. The production rendering migration is
further along than the original plan described.

## Implemented foundation

Core owns topology and movement. `CampaignOverworld` clones the TWC template
and imports Core masks through its layer bindings. `OverworldGroundLayer` now
owns broad biome/landscape ground, roads, town paving, bridges and in-grid water.
`OverworldBiomeRenderer` retains a compatibility/cache handle; its old quad
renderer is only a fallback for templates without `PaintedGroundOutput`.
Separate TWC layers own mountains, tree walls, settlements, cosmetics and ocean.

`OverworldTerrainCache` compares `DioramaArtIdentity`, which includes template,
kit, ground style, diorama catalog and bindings. Editor identity includes asset
dependency hashes; player identity uses serialized/runtime resource facts.
`DioramaGroundTests` already covers single surface ownership, priority, seams,
determinism and a ground-style identity change. Protected placement and cosmetic
budgets also have existing tests.

## Remaining

1. Define a composed style profile with stable semantic-to-blueprint GUID
   bindings, explicit layer roles and artist-owned derivations.
2. Validate required masks, references/order, dimensions and ownership; distinguish
   intentionally empty layers from execution errors. Preserve movement authority.
3. Generalize invalidation to the chosen profile's complete dependencies and
   verify runtime style swaps, failure cleanup and shared-asset isolation.
4. Extend existing placement/ownership tests to composed styles, preserving
   protected approaches after every transform and bounding aggregate costs.
5. Measure target-device cost for the resulting styles in production and the
   playground. Existing diorama samples are limited to their recorded setup.

See [current rendering](../Docs/OverworldVisualDetailAndModels.md),
[ground ownership](../Docs/Art/DioramaStyle.md#ground-channel-and-ownership-contract)
and [playground](../Docs/Art/EnvironmentPlayground.md).
