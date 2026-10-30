# Bamao game UI theme

The hero picker and runtime inn dialog use the same GameUISkin helpers and authored button
prefab; scene/prefab styling remains reusable maintenance tooling.

All game scenes and UI prefabs use the shared theme at `Assets/Resources/UI/BamaoTheme.asset`, referencing the original assets in `Assets/Bamao/BamaoUIPack`.

The theme uses parchment panels, short parchment buttons, typing fields, framed tracks, round slider handles and square checkboxes. Text uses brown ink on paper; world labels and resource bars retain light text. Selected controls use gold, with distinct pressed and disabled states. Bamao's Magical Neverland font is used for authored headings, while dense text retains the readable body font.

`GameUISkin` uses these resources for generated dungeon and campaign interfaces. The legacy GUI skin covers autoplay and development panels. Button borders are drawn at compact sizes so they do not crowd labels.

Shared button backgrounds inherit their sprite, **Sliced** image type and **Pixels Per Unit Multiplier: 1** from `Assets/Resources/UI/GameButtonBackground.prefab`. Dungeon controls instead serialize a `DungeonUIRole` so their wood, paper, and cross treatments survive baking and migration. `GameButton.prefab` includes the shared background for runtime-created buttons. Labels, icons, layouts and interaction colors remain on their owning buttons. Original background Image components are retained to preserve existing references.

Use **Tools > Eternal Enigma > UI > Migrate Button Backgrounds to Shared Prefab** to migrate new or existing game buttons without restyling other controls. It preserves Button identities, callbacks and navigation, processes source prefabs before their dependents, and removes background styling overrides. Re-running it on migrated assets makes no changes. Save open scenes and close Prefab Mode first. **Validate Saved Button Styles** checks every authored button, including inactive ones, for shared-prefab inheritance, sliced mode and multiplier 1.

Use **Tools > Eternal Enigma > UI > Bake Button Styles** to reapply the full theme to owned scenes and prefabs. The command also styles panels, fields, sliders, toggles, scrollbars and tabs. Button backgrounds keep their prefab inheritance.

Parchment panels similarly inherit from `Assets/Resources/UI/GamePanelBackground.prefab`, with sliced mode and multiplier 1. **Migrate Panel Backgrounds to Shared Prefab** migrates panels using the theme's parchment sprite; **Validate Saved Panel Styles** checks their inheritance. Panel containers and contents keep their layout. Local tint and input-blocking settings are preserved, serialized Image references follow the new background, and decorative backgrounds ignore layout groups. Fade overlays, masks and graphics inside buttons are excluded. `GameUISkin.Panel` instantiates the shared panel prefab for runtime UI; use `GameUISkin.PanelGraphic` to access the visible background of an authored panel container.

Selected Bamao sprite import borders were configured for nine-slicing; original texture art is unchanged. Screenshots and regression summaries are under `Temp/UIValidation` and `Temp/DungeonUI`.

## Shared gameplay dock

The initial asset audit found 1,182 images and 236 prefabs in the installed pack, 44 distinct pack images referenced by project scenes/prefabs/resources, and eight sprites exposed by the shared theme. The dungeon theme now exposes these explicit roles and reusable prefab references:

| Role | Sprite under `Assets/Bamao/BamaoUIPack/Sprites` |
| --- | --- |
| Wood container | `Button/Popup_wood_bg` |
| Heading ribbon | `Button/Popup_paper_title` |
| Description/history paper | `Button/Popup_paper_bg` |
| Tabs/HUD | `Button/wood button_3` |
| Primary action | `Button/paper button green` |
| Secondary action | `Button/paper button white` |
| Close | `Character sheet/Button_cross_B` |
| Square icon frame | `Equipment 2/F_Brown` |
| Selected icon | `Equipment 2/F_select frame` |
| Resource track | `Select Character/State_blank` |
| HP/Mana/hunger fills | `Select Character/State_HP`, `State_blue`, `State_yellow` |

Run **Tools > Eternal Enigma > UI > Author Dungeon Dock** to regenerate the dungeon copies under `Assets/Resources/UI/Dungeon` and install them in DungeonScene. The party window uses the same compact dock geometry in town, overworld and dungeon. Stretchable roles use slicing; square icons and the cross preserve aspect ratio. `DungeonTextStyle` preserves brown text on paper and light text on wood during generic styling. Dense descriptions use the existing body font; decorative lettering is reserved for headings.

The dock uses safe-area anchors `(0.73, 0.03)`–`(0.98, 0.88)`. Persistent HUD launcher tabs expose Inventory, Equipment, Skills and Stats, plus Capabilities on the overworld; the active tab has a gold highlight and underlined text. Town and overworld provide a rightmost Options button, while dungeon reuses its existing HUD Options control above the party dock. Full service dialogs and dungeon targeting hide the launcher tabs; the dungeon Options control remains visible but disabled while targeting. Options returns to the previous party tab and focus when closed.

The root dock shows the inspected hero's name and one keyboard/controller-reachable corner close button, without duplicate tabs or footer exits. The close target is 64 logical pixels; headings reserve space beside it. Skills retain a separate description pane; other lists use the available panel height. Section labels and wrapped rows grow to fit their text. In the dungeon, inventory and skills belong to the controlled character. Tab/Shift+Tab or controller character-switch inputs change control while browsing, and HUD portraits accept left clicks above the transparent input shield. Switching refreshes the current tab and retains each character's entry/scroll state. Nested actions and targeting keep their original actor. Browsing spends no turns or resources.

Only the top dungeon dialog canvas is visible; targeting hides underlying panels. Cancel restores selection and scroll positions. Closing the final gameplay menu releases controller focus from the HUD so movement and confirm resume gameplay. HUD buttons remain clickable. Each menu gets a selection arrow, including external party tabs; nested dialogs show only their own arrow. Inventory entries open eligible Use/Equip/Unequip, Throw and Drop actions with one corner exit. Stat and equipment-total entries open descriptions without item commands. The minimap hides while browsing and recent events fit to the dock's left. Both history entry points call `GameMessages.ShowHistory()`, sharing one dialog with a single corner exit, duplicate-open protection, an empty state, newest-entry positioning, and keyboard/controller scrolling. History remains scene-local with existing coalescing and dungeon turn messages.

Native 3D targeting supports pointer preview, left-click confirmation and right-click cancellation. A held opening click must be released before targeting accepts another click; HUD hits and invalid targets cannot confirm. Keyboard/controller targeting remains available, including in terminal presentation. Dungeon Wait uses **Period / left stick press**; Full Control uses **F / right stick press**, without spending a wait action.

## UI audit workflow

Options and service screens use one primary parchment surface. Details use plain
insets, Hall of Fame cards use slate, and confirmations dim the parent screen.
`GameUISkin.RibbonHeader` reuses the theme ribbon for screen and section titles,
keeps lettering clear of its folded ends, and never intercepts input. Trainer rows
separate ability names, rank/availability, and cost; the detail-scroll control
appears only when the description needs scrolling.

Run **Tools > Eternal Enigma > Tests > Run UI Audit Captures** for main menu, save slots, hero selection, settings, town/overworld docks and services, and dungeon dialogs. Captures cover 960×600, 1280×720, 1920×1080 and 2560×1080; dungeon adds 1280×800. Screenshots and text bounds/font/overflow reports are in `Temp/UIScreenAudit`; dungeon screenshots are in `Temp/DungeonDock`. Inspect these alongside layout assertions, since an overflow flag alone does not establish readability.

Run **Tools > Eternal Enigma > Tests > Run UI Audit Controls** separately for keyboard, gamepad and mouse focus, nested cancellation, Options access, shortcuts, shop/travel guardrails and party state restoration. Both commands write `Temp/HarnessResults/PlayMode.json` and `.xml`. See [test instructions](../Assets/Tests/README.md#ui-audit) for MCP invocation. These are audit procedures; the dated verification below describes an earlier run.

Supported gameplay schemes are Keyboard & Mouse and Gamepad. The incomplete
Touch, Joystick, and XR template schemes are excluded from automatic switching.
**Run UI Audit Devices** checks device switching and takeover prevention. See
[the October 14 audit](UIAudit.md) for current corrections and verification.

## Overworld presentation

The terrain checkerboard had two presentation sources: inconsistent normals between height-field modules/chunks and per-face box projection switching axes. `EnvironmentBatch.SmoothTerrain` now shares normals at coincident height-field vertices, and the mountain material and its authoring source use planar projection. Navigation masks are unchanged. The gate's atlas-crossing triangles now use stable material swatches; one gate is centered across each locked route, aligned to the route, and scaled from its mesh bounds. Unlock requirements and shortcut tint distinctions remain. Occupied location models stay visible during spawning, movement, warping and party changes.

A follow-up screenshot exposed a separate atlas defect on location platforms and perimeter posts near towns. `OverworldScene.BuildLocationFootprint` had assigned the complete building atlas to standard cube UVs. Platforms now use continuous world-projected paving, positioned above the ground without overlapping it; each post face samples only the padded masonry swatch. The fixed-seed test checks every generated location's material and UV region on both initial and restored visits, with close-up captures named `location-platform-*`.

The grass source at `ArtSource/PaintedEnvironment/Ground_Grassland.png` and its runtime texture now contain continuous green grass without exposed dirt. The existing painted-environment importer preserves the runtime GUID and creates matching repeat edges. The source was edited with built-in ImageGen using this prompt:

> Use case: precise-object-edit. Asset type: seamless square tileable grass albedo texture for an existing stylized fantasy game. Edit target: the supplied Grassland ground texture. Primary request: remove ALL exposed brown dirt patches and replace them with continuous healthy green grass. Make coverage even and less patchy: subtle small-scale variation only, no broad bright/dark islands or diagonal bands. Preserve the original hand-painted brushwork, tiny grass blade scale, and green palette but moderately soften contrast for readability under game characters. Orthographic top-down flat diffuse surface, evenly lit, opaque, seamless matching opposite edges. No soil, paths, rocks, flowers, checkerboard, grids, shadows, borders, text or objects. Output only the usable square grass texture.

Run **Tools > Eternal Enigma > Author Overworld Presentation** to reapply grass, projection and gate authoring. Fixed-seed visual regression captures for seeds 42 and 12345, including restored maps, are in `Temp/OverworldPresentation/Before` and `After`. Unity MCP invokes the authoring and harness test menu commands; results are written to `Temp/HarnessResults`.

## Verification — 2026-10-06

58 unique tests passed across the regression run and focused reruns (52 PlayMode, six EditMode). The result index is `Temp/DungeonDock/Verification.json`; it identifies the source XML files and uses each test's latest result. Final checks cover portrait raycasts above the input shield, keyboard/controller character switching, actor retention in nested actions, direct item actions, exact-item targeting and costs, cancellation and scroll restoration, long descriptions, history reopening/empty state/scrolling, targeting-text overflow, terrain seams, atlas UVs, locks and travel, and repeated style baking without layout changes.

All eight dungeon states were captured at the four resolutions: HUD, inventory, skills, item actions, nested picker, legacy inventory picker, targeting, and history. Targeting banners now fit beside the resources; the obsolete floor-label frame is removed. Invisible scroll controls have an explicit `InputSurface` role so Bake Button Styles cannot paint an extra button over their parchment.

Older regression fixtures were updated for the existing campaign-slot and party-menu flows, painted biome textures, and the new top-dialog-only presentation. Test input scopes temporarily suspend existing physical devices and restore them afterward: a connected DualShock's background reports had been replacing the synthetic `Gamepad.current` during tests. No production input behavior was changed for that test isolation.
