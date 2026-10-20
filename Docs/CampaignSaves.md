# Campaign saves and authoring

Campaign sessions use three independent `CampaignSlot_v3_0` through `CampaignSlot_v3_2` PlayerPrefs entries. Every new save records `FormatVersion = 3`; missing or different versions are rejected. The Mana/Skill Points refactor intentionally invalidates earlier saves. Old slot entries are not loaded or migrated; players must start a new campaign. `SaveSystem.ActiveSlot` selects the session; scoped stores restore both storage and slot selection.

`Town.WriteSaveData`, `Town.SaveProgress`, `CampaignParty.Capture` and `SaveSystem.Capture` update memory. Travel, purchases, training, recruitment, equipment, rest, retreat, and dungeon rewards do not write PlayerPrefs. `CampaignSaving.Commit` clones the captured state, adds summary metadata and an awake arrival position, and writes the entire snapshot once. Writes occur at initial creation, home Sleep and Save, inn Save game, and the Hall of Fame's explicit Save completed campaign button. Finale homecoming captures Completion in memory and displays its finishing party and campaign statistics; opening or closing this summary does not save. Later home/inn saves retain the completion record. The storage contract requires a failed write to leave previous bytes intact.

The main-menu slot browser reads only serialized summaries and referenced art. It does not construct a campaign or terrain. Completed slots can Continue ordinary exploration; the finale itself cannot be replayed. Corrupt saves remain in storage and display an error. Replacement is confirmed before hero selection; canceling hero selection does not touch the stored campaign.

Quit discards live progress without applying defeat penalties. Defeat offers Load last save or Main menu. Recovery loads the selected slot, reconstructs its complete CampaignContext, then creates destination actors. There are no nested checkpoints or pre-run inventory snapshots.

Hero records contain campaign level and exact cumulative experience, including benched members. Dungeon entry reconstructs starting/class stats and earned growth before equipment/passives; dungeon returns update progression in memory. Temporary combat statuses are not serialized.

In town-0, the first residential slot after the authored buildings is the home. Its generated walkable interior contains a bed instead of a vendor; the ordinary inn remains separate. New campaigns begin at the bedside and save after the camera is positioned. `HomeBed` exposes its tile, sleeping anchor and animation controller. The sleep sequence reserves the awake tile, locks gameplay, hides weapons, fades out, heals and commits, then wakes and restores equipment/facing/input. No-animation mode commits immediately. Failed saves still complete waking and report the error.

New journeys begin on preparation day. The first Bread comes from a one-time 0G bakery transaction, and the protagonist must learn an active skill before sleeping. `RequiresOpeningPreparation`, `BoughtOpeningBread` and `LearnedOpeningSkill` persist with the campaign. Before the errands are complete the bed offers **Save and quit**, which writes partial progress without advancing the day. Once ready, **Sleep and save until morning** includes the permanent `Opening day` key in the same snapshot as the prepared party. The live key is granted only after storage succeeds; failed writes leave the previous save and closed entrance intact. Initial saves and inn saves never grant this key. Defeat or Continue restores the saved day, skills, inventory and completed errands. Save format 3 requires a fresh campaign after the resource-name refactor.

New snapshots also record `TownLayoutVersion = 1`, which selects the compact home and nearby bakery/trainer arrangement in town-0. Existing version-3 saves with a missing layout version retain version 0 and their saved town geometry and arrival coordinates.

Fresh journeys opt into `HomeLessonVersion = 1` before their initial save. The first-home sequence records `ReadHomeBook` when the optional book is read, grants the permanent `HomeKey` capability from the chest, then records `HomeDoorUnlocked` when the key opens the front door. Players may discover the chest before reading the book, and the key is retained after use. The current objective points to the book, then the chest, and advances directly to the door whenever the key is held. The unfinished lesson prevents dungeon entry, leaving town and advancing preparation day. These transitions change memory only and persist at the ordinary explicit save boundaries. Existing saves with a missing or zero home-lesson version bypass the sequence. `HomeKey` has stable capability value 29 and is excluded from both procedural campaign capability pools, preserving existing seeded campaigns and their fingerprints.

New journeys opt into `OpeningLessonVersion = 1`. Their first learned active skill is recorded as `OpeningSkillId`; `PractisedOpeningSkill` records an actual successful release, and `UsedOpeningRecovery` records useful item use. Movement and interaction prompts retire after their corresponding real actions. The safe first-floor rehearsal is optional: stairs always remain available, missing resources never block travel, and recovery prompts appear only when supplies can help. Training consumes normal Mana and ammunition and does not alter party HP or Hunger. Healing practice supplies a friendly wounded target. Existing saves do not gain a mandatory tutorial.

The first story-dungeon victory sets `OpeningClearNeedsSave` in memory and shows a brief town-gate reveal on return. The current objective reminds the player to save at home; only a successful `CampaignSaving.Commit` clears the reminder in live state. Its saved snapshot also has the reminder cleared. A failed save preserves both the reminder and the previous durable save. Gate reveals themselves never write a save.

Active playtime uses unscaled delta time and excludes unfocused applications, settings/pause, and scene loading. Completed adventures continue accumulating active playtime. It is persisted only with an explicit save.

## Background artwork

Open `Assets/Scenes/Editor/BiomeBackgrounds.unity` through **Tools > Eternal Enigma > Campaign Backgrounds > Open Authoring Scene**. It is deliberately absent from build settings.

The authoring component lists Grassland, Desert, Water, Mountain, Forest, Tundra, Marsh and Volcanic sets. Each contains editable mesh objects, material references, a camera and a directional light. Select a preview biome in the Inspector, adjust the objects or camera directly, then use **Bake Selected Biome** or **Bake All Biomes**. Baking never regenerates scenery. It restores activation, camera target/aspect and the active render target even when rendering fails.

Bakes are 1920×1080 PNGs in `Assets/Art/CampaignBackgrounds`. `Assets/Resources/CampaignBackgrounds.asset` maps all eight biomes explicitly and supplies neutral artwork. Updating art has no effect on campaign saves.

## Verification

Run Core checks before Unity checks:

- `dotnet test Core/EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release`
- Unity menu **Tools > Eternal Enigma > Tests > Run Campaign Saves EditMode**
- `node Tools/unity-mcp.mjs harness Campaign`
- `node Tools/unity-mcp.mjs harness OpeningPreparation`
- `node Tools/unity-mcp.mjs harness Town`

`CampaignSlotStoreTests` covers isolation, selection scopes, failed writes, corrupt reads, progression round trips and all background mappings. `CampaignSleepTests` covers the real home, animated single-write saving, awake-state loading, progression rollback and immediate sleeping.
