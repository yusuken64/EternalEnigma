# Biome decorations

Editable artwork lives in `ArtSource/BiomeDecorations/BiomeDecorations.blend`.
`build.py` is the repeatable Blender MCP authoring script; it preserves unrelated
objects and owns only the `EE Biome Decorations` collection. `manifest.json`
records all 48 model parts, their bounds, and triangle counts. Production FBX
files are under `Assets/Art/EternalEnigma/BiomeDecorations`.

The eight sets contain a wall fixture, ornament, natural accent, assembled lamp
post, sign pole and separate arrow panel. Wall and post fixtures share the same
modeled geometry. Wall props stay within 120 triangles; lamp assemblies and
sign poles with three panels stay within 240. No imported prefab has a collider,
navigation obstacle, interaction component, or light.

Measured maxima are 96 triangles per wall prop, 172 per lamp assembly and 144
per sign with three panels (text excluded). The repeat-import audit checked 165
model/resource metadata files and found zero changed GUIDs.

## Rebuilding

1. Execute `build.py` in Blender through Blender MCP, then inspect the viewport.
2. Through Unity MCP, run **Tools > Eternal Enigma > Art > Import Biome Decorations**.
   This updates existing mesh/material/prefab assets in place and retains GUIDs.
3. Run **Build Biome Decoration Gallery** to update the prefab, contact sheet and
   Environment Playground binding. Key **6** opens the decoration gallery.
4. Run **Bind Biome Decoration Scenes** to bind the catalog in the production
   overworld, town and dungeon scenes and the playground, and bake the town preview.
5. **Audit Biome Decoration Towns** generates eight town captures and a CSV with
   geometry counts and placement timings.

The generated catalog is `Assets/Resources/BiomeDecorations/Catalog.asset`.
It contains surface eligibility, per-context scales, fixture effect offsets,
and explicit facade sockets. Facade exclusions were measured from the existing
`House`, `SmartHouseEdge` and `SmartHouseOuter` Blender meshes. Unknown facades
are skipped instead of assuming their openings are safe.

## Placement and ownership

TWC smart layers register the geometry of rendered edge pieces. Flat floors and
biome seams do not create wall surfaces. Planar faces must be covered by their
triangles; inclined rock triangles use an inscribed mounting area. Legacy
Grassland uses short mesh intersection queries against its actual rendered
stone, including a nine-sample support check. These queries run at generation,
not during gameplay frames, and create no physics objects.

Wall selection uses a separate hash namespace, three-cell spacing across region
borders, a 24-per-region cap, and fixture/ornament/accent weights of 2:1:2.
Natural walls receive accents. Authored house sockets allow one fixture and one
ornament per house. Lamp posts use supported road verges, four-cell separation,
two-cell marker clearance and a 24-per-town cap. Nearby wall fixtures are
suppressed while ornaments remain eligible.

Static bodies are combined with `EnvironmentBatch`; effects and sign text stay
separate. The generation output owns a single `Biome decorations` root, including
its combined meshes. Rebuilding explicitly releases old owned meshes. Overworld
cache ownership follows the existing generated world. A shared effect budget
animates at most the nearest 32 camera-visible fixtures; glow uses small meshes,
not point lights.

Dungeon effects store the exact adjacent floor cell in actor coordinates. This
avoids the half-cell difference between rendered TWC tile centers and fog queries.
An effect with no supported adjacent floor is omitted. Preview visibility must be
requested explicitly; gameplay uses current visibility, not explored history.
The glow scale survives animation and cached-root activation without accumulating
shrinkage. Sign labels share one camera-frustum calculation per frame.
Production dungeons, including the starting Grassland biome and old saves, now
use the shared biome presentation. Layout selection/version remains independent.
The explicit legacy comparison in Environment Playground remains available.
In that comparison, legacy Grassland torch rendering finishes its existing random draws;
its generated output is then removed and replaced. The authored template is not
modified. A complete enabled/disabled build comparison verifies identical random
state and logical masks, including this legacy path.

## Signs and names

`BiomeRoadGraph` connects cardinal road neighbors and consecutive non-warp route
cells. It collapses adjacent junction cells, searches outgoing branches without
reversing through the junction, and ranks up to three distinct town destinations
by distance and stable ID. Approaches precede junctions and waymarkers. Signs
require dry supported verges, six-cell separation and at most eight signs per
32-cell region. They do not reveal towns or affect travel permissions.
Panels tilt upward for the production camera. Billboard labels include a
camera-relative arrow derived from the same outgoing road segment, with bounded
autosizing and at most two lines. Production-label tests check complete text
bounds and matching names from the campaign accessor.

Town names are generated in the Unity-free Core assembly with eight vocabularies
and an independent hash stream. `CampaignSnapshot.TownNames` and `NamingVersion`
preserve existing names and backfill old saves. `GetTownDisplayName` supplies sign
text, town UI, travel titles and town destination labels. Presentation names are
excluded from the logical campaign fingerprint; generator versions are unchanged.

## Streetlight sample audit (historical)

The vendor Village demo was removed during the diorama cleanup. At inspection,
its VillageAsset had an active
StreetLights blueprint that adds Houses and expands it. Subtract and Select are
disabled. `RoadStreetLightsPreset.asset` has only an edge tile, rotated +90 degrees
around Y; the build layer merges tiles. `streetLight_mesh.prefab` contains only
Transform, MeshFilter and MeshRenderer components. The new placement uses the
road boundary with deterministic spacing rather than adopting that sample's density.

## Verification artifacts

- `Previews/BiomeDecorations.png`: imported Unity contact sheet.
- `Previews/BiomeTown_*.png`: generated town views across all eight biomes.
- `ArtSource/BiomeDecorations/Viewport.png`: Blender viewport capture.
- `Verification/BiomeTowns.csv`: counts and editor placement timings.
- `Verification/BiomeEditMode.json`: targeted Unity Edit Mode result.
- `Verification/BiomeVisibilityPlayMode.xml`: effect budget, visibility and
  hide/restore Play Mode result (saved independently of MCP reconnects).
- `Verification/BiomeCore.trx`: all 343 Core tests, including deterministic names,
  collision handling, save backfill and unchanged campaign fingerprints.
- `Verification/BiomeReimport.json`: stable asset GUID audit.
- `Verification/BiomeProductionPlayMode.xml` and `.json`: combined production
  run, 11 passing tests. The dungeon test iterates all 32 combinations; the
  playground test cycles all eight town biomes and rebuilds the mixed overworld.
- `Verification/BiomeTravelPlayMode.xml`: focused name/label, travel, cache and
  animation-budget rerun after the final sign readability adjustment.
- `Previews/BiomeGameplay_*.png`: close views rendered in production scenes.
- `Verification/BiomeRender_*.csv`: paired editor CPU render-submission timings
  at 1600×1200, 5 warmup renders and 30 measured renders per state. The camera
  stays at the same district view for both states; generation is excluded.
  These are not GPU frame times or player-build performance guarantees. Small
  differences are within editor noise; geometry counts cover the entire root.

The production test harness temporarily overrides the saved full-control option
and restores it afterwards. Otherwise a turn waiting for player orders is
incorrectly treated as a stuck floor transition by these automatic tests.
The town assertions use the resolved spawn/spine and compare terrain owners
across rebuilds rather than assuming the older 20-cell town layout.

The town audit exposed derived smart-road/shop blueprint actions retaining old
layout options. `CoreTownLayerGenerator.Configure` now copies the same resolved
options to those source actions while preserving their derived action stacks.
This changes no Core generation algorithm or generator version.

Unity's test runner saved the already-dirty MainMenu scene when entering Play
Mode. Its serialized UI prefab overrides and HP/SP defaults have been preserved;
no biome integration was added to that scene.

The Core suite passed 343 tests and the decoration Edit Mode suite passed 11.
The latter includes all 32
dungeon biome/environment/layout builds, visible output, mask/random-state
preservation, repeated-build cleanup, catalog budgets, socket exclusions,
cross-region spacing, loops/disconnected roads, every arrow's actual road path,
bridge/gate/water/prop rejection, Unity save serialization and real campaign sign
placement. The enabled/disabled legacy build also checks the complete renderer's
random stream. No compiler errors remained during final verification.

Run the combined production checks through **Tools > Eternal Enigma > Tests >
Run Biome Decorations**. **Run Biome Travel** repeats the targeted production
captures and cache checks. The Core command is:

```powershell
dotnet test Core/EternalEnigma.Core/EternalEnigma.Core.Tests/EternalEnigma.Core.Tests.csproj
```

## Starting dungeon and floor alignment follow-up

Production Grassland now follows the same theme renderer and unit map transform
as the other biomes, including runs restored from older saves. The layout generator
selection is unchanged. The common floor sits at Z=0.001, immediately behind feet
and selection sprites at Z=0; the throne carpet keeps only a smaller depth bias.
Imported scenery and custom scenery prefab bases meet that plane. Pickups, gold,
stairs and traps are aligned at spawn by moving their visual children while keeping
their logical roots and interaction cells fixed.

Holding target previously advanced the melee destination twice. Melee now resolves
its destination directly from the actor's cell plus facing, so an adjacent enemy
is hit while aiming and the hero remains stationary. The controller regression
also exercises normal melee and skill targeting, cancellation and confirmation.

Repeat these checks through **Run Dungeon Floor Fix EditMode** and **Run Dungeon
Floor Fix PlayMode** in the same Tests menu. Saved results are
`Verification/DungeonFloorEditMode.json` and `Verification/DungeonFloorPlayMode.json`
with their NUnit XML counterparts. Gameplay captures remain in
`Previews/DungeonThemes/Gameplay_*.png`.
The final run passed all 12 Edit Mode tests and both Play Mode tests, including
all 32 biome/environment/regular-or-throne transitions. The final console had no
errors. Grassland interior, throne, and Mountain outdoor captures were inspected.
