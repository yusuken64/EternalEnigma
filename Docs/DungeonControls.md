# Dungeon controls and event log

Options > Gameplay stores Full Control and animation mode across sessions.

- **Full Control:** choose each living party hero's action in order. Additional actions prompt again. Summons remain autonomous; forced status actions still resolve automatically. The party leader is restored before enemies act. Toggle changes apply on the next round.
- **Current:** animate visible actions.
- **Controlling hero:** animate the selected hero and incoming actions affecting that hero.
- **Your action only:** animate the player's command and its consequences; other actions resolve without animations. Changes apply at the next action boundary.
- **No animation:** resolve actions and movement instantly without animated effects or combat callouts. Results and event history remain available.

The same animation setting appears in the autoplay panel and applies to dungeon, town, and overworld play. Autoplay supports speeds from 0.5× through 32×.

The dungeon HUD shows portraits, resources, status badges, order state, floor, gold and bag space. Wait uses period on keyboard or right-stick press on gamepad. Click status badges to inspect their names and remaining durations.

The Events / History panel scrolls through the entire current turn or the latest 100 historical messages. Current-turn events have no 100-entry cap. Results remain visible while waiting for the next command; they clear only when the next turn actually commits an action. Combat, healing, skills, items, pickups and status outcomes are recorded during resolution independently of animation playback. Movement is omitted. Hidden enemy actions remain hidden.

Regression entry points: Tools > Eternal Enigma > Tests > Run Dungeon Controls and Run Messages.

The dungeon layout reserves four equal hero rows on the left, a transparent minimap on the right, and scrolling event history at the bottom. Portraits and resource bars remain visible for the full party. Damage/healing/status callouts use consistent screen-space sizing; Level Up uses a larger callout. Skipped animations still record their outcomes without playing callouts.

Turn feedback uses a gold portrait border while awaiting input and a green border while that hero acts. During enemy actions, hero portrait highlights turn off. Event history sits against the bottom edge; the separate action/turn strip has been removed.
