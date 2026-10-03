# Current environment assets

The production kit lives in `Assets/Art/EnvironmentKit` and `Resources/EnvironmentKit/Kit.asset`.
It contains imported low-poly models, biome palettes, building textures and native TWC presets.
The old pre-replacement primitive measurements are superseded by [imported kit specifications](EnvironmentKitSpecs.md).

Overworld terrain combines broad biome surfaces with smart mountains/summits, roads, house/wall
footprints, coastline, ocean and bounded props. Town terrain uses Core plans, smart houses,
service interiors, roads/alleys and biome props. Campaign town size follows the detailed Core
layout instead of the old fixed preview dimensions. Gameplay masks remain separate from art.

`Tools > Eternal Enigma > Art > Audit Environment` measures the current imported prefabs,
materials and TWC assets and refreshes this report. `EnvironmentKitTests` and the playground
fixtures cover geometry and rendering integration. Run fresh measurements after asset edits;
mesh counts do not establish target-device frame rates.

See [playground](EnvironmentPlayground.md), [validation](EnvironmentValidation.md) and
[Core/TWC boundary](../CoreTwcBridge.md). Construction scripts/installers have been removed;
Blender sources, runtime meshes and authored presets remain.
