# Campaign presentation

The new-game picker renders the starting protagonist's existing animated model.
Choosing a class changes that hero's combat build, not their appearance or the
world seed. The first starting-party member receives the selected primary and
optional secondary class; other starting allies retain their configured classes.

- Primary class supplies starting stat bonuses, growth each level, weapon access,
  and its full skill tree (up to rank 5).
- Secondary class adds its weapon access and non-mastery skills through tier 2,
  capped at rank 3. Primary progression remains available.
- A new hero learns the primary class's tier 1 mastery. Other skills shown in the
  picker are training options, not automatically learned starting skills.
- Campaign layout, story gates, keys, boats, and companion capability rewards are
  determined by campaign generation and progression, not this class selection.

Button sprites, tint states and label colors are saved directly in the gameplay
scenes and their prefabs. The editor-only command **Tools > Eternal Enigma > UI >
Bake Button Styles** applies the theme explicitly; there is no runtime scan or
button styling component. **Validate Saved Button Styles** checks saved assets
outside Play Mode. Dynamically populated menus instantiate the already styled
`Resources/UI/GameButton.prefab`, setting only labels, layout, callbacks and data.
Autoplay/sandbox IMGUI controls use the serialized `Resources/UI/GameSkin.guiskin`.
Artwork in Resources/UI comes from the project's existing Bamao UI pack.
CampaignHUD supplies normal-play travel messages, interaction/warp buttons and
town companion management (P). Seed, key inventory, route requirements and biome
summaries remain in the overworld sandbox only. The southern town gate still
uses the normal movement interaction.

Validation: `node Tools/unity-mcp.mjs harness Presentation` runs picker lifecycle,
button skin, menu input and campaign travel regression tests. Picker screenshots
are written to `Temp/UIValidation`.

Button styling checks: `node Tools/unity-mcp.mjs harness ButtonStyles`.
