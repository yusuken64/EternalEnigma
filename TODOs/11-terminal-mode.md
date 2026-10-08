# Remaining terminal-mode integration and acceptance

Audited 2026-10-08. Terminal mode is a presentation option in the Unity game.
Normal campaign objects, input, combat, services and saves remain authoritative;
there is no separate full-game terminal simulation.

## Implemented

- Core `Terminal/` contains cells, cached frames, literal TMP serialization,
  viewports, glyph rules, three map renderers and `TerminalMenuModel`.
- `Assets/Scripts/Terminal/` contains the persistent runtime owner, off-by-default
  preference, bundled monospace font, screen, scene snapshots and toggle controls.
  Main-menu/settings buttons and F11 select the presentation; switching is deferred
  while dungeon turns, town actions or overworld movement are busy.
- Town, overworld and dungeon snapshots read live actors and logical cells.
  Dungeon rendering reads minimap knowledge and party sight, includes reveal and
  disguise handling, and exposes selected/valid target cells from `TargetDialog`.
- Terminal presentation hides the redundant dungeon minimap/event feed while
  retaining their knowledge/history. Animation preferences and gameplay timing
  are preserved; world cameras and generation remain active.
- `TerminalMainMenuView` now supplies an animated ASCII title/backdrop and
  restyles the existing main-menu buttons, restoring them on mode exit.
  This uses uGUI callbacks and focus, not a terminal-native transaction adapter.

## Remaining implementation

- [ ] Bind the pure menu model to town services, dungeon commands and shared
  dialogs in the coverage table below. Revalidate actors/items/targets on confirm;
  cancel and stale selections must spend no resources or turns.
- [ ] Give terminal modals exclusive input ownership and restore previous focus.
  Keep each existing uGUI route visible until its full replacement passes.
- [ ] Complete targeting presentation beyond the current selected cell and valid
  cells: verify missile endpoint/direction, area preview and target labels for
  character, prop, tile, inventory and missile flows.
- [ ] Audit knowledge parity with the minimap, especially revealed treasure,
  hazards/traps, mimics and multi-cell actors. `TerminalSceneSnapshots.Dungeon`
  currently handles treasure-revealed gold explicitly; confirm dropped items and
  containers obey their corresponding reveal rules before claiming parity.
- [ ] Review snapshot allocation/static-map caching. Bounded polling and output
  caching exist, but the dungeon adapter allocates a visibility grid per capture.

| Flow | Current presentation / remaining work |
| --- | --- |
| Main menu | ASCII backdrop and restyled uGUI buttons implemented; controller-aware footer remains in [task 03](03-input-prompts.md). |
| Slots, hero selection, settings | Existing uGUI retained; terminal equivalents and focus validation remain. |
| Town buildings, buy/sell, trainer, recruit/dismiss, rest/save, equipment | uGUI retained; adapters must call existing TownServices, TrainerOffers and EquipmentTransferService paths. |
| Dungeon party, skills, items, equipment, stairs, pause, results | uGUI retained; adapters must reuse action factories and the existing turn-command pipeline. |
| Targeting | Existing input/dialog logic retained; ASCII cell markers implemented, full preview and confirm/cancel checks remain. |
| Overworld party/capabilities, warps, gates, confirmations, history | uGUI retained; live map facts exist, menu adapters remain. |
| Level-up and game-over/victory | uGUI retained; terminal equivalents remain. |

## Remaining acceptance

- [ ] Run the current `TerminalIntegrationTests` and `TerminalPlayModeTests`,
  including main-menu restoration and duplicate-UI hide/restore. The earlier
  handoff reported four EditMode tests and the three map/floor-replacement checks
  passing; its later duplicate-UI rerun timed out. That is not a passing result
  for the current six-test PlayMode fixture.
- [ ] Verify clean startup, direct scene launch, repeated travel, floor replacement,
  teardown and domain reload disabled: one owner/screen and no stale state.
- [ ] Exercise targeting, toggles during movement/replay/loading/open menus,
  input consumption, cursor/focus, and mouse interaction with retained uGUI.
- [ ] Inspect 1280x720, 1920x1080, a small window and live resizing for aligned
  glyphs, literal text, unclipped HUD/log and transition/modal ordering.
- [ ] Complete a shared-save playthrough: terminal New Journey -> town -> overworld
  -> dungeon; fight, target a skill/item, descend, transact and explicitly save.
  Continue in 3D and compare party, inventory, gold and progression; repeat in
  reverse using disposable test slots.
- [ ] Build and smoke-test Windows and WebGL after integration. Earlier diorama
  builds predate terminal mode and do not verify its font/UI resources.
- [ ] If world-render suppression is added later, preserve camera/audio/projection
  dependencies and verify complete restoration across scene changes.

Run offline builds/tests and rebuild/import the Core DLL as described in
[Core](../Core/README.md). Unity/player checks are separate. The original offline
handoff reported 385 Core tests passing; no new test run or DLL hash comparison
was performed during this documentation audit.
