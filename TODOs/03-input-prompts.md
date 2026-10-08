# Remaining control-prompt work

Audited 2026-10-08. Shared device detection and the original prompt conversion
are implemented in `ControlDeviceState`, `InputPrompts`, `CursorManager` and
`MenuUIInputModule`. The proposed `InputDevice` class was implemented under
the name `ControlDeviceState`; do not add a second detector.

`InputPromptsTests` (2 tests) and `ControlDeviceStateTests` (1 test) passed in
the retained 2026-10-07 [EditMode](../Docs/Art/Previews/Diorama/Verification/PostCleanupEditMode.xml)
and [PlayMode](../Docs/Art/Previews/Diorama/Verification/PostCleanupPlayMode.xml) reports.
Those results precede the newer terminal main-menu work.

## Remaining

- [ ] Convert the keyboard-only hint in `TerminalMainMenuView.Initialize`
  (`UP/DOWN...ENTER...F11`) and the hardcoded navigation footer in
  `PartyMenuPicker.Description` to shared device-aware wording. Refresh open
  views when the device changes, and check newer UI for similar literals.
- [ ] Decide keyboard/gamepad shortcuts for Equipment and Stats. Their launcher
  tabs work, but `PartyMenuLauncher.Refresh` only advertises Inventory and Skills
  shortcuts; the original E/T shortcut proposal was not implemented.
- [ ] Check Xbox/PlayStation hardware labels and mid-screen switching in the
  remaining views. Virtual-device tests do not establish physical-device acceptance.

Optional: binding-derived text when rebinding is supported, and a licensed TMP
sprite glyph set with controller-family detection. Neither is required to finish
the existing text-prompt feature.

New hints should follow [control prompts](../Docs/DungeonControls.md#control-prompts).
