# Campaign presentation

New Journey uses `ProtagonistHeroPicker`: a paged roster of authored heroes with their
animated model and fixed class combination. Begin Journey creates a save with that hero as
the sole starting protagonist; companions are recruited later. Class choice does not alter
campaign topology, and the normal menu no longer offers an arbitrary class picker.

Hero class definitions supply starting bonuses, growth, weapon access and ranked skill trees.
Secondary classes add eligible non-mastery skills through tier 2 and rank 3. New heroes
receive starting mastery skills. Training uses skill points, not gold, for class heroes.

Buttons and UI references are stored directly in scenes/prefabs. **UI > Bake Button Styles**
is a reusable maintenance tool; **Validate Saved Button Styles** checks authored assets.
Generated menus instantiate `Resources/UI/GameButton.prefab`. Autoplay/sandbox IMGUI uses
`Resources/UI/GameSkin.guiskin`. Both reference the Bamao theme.

`CampaignHUD` supplies normal travel messages, interactions, warp buttons and town companion
management. Diagnostic seed/gate/biome controls belong to the sandbox. The town exit uses
normal movement interaction. [Animated main menu](Art/MainMenu.md) describes the saved stage.

Checks: `node Tools/unity-mcp.mjs harness Presentation` and `harness ButtonStyles`.
See [campaign flow](CampaignFlow.md) for saves and [classes](Classes.md) for learning rules.
