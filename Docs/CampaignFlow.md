# Campaign and sandbox flow

One Overworld scene serves both modes. Common owns CampaignContext; campaign
snapshots include identity, generation versions/fingerprint, tile position,
permanent abilities, keys, opened and resolved gates, claimed rewards, completed
locations, visited towns, active companions, last entered town and pending run.
An explicit CampaignFormatVersion distinguishes legacy saves even when Unity
materializes missing nested objects. Snapshot version 1 rejects incompatible generation versions rather than silently
reinterpreting positions. Saves without Campaign retain the original town flow.

New Game starts inside town-0. The cyan southern exit at (10, 0) returns to its
marker; the party spawns at (10, 2). A reserved corridor clears houses, trees and
placement masks before mesh construction. Each town has its own stable seed and
shop namespace. CampaignTravelService validates entries, prepares parameters,
saves before transitions and blocks duplicate callbacks until the scene is ready.

Dungeon tiers are 1?5, 5?10, 10?20, 20?30 and 30?40. Layout seeds are a stable
hash of campaign seed, location ID and floor. Victory returns to the dungeon
marker. Defeat and explicit abandonment apply the existing gold/item rules and
return inside the last entered town. A pending run loaded by Continue restores
its entrance and pre-run town inventory/party without awarding victory or
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
