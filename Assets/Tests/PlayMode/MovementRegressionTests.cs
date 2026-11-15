#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[PrebuildSetup(typeof(HarnessSceneBootstrap))]
[PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public class MovementRegressionTests
{
    private GameTestHarness harness;
    [UnitySetUp] public IEnumerator SetUp() { harness = new GameTestHarness(); yield return null; }
    [UnityTearDown] public IEnumerator TearDown() => harness.Cleanup();

    // Exercise the existing decision methods with sampled input, without changing bindings or polling.
    private static void Input(Vector3Int direction, bool hold)
    {
        typeof(PlayerInputHandler).GetProperty("moveInput").SetValue(PlayerInputHandler.Instance, new Vector2(direction.x, direction.y));
        typeof(PlayerInputHandler).GetProperty("holdPosition").SetValue(PlayerInputHandler.Instance, hold);
    }
    private static void Decide(object controller) => controller.GetType()
        .GetMethod("DeterminePlayerAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

    [UnityTest]
    public IEnumerator DungeonInputStillTurnsInPlaceThenMovesThroughTurnPipeline()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var dungeon = harness.Game.CurrentDungeon;
        var origin = harness.Ally.TilemapPosition;
        var direction = dungeon.GetValidWalkDirections(origin).First(f => {
            var target = origin + GridMovement.GetFacingOffset(f);
            return dungeon.GetInteractable(target) == null && dungeon.GetCharacterAtPosition(target) == null;
        });
        var offset = GridMovement.GetFacingOffset(direction);
        var controller = harness.Game.PlayerController;
        Input(offset, true);
        Decide(controller);
        Assert.That(harness.Ally.CurrentFacing, Is.EqualTo(direction));
        Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(origin));
        Input(offset, false);
        typeof(PlayerController).GetField("holdTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, 1f);
        Decide(controller);
        Input(Vector3Int.zero, false);
        yield return null;
        yield return harness.WaitForIdle();
        Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(origin + offset));
        Assert.That(Vector3.Distance(harness.Ally.transform.position, dungeon.CellToWorld(origin + offset)), Is.LessThan(0.001f));
        Assert.DoesNotThrow(() => dungeon.GetWalkableNeighborhoodTiles(Vector3Int.zero));
        Assert.That(dungeon.GetPositionWith(origin, n => false), Is.EqualTo(origin));
    }

    [UnityTest]
    public IEnumerator DungeonHeldDirectionCanMoveAgainAfterACompletedTurn()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var dungeon = harness.Game.CurrentDungeon;
        var controller = harness.Game.PlayerController;
        var ally = harness.Ally;
        var origin = ally.TilemapPosition;
        var direction = dungeon.GetValidWalkDirections(origin).First(f => {
            var step = GridMovement.GetFacingOffset(f);
            return dungeon.CanWalkTo(origin + step, origin + step * 2) &&
                dungeon.GetCharacterAtPosition(origin + step) == null &&
                dungeon.GetCharacterAtPosition(origin + step * 2) == null &&
                dungeon.GetInteractable(origin + step) == null &&
                dungeon.GetInteractable(origin + step * 2) == null;
        });
        var offset = GridMovement.GetFacingOffset(direction);
        var hold = typeof(PlayerController).GetField("holdTime", BindingFlags.Instance | BindingFlags.NonPublic);
        var release = typeof(PlayerController).GetField("releaseInput", BindingFlags.Instance | BindingFlags.NonPublic);
        release.SetValue(controller, false);
        Input(offset, false);
        hold.SetValue(controller, 1f);
        Decide(controller);
        Input(Vector3Int.zero, false);
        yield return null;
        yield return harness.WaitForIdle();
        Assert.That(ally.TilemapPosition, Is.EqualTo(origin + offset));

        controller.FocusCommand(ally); // The turn system refocuses this same hero.
        Assert.That(release.GetValue(controller), Is.False, "Refocusing the same hero must not demand a key release.");
        Input(offset, false);
        hold.SetValue(controller, 1f);
        Decide(controller); // The direction is still held.
        Input(Vector3Int.zero, false);
        yield return null;
        yield return harness.WaitForIdle();
        Assert.That(ally.TilemapPosition, Is.EqualTo(origin + offset * 2));
    }

    [UnityTest]
    public IEnumerator TownDungeonPromptsOnlyWhenMovingIntoTheEntrance()
    {
        yield return harness.LoadTown(new TestScenario().CreateSave());
        var town = Object.FindFirstObjectByType<Town>();
        var player = town.TownPlayer;
        var hero = player.ControllingTownAlly;
        var manager = Object.FindFirstObjectByType<TownMenuManager>();
        var entrance = town.TownBuildings.Single(b => b.Definition.DialogId == "entrance");
        var door = entrance.TilemapPosition;
        var approach = door + Vector3Int.down;
        hero.TilemapPosition = approach;
        hero.transform.position = town.WalkableMap.CellToWorld(approach);

        IEnumerator Move(Vector3Int direction, bool hold = false)
        {
            Input(direction, hold);
            Decide(player);
            Input(Vector3Int.zero, false);
            yield return harness.WaitUntil(() => !player.IsBusy, "town entrance movement");
        }

        yield return Move(Vector3Int.up);
        Assert.That(hero.TilemapPosition, Is.EqualTo(door));
        Assert.That(manager.DialogStack, Is.Empty, "Arriving in front of the dungeon must not open its prompt.");

        yield return Move(Vector3Int.up, hold: true);
        Assert.That(hero.CurrentFacing, Is.EqualTo(Facing.Up));
        Assert.That(manager.DialogStack, Is.Empty, "Turning toward the dungeon must not enter it.");
        yield return Move(new Vector3Int(1, 1));
        Assert.That(hero.TilemapPosition, Is.EqualTo(door));
        Assert.That(manager.DialogStack, Is.Empty, "A diagonal step must not enter the doorway.");

        yield return Move(Vector3Int.up);
        Assert.That(manager.CurrentDialog, Is.TypeOf<EntranceDialog>());
        Assert.That(manager.DialogStack.Count, Is.EqualTo(1));
        Assert.That(hero.TilemapPosition, Is.EqualTo(door), "The dungeon facade is not a walkable town room.");

        ((EntranceDialog)manager.CurrentDialog).Cancel_Clicked();
        yield return null;
        yield return harness.WaitUntil(() => !player.IsBusy, "cancel dungeon entrance");
        Assert.That(manager.DialogStack, Is.Empty);
        Assert.That(hero.TilemapPosition, Is.EqualTo(door), "Cancel should leave the party at the entrance.");

        yield return Move(Vector3Int.down);
        Assert.That(hero.TilemapPosition, Is.EqualTo(approach));
        Assert.That(manager.DialogStack, Is.Empty);
        yield return Move(Vector3Int.up);
        Assert.That(manager.DialogStack, Is.Empty);
        yield return Move(Vector3Int.up);
        Assert.That(manager.CurrentDialog, Is.TypeOf<EntranceDialog>(), "The entrance remains usable after canceling.");
        ((EntranceDialog)manager.CurrentDialog).Cancel_Clicked();
    }

    [UnityTest]
    public IEnumerator TownInputStillTurnsInPlaceThenMovesAndRecordsTrail()
    {
        yield return harness.LoadTown(new TestScenario().CreateSave());
        var world = Object.FindFirstObjectByType<Town>();
        var player = world.TownPlayer;
        var origin = player.ControllingTownAlly.TilemapPosition;
        var direction = System.Enum.GetValues(typeof(Facing)).Cast<Facing>().First(f => {
            var target = origin + GridMovement.GetFacingOffset(f);
            return player.WalkableMap.CanWalkTo(origin, target) &&
                !world.TownBuildings.Any(b => b.TilemapPosition == target) &&
                !world.TownAllies.Any(a => a.TilemapPosition == target);
        });
        var offset = GridMovement.GetFacingOffset(direction);
        Input(offset, true);
        Decide(player);
        Assert.That(player.ControllingTownAlly.CurrentFacing, Is.EqualTo(direction));
        Assert.That(player.ControllingTownAlly.TilemapPosition, Is.EqualTo(origin));
        Input(offset, false);
        Decide(player);
        Input(Vector3Int.zero, false);
        yield return harness.WaitUntil(() => !(bool)typeof(TownPlayer)
            .GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player), "town move");
        Assert.That(player.ControllingTownAlly.TilemapPosition, Is.EqualTo(origin + offset));
        Assert.That(player.GetNthFromLastPosition(0), Is.EqualTo(origin + offset));
        Assert.That(Vector3.Distance(player.ControllingTownAlly.transform.position,
            player.WalkableMap.CellToWorld(origin + offset)), Is.LessThan(0.001f));
    }
}
#endif
