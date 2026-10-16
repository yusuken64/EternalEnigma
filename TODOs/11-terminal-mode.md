# Remaining terminal-mode integration and acceptance

Audited 2026-10-08; implementation and UI audit updated 2026-10-15. Terminal mode is a presentation option in the Unity game.
Normal campaign objects, input, combat, services and saves remain authoritative;
there is no separate full-game terminal simulation.

## Implemented

- Core `Terminal/` contains cells, cached frames, literal TMP serialization,
  viewports, glyph rules, three map renderers and `TerminalMenuModel`.
- `Assets/Scripts/Terminal/` contains the persistent runtime owner, off-by-default
  preference, bundled monospace font, screen, scene snapshots and toggle controls.
  Main-menu/settings buttons, F11 and controller View/Share select the presentation; switching is deferred
  while dungeon turns, town actions or overworld movement are busy.
- Town, overworld and dungeon snapshots read live actors and logical cells.
  Dungeon rendering reads minimap knowledge and party sight, includes reveal and
  disguise handling, and exposes selected/valid target cells from `TargetDialog`.
- Terminal presentation hides the redundant dungeon minimap/event feed while
  retaining their knowledge/history. Animation preferences and gameplay timing
  are preserved; world cameras and generation remain active.
- `TerminalMainMenuView` supplies an animated ASCII title/backdrop and
  restyles the existing main-menu buttons, restoring them on mode exit.
  Its footer follows the active input device and hides when the menu owner is suspended.
- `TerminalMenuBridge` binds `TerminalMenuModel` to the current menu scope's
  live controls. `TerminalMenuSession` uses stable row IDs, refreshes on submit,
  checks ownership/active/interactable state and changed labels, and rejects
  duplicate input sequences. Production Submit callbacks retain transaction and
  turn-command validation. No town/equipment/combat transaction is duplicated.
- `TerminalMenuStyle` applies and restores monospace text and dark panels on
  active dialogs. Original uGUI remains visible and clickable; native text fields,
  sliders, dropdowns, preview scrolling, party tabs and Back/focus scopes retain
  their production control routes.
- Target snapshots distinguish valid cells (`?`), affected area (`*`), missile
  path/direction and selected endpoint (`X`). Labels include skill/item, selected
  character/prop/tile, area radius and device-aware confirm/cancel controls.
- Dungeon knowledge follows minimap treasure (gold, dropped items, containers),
  explored hazards, trap discovery/Farsight and enemy disguise rules. Visible
  multi-cell actors occupy their full footprint; unseen mimics do not become loot.
- Immutable `TerminalKnowledge` reuses unchanged visibility and allocates a new
  grid only when it changes. Older captures remain immutable. Maps without fog
  need no visibility array. Static terrain is cached per dungeon floor, town plan
  and overworld grid; live gates, keys, actors, resources and messages still refresh.

## Implementation checklist

- [x] Bind the pure menu model to town services, dungeon commands and shared
  dialogs in the coverage table below. Revalidate actors/items/targets on confirm;
  cancel and stale selections must spend no resources or turns.
- [x] Give terminal modals exclusive input ownership and restore previous focus.
  Keep each existing uGUI route visible until its full replacement passes.
- [x] Complete targeting presentation beyond the selected cell and valid
  cells: missile endpoint/direction, area preview and target labels for
  character, prop, tile, inventory and missile flows.
- [x] Align knowledge rules with the minimap for revealed treasure,
  hazards/traps, mimics and multi-cell actors.
- [x] Remove per-capture full visibility allocation/copying and cache static terrain.

These checkmarks describe implementation, not a completed player acceptance pass.

| Flow | Current presentation / remaining work |
| --- | --- |
| Main menu | ASCII backdrop, device-aware footer and live-control menu-model binding. |
| Slots, hero selection, settings | Shared terminal adapter/style; native widget behavior retained. |
| Town buildings, buy/sell, trainer, recruit/dismiss, rest/save, equipment | Shared adapter dispatches production controls and existing TownServices, TrainerOffers and EquipmentTransferService paths. |
| Dungeon party, skills, items, equipment, stairs, pause, results | Shared adapter dispatches existing action factories and turn-command pipeline. |
| Targeting | Production target input, expanded ASCII preview and stale caster/floor/command validation. |
| Overworld party/capabilities, warps, gates, confirmations, history | Shared scoped menu adapter; live map facts retained. |
| Level-up and game-over/victory | Shared adapter/style through existing DialogController scopes. |

## Verification and remaining acceptance

- [x] Run the expanded terminal integration, presentation, interaction and Core
  fixtures, covering stale/duplicate submits, shop revalidation, knowledge and
  disguise parity, target preview/cancel/confirm, font/style restoration,
  duplicate-UI hide/restore, grid resizing and shared-save Continue in both
  directions. Current run counts and evidence are in the
  [terminal UI audit](../Docs/TerminalUIAudit.md).
- [x] Exercise scene/floor covers, destination binding, return to the main menu,
  and one runtime/screen owner using the editor harness with domain reload
  disabled. This does not replace cold-start player testing.
- [x] Exercise keyboard, mouse and simulated gamepad targeting, guarded HUD
  clicks, deferred toggles, modal focus and external party-header controls.
- [x] Capture menus, services, party docks, maps, targeting, history, level-up and
  results at 960x600, 1280x720, 1920x1080 and 2560x1080. Inspect each screen at
  the smallest size and representative wide captures; automated viewport changes
  and resize checks supplement pixel review.
- [ ] Verify cold startup and sustained repeated travel/floor replacement in a
  player, including toggles during replay and manual live-window resizing.
- [ ] Smoke-test physical controllers; automated device-event injection does not
  verify hardware-specific behavior.
- [ ] Complete a shared-save playthrough: terminal New Journey -> town -> overworld
  -> dungeon; fight, target a skill/item, descend, transact and explicitly save.
  Continue in 3D and compare party, inventory, gold and progression; repeat in
  reverse using disposable test slots.
  `SharedSaveContinuesBothDirectionsWithMatchingPartyInventoryGoldAndProgression`
  covers shop purchase, inn save, mode switching and real campaign Continue in
  both directions using the harness's memory save store. Its seeded progressed
  fixture is not a full town/overworld/dungeon combat playthrough.
- [ ] Build and smoke-test Windows and WebGL after integration. Earlier diorama
  builds predate terminal mode and do not verify its font/UI resources.
- [ ] If world-render suppression is added later, preserve camera/audio/projection
  dependencies and verify complete restoration across scene changes.

Run offline builds/tests and rebuild/import the Core DLL as described in
[Core](../Core/README.md). Unity/player checks are separate. Record current run
results here during integration; source implementation alone is not player acceptance.
