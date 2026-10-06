# Ability test scene

The lab uses committed skill/effect assets and remains a reusable diagnostic scene.
Its refresh/capture tools are retained; completed production asset installers are removed.

In Unity, choose **Tools > Eternal Enigma > Combat Effects > Play Ability Test Scene**.
Alternatively open `Assets/Scenes/AbilityTestLab.unity` and press Play. This is an Editor playground; it loads the real Common and Dungeon scenes at runtime.

- The tester learns all available skill assets, including passives. Quick Casting is opt-in so the default demonstration preserves authored charge times. Lab-owned copies cost zero mana, and SP is replenished continuously.
- Search the list on the left and select an ability. The right panel offers **Cast ability**, rank 1–5, target distance, and **Preview effects only**.
- **Cast all abilities** runs every active ability alphabetically, then previews each passive's effects. It ignores list filters, resets between demonstrations, and uses adjacent targets. Progress is shown in the panel; **Stop after current ability** safely ends the run. Unavailable or failed abilities are skipped with reasons in the Console. Your reset and target-distance settings are restored afterward.
- **Events / History** records instant casts as `[Cast] Caster casts Ability.` Delayed casts record their start and release separately. Effect-only previews, including passive previews during the routine, appear as `[Preview]` entries and do not count as casts.
- A line of stationary enemies and an adjacent area target support single-target and area attacks. The wounded friendly target supports heals, buffs, and cleansing. Reset recreates the targets and clears statuses, summons, traps, and effects.
- Equipment is prepared automatically for bows or shields. The inventory contains replenishable copies of every item. Inventory abilities select the first eligible item.
- Casting Dance and Casting Chorus prepare a charging friend. Bow skills equip 20 Steel Arrows. Revive prepares a downed friend; Disarm prepares a revealed trap. Retreat resolves without leaving the playground.
- Turn off **Reset arena before each cast** to test combinations, songs, commands, and cleansing. **Apply poison to friendly target** supplies an ailment. **Advance status durations** tests expiry.
- Passives are learned and active; their assigned effects can be previewed independently. This combined loadout is for interaction and visual testing, not balance measurements.

AI and ordinary movement input are paused. Casts use the real action resolution, response, and VFX playback paths one action at a time. Full dungeon rounds do not run automatically. Status ticking is manual so auras remain available for inspection.

Exit Play Mode to finish. The lab uses an isolated save store, temporary preference overrides, and cloned skills; it does not write a player save or change source skill assets. The launch command restores the previous Play Mode start scene when stopped.

**Create Or Refresh Ability Test Scene** rebuilds the scene's ability references after new skill assets are added. **Check Running Ability Test Scene** exercises representative casts and writes its result and a Game view capture to `Temp/AbilityTestLab`.

## Casting and ammunition

**Cast ability** starts a delayed cast. **Advance casting action** spends one charging action, or releases an already ready cast. A cast time of N always requires N charging actions (including start) and one release action. **Cast all abilities** steps these actions automatically. **Enable Quick Casting passive** subtracts one initial charging action and can make a spell instant.

The live dungeon uses the same pending state: SP is paid once at start, arrows are consumed on firing, and generated effects/status ticks do not advance a cast. Mobile Casting permits walking and initiating an ally swap while charging; release is stationary. Displacement, incapacitation, silence, death and floor transitions interrupt; damage alone does not. Casting Dance/Chorus can make a cast ready but cannot release it.

Arrows occupy the offhand beside a main-hand bow. Wooden/Iron/Steel stacks have 20 capacity, damage multipliers 1/1.25/1.5, and straight-shot target caps 1/2/3. Aimed Shot and Ice Bolt pierce two targets, Lightning Bolt three, and Piercing Arrow has no cap. Allies do not consume penetration; scenery and diagonal corners stop shots. Splash missiles detonate at first impact and affect their entire radius.

Missing equipped ammunition produces exactly `no arrows` before action acceptance. Bag stacks never supply shots or automatically replace an exhausted stack. The party card shows casting charge/readiness and equipped ammunition; Events / History records start, release, interruption and fizzle once during resolution.

**Tools > Eternal Enigma > Combat Effects > Capture Casting Showcase** captures charging/release frames for Fireball, Heal, Piercing Arrow and Casting Chorus under `Logs/CastingShowcase`, with sampled audio peaks and reset cleanup in `report.txt`. The capture temporarily unmutes editor audio, sets an audible effects level, and pauses music, restoring all audio settings afterward. **Run Casting**, **Run Casting Regression**, and **Run Casting EditMode** under the Tests menu cover the runtime and asset rules.
