#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[PrebuildSetup(typeof(HarnessSceneBootstrap))]
[PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public class SightPlaybackTests
{
    private GameTestHarness harness;
    [UnitySetUp] public IEnumerator SetUp() { harness = new GameTestHarness(); yield return null; }
    [UnityTearDown] public IEnumerator TearDown() => harness.Cleanup();

    [UnityTest]
    public IEnumerator BigSlimeAnimatesWhenOnlyItsLeadingEdgeEntersSight()
    {
        yield return harness.LoadDungeon(new TestScenario());
        var game = harness.Game;
        var dungeon = game.CurrentDungeon;
        // Keep the actor outside party sight; provide one visible playback cell.
        var origin = new Vector3Int(dungeon.dungeonWidth + 10, dungeon.dungeonHeight + 10, 0);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Enemy>(
            "Assets/Prefabs/Dungeon/Enemies/Enemy_Slime_Big.prefab");
        var enemy = Object.Instantiate(prefab, game.transform);
        enemy.SetPosition(origin);
        Assert.That(enemy.FootPrint, Is.EqualTo(FootPrint.Size3x3));
        var destination = origin + Vector3Int.right;
        var leadingEdge = destination + Vector3Int.right;
        var move = new MovementAction(enemy, origin, destination);
        move.SetPlaybackContext(DungeonAnimationMode.Current, false, harness.Ally, enemy);
        var camera = game.PlayerController.CameraController.Camera;
        var cameraPosition = camera.transform.position;
        var cameraRotation = camera.transform.rotation;
        try
        {
            camera.transform.position = dungeon.CellToWorld(leadingEdge) + Vector3.back * 10;
            camera.transform.rotation = Quaternion.identity;
            game.PlaybackVisibleTiles.Clear();
            Assert.That(move.ShouldAnimate(enemy), Is.False, "Fully hidden movement should still be skipped.");
            game.PlaybackVisibleTiles.Add(leadingEdge);
            Assert.That(move.AnimationCells(enemy), Does.Contain(leadingEdge));
            Assert.That(move.ShouldAnimate(enemy), Is.True,
                "The entering edge is visible even though both center tiles remain hidden.");
            enemy.FootPrint = FootPrint.Size1x1;
            Assert.That(move.ShouldAnimate(enemy), Is.False,
                "A single-cell actor must not gain visibility from a neighboring cell.");
        }
        finally
        {
            game.PlaybackVisibleTiles.Clear();
            camera.transform.position = cameraPosition;
            camera.transform.rotation = cameraRotation;
            Object.DestroyImmediate(enemy.gameObject);
        }
    }

    [UnityTest]
    public IEnumerator SightTracksPlaybackAndOffscreenActionsStillApplyResults()
    {
        yield return harness.LoadDungeon(new TestScenario());
        // The entry floor is a small throne room; use a generated dungeon for occlusion.
        harness.Game.AdvanceFloor();
        yield return null;
        yield return harness.WaitForIdle();
        var game = harness.Game;
        var dungeon = game.CurrentDungeon;
        var ally = harness.Ally;
        var origin = ally.TilemapPosition;
        var remote = Enumerable.Range(0, dungeon.dungeonWidth)
            .SelectMany(x => Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
            .OrderByDescending(p => TileWorldDungeon.ChevDistance(p, origin))
            .First(p => dungeon.IsWalkable(p) && !game.PartyVisibleTiles.Contains(p)
                && !game.AllCharacters.Any(c => c.TilemapPosition == p));
        yield return harness.SpawnEnemy("Enemy_Slime", remote);
        var enemy = (Enemy)game.Enemies.First(e => e.TilemapPosition == remote);
        game.UpdateMiniMap();
        Assert.That(dungeon.CanSee(ally, enemy), Is.False);
        var selector = new TargetSelector { Team = TargetTeam.Enemies, Area = TargetArea.Visible };
        Assert.That(selector.GetTargets(ally).Contains(remote), Is.False);
        Assert.That(new AttackAction(enemy, remote, remote).ShouldAnimate(enemy), Is.False);

        // Shared party knowledge alone must not force playback outside the camera.
        var camera = game.PlayerController.CameraController.Camera;
        var cameraRotation = camera.transform.rotation;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
        game.PartyVisibleTiles.Add(remote);
        Assert.That(new AttackAction(enemy, remote, remote).ShouldAnimate(enemy), Is.False);
        camera.transform.rotation = cameraRotation;
        game.UpdateMiniMap();

        // An affected onscreen target matters even if the actor itself is unseen.
        var damageToPlayer = new TakeDamageAction(enemy, ally, 0);
        damageToPlayer.ExecuteImmediate(enemy);
        Assert.That(damageToPlayer.ShouldAnimate(enemy), Is.True);

        // Logical movement is resolved ahead of playback. Fog follows displayed positions.
        ally.transform.position = dungeon.CellToWorld(remote);
        game.RefreshSight();
        Assert.That(ally.TilemapPosition, Is.EqualTo(origin));
        Assert.That(game.PartyVisibleTiles.SetEquals(dungeon.GetVisibleTiles(ally, remote)), Is.True);
        var minimap = Object.FindFirstObjectByType<Minimap>();
        Assert.That(minimap.dungeonMap[origin.x, origin.y].visibility, Is.EqualTo(Minimap.MinimapTileVisibility.Explored));
        Assert.That(FogOverlay.Instance.IsCurrentlyVisible(enemy.transform.position), Is.True);
        ally.transform.position = dungeon.CellToWorld(origin);
        game.RefreshSight();

        int hp = enemy.Vitals.HP;
        var damage = new TakeDamageAction(ally, enemy, 1);
        damage.ExecuteImmediate(enemy);
        yield return enemy.ExecuteActionRoutine(damage);
        Assert.That(enemy.Vitals.HP, Is.EqualTo(hp - 1));
        Assert.That(enemy.DisplayedVitals.HP, Is.EqualTo(enemy.Vitals.HP));
        Assert.That(FogOverlay.Instance.IsCurrentlyVisible(enemy.transform.position), Is.False);
        yield return null;
        Assert.That(enemy.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff), Is.True);

        var status = new ApplyStatusEffectAction(enemy, game.StatusEffectPrefabs.First(), ally);
        status.ExecuteImmediate(enemy);
        Assert.That(status.ExecuteRoutine(enemy, true).MoveNext(), Is.False);
        Assert.That(enemy.StatusEffects.Last().gameObject.activeSelf, Is.True,
            "Skipping presentation must still activate persistent status effects.");

        var death = new DeathAction(enemy, ally);
        death.ExecuteImmediate(enemy);
        Assert.That(death.ExecuteRoutine(enemy, true).MoveNext(), Is.False);
        Assert.That(enemy.VisualParent.activeSelf, Is.False);
        Assert.That(game.DeadUnits.Contains(enemy), Is.True);
    }
}
#endif
