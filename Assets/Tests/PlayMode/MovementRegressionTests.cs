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
    public IEnumerator OverworldInputStillTurnsInPlaceThenMovesAndRecordsTrail()
    {
        yield return harness.LoadOverworld(new TestScenario().CreateSave());
        var world = Object.FindFirstObjectByType<Overworld>();
        var player = world.OverworldPlayer;
        var origin = player.ControllingOverworldAlly.TilemapPosition;
        var direction = System.Enum.GetValues(typeof(Facing)).Cast<Facing>().First(f => {
            var target = origin + GridMovement.GetFacingOffset(f);
            return player.WalkableMap.CanWalkTo(origin, target) &&
                !world.OverworldBuildings.Any(b => b.TilemapPosition == target) &&
                !world.OverworldAllies.Any(a => a.TilemapPosition == target);
        });
        var offset = GridMovement.GetFacingOffset(direction);
        Input(offset, true);
        Decide(player);
        Assert.That(player.ControllingOverworldAlly.CurrentFacing, Is.EqualTo(direction));
        Assert.That(player.ControllingOverworldAlly.TilemapPosition, Is.EqualTo(origin));
        Input(offset, false);
        Decide(player);
        Input(Vector3Int.zero, false);
        yield return harness.WaitUntil(() => !(bool)typeof(OverworldPlayer)
            .GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player), "overworld move");
        Assert.That(player.ControllingOverworldAlly.TilemapPosition, Is.EqualTo(origin + offset));
        Assert.That(player.GetNthFromLastPosition(0), Is.EqualTo(origin + offset));
        Assert.That(Vector3.Distance(player.ControllingOverworldAlly.transform.position,
            player.WalkableMap.CellToWorld(origin + offset)), Is.LessThan(0.001f));
    }
}
#endif
