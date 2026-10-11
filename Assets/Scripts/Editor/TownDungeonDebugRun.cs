using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Manual, isolated district playthrough. All traversal and exits use production actions.</summary>
[InitializeOnLoad]
public static class TownDungeonDebugRun
{
    const string Key = "EternalEnigma.TownDungeonDebug";
    const string Folder = "Logs/TownDungeonDebug";
    static readonly MethodInfo Act = typeof(AutoplayRunner).GetMethod("Act", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly AutoplayRoute route = new();
    static readonly Queue<Vector3Int> goals = new();
    static readonly List<FloorResult> floors = new();
    static readonly Dictionary<Enemy, int> enemyHP = new();
    static string location, directory;
    static bool entered, finished, recordedSetup;
    static int currentFloor = -1;
    static double nextTick, deadline;
    static Vector3Int? previousCell;
    static ShopRoom room;
    static DroppedItem pickup;
    static InventoryItem pickupItem;

    [Serializable] sealed class FloorResult
    {
        public int Floor, Seed, Width, Height, Buildings, Steps, DoorwaySteps, RejectedDiagonals, DamagedEnemies;
        public bool InteriorVisited, LootCollected, GateReached;
        public string Role, Fingerprint;
    }
    [Serializable] sealed class Result
    {
        public string Location, Outcome, Reason, RunDirectory;
        public bool DebugGodmode = true, TeleportedToEntrance = true;
        public List<FloorResult> Floors;
    }

    static TownDungeonDebugRun()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.EraseBool(Key);
            int control = SessionState.GetInt(Key + ".Control", -1);
            DungeonPreferences.FullControlOverride = control < 0 ? null : control == 1;
            SessionState.EraseInt(Key + ".Control");
        };
    }

    [MenuItem("Tools/Eternal Enigma/Town Dungeons/Run Debug District")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Finish the current Play Mode session first.");
        // Also support projects with Enter Play Mode domain reload disabled.
        entered = finished = recordedSetup = false;
        currentFloor = -1; nextTick = 0; previousCell = null;
        location = directory = null;
        route.Clear(); goals.Clear(); floors.Clear(); enemyHP.Clear();
        bool? control = DungeonPreferences.FullControlOverride;
        SessionState.SetInt(Key + ".Control", control.HasValue ? (control.Value ? 1 : 0) : -1);
        SessionState.SetBool(Key, true);
        AutoplayWindow.Launch(new AutoplayOptions { Seed = 42, RunCount = 1, DebugPlaythrough = true,
            Godmode = true, InfiniteResources = true, DebugCoverage = false, NoAnimations = false,
            Speed = 8, TimeLimitMinutes = 10, StallSeconds = 60, ReportRoot = Folder });
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || finished) return;
        var run = AutoplayRunner.Active;
        if (run == null || run.Report == null) return;
        if (!recordedSetup)
        {
            recordedSetup = true;
            DungeonPreferences.FullControlOverride = false;
            run.Report.ValidationOnly = true;
            run.SetPanelVisible(false);
            directory = run.DirectoryPath;
            deadline = EditorApplication.timeSinceStartup + 600;
        }
        if (!run.Running) { Save(run.Report.Outcome, run.Report.Reason); finished = true; return; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("District debug playthrough exceeded ten minutes.");
            var common = Common.Instance;
            if (common?.CampaignContext == null || common.Travel.IsTransitioning) return;
            var context = common.CampaignContext;
            if (!entered)
            {
                var town = Object.FindFirstObjectByType<Town>();
                if (town == null || !town.IsReady || town.TownPlayer.IsBusy) return;
                location = context.Campaign.Locations.Where(l => l.Variant == DungeonVariant.Town).OrderBy(l => l.Tier).ThenBy(l => l.Id).First().Id;
                town.WriteSaveData();
                // Debug setup only: start at the chosen entrance, without granting any clears or keys.
                context.Position = context.Grid.Locations[location];
                if (!common.Travel.EnterLocation()) throw new InvalidOperationException("Unable to enter " + location);
                entered = true; Save("Running", "Entered district through campaign travel.");
                return;
            }
            if (context.Completed.Contains(location) && Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true)
            {
                bool complete = floors.Count == context.RunPlan(location).Floors.Count && floors.All(f => f.GateReached) &&
                    floors.Where(f => f.Role == "Regular").All(f => f.InteriorVisited && f.LootCollected && f.DoorwaySteps >= 6 && f.RejectedDiagonals == 2) &&
                    run.Report.IssueCount == 0;
                run.enabled = true;
                string reason = complete ? "All district floors traversed; interiors and loot exercised; final gate returned to overworld." : "District completed but debug coverage is incomplete.";
                // Finish owns the final screenshot; two capture requests in one frame can collide.
                run.Finish(complete ? "Passed" : "Failed", reason);
                Save(complete ? "Passed" : "Failed", reason); finished = true; return;
            }
            var game = Game.Instance;
            if (game == null || !game.IsReady || game.NewFloorMessage.gameObject.activeSelf) return;
            var dungeon = game.CurrentDungeon;
            if (dungeon.Floor.Variant != DungeonVariant.Town) throw new InvalidOperationException("The run left its town variant.");
            if (currentFloor != game.PlayerController.Floor)
            {
                currentFloor = game.PlayerController.Floor; route.Clear(); goals.Clear(); enemyHP.Clear(); previousCell = null;
                room = null; pickup = null; pickupItem = null;
                var floor = dungeon.Floor;
                floors.Add(new FloorResult { Floor = currentFloor, Seed = floor.Seed, Width = floor.Width, Height = floor.Height,
                    Buildings = floor.TownPlan.Interiors.Count, Role = context.RunPlan(location).Floor(currentFloor).Options.Role.ToString(),
                    Fingerprint = DungeonFloorFingerprint.Compute(floor) });
                if (!dungeon.IsThroneFloor)
                {
                    var ally = game.PlayerController.ControlledAlly;
                    room = floor.TownPlan.ShopRooms.OrderBy(r => (r.Door.ToCell() - ally.TilemapPosition).sqrMagnitude).First();
                    goals.Enqueue(room.Door.ToCell() + Vector3Int.up * 2);
                    pickup = dungeon.Interactables.OfType<DroppedItem>().OrderBy(p => (p.Position - goals.Peek()).sqrMagnitude).First();
                    pickupItem = pickup.InventoryItem;
                    goals.Enqueue(pickup.Position);
                    game.PlayerController.Inventory.MaxItems = 500;
                    run.enabled = false;
                }
                Capture("floor-" + currentFloor + "-entrance");
                Save("Running", "Floor " + currentFloor);
            }
            var actor = game.PlayerController.ControlledAlly;
            var result = floors.Last();
            var cell = actor.TilemapPosition;
            if (previousCell.HasValue && previousCell.Value != cell)
            {
                result.Steps++;
                if (dungeon.Floor.TownPlan.IsDoorway(cell.ToGridPoint()) || dungeon.Floor.TownPlan.IsDoorway(previousCell.Value.ToGridPoint()))
                {
                    if (cell.x != previousCell.Value.x && cell.y != previousCell.Value.y) throw new InvalidOperationException("Diagonal doorway movement observed.");
                    result.DoorwaySteps++;
                }
            }
            previousCell = cell;
            foreach (var enemy in game.Enemies.OfType<Enemy>())
            {
                if (enemyHP.TryGetValue(enemy, out int hp) && hp > enemy.Vitals.HP) result.DamagedEnemies++;
                enemyHP[enemy] = enemy.Vitals.HP;
            }
            if (dungeon.Interactables.OfType<Stairs>().Any(s => s.Position == cell))
            { if (!result.GateReached) Capture("floor-" + currentFloor + "-gate"); result.GateReached = true; }
            if (run.enabled || run.Paused || game.TurnManager.IsProcessingTurn || EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + .03;
            if (MenuManager.Instance.Opened) throw new InvalidOperationException("Unexpected dialog during district exploration.");
            if (goals.Count > 0 && cell == goals.Peek())
            {
                if (!result.InteriorVisited)
                {
                    result.InteriorVisited = room.Floor.Contains(cell.ToGridPoint());
                    foreach (var offset in new[] { new Vector3Int(-1,-1), new Vector3Int(1,-1) })
                    {
                        if (dungeon.CanWalkTo(room.Door.ToCell() + offset, room.Door.ToCell())) throw new InvalidOperationException("Door accepted a diagonal approach.");
                        result.RejectedDiagonals++;
                    }
                    Capture("floor-" + currentFloor + "-interior");
                }
                else if (pickup != null && dungeon.Interactables.Contains(pickup))
                {
                    Act.Invoke(run, new object[] { actor, new PickUpItemAction(pickup), "Debug collect district loot" }); return;
                }
                else
                {
                    result.LootCollected = game.PlayerController.Inventory.InventoryItems.Contains(pickupItem);
                    Capture("floor-" + currentFloor + "-loot");
                }
                goals.Dequeue(); route.Clear(); Save("Running", "Exploring floor " + currentFloor);
            }
            if (goals.Count == 0) { run.enabled = true; return; }
            var skills = new AllySkillPolicy(game, actor, 0) { IncludeControlledAlly = true };
            var attack = new AllyAttackPolicy(game, actor, 0);
            GameAction action = skills.ShouldRun() ? skills.GetActions().FirstOrDefault() : null;
            action ??= attack.ShouldRun() ? attack.GetActions().FirstOrDefault(a => a.IsValid(actor)) : null;
            if (action == null)
            {
                bool Open(Vector3Int p) => dungeon.IsWalkable(p) && dungeon.OverlapsAnyOtherCharacter(actor, Character.ToBounds(actor.FootPrint, p), true) == null;
                AStar.Node[,] Grid()
                {
                    var grid = Character.GetAStarGrid();
                    foreach (var node in grid) node.IsWalkable &= Open(new Vector3Int(node.X, node.Y));
                    return grid;
                }
                bool Step(Vector3Int a, Vector3Int b) => dungeon.CanWalkTo(a,b) && Open(b);
                var next = route.Next(cell, goals.Peek(), Grid, Step);
                if (!next.HasValue)
                    foreach (var enemy in game.Enemies.Where(e => e != null).OrderBy(e => (e.TilemapPosition-cell).sqrMagnitude))
                    {
                        foreach (var offset in SkillCastOptions.Directions)
                        {
                            next = route.Next(cell, enemy.TilemapPosition + offset, Grid, Step);
                            if (next.HasValue) break;
                        }
                        if (next.HasValue) break;
                    }
                if (!next.HasValue) throw new InvalidOperationException("No route to debug interior/loot objective.");
                actor.SetFacingByTargetPosition(next.Value);
                action = new MovementAction(actor, cell, next.Value);
            }
            Act.Invoke(run, new object[] { actor, action, "Debug district " + action.GetType().Name });
        }
        catch (Exception error)
        {
            run.enabled = true; run.Finish("AutomationError", error.ToString()); Save("AutomationError", error.ToString()); finished = true;
        }
    }

    static void Capture(string name)
    {
        Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
    }
    static void Save(string outcome, string reason)
    {
        Directory.CreateDirectory(Folder);
        string json = JsonUtility.ToJson(new Result {
            Location = location, Outcome = outcome, Reason = reason, RunDirectory = directory, Floors = floors }, true);
        File.WriteAllText(Path.Combine(Folder, "district.json"), json);
        if (!string.IsNullOrEmpty(directory)) File.WriteAllText(Path.Combine(directory, "district.json"), json);
    }
}
