# Animated main menu

`Assets/Scenes/MainMenu.unity` contains an editable `Menu Stage`: the MC03 hero
with staff and shield, a red slime, three purple bats, dungeon tiles, columns,
burning wall torches, a chest, stacked crates, an urn, a skull, and fallen masonry.
Two companions join the foreground party, with a giant demon king, a flying
dragon, and skeleton sentinels behind them. A modular, five-tower castle and
faceted distant mountains fill the background. A menu-specific procedural
twilight skybox adds drifting clouds, stars, and a moon; distance fog separates
the castle from the foreground.
The title, menu buttons, navigation, and click handlers remain on the original
**Screen Space - Overlay** canvas. The old screenshot is no longer referenced by
the scene; its source image remains available as the composition reference.
New Journey opens a paged roster of existing heroes. Choosing one previews their
model and fixed class or classes; Begin Journey starts the campaign with that hero
as protagonist. The default hero remains available to debug launches.

The stage uses visual models only. Menu material variants live in
`Assets/Art/MainMenu`; editing them does not recolor gameplay assets.
The eight dungeon surface textures are retained in `Assets/Art/MainMenu/Textures`
from the original TileWorldCreator Dungeon sample (`Version 2 Tiles/Dungeon/_textures`).
These keep the floor, walls, columns, chest, and torches matched to their authored UVs
after the sample pack is removed. The diorama art audit preserves menu texture copies
instead of replacing them with a shared masonry texture.
The stage uses Town's neutral key-light color and intensity with the shared
`ScenePresentation` ambient fill and fixed indoor sun angle, rotated onto its XZ floor.
Backdrop monsters use the same Standard lit material settings as gameplay enemies.
**Tools > Eternal Enigma > Main Menu > Apply Gameplay Lighting** reapplies this setup;
the twilight sky and local torch lights provide the menu atmosphere.
`MenuSceneMotion` plays existing animation clips without root motion and adds
bounded, unscaled hovering and wobble. Position, scale, animation phase, speed,
and amplitude are editable in the scene. The camera sits low and looks upward
through a perspective lens to match the reference's looming slime and receding
walls. `MenuCameraFraming` widens the vertical field of view on narrower displays,
including 16:10, preserving the horizontal composition without stretching models.

The stage is edited directly in `Assets/Scenes/MainMenu.unity`; its completed builder is
removed. **Tools > Eternal Enigma > Main Menu > Capture Scene** remains and exports aspect-ratio
scenery previews to `Temp/MainMenuValidation`. Use Game view to inspect the overlay UI together
with the stage.

Autoplay forces the effective `DungeonPreferences.FullControl` value off while
`AutoplayRunner.BlocksPlayerInput` is true. Saved preferences and the nullable
override are untouched. Pause, the return prompt, and finished-run inspection
retain that ownership. Takeover and destruction restore normal resolution;
turn settings still change at the existing next-round boundary. Gameplay options
show a disabled `Full Control: Off (Autoplay)` control during ownership.

Regression coverage: `AutoplayControlPreferenceTests`, `MainMenuPresentationTests`,
and the opt-in `Tools > Eternal Enigma > Tests > Run Autoplay` session tests.
