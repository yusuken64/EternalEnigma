# TODO: Terminal mode (play the full game as a simulated console)

## Goal and delivery split

A "Terminal mode" option in the Unity client presents the whole game as colored ASCII: overworld, towns, dungeon combat, items, allies and, eventually, menus. The same campaign and saves work in terminal and 3D modes.

**The first implementing agent has repository access and the .NET SDK, but no Unity installation, editor, MCP connection or license.** It should complete the standalone renderer, tests and as much Unity integration source as possible. A second agent with Unity access will finish integration, resolve import/compile/runtime issues, and verify the result. Do not stop all work because one integration detail needs Unity.

Track implementation and validation separately. Passing .NET tests proves the standalone rendering code works; it does not prove Unity adapters compile, TMP renders correctly, or the game is playable. Leave those checks explicitly pending for the Unity handoff.

The playable first milestone retains existing uGUI menus and HUD over the terminal map. Terminal-native menus are a second milestone; neither milestone requires a separate game simulation.

## Architecture and constraints

- Normal Town, Overworld and DungeonScene objects, generation, input, combat, services and saving remain authoritative. Terminal code reads their state and changes presentation.
- Put engine-independent cells, snapshots, viewport logic, glyph selection and text serialization in `Core/EternalEnigma.Core/EternalEnigma.Core/Terminal/`, targeting the existing `netstandard2.1` library. Test them in `Core/EternalEnigma.Core/EternalEnigma.Core.Tests/Terminal/` with the existing .NET 10/xUnit setup.
- Put Unity adapters and runtime UI construction in `Assets/Scripts/Terminal/`, inside the existing `EternalEnigma.Game` assembly. Convert Unity values to Core data at this boundary. Core must not reference `UnityEngine`, TMP, TWC, scenes or the Explorer executable.
- Build the terminal Canvas and controls in C#. Hook them into existing script lifecycle methods; avoid required Inspector assignments, hand-edited scene/prefab YAML, editor menu commands and generated TMP assets in the first agent's work.
- The initial black, opaque terminal Canvas covers the world while cameras remain available. Suppressing 3D rendering is a later optimization that requires Unity verification. In particular, disabling the main camera can break `Camera.main` lookups during dungeon setup and UI projection.
- Keep the existing input handlers for the first milestone. Rendering must never move characters, spend resources, grant rewards, submit commands or advance random-number generators.
- Preserve unrelated working-tree changes. Do not edit Unity-generated root `.csproj` files or rely on `Library/`, installed Unity DLLs or local machine paths for offline validation.

## Source map for the implementing agent

Paths below are relative to the repository root. Recheck members while implementing; use symbols rather than historical line numbers.

| Area | Existing source and integration seam |
| --- | --- |
| Persistent lifetime | `Assets/Scripts/Common/Common.cs`: `Common.Initialize`, `LoadingSceneIntegration.EnsureCommon`; attach one runtime owner under the persistent Common object. |
| Preferences and entry points | `Assets/Scripts/MainMenu/DisplayPreferences.cs`, `DisplayOptions.cs`, `MainMenu.cs`, `GlobalSettings.cs`; `MainMenu.Start` waits for Common and exposes `IsReady`. |
| Dungeon state | `Assets/Scripts/Dungeon/Game/Game.cs`: `IsReady`, `CurrentDungeon`, `AllCharacters`, `DownedAllies`, `PartyVisibleTiles`, `FloorReveal`; `PlayerController.ControlledAlly` identifies the current input actor. |
| Dungeon map and knowledge | `Assets/Scripts/TileWorldDungeon.cs`: `Floor`, `GetFloorMask`, `IsWalkable`, `Interactables`; `Assets/Scripts/Dungeon/Game/UI/Minimap.cs`: `dungeonMap` and its unseen/explored/visible states. |
| Combat and speed | `Assets/Scripts/Dungeon/System/TurnManager.cs`: `SubmitCommand`, immediate resolution and replay; `Assets/Scripts/Common/DungeonPreferences.cs`: `AnimationMode`, `AnimationOverride`, `NoAnimations`. |
| Target selection | `Assets/Scripts/Dungeon/Game/Menu/TargetDialog.cs`: character, prop, tile and missile selection. Current indicators are world objects and will be covered by the terminal. |
| Overworld | `Assets/Scripts/Overworld/OverworldScene.cs`: `IsReady`, `Context`, `Map.CurrentGrid`, `Position`, `Player`, `Followers`; `CampaignContext.Gates` owns gate state. |
| Town | `Assets/Scripts/Town/Town.cs`: `IsReady`, `Plan`, `Services`, `TownAllies`, `ShopVendors`, `Townsfolk`. The controlled cell is `TownPlayer.ControllingTownAlly.TilemapPosition`, not `TownPlayer.transform.position`. |
| Messages and focus | `Assets/Scripts/MainMenu/GameMessages.cs`: scene-local `History`; `MenuUIInputModule.cs`: dialog focus and input consumption. |
| Glyph reference | `Core/EternalEnigma.Core/EternalEnigma.Campaign.Explorer/`: `MapRenderer`, `TownRenderer`, `DungeonRenderer`, `TownServiceGlyphs`. Port glyph mappings, not `ExplorerSession` or its simulated progression. |
| Existing offline checks | `Core/README.md`, `Core/global.json`, `Core/Directory.Build.props`, `.github/workflows/core.yml`; Core's assembly architecture tests reject engine dependencies. |

The Explorer targets .NET 10, uses its own session state and does not implement the live game's combat. Unity gameplay still depends on MonoBehaviours, ScriptableObjects, coroutines and live town objects. Extracting the full game into a headless executable is outside this task.

## Part A: implementation without Unity

### 1. Standalone rendering contracts and tests

- [ ] Add `TerminalColor` (RGB bytes), `TerminalCell` (one glyph plus color), `TerminalFrame`, `TerminalPalette` and `TerminalViewport` to Core's new `Terminal` namespace. Do not use `Color32`, `Vector3Int` or Unity attributes here; existing Core `GridPoint` is suitable for coordinates.
- [ ] Define read-only snapshots for dungeon, overworld and town. They carry dimensions, terrain/features, visibility, actor IDs/cells/glyph categories, party statistics, messages and optional targeting state. They must not retain references to Unity objects or mutable live collections.
- [ ] Split ownership clearly: Unity adapters supply current facts and permitted visibility; pure renderers choose layout, glyphs and colors. Keep simulation decisions in the existing game. For static maps, reuse immutable Core world descriptions where practical rather than copying the whole map each frame.
- [ ] Implement `Put`, clipped `Write`, `Fill`, `Box`, plain-text output and `ToRichText()`. Use ASCII `+`, `-`, `|` borders initially. Normalize unsupported/control characters so every printable cell occupies one column; log wrapping and clipping must be deterministic.
- [ ] Merge adjacent equal-color cells into `<color=#rrggbb>` runs and close tags correctly. Treat all game text as literal: test `<`, `>`, `&`, and strings resembling TMP tags, including `</noparse>`. Use TMP-compatible literal handling; do not assume HTML entity escaping works. Renderer-generated tags alone may affect formatting.
- [ ] Reuse buffers and a `StringBuilder`, and cache the final string. Compare completed frames so clearing and repainting identical content does not force a new string/TMP update. Color changes, resize and removal of old glyphs must invalidate correctly.
- [ ] Define viewport rules: Y increases upward in the map and rows run downward in text; center on the controlled actor and clamp to map bounds. Pad maps smaller than the viewport. Reserve party/log rows and collapse the sidebar on narrow windows. Give target selection a viewport focus so a selected target remains visible.
- [ ] Implement `DungeonTerminalRenderer`, `OverworldTerminalRenderer` and `TownTerminalRenderer` over these snapshots. Port only the needed Explorer glyph rules. Do not reference the net10.0 Explorer from the netstandard library or reuse generated spawn lists as live entity state.
- [ ] Add small, readable fixtures with expected glyph rows and color cells for all three surfaces. Cover borders/corners, tiny windows, overlapping glyph priority, movement, selection, hidden content, gate/key/warp changes and unchanged-frame caching. Fixtures simulate renderer inputs, not combat.

**Offline completion:** the actual production rendering code builds for netstandard2.1 and its xUnit tests pass without engine assemblies. A fixture can produce reviewable plain text and TMP markup without starting Unity.

### 2. Runtime owner, font and screen source

- [ ] Add `TerminalMode.cs` with an off-by-default PlayerPrefs preference, an idempotent setter and a `Changed` event. Separate the saved/requested setting from effective runtime activation so missing UI resources or unsupported scenes can fall back safely. Do not add presentation preferences to `GameSaveData`.
- [ ] Add `TerminalRuntime.Ensure(Common)` and call it from `Common.Initialize` after Common's services are initialized and before its player scene load. Make it idempotent, own its children, reset static caches on subsystem registration, and unsubscribe on destruction. Do not require a component to be added to `Common.unity`.
- [ ] Add `TerminalScreen` and a small runtime UI factory. Create a Screen Space Overlay Canvas, opaque black background and one `TextMeshProUGUI` with fixed cell spacing, top-left alignment, no wrapping and no autosizing. Existing `GameUISkin` construction helpers are under `UNITY_EDITOR`; do not call those helpers in player code.
- [ ] Start the terminal Canvas at sorting order `-100`, below the existing overlay HUD/dialog canvases. Set terminal background/text `raycastTarget = false`; the map must not intercept menu clicks. Reuse the scene EventSystem and input module. The Unity agent must confirm ordering, especially transitions, settings and world-space prompts.
- [ ] Bundle a redistributable monospace TTF and its license under `Assets/Resources/Terminal/` (for example, `TerminalMono.ttf`), recording its source/version. Load the Font resource and create a dynamic font through `TMP_FontAsset.CreateFontAsset(Font)` at runtime; prewarm printable ASCII. This avoids an editor-generated SDF asset as an implementation prerequisite.
- [ ] Provide a fallback to the already bundled LiberationSans SDF with TMP fixed-width `<mspace>` formatting. Missing glyphs use ASCII substitutes; box-drawing Unicode is optional. If font acquisition/import needs Unity help, leave that item pending and continue with the fallback. Never depend on an operating-system font path. Destroy only runtime-owned font/material/atlas objects.
- [ ] Calculate rows and columns from the text area's usable size and font metrics in the same Canvas units. Rebuild on resize/font changes. Validate resource readiness before activating the opaque screen; retain usable existing UI if neither font path works.
- [ ] Bind/unbind scene adapters on scene load/unload, with readiness checks for `Game`, `Town` and `OverworldScene`. Clear stale references and cached terrain at floor/campaign changes. Show a loading frame while a supported scene initializes; keep the normal front-end visible in MainMenu for milestone one.
- [ ] Use bounded polling, initially 10-15 updates per unscaled second, plus immediate refresh on activation/scene changes. Cache scene references and static terrain; do not scan the scene or clone the full floor mask every frame. Assign TMP text only when the finished frame changes.
- [ ] Keep gameplay objects, cameras, AudioListeners, portrait render-texture cameras and TWC generation active. An opaque terminal is sufficient for the first visual milestone; no rendering-performance improvement is claimed yet.

### 3. Live snapshot adapters and playable map behavior

- [ ] **Dungeon:** capture `CurrentDungeon.Floor`/floor mask, live `Interactables`, logical `TilemapPosition` and current `Vitals`. Use `@` for the controlled hero, stable party-slot glyphs for other heroes, separate summon/downed markers, and a deterministic enemy-type mapping with a fallback. Do not infer positions or health from animated transforms/displayed UI.
- [ ] **Dungeon knowledge:** read existing minimap exploration and `PartyVisibleTiles`, including when terminal mode is enabled halfway through a floor. Preserve `FloorReveal` effects, hidden traps, mimics/disguises and multi-cell actor visibility according to existing minimap/game rules. Draw explored terrain dimly; ordinary enemies/items must not leak through fog. Clear floor-specific caches on floor replacement, not merely when its number changes.
- [ ] **Targeting is required in milestone one:** add a small read-only selection snapshot to `TargetDialog` where current private members prevent observation. Cover selected character/prop/tile, valid target cells, missile direction/endpoint and available area preview. Draw a distinguishable cursor/highlight and selected-target label in ASCII. Keep selection, confirm/cancel, eligibility and action creation in the existing dialog/controller. Rendering must not change valid targets.
- [ ] **Overworld:** read `OverworldScene.Position`, `Context.Grid`, campaign locations, gates, collected keys, warps and live followers. Reflect current gate/warp state through existing Core queries. Never create a second `ExplorerSession`, simulate victories, or regenerate the campaign just to draw it.
- [ ] **Town:** read `Town.Plan`, the controlled ally's logical cell, recruited/waiting allies, live vendors and `TownNpc.Cell`. Render service doors from current building/service definitions and interiors from the plan. Generated ally slots/vendor anchors alone are insufficient once actors move or are recruited.
- [ ] **HUD/log:** capture party HP/SP/level, current actor/turn status, location/floor and a bounded tail of `GameMessages.History`. Read the scene's existing component rather than scraping its TMP text or posting duplicate messages. Add a narrow read-only accessor only where necessary.
- [ ] In each adapter, tolerate initialization, destroyed actors, floor teardown, no current campaign and scene changes. Suspend capture while authoritative state is not ready; an empty/loading frame is preferable to exceptions or stale campaign content.

### 4. Entry points, speed and reversible mode switching

- [ ] Create a "Terminal mode: On/Off" control in code from `MainMenu.Start` after readiness, and in `DisplayOptions` initialization. Parent controls to existing menu/settings containers; explicitly wire callbacks and navigation without adding unassigned serialized fields. Refresh labels when the preference changes and avoid duplicate controls/listeners on reopen.
- [ ] Preserve normal `CampaignSlots`, hero selection, New Journey/Continue and `Travel` behavior. The preference changes presentation only; do not introduce terminal-specific save files or migration.
- [ ] Add a debug F11 toggle using the existing Input System, with a null-device guard. Provide menu access independently of the hotkey. Consume the toggle input and apply presentation changes at a safe movement/turn boundary; do not interrupt a coroutine or discard a queued action.
- [ ] Make the effective dungeon animation policy return `NoAnimations` while terminal presentation is active. Preserve the saved animation preference and existing `AnimationOverride` value instead of overwriting either; switching back restores their normal precedence. Keep immediate resolution, replay completion, displayed-state synchronization and turn bookkeeping running.
- [ ] Skip only the cosmetic two-second wait in `Game.AdvanceFloorRoutine` when terminal presentation is active. Keep generation callbacks, floor-start passives, actor setup, minimap refresh, readiness and transition completion. Do not disable Game, TurnManager or TWC, or change global time scale.
- [ ] Leave town/overworld movement routines intact initially; their animation can finish behind the overlay. If removing those delays is needed later, do it with Unity tests for movement locks and arrival interactions.
- [ ] Switching off must restore normal presentation without mutating campaign state, consuming a turn, resetting exploration or closing a gameplay action. Preserve normal UI as the fallback until replacement terminal menus are verified.

### 5. Prepare terminal-native menus after the map milestone

This is the second product milestone. The first agent should implement the pure menu model and tractable adapters, recording exactly which integrations remain. Unity-only uncertainties should not block renderer or menu-model work.

- [ ] Add a pure `TerminalMenuModel`: stable option IDs, labels, enabled states/reasons, selected ID, pagination, nested menus, confirm/cancel and focus restoration. Tests cover disabled/empty lists, removed selections, scrolling, nested cancel and one activation per input. Game objects and side effects stay in the Unity adapter.
- [ ] Build a coverage table in this file mapping each existing flow to its model, service/action call, fallback and verification status. Inspect current `PartyMenu` and menu-manager entry points as well as legacy inventory/skill dialogs before duplicating UI logic.
- [ ] **Town:** buy/sell, trainer/ranks, recruit/dismiss, rest/save, equipment and building choices. Call `TownServices`, `TrainerOffers` and `EquipmentTransferService` through the same validated paths used by current menus. Preserve stock, gold, eligibility and failure messages.
- [ ] **Dungeon:** party/skills/items/equipment, inventory-targeted effects, character/prop/tile/missile targeting, stairs, pause and results. Reuse current action factories and `TurnManager.SubmitCommand`/existing `SetAction` paths as appropriate; do not execute effects directly. Revalidate the live actor/item/target on confirm. Cancel and stale selections must not consume SP, items or turns.
- [ ] **Overworld and shared UI:** party changes, warp destinations, gate choices, message/confirmation dialogs and event history. Before claiming all-terminal UI, also cover main menu, slots, hero selection, settings, level-up choices and game-over/victory flows.
- [ ] Integrate modal ownership with `MenuUIInputModule` and the existing scene menu manager/input maps. Exactly one UI path may consume each navigation/confirm/cancel event. Suppress movement while a terminal modal owns input and restore the previous focus on close.
- [ ] Hide or bypass an existing uGUI dialog only after its terminal equivalent handles the full flow and its Unity checks pass. A terminal modal must not open underneath an active uGUI dialog or dispatch the same transaction twice. Keep unfinished routes on the visible uGUI fallback and list them in the handoff.

### 6. Offline verification and deliverables

Run from the repository's `Core` directory; `global.json` selects the .NET 10 SDK. These commands use no Unity installation:

```powershell
dotnet restore EternalEnigma.Core/EternalEnigma.Core.slnx
dotnet build EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release --no-restore
dotnet test EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release --no-build --logger trx --results-directory TestResults
```

- [ ] Run the rendering/menu tests and existing Core regressions, including the architecture test. Keep warnings-as-errors and the netstandard2.1 runtime target. The existing Core workflow discovers new tests automatically.
- [ ] Include fixtures that prove fog does not expose hidden actors/items; map orientation and clamping are correct; live removals/gate changes update glyphs; hostile-looking rich text remains literal; and confirm/cancel/menu pagination behave correctly. Keep deterministic text/color expectations independent of fonts and GPU output.
- [ ] Review the Unity adapter source against actual members and assembly accessibility. Do not use fake Unity/TMP stubs or a successful root `.csproj` build as evidence that Unity integration works. Compile/execution of those adapters remains a Unity-agent task.
- [ ] Rebuild and copy the Core runtime DLL after source changes so Unity consumes the same renderer that was tested. From `Core`, after the successful build/test above:

```powershell
Copy-Item -LiteralPath 'EternalEnigma.Core/EternalEnigma.Core/bin/Release/netstandard2.1/EternalEnigma.Core.dll' -Destination '../Assets/Plugins/EternalEnigma.Core/EternalEnigma.Core.dll'
Get-FileHash -LiteralPath 'EternalEnigma.Core/EternalEnigma.Core/bin/Release/netstandard2.1/EternalEnigma.Core.dll'
Get-FileHash -LiteralPath '../Assets/Plugins/EternalEnigma.Core/EternalEnigma.Core.dll'
```

- [ ] Confirm the two hashes match. Include the updated DLL with the implementation and preserve its existing `.meta` GUID/settings; copying it needs no Unity editor. Do not import test/Explorer assemblies or framework DLLs into Assets.
- [ ] Add stable, unique `.meta` GUIDs for new Assets files/folders using neighboring importer formats. Include font data and license when available. No scene/prefab edits are required for the initial runtime-created UI; record any later serialized asset work explicitly.
- [ ] Author focused Unity tests under `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/` where the existing harness supports them, but mark them **authored, not run**. Do not run Unity MCP, the editor or Unity batchmode from the first agent's environment.
- [ ] Update the handoff below with implemented file paths, completed steps, command results, fixture outputs, known compile/runtime assumptions, remaining menu routes and exact work requiring Unity. Use statuses such as **implemented + .NET verified**, **implemented + Unity unverified**, and **remaining Unity work**. Do not describe the entire feature as verified.

## Part B: finish and verify with Unity access

Use the Unity version in `ProjectSettings/ProjectVersion.txt` and the project's Unity MCP tools where available. This agent owns any remaining integration code, asset setup and fixes, as well as validation.

- [ ] Import the updated Core DLL, new scripts and font; resolve compilation/import errors. Check player-runtime code is outside `UNITY_EDITOR`, scripts are in the intended assemblies, and font/TMP resources are included in builds. If dynamic font setup cannot meet layout/build requirements, create a project-owned TMP font asset and bind it through the runtime resource loader.
- [ ] Inspect runtime construction from a clean launch, direct gameplay scene startup and repeated menu/travel cycles: one persistent terminal owner, one screen, no duplicated listeners or EventSystems, and no stale floor/campaign references. Test domain reload disabled as well as normal startup.
- [ ] **EditMode:** preference persistence/defaults; runtime construction helpers; font glyph availability/advance; literal TMP parsing; effective animation preference restoration. Verify the actual TMP output agrees with the offline cell widths, including `<`/`>` and narrow layouts.
- [ ] **PlayMode:** use `Assets/Tests/PlayMode/Support/GameTestHarness.cs` and existing campaign/menu fixtures. Load real scenes, wait for readiness, assert glyphs only for currently visible or legitimately revealed content, submit moves/actions and check resulting cells, HP/SP and messages. Test floor replacement, traps/disguises/reveal effects and switching modes after exploration.
- [ ] Verify character, prop, tile, inventory and missile targeting with keyboard/controller navigation, confirm and cancel. Check the selected target remains visible, cancellation is free, each command executes once, and hidden world indicators have a terminal equivalent.
- [ ] Verify settings, main-menu toggle, menu focus, mouse clicks on retained uGUI, input locking and transition overlays. Test toggles during movement, action replay, floor generation and open menus; queue or reject transitions safely where necessary.
- [ ] Validate every terminal-native menu route that was implemented, then finish outstanding routes from the coverage table. Until each replacement passes, retain its uGUI fallback. Run relevant existing settings, controller, menu, town-service, targeting, travel and save regressions after integration changes.
- [ ] Visually inspect at 1280x720, 1920x1080, a small window and during live resizing: aligned cells, legible font/colors, unclipped HUD/log, targeting, dialogs and loading transitions. Retain representative captures of all three maps and a modal.
- [ ] **Shared-save acceptance:** terminal on -> New Journey -> town-0 -> overworld -> story-0; fight, use a targeted skill/item, descend, return to town, perform a service transaction and save. Disable terminal -> Continue in 3D and compare party/inventory/gold/progression. Save in 3D -> Continue in terminal as the reverse check. Use disposable test slots.
- [ ] Build and smoke-test the project's supported player targets, including Windows and WebGL, respecting any existing project build restrictions. Check font resources/materials, runtime-created controls, disabled-mode behavior and logs in the actual player. A passing Editor test is not player-build validation.
- [ ] Optionally suppress world rendering after correctness is established. Keep camera objects/controllers available for lookups, projection and audio; preserve UI/portrait cameras. Record and restore any camera masks/render state that this feature changes. Verify newly loaded scenes, late-created cameras, transitions and mode-off restoration before enabling the optimization. Do not blanket-disable all cameras.

## Handoff record (fill during implementation)

| Deliverable | Status / evidence / remaining work |
| --- | --- |
| Core cells, renderer and snapshots | Planned; no implementation is claimed by this document update. |
| Offline tests and fixture output | Pending; record exact commands and results. |
| Runtime owner, screen and font | Pending; distinguish source implementation from import/layout verification. |
| Town/overworld/dungeon adapters and targeting | Pending; record member/API assumptions needing Unity. |
| Entry points, speed and mode restoration | Pending; verify original preferences and input behavior survive toggling. |
| Terminal-native menu coverage | Pending; list implemented routes and retained uGUI fallbacks. |
| Core DLL and new asset metadata | Pending; record DLL hash agreement and any missing font/import work. |
| Unity tests | Pending; distinguish authored tests from executed results. |
| Gameplay, save compatibility and player builds | Pending; owned by the Unity-enabled agent. |

## Out of scope

- Extracting combat or town simulation from Unity, replacing saves with Explorer state, or delivering a standalone full-game terminal executable.
- Skipping TileWorldCreator generation/mesh callbacks: current scene readiness depends on them. Optimize only as a separate follow-up.
- Reauthoring the existing scene/prefab UI wholesale. Use runtime integration for this feature and leave unrelated presentation work intact.
