# Ability test scene

In Unity, choose **Tools > Eternal Enigma > Combat Effects > Play Ability Test Scene**.
Alternatively open `Assets/Scenes/AbilityTestLab.unity` and press Play. This is an Editor playground; it loads the real Common and Dungeon scenes at runtime.

- The tester learns all 202 skill assets, including passives. Lab-owned copies cost zero mana, and SP is replenished continuously.
- Search the list on the left and select an ability. The right panel offers **Cast ability**, rank 1–5, target distance, and **Preview effects only**.
- **Cast all abilities** runs every active ability alphabetically, then previews each passive's effects. It ignores list filters, resets between demonstrations, and uses adjacent targets. Progress is shown in the panel; **Stop after current ability** safely ends the run. Unavailable or failed abilities are skipped with reasons in the Console. Your reset and target-distance settings are restored afterward.
- **Events / History** records each actual cast as `[Cast] Caster casts Ability.` Effect-only previews, including passive previews during the routine, appear as `[Preview]` entries and do not count as casts.
- Two stationary enemies support single-target and area attacks. The wounded friendly target supports heals, buffs, and cleansing. Reset recreates the targets and clears statuses, summons, traps, and effects.
- Equipment is prepared automatically for bows or shields. The inventory contains replenishable copies of every item. Inventory abilities select the first eligible item.
- Revive prepares a downed friend; Disarm prepares a revealed trap. Retreat resolves without leaving the playground.
- Turn off **Reset arena before each cast** to test combinations, songs, commands, and cleansing. **Apply poison to friendly target** supplies an ailment. **Advance status durations** tests expiry.
- Passives are learned and active; their assigned effects can be previewed independently. This combined loadout is for interaction and visual testing, not balance measurements.

AI and ordinary movement input are paused. Casts use the real action resolution, response, and VFX playback paths one action at a time. Full dungeon rounds do not run automatically. Status ticking is manual so auras remain available for inspection.

Exit Play Mode to finish. The lab uses an isolated save store, temporary preference overrides, and cloned skills; it does not write a player save or change source skill assets. The launch command restores the previous Play Mode start scene when stopped.

**Create Or Refresh Ability Test Scene** rebuilds the scene's ability references after new skill assets are added. **Check Running Ability Test Scene** exercises representative casts and writes its result and a Game view capture to `Temp/AbilityTestLab`.
