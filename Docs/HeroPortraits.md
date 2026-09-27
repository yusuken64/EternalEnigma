# Hero portraits

Open `Assets/Scenes/HeroPortraitStudio.unity`. Select **Hero Portrait Studio** in the hierarchy. Its inspector has Previous/Next Hero, Save Portrait and Assign, and Capture All Heroes buttons. In Play Mode the Game view also has Previous/Next controls.

The Portrait Camera renders a fixed idle pose to `Assets/Art/HeroPortraits/PortraitCapture.renderTexture` (512 x 640), with a transparent background. Captures retain PNG alpha and import as transparent sprites. Key/fill lights and the camera are editable in the scene. Framing is recalculated when switching heroes, including wide headwear. Adjust the camera after selecting a hero to override a particular capture, then click Save Portrait and Assign.

PNG files and imported sprites live in `Assets/Art/HeroPortraits`. Each of the 24 hero prefabs stores its own `TownAlly.Portrait` reference. Dungeon allies copy that reference during model initialization. Existing FaceCamDisplay panels prefer these saved images and retain the live-camera fallback for characters without portraits. No save-file migration is needed.

**Tools > Eternal Enigma > Portraits > Build Scene and Capture All** recreates the capture scene and exports the roster, or captures from the existing scene if it is already open. The base TownAlly template has no model and is excluded.

**Tools > Eternal Enigma > Tests > Run Hero Portraits** verifies the complete roster's image assignments, scene cycling, and portrait UI binding.
