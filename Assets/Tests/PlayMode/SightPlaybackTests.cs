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
