# Bamao game UI theme

The hero picker and runtime inn dialog use the same GameUISkin helpers and authored button
prefab; scene/prefab styling remains reusable maintenance tooling.

All game scenes and UI prefabs use the shared theme at `Assets/Resources/UI/BamaoTheme.asset`, referencing the original assets in `Assets/Bamao/BamaoUIPack`.

The theme uses parchment panels, short parchment buttons, typing fields, framed tracks, round slider handles and square checkboxes. Text uses brown ink on paper; world labels and resource bars retain light text. Selected controls use gold, with distinct pressed and disabled states. Bamao's Magical Neverland font is used for authored headings, while dense text retains the readable body font.

`GameUISkin` uses these resources for generated dungeon and campaign interfaces. The legacy GUI skin covers autoplay and development panels. Button borders are drawn at compact sizes so they do not crowd labels.

Button backgrounds inherit their sprite, **Sliced** image type and **Pixels Per Unit Multiplier: 1** from `Assets/Resources/UI/GameButtonBackground.prefab`. Edit that prefab to change the background across the game. `GameButton.prefab` includes it for runtime-created buttons; authored buttons use the same nested prefab. Labels, icons, layouts and interaction colors remain on their owning buttons. The original background Image components are disabled and retained to preserve existing object references.

Use **Tools > Eternal Enigma > UI > Migrate Button Backgrounds to Shared Prefab** to migrate new or existing game buttons without restyling other controls. It preserves Button identities, callbacks and navigation, processes source prefabs before their dependents, and removes background styling overrides. Re-running it on migrated assets makes no changes. Save open scenes and close Prefab Mode first. **Validate Saved Button Styles** checks every authored button, including inactive ones, for shared-prefab inheritance, sliced mode and multiplier 1.

Use **Tools > Eternal Enigma > UI > Bake Button Styles** to reapply the full theme to owned scenes and prefabs. The command also styles panels, fields, sliders, toggles, scrollbars and tabs. Button backgrounds keep their prefab inheritance.

Parchment panels similarly inherit from `Assets/Resources/UI/GamePanelBackground.prefab`, with sliced mode and multiplier 1. **Migrate Panel Backgrounds to Shared Prefab** migrates panels using the theme's parchment sprite; **Validate Saved Panel Styles** checks their inheritance. Panel containers and contents keep their layout. Local tint and input-blocking settings are preserved, serialized Image references follow the new background, and decorative backgrounds ignore layout groups. Fade overlays, masks and graphics inside buttons are excluded. `GameUISkin.Panel` instantiates the shared panel prefab for runtime UI; use `GameUISkin.PanelGraphic` to access the visible background of an authored panel container.

Selected Bamao sprite import borders were configured for nine-slicing; original texture art is unchanged. Screenshots and regression summaries are under `Temp/UIValidation` and `Temp/DungeonUI`.
