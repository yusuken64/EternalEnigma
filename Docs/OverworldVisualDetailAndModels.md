# Current overworld rendering

Core grid masks determine movement, gates, roads, locations and biome identity.
`CampaignOverworld` imports them into a clone of `Assets/Overworld/CampaignTerrain.asset`.

`OverworldGroundLayer` owns broad landscape/playable ground, forest and mountain
floor, roads, town paving, bridges and in-grid water. `OverworldBiomeRenderer`
exposes the TWC ground through a compatibility/cache handle; its old quad path is
a fallback for templates without `PaintedGroundOutput`. Separate TWC layers
supply mountain/coast silhouettes, tree walls, settlements, props and outer ocean.
See the [ground channel contract](Art/DioramaStyle.md#ground-channel-and-ownership-contract).

The production tree picker uses selected Tiny Fantasy World adapters and authored
diorama forms. Coordinate hashes preserve cosmetic choices without consuming
gameplay RNG. Protected approaches, roads, bridges, gates and locations constrain
placement. `OverworldCosmetics` caps props at 160 per 32-cell chunk and 600,000
triangles overall. `OverworldTreeWallLayer` separately caps trees at 192 and
96,000 triangles per chunk, with 2,000,000 triangles overall. Other layers have
their own costs; these are generation limits, not measured frame rates.

`OverworldTerrainCache` retains TWC output across visits and checks
`DioramaArtIdentity` for template, kit, ground style, catalog and binding changes.
Editor identity includes dependency hashes; player identity uses serialized and
runtime resource facts. Dynamic gates/markers update separately.

Edit committed templates, catalogs, materials and prefabs through Unity.
`Assets/Art/EnvironmentKit` retains the compact base kit; `Assets/Art/Diorama`
contains the restyled assets and adapters. Sources include
`ArtSource/Environment/EnvironmentKit.blend` and `ArtSource/Diorama`.
Current scale, palettes, authoring commands and captures are in
[the diorama guide](Art/DioramaStyle.md). The older
[base-kit measurements](Art/EnvironmentKitSpecs.md) do not describe the current
tree/facade budgets.

The [playground](Art/EnvironmentPlayground.md) exercises production town/overworld
templates and smart-rule examples. General composed style profiles and binding
validation remain in [task 08](../TODOs/08-twc-biome-styling-plan.md); the ground
migration itself is implemented. WebGL runtime acceptance remains in
[task 09](../TODOs/09-diorama-restyle-plan.md).
