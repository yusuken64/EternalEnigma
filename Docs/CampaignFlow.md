# Campaign and sandbox flow

One Overworld scene serves both modes. Common owns CampaignContext; campaign
snapshots include identity, generation versions/fingerprint, tile position,
permanent abilities, keys, opened and resolved gates, claimed rewards, completed
locations, visited towns, active companions, last entered town and pending run.
An explicit CampaignFormatVersion distinguishes legacy saves even when Unity
materializes missing nested objects. Snapshot version 1 rejects incompatible generation versions rather than silently
reinterpreting positions. Saves without Campaign retain the original town flow.

New Game starts inside town-0. The southern exit at (10, 0) is red and locked
until the interior dungeon is cleared, then turns cyan and returns to the town's
overworld marker; the party spawns at (10, 2). A reserved corridor clears houses, trees and
placement masks before mesh construction. Each town has its own stable seed and
shop namespace. CampaignTravelService validates entries, prepares parameters,
saves before transitions and blocks duplicate callbacks until the scene is ready.

Dungeon tiers are 1?5, 5?10, 10?20, 20?30 and 30?40. Layout seeds are a stable
hash of campaign seed, location ID and floor. Victory returns inside the parent town for an interior dungeon, or to the dungeon
marker for an overworld dungeon. Defeat and explicit abandonment apply the existing gold/item rules and
return inside the last entered town. A pending run loaded by Continue restores
its parent town or overworld entrance and pre-run town inventory/party without awarding victory or
applying defeat losses. Completion commits once per pending run; repeatable
rewards can be earned on subsequent runs. Final victory sets Finished.

TownSaveData stores the active party's inventory/equipment representation;
GameSaveData.Roster preserves all character records, including benched members.
The protagonist stays selected, with up to three companions. Paid recruits grant
no traversal abilities. Campaign reward companions use editable capability-to-
prefab entries in TownConfiguration.CampaignCompanions; an empty mapping falls
back to a deterministic entry in the existing ally catalog. Dungeon allies are
rebuilt from the saved active records, including skills and equipment.

Sandbox uses the same context, generation, movement and completion code, with
an isolated in-memory save. Its separate controls simulate victory without scene
travel and allow immediate eligible reward claims. Claims still respect key
conditions. Exiting restores the player's original save; SaveSystem blocks both
writes and clears while sandbox is active.

Verification commands from the repository root:

```powershell
dotnet test Core/EternalEnigma.Core/EternalEnigma.Core.Tests
node Tools/unity-mcp.mjs harness EditMode
node Tools/unity-mcp.mjs harness Campaign
node Tools/unity-mcp.mjs harness Overworld
node Tools/unity-mcp.mjs harness Town
```

Unity must have this project open with its local MCP bridge enabled. Test harnesses
use MemorySaveStore and the production scenes. Results are written to
Temp/HarnessResults. Import the Release core DLL after changing the core using
Tools > Eternal Enigma > Core > Build and Import DLL.

## Town dungeon entrances

Campaign generation version 7 represents interior dungeons as separate graph nodes
with `ParentTownId` and one open route to their town. Towns 0-3 contain story
dungeons, town 4 contains repeatable-4, and additional towns contain their own repeatable dungeons. Generation fills
any biome lacking a tier town, guaranteeing at least one town per biome.
There are six or seven towns depending on the progression stages.
The town entrance lists these graph destinations and uses their campaign tier;
legacy donation-gated floor choices apply only outside campaign mode.

Grid version 10 projects interior nodes onto the parent town tile, with a
one-point route and no separate dungeon marker. `BeginTownDungeon` validates
town ownership and entry context. Completion, interruption recovery and saves
retain the dungeon ID; returning from an interior run restores the town scene.
The opening is town-0 -> story-0 (inside town) -> town-exit -> repeatable-0
(outside town) -> starter-exit -> checkpoint-0. Story victory permanently unlocks
the town scene exit with the Town gate key. Outdoor dungeon victory awards the
separate Town area key; the player must use it at the overworld area gate.
Defeat or interruption awards neither key.
Previous campaign/grid generation versions are rejected by save validation.

Regions are modestly compact: ordinary capability providers share existing
checkpoints and landmarks, while both return-reward sites remain distinct. Vehicles at the same stage share
a pair of Engineering-gated converter sites. Seed 42 has 45 graph locations
(previously 62). Its occupied map spans 182x182 tiles (previously 248x248),
within the same 256x256 canvas,
and distinct overworld destinations use 11-tile minimum spacing (6 for the
starting town/outdoor dungeon pair). Exact walking savings vary by seed and route.
