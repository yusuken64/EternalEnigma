# Action audio

The sound palette retains the installed RPG Essentials combat and menu clips. Nine original short mono cues add a rising gate glissando, level-up dings, related purchase/sale chimes, a cartoony door latch/creak, a universal casting chirp, and quiet wait/rest/status indicators. These are stylistic retro RPG references, not recordings from commercial games. Rebuild them deterministically with `python Tools/generate-action-audio.py`.

## Routing and loudness

`AudioManager.PlayCue` owns semantic routing, per-cue gain, rapid-input throttling, and same-frame UI deduplication. UI cues use UI; all world and character outcomes use Effects (the mixer's existing Sound group); music uses Music. Each group feeds Master. New cues peak below full scale to leave mixing headroom. Navigation/wait gain is 0.12/0.10; confirmations are 0.36; ordinary effects are 0.60; gate unlocks and level gains are 0.72. Release clips retain their distinct existing timbres. Big actions get their character from the existing exaggerated elemental effects, rather than extra loudness on routine indicators.

All one-shot source pools are 2D, have autoplay disabled, and receive their correct mixer group on every playback. Owned visual-effect instances are staged inactive and their embedded audio sources disabled before activation; only the audio manager plays their cues. Volume preferences remain unchanged.

## Coverage

| Event | Cue / owner | Channel |
|---|---|---|
| Input changes menu selection, party tab, hero, target, settings, digit | Navigate; input owner or settings control, never selection arrows | UI |
| Accept / activate | Confirm; button or world-target confirmation | UI |
| Back / cancel / Resume | Cancel; explicit button role or back-input path | UI |
| Open / close menu | MenuOpen/MenuClose; suppressed if activation already sounded | UI |
| Terminal mode button / F11 / controller View; terminal history | Confirm for mode changes; Confirm or MenuOpen for history, once per input | UI |
| Rejected player command, invalid target, failed transaction | Denied, throttled | UI |
| Every active ability starts | AbilityStart after validation, once | Effects |
| Ability releases | Explicit elemental/type clip; see [ability assignments](AudioAbilityCoverage.md) | Effects |
| Charged enemy spell, Confusion, fire breath, spell item | Universal start plus assigned release | Effects |
| Basic swing / ranged attack / thrown item | Attack / profile release / Throw | Effects |
| Health loss, including traps and status ticks | Damage on resolved positive damage | Effects |
| Miss / zero-damage block | Miss / Block | Effects |
| Healing / SP restoration / status change | Heal / quiet Status | Effects |
| Death / revival | Death / Revive | Effects |
| Item use / equip / unequip / drop / pickup | Respective committed action cue | Effects |
| Gold / keys / encounter supplies / recovered possessions | Pickup only on receipt | Effects |
| Gathering / disarming | Gather on success; Denied on failure | Effects / UI |
| Dungeon door / container | DoorOpen / ContainerOpen once at opening; key, lockpick and bash paths share owner | Effects |
| Town room entry | DoorOpen only for committed controlled-character threshold crossing | Effects |
| Enter town from overworld | DoorOpen once when travel commits | Effects |
| Trap activation / evasion / warp | Trap once per triggered trap; Miss / Teleport for corresponding outcome | Effects |
| Warp / summon / mimic reveal / explosion | Assigned effect profile, guarded against duplicate replay | Effects |
| Stairs / retreat / finale portal | Retreat / Teleport, only successful transition | Effects |
| Wait / rest / field kitchen | Quiet Wait for commanded waits; Rest for completed rest | Effects |
| Buy / sell / paid training or recruitment / donation | Distinct Purchase and Sell cues after success | Effects |
| Overworld gate / shortcut unlock | GateUnlock ascending glissando only for a new unlock | Effects |
| Level gained | LevelUp ding sequence; simultaneous gains coalesce | Effects |

Walks and turns are silent in dungeon, town, and overworld. Followers crossing a room doorway, save loading, roof refreshes, opening an already-open gate, and reading existing levels do not sound. Automatic waits, forced incapacitation turns, passive stat bookkeeping, XP without a level gain, and helper actions already represented by a parent ability remain silent. Failed AI candidate evaluations are not player rejection cues.

Gameplay actions queue successful outcomes during simulation and play them during replay. Damage and healing sound at projectile contact; piercing shots sound each contact in flight order. Animation preferences do not suppress sound. Visibility still suppresses hidden enemy action audio; queued cues and cast/area sequences have independent once guards. Charged starts never release early, and invalid/interrupted releases are silent.

## Authoring and verification

Run **Tools → Eternal Enigma → Audio → Author All** to assign the Common scene catalog, all active skill assets (including embedded/legacy skills), profile releases, and explicit cancel-button roles. Missing cue assignments or unknown active ability families fail authoring. Combat profile rebuilds reapply the audio assignments. The runtime puzzle Reset Floor ability assigns its utility sound when created.

Legacy authored Unity buttons use the audio-capable button subclass while retaining their existing component IDs, listeners and first-click behavior. **Tools → Audio → Migrate native menu buttons** repeats this targeted migration if older UI assets are reimported; reload any open scenes after it finishes. Runtime dialog owners also assign the semantic role of their Back/Cancel controls.

Run **Audio → Test Edit Mode** and **Audio → Test Play Mode** through Unity MCP. **Audio → Test Menu Feedback** reruns just the menu/input coverage. These use the durable project harness and write `Temp/HarnessResults/{EditMode,PlayMode}.json` and XML. Coverage includes skill assignments, cast stages/interruptions, visual-prefab source suppression, menu input/focus/cancel, independent mixer preferences, actual damage/traps/level gains, silent walking, doors, trading, and one-time gate unlocks.

Verified on 2026-10-16 UTC: 5 asset tests passed; the gameplay run passed 59 tests and the final menu recheck passed 31, covering 61 distinct runtime tests overall. All runs had zero failures or skips. The final compilation succeeded, the Unity error console was empty, and `git diff --check` passed. Retained reports and their aggregate are in `Temp/SoundAudit/{AudioEditMode,AudioPlayMode,MenuPlayMode,verification}.json` with matching XML for each run. The nine generated WAVs passed mono/44.1 kHz/16-bit, clipping, duration and DC-offset checks; their measured peaks range from -15.3 to -4.2 dBFS. This is automated asset, routing and behavior validation, not a subjective listening review.
