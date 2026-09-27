# Bamao game UI theme

All game scenes and UI prefabs use the shared theme at `Assets/Resources/UI/BamaoTheme.asset`, referencing the original assets in `Assets/Bamao/BamaoUIPack`.

The theme uses parchment panels, short parchment buttons, typing fields, framed tracks, round slider handles and square checkboxes. Text uses brown ink on paper; world labels and resource bars retain light text. Selected controls use gold, with distinct pressed and disabled states. Bamao's Magical Neverland font is used for authored headings, while dense text retains the readable body font.

`GameUISkin` uses these resources for generated dungeon and campaign interfaces. The legacy GUI skin covers autoplay and development panels. Button borders are drawn at compact sizes so they do not crowd labels.

Use **Tools > Eternal Enigma > UI > Bake Button Styles** to reapply the full theme to owned scenes and prefabs. The command also styles panels, fields, sliders, toggles, scrollbars and tabs. It preserves gameplay objects, portraits, item icons, button callbacks and navigation. Save open scene edits before baking. **Validate Saved Button Styles** checks every authored button.

Selected Bamao sprite import borders were configured for nine-slicing; original texture art is unchanged. Screenshots and regression summaries are under `Temp/UIValidation` and `Temp/DungeonUI`.
