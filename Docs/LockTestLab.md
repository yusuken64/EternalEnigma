# Lock and key test scene

Open `Assets/Scenes/LockTestLab.unity` and press Play, or choose
**Tools > Eternal Enigma > Locks > Play Lock Test Scene**. The lab loads the production
overworld and starts beside a locked masonry gate. Stop Play to leave the lab.

The gallery currently has 408 stations: every physical lock has adjacent north-south
and east-west versions, plus eight waystones. Every entry in `LockSkinCatalog.Skins`
is included. All 24 named town-gate,
road-gate and waystone keys are acquired, and all 30 capabilities are permanently
available. Additional stations exercise a two-capability solution, alternative
solutions, and a shortcut opened from its far side. Region-specific variants use
their matching biome materials.

Use **Previous**, **Next**, the station list, or **Page Up / Page Down** to move
between examples. The **Capabilities**, **Keys** and **Special** filters narrow the
list. **Visit far side** places the party across the selected obstacle. The panel
shows the expected solution for review; the ordinary game menus retain their own copy.
The direction filter shows either orientation, and **Visit the other orientation**
jumps directly to the matching rotated station. Both versions use the same key.

Press **Interact** or the normal interaction button. The shared Capabilities tab
opens, and **Use** starts normal world targeting. Confirm on the gate to apply the
capability or key. Escape returns through the menu stack. Narrative descriptions and
failed-action feedback remain in History. The lab panel hides while game menus are open.

**Reset this lock** or **Reset all locks** restores the locked appearance and rules
without removing any acquired capability or key. The lab uses a disposable sandbox
and isolated save store; it never overwrites a player campaign.

**Tools > Eternal Enigma > Tests > Run Lock Test Lab** loads the actual scene and
opens/resets every station through the production capabilities and targeting code.
It also checks cancelled partial solutions, stale targets, and terminal pointer
confirmation. Results are written to `Temp/HarnessResults/PlayMode.json` and `.xml`;
captures and a station-count report go to `Temp/LockTestLab`.

Recreate the launcher scene with **Locks > Create Or Refresh Lock Test Scene**.
The runtime gallery reads the lock catalog, so new skins automatically appear.

The gallery suppresses settlement landmarks so each station shows only its intended
lock. Named keys use seven gate silhouettes, including masonry arches, iron wickets,
timber doors, palisades, toll booms, sealed stone doors and chains. Other locks reuse
game props and 49 Blender-authored designs for terrain, water crossings, mechanisms,
shrines, camps and waystones. Gate frames remain when their barriers open; waystone
runes light up when unlocked.
East-west gates use low, open-top frames so the camera can show their closed barriers.

Campaign menus hide completed keys and capabilities with no remaining uses. **Show all**
restores the full acquired list; ownership and save data are preserved. The check includes
undiscovered locks, ongoing traversal, unclaimed rewards and encounter requirements.
The lab defaults to **Show all**. Use **Hide completed** to audit filtering, then reset
the locks to restore their uses.

**Locks > Capture Model Contact Sheets** renders every station at a fixed scale to
`Temp/LockTestLab/VisualAudit`, with a TSV mapping back to its skin. **Tests > Run Lock
Models** checks all model references, geometry, ground contact, textures and closure
states. The Play Mode lab test also captures representative models in the overworld.

Blender sources and the repeatable import process are in [ArtSource/Locks](../ArtSource/Locks/README.md).
