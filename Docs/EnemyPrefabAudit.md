# Enemy prefab checks

Run `node Tools/unity-mcp.mjs harness Enemies`, or use **Tools > Eternal Enigma > Tests > Run Enemies**.
The PlayMode check discovers every prefab in `Assets/Prefabs/Dungeon/Enemies` and loads them in a production dungeon.
It checks team, positive attack stats and turn budgets, player targeting, eventual melee selection despite special-policy rolls,
damage target/amount, attack budget consumption, and movement/attack/hit/death animation playback.
It includes the large slime's wider footprint. The current audit passes all 33 prefabs.

Corrections:

- Demon King, Dragon, Flying Demon (`Enemy_FylingDemon`), and Lizard Warrior were on the player team and could target themselves. They now use the enemy team.
- Monster Plant, Werewolf, and Salamander had zero strength. Their basic-attack strengths are now 8, 12, and 14 respectively, in both the prefabs and source monster data. These are tuning values, not a balance certification.
- Animation lookup now checks playable controller states instead of the currently playing clip, handles monster-prefixed names, and selects forward flight/walking correctly. Worm Monster has no locomotion clip, so movement uses its idle animation.

This check covers baseline combat and animation wiring. It does not certify every special ability or combat balance.
