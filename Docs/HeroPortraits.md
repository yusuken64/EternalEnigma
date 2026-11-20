# Hero portraits

Open `Assets/Scenes/HeroPortraitStudio.unity`. Select **Hero Portrait Studio** in the hierarchy. Its inspector has Previous/Next Hero, Save Portrait and Assign, and Capture All Heroes buttons. In Play Mode the Game view also has Previous/Next controls.

The Portrait Camera renders a fixed idle pose to `Assets/Art/HeroPortraits/PortraitCapture.renderTexture` (512 x 640), with a transparent background. Captures retain PNG alpha and import as transparent sprites. Key/fill lights and the camera are editable in the scene. Framing is recalculated when switching heroes, including wide headwear. Adjust the camera after selecting a hero to override a particular capture, then click Save Portrait and Assign.

PNG files and imported sprites live in `Assets/Art/HeroPortraits`. Each of the 24 hero prefabs stores its own `TownAlly.Portrait` reference. Dungeon allies copy that reference during model initialization. Existing FaceCamDisplay panels prefer these saved images and retain the live-camera fallback for characters without portraits. Portrait references belong to prefabs, not save data.

**Tools > Eternal Enigma > Portraits > Build Scene and Capture All** recreates the capture scene and exports the roster, or captures from the existing scene if it is already open. The base TownAlly template has no model and is excluded.

**Tools > Eternal Enigma > Tests > Run Hero Portraits** verifies the complete roster's image assignments, scene cycling, and portrait UI binding.

The dungeon, town and overworld party HUDs share compact cards. Portraits use a
72 x 72 rounded-square UI mask with a thin gold frame and a proportional crop
centered on the face. The portrait's sorting Canvas sits above the mask, so the
image remains clipped when portrait controls rise above the party-menu shield.
Selection and turn highlights sit outside the mask.

Rows start at 280 x 104 reference pixels, with 8 pixels between rows. The layout
measures the existing text at its authored font sizes and grows vertically for
names, levels, charging, ammunition and status badges. If a full party exceeds
the reserved height, it uses more of the existing party column's width. Names
and action text have small contrast backings; enclosing parchment is disabled.
Downed names retain their level. Semantic fonts continue through `GameTextStyle`
and `GameTypography`, with Marcellus for persistent party information.

**Tools > Eternal Enigma > UI > Apply Compact Party HUD** updates the two card
templates, travel HUD and dungeon scene while retaining their bindings. The
older **Apply Party Portrait Defaults** command uses the same authoring path.
**Tests > Run Compact Party HUD** checks information, portrait sorting, status
inspection, party switching and input guards, and captures one-to-four-card
layouts at 960x600, 1280x720, 1280x800, 1920x1080 and 2560x1080.
Captures and the full portrait contact sheet are in `Temp/CompactPartyHud` and
`Temp/PartyPortraitLayout`. **Run Compact Party Assets** checks typography and
the saved portrait roster.

Overworld sandbox diagnostics are hidden by default. In a running sandbox, use
**Tools > Eternal Enigma > Debug > Toggle Overworld Diagnostics** to opt in.
The debug panels stay out of normal HUD reviews, including terminal-mode checks.
