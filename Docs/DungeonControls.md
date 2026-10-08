# Dungeon controls and event log

Controls apply to the current fixed-hero campaign flow and the isolated test modes.
The town inn uses normal dialog confirmation/Back handling.

## Controller controls

| Action | Xbox / PlayStation |
| --- | --- |
| Move in town/combat; navigate menus and targets | D-pad or left stick |
| Confirm; attack/interact in gameplay | A / Cross |
| Back in menus; hero commands in combat; party panel in campaign town | B / Circle |
| Inventory | X / Square |
| Skills | LB / L1 |
| Hold position while choosing facing | Y / Triangle |
| Switch controlled hero in gameplay; next inspected hero in Party Menu | RB / R1 |
| Wait a combat turn | Right-stick press |
| Minimap | View / Share |
| Settings | Menu / Options |

## Shared party window

Town, overworld and dungeon expose Inventory, Equipment, Skills and Stats; the
overworld also has Capabilities. Q / X / Square opens Inventory, and R / LB / L1
opens Skills. At the root, the active shortcut closes the window and the other
switches tabs. Equipment and Stats have clickable launcher tabs but no dedicated
keyboard/gamepad shortcut. Shortcuts are ignored inside action and target pickers.

Navigate with WASD/arrows, D-pad/left stick or mouse. Confirming a dungeon active
skill starts casting or target selection directly; items open their action list.
Escape / B / Circle / Back returns one level. The compact root dock hides the
separate description pane; nested pickers and service dialogs retain their own
details and scrolling controls.

Tab / RB / R1 selects the next hero; Shift+Tab / LT / L2 selects the previous hero.
In town/overworld this changes inspection only. In the dungeon dock it changes the
controlled hero while browsing; nested actions and targeting retain their original
actor. Dungeon HUD portraits also support clicking to switch heroes.

Inventory lists shared bag items; Equipment shows Weapon, Off-hand and Accessory
slots and per-item bonuses, with stat previews in the change picker. Skills separates
active/passive entries. Stats
shows progression, attributes, vitals, combat values and resistances, with free
attribute spending. Empty lists keep navigation and Back available; unavailable
actions explain their restrictions. Dungeon actions retain actor-turn eligibility,
targeting, costs and replay. Overworld allows equipment changes and attribute
spending; items and skills are otherwise inspectable only.

The shared Common scene owns the single `PlayerInput`. DungeonScene must not add another:
multiple active `PlayerInput` components disable Unity's automatic control-scheme switching,
which can leave gameplay on keyboard even while controller menu navigation works.

Controller regressions: **Tools > Eternal Enigma > Tests > Run Controller Flows**.
These use virtual gamepad events through Unity's Input System and production scenes.
Physical hardware connection and platform-specific button labels still need a device check.

## Control prompts

Visible hints use `InputPrompts` in `Assets/Scripts/Common` for device-specific names. Store
tokens such as `{Interact}` in messages and call `InputPrompts.Format` when drawing them;
avoid literal combined hints such as “Enter / A”. `ControlDeviceState` detects deliberate
gamepad, keyboard, or mouse activity once per frame. The persistent `CursorManager` in the
Common scene polls it, and `MenuUIInputModule` also polls while a menu is active. Keyboard
or mouse activity restores keyboard prompts and the visible cursor; gamepad activity hides
the cursor. An unchanged stick position does not switch devices.

## Terminal presentation

Main-menu and Display settings controls, or F11, toggle ASCII map presentation.
Town, overworld and dungeon use live gameplay state and the same campaign saves.
The main menu has an ASCII backdrop and restyled buttons; gameplay dialogs retain
uGUI while terminal-native adapters are unfinished. The redundant dungeon minimap
and event feed hide while their knowledge/history remain active. Animation settings
are preserved. See [remaining terminal work](../TODOs/11-terminal-mode.md).

## Gameplay settings

Options > Gameplay stores Full Control and animation mode across sessions.

- **Full Control:** choose each living party hero's action in order. Additional actions prompt again. Summons remain autonomous; forced status actions still resolve automatically. The party leader is restored before enemies act. Toggle changes apply on the next round.
- **Normal:** animate visible actions.
- **Animate allied actions:** animate chains started by allies, including summons, and enemy chains that affect an ally.
- **Animate only controlled hero actions:** animate chains started by the hero controlled when the action begins.
- **No animations:** resolve actions and movement instantly without animated effects or combat callouts. Results and event history remain available.

Animation changes apply to the next action chain. Each chain uses one playback decision, while hidden actions remain hidden.

The same animation setting appears in the autoplay panel and applies to dungeon, town, and overworld play. Autoplay supports speeds from 0.5× through 32×.

The dungeon HUD shows portraits, resources, status badges, order state, floor, gold and bag space. Wait uses period on keyboard or right-stick press on gamepad. Click status badges to inspect their names and remaining durations.

The collapsed Events panel shows three messages. History scrolls through the latest 100 historical messages. Current-turn storage has no 100-entry cap. Results remain visible while waiting for the next command; they clear only when the next turn actually commits an action. Combat, healing, skills, items, pickups and status outcomes are recorded during resolution independently of animation playback. Movement is omitted. Hidden enemy actions remain hidden.

Regression entry points: Tools > Eternal Enigma > Tests > Run Dungeon Controls and Run Messages.

The dungeon layout reserves four equal hero rows on the left, a transparent minimap on the right, and scrolling event history at the bottom. Portraits and resource bars remain visible for the full party. Damage/healing/status callouts use consistent screen-space sizing; Level Up uses a larger callout. Skipped animations still record their outcomes without playing callouts.

Turn feedback uses a gold portrait border while awaiting input and a green border while that hero acts. During enemy actions, hero portrait highlights turn off. Event history sits against the bottom edge; the separate action/turn strip has been removed.
