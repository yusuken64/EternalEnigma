# Current overworld rendering

Core grid masks determine movement, gates, roads, locations and biome identity.
`OverworldBiomeRenderer` supplies broad textured terrain. `CampaignOverworld` feeds the
committed TWC template; smart build layers supply mountains/summits, houses, walls, roads,
shoreline and surrounding ocean. `OverworldCosmeticLayer` supplies constrained props.

The imported EnvironmentKit includes biome trees, grouped variants, rocks, plants, buildings,
portals, gates, shrine and native smart-tile pieces. `TreeModels.asset` provides weighted
biome-filtered choices. Coordinate hashes keep cosmetic choices stable without spending
gameplay RNG. Tall/low decoration protects approaches, roads, bridges, gates and locations.
The cosmetic layer caps placement at 48 props per 32-cell chunk and 120,000 triangles overall;
other geometry layers have separate costs.

Edit `Assets/Overworld/CampaignTerrain.asset`, `Assets/Art/EnvironmentKit` presets/materials,
and `Assets/Resources/EnvironmentKit/Kit.asset` in Unity. Source meshes are retained in
`ArtSource/Environment/EnvironmentKit.blend`; completed construction scripts/installers are
removed. Committed meshes and presets remain the editable production assets.

The playground exercises production town/overworld templates and a smart-rule example asset.
See [environment playground](Art/EnvironmentPlayground.md), [mesh specifications](Art/EnvironmentKitSpecs.md)
and [model guide](BlenderModelGuide.md). Saved screenshots/cost measurements are examples,
not target-device performance guarantees.

Full style-profile-driven TWC ownership of broad biome terrain remains unfinished. Its actual
remaining scope is in [TWC styling integration](TWCBiomeStylingIntegrationPlan.md).
