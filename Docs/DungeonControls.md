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

## Shared Inventory and Skills window

Town, overworld, and dungeon use the same window. Q / X / Square opens Inventory;
R / LB / L1 opens Skills. At the root, the active shortcut closes the window and
the other switches tabs. Shortcuts are ignored inside action and target pickers.

Navigate with WASD/arrows, D-pad/left stick, or mouse. Highlighting only inspects;
Enter/Space or A/Cross opens action choices. Confirm Use, Cast, Equip, or Sell to
execute or choose a target. Escape / B / Circle / Back returns one level.
Tab / RB / R1 browses the next hero; Shift+Tab / LT / L2 browses the previous hero.
Browsing never changes the controlled world character. Move right from a list row
to scroll a long description with up/down; move left to return to the list.

Empty lists keep tabs, hero browsing, and Back available. Inventory lists equipped
instances before shared bag contents; Skills separates learned active and passive
skills. Unavailable actions explain their restrictions in the details panel.
Dungeon execution still requires the controlled actor's turn and uses the existing
targeting, costs, replay, and turn systems. Overworld permits equipment changes only.

The shared Common scene owns the single `PlayerInput`. DungeonScene must not add another:
multiple active `PlayerInput` components disable Unity's automatic control-scheme switching,
which can leave gameplay on keyboard even while controller menu navigation works.

Controller regressions: **Tools > Eternal Enigma > Tests > Run Controller Flows**.
These use virtual gamepad events through Unity's Input System and production scenes.
Physical hardware connection and platform-specific button labels still need a device check.

## Gameplay settings

Options > Gameplay stores Full Control and animation mode across sessions.

- **Full Control:** choose each living party hero's action in order. Additional actions prompt again. Summons remain autonomous; forced status actions still resolve automatically. The party leader is restored before enemies act. Toggle changes apply on the next round.
- **Current:** animate visible actions.
- **Controlling hero:** animate the selected hero and incoming actions affecting that hero.
- **Your action only:** animate the player's command and its consequences; other actions resolve without animations. Changes apply at the next action boundary.
- **No animation:** resolve actions and movement instantly without animated effects or combat callouts. Results and event history remain available.

The same animation setting appears in the autoplay panel and applies to dungeon, town, and overworld play. Autoplay supports speeds from 0.5× through 32×.

The dungeon HUD shows portraits, resources, status badges, order state, floor, gold and bag space. Wait uses period on keyboard or right-stick press on gamepad. Click status badges to inspect their names and remaining durations.

The collapsed Events panel shows three messages. History scrolls through the latest 100 historical messages. Current-turn storage has no 100-entry cap. Results remain visible while waiting for the next command; they clear only when the next turn actually commits an action. Combat, healing, skills, items, pickups and status outcomes are recorded during resolution independently of animation playback. Movement is omitted. Hidden enemy actions remain hidden.

Regression entry points: Tools > Eternal Enigma > Tests > Run Dungeon Controls and Run Messages.

The dungeon layout reserves four equal hero rows on the left, a transparent minimap on the right, and scrolling event history at the bottom. Portraits and resource bars remain visible for the full party. Damage/healing/status callouts use consistent screen-space sizing; Level Up uses a larger callout. Skipped animations still record their outcomes without playing callouts.

Turn feedback uses a gold portrait border while awaiting input and a green border while that hero acts. During enemy actions, hero portrait highlights turn off. Event history sits against the bottom edge; the separate action/turn strip has been removed.
