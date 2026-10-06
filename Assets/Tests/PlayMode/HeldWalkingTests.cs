#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.World;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
public sealed class HeldWalkingTests
{
    private GameTestHarness harness;
    private DungeonAnimationMode? previousMode;
    private bool? previousControl;
    private Keyboard keyboard;
    private Scene overworld;
    private TestInputScope inputScope;

    [UnitySetUp] public IEnumerator Setup()
    {
        inputScope = new TestInputScope();
        previousMode = DungeonPreferences.AnimationOverride;
        previousControl = DungeonPreferences.FullControlOverride;
        DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
        DungeonPreferences.FullControlOverride = false;
        harness = new GameTestHarness();
        yield return null;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (overworld.IsValid() && overworld.isLoaded) yield return SceneManager.UnloadSceneAsync(overworld);
        yield return harness.Cleanup();
        DungeonPreferences.AnimationOverride = previousMode;
        DungeonPreferences.FullControlOverride = previousControl;
        inputScope.Dispose();
    }

    private static void Field(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Input(Vector3Int direction, bool hold = false)
    {
        // Freeze the sampler, keeping the production controllers and their timing active.
        var input = PlayerInputHandler.Instance;
        input.enabled = false;
        typeof(PlayerInputHandler).GetProperty("moveInput").SetValue(input, new Vector2(direction.x, direction.y));
        typeof(PlayerInputHandler).GetProperty("isMoving").SetValue(input, direction.sqrMagnitude > .1f);
        typeof(PlayerInputHandler).GetProperty("holdPosition").SetValue(input, hold);
    }

    private static void Decide(object controller) => controller.GetType()
        .GetMethod("DeterminePlayerAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

    private static AnimatorStateInfo State(HeroAnimator hero)
    {
        hero.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        hero.Animator.Update(0);
        return hero.Animator.GetCurrentAnimatorStateInfo(0);
    }

    private static void AssertAction(HeroAnimator hero, AnimatedAction action, string message = null)
    {
        State(hero);
        var clip = hero.Animator.GetCurrentAnimatorClipInfo(0).Single().clip;
        Assert.That(hero.StanceAnimations.SelectMany(s => s.NamedAnimations)
            .Where(a => a.AnimationAction == action).SelectMany(a => a.Animations), Does.Contain(clip), message);
    }

    private static void AssertContinued(HeroAnimator hero, AnimatorStateInfo before)
    {
        var after = State(hero);
        AssertAction(hero, AnimatedAction.MoveFWD);
        Assert.That(after.fullPathHash, Is.EqualTo(before.fullPathHash), "Held steps retain their selected walk clip.");
        Assert.That(after.normalizedTime, Is.GreaterThanOrEqualTo(before.normalizedTime), "Walk time must not restart at tile boundaries.");
    }

    private static void Capture(string name)
    {
        Directory.CreateDirectory("Temp/HeldWalk");
        var camera = Camera.main;
        if (camera == null) return;
        var target = RenderTexture.GetTemporary(960, 600, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var pixels = new Texture2D(960, 600, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Temp/HeldWalk/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(pixels);
        }
    }

    [UnityTest] public IEnumerator TownHeldStepsAndDirectionChangesKeepTimeAndReleaseFinishesSquare()
    {
        var save = new TestScenario().CreateSave();
        save.TownSaveData.RecruitedAlliesData.Add(new TownAllyData { AllyName = "Avery" });
        save.TownSaveData.RecruitedAlliesData.Add(new TownAllyData { AllyName = "Morgan" });
        save.TownSaveData.RecruitedAlliesData.Add(new TownAllyData { AllyName = "Alex" });
        yield return harness.LoadTown(save);
        var town = Object.FindFirstObjectByType<Town>();
        var player = town.TownPlayer;
        Field(player, "initialied", false); // Dispatch one decision at each inspected boundary.
        var leader = player.ControllingTownAlly;
        var follower = player.RecruitedAllies.First(a => a != leader);
        var origin = leader.TilemapPosition;
        var offset = System.Enum.GetValues(typeof(Facing)).Cast<Facing>().Select(GridMovement.GetFacingOffset)
            .First(d => player.WalkableMap.CanWalkTo(origin, origin + d) && town.CanEnter(origin + d) &&
                !town.TownBuildings.Any(b => b.TilemapPosition == origin + d) &&
                !town.TownAllies.Any(a => a.TilemapPosition == origin + d));
        Assert.That(player.RecruitedAllies.Count, Is.EqualTo(4));
        foreach (var member in player.RecruitedAllies.Where(a => a != leader))
        {
            member.TilemapPosition = origin;
            member.transform.position = leader.transform.position;
        }
        player.WalkPositionHistory.Clear();
        player.WalkPositionHistory.Add(origin);
        Input(offset);
        Decide(player);
        yield return harness.WaitUntil(() => !player.IsBusy, "first held town step");
        AssertAction(leader.HeroAnimator, AnimatedAction.MoveFWD);
        AssertAction(follower.HeroAnimator, AnimatedAction.Idle);
        var previous = State(leader.HeroAnimator);
        Input(-offset);
        Decide(player);
        yield return harness.WaitUntil(() => !player.IsBusy, "turned held town step");
        AssertContinued(leader.HeroAnimator, previous);
        AssertAction(follower.HeroAnimator, AnimatedAction.MoveFWD);
        var followerState = State(follower.HeroAnimator);
        Input(offset);
        Decide(player);
        yield return harness.WaitUntil(() => !player.IsBusy, "third held town step");
        AssertContinued(follower.HeroAnimator, followerState);
        Capture("Town-held");
        Input(-offset);
        Decide(player);
        yield return null; // Release during interpolation, not before starting it.
        Input(Vector3Int.zero);
        yield return harness.WaitUntil(() => player.RecruitedAllies.All(a =>
            Vector3.Distance(a.transform.position, player.WalkableMap.CellToWorld(a.TilemapPosition)) < .001f), "all four heroes arrived");
        int arrivedFrame = Time.frameCount;
        yield return harness.WaitUntil(() => !player.IsBusy, "released town step");
        Assert.That(Time.frameCount - arrivedFrame, Is.LessThanOrEqualTo(1), "Completed followers must not add idle frames.");
        Assert.That(leader.TilemapPosition, Is.EqualTo(origin));
        AssertAction(leader.HeroAnimator, AnimatedAction.Idle);
        AssertAction(follower.HeroAnimator, AnimatedAction.Idle);
        yield return new WaitForSeconds(.2f);
        Assert.That(leader.TilemapPosition, Is.EqualTo(origin));
        Capture("Town-idle");

        // Cancellation cannot replace an action pose, even if a step completes later.
        foreach (var action in new[] { AnimatedAction.GetHit, AnimatedAction.Die, AnimatedAction.Attack })
        {
            leader.HeroAnimator.BeginWalk(true);
            leader.HeroAnimator.PlayAnimation(action);
            leader.HeroAnimator.StopWalkContinuation();
            leader.HeroAnimator.CompleteWalk(false);
            AssertAction(leader.HeroAnimator, action);
        }
        leader.HeroAnimator.PlayIdleAnimation();
        leader.HeroAnimator.BeginWalk(true);
        leader.HeroAnimator.StopWalkContinuation();
        leader.HeroAnimator.CompleteWalk(true);
        AssertAction(leader.HeroAnimator, AnimatedAction.Idle, "A release stays latched even if input returns before arrival.");
        Input(offset);
        DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
        Decide(player);
        yield return harness.WaitUntil(() => !player.IsBusy, "unanimated town step");
        AssertAction(leader.HeroAnimator, AnimatedAction.Idle);
    }

    [UnityTest] public IEnumerator DungeonHeldTurnsSwapReleaseAndCombatPreserveCorrectPoses()
    {
        yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Avery", "Morgan", "Alex" } });
        var game = harness.Game;
        foreach (var enemy in game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
        game.Enemies.Clear();
        var controller = game.PlayerController;
        Field(controller, "releaseInput", false);
        Field(controller, "menuCooldown", -1000f); // Inspect completed turns before issuing the next command.
        var leader = harness.Ally;
        var follower = game.Allies.First(a => a != leader);
        Assert.That(game.Allies.Count, Is.EqualTo(4));
        foreach (var member in game.Allies.Where(a => a != leader)) member.AllyStrategy = AllyStrategy.HoldPosition;
        var followerOrigin = follower.TilemapPosition;
        follower.AllyStrategy = AllyStrategy.HoldPosition;
        var origin = leader.TilemapPosition;
        var offset = game.CurrentDungeon.GetValidWalkDirections(origin).Select(GridMovement.GetFacingOffset)
            .First(d => game.CurrentDungeon.GetCharacterAtPosition(origin + d) == null &&
                game.CurrentDungeon.GetInteractable(origin + d) == null);
        for (int step = 0; step < 3; step++)
        {
            var before = step == 0 ? default : State(leader.HeroAnimator);
            Input(step % 2 == 0 ? offset : -offset);
            Field(controller, "holdTime", 1f);
            Decide(controller);
            yield return null;
            yield return harness.WaitForIdle();
            AssertAction(leader.HeroAnimator, AnimatedAction.MoveFWD);
            if (step > 0) AssertContinued(leader.HeroAnimator, before);
            AssertAction(follower.HeroAnimator, AnimatedAction.Idle);
        }
        Capture("Dungeon-held");
        Input(-offset);
        Field(controller, "holdTime", 1f);
        Decide(controller);
        yield return harness.WaitUntil(() => leader.transform.position != game.CurrentDungeon.CellToWorld(origin + offset),
            "dungeon interpolation started");
        Input(Vector3Int.zero);
        yield return null;
        yield return harness.WaitForIdle();
        Assert.That(leader.TilemapPosition, Is.EqualTo(origin));
        AssertAction(leader.HeroAnimator, AnimatedAction.Idle);

        // Both heroes in a moving swap can retain their walk independently.
        follower.SetPosition(origin + offset);
        Input(offset);
        Field(controller, "heldWalk", true);
        var swap = new SwapAllyPositionAction(leader, follower);
        swap.ExecuteImmediate(leader);
        yield return swap.ExecuteRoutine(leader);
        AssertAction(leader.HeroAnimator, AnimatedAction.MoveFWD);
        AssertAction(follower.HeroAnimator, AnimatedAction.MoveFWD);
        var swapLeaderState = State(leader.HeroAnimator);
        var swapFollowerState = State(follower.HeroAnimator);
        swap = new SwapAllyPositionAction(leader, follower);
        swap.ExecuteImmediate(leader);
        yield return swap.ExecuteRoutine(leader);
        AssertContinued(leader.HeroAnimator, swapLeaderState);
        AssertContinued(follower.HeroAnimator, swapFollowerState);
        controller.StopHeldWalk();
        AssertAction(follower.HeroAnimator, AnimatedAction.Idle);

        // A scripted movement remains a single step even with direction input sampled.
        follower.SetPosition(followerOrigin);
        yield return harness.ExecuteAction(new MovementAction(leader, leader.TilemapPosition, origin + offset));
        AssertAction(leader.HeroAnimator, AnimatedAction.Idle);
        Input(-offset);
        Field(controller, "holdTime", 1f);
        Decide(controller);
        yield return null;
        yield return harness.WaitForIdle();
        AssertAction(leader.HeroAnimator, AnimatedAction.MoveFWD);
        yield return harness.ExecuteAction(new WaitAction());
        AssertAction(leader.HeroAnimator, AnimatedAction.Idle);
        // An allied non-movement action follows the leader's move in playback.
        // It must see the leader idle before posing damage, and cleanup cannot erase that pose.
        var pause = new WalkPauseProbe(leader);
        follower.SetAction(pause);
        Input(offset);
        Field(controller, "holdTime", 1f);
        Decide(controller);
        yield return null;
        yield return harness.WaitForIdle();
        Assert.That(pause.Played, Is.True);
        AssertAction(leader.HeroAnimator, AnimatedAction.GetHit);
        leader.PlayIdleAnimation();
        Input(-offset);
        Field(controller, "heldWalk", true);
        yield return harness.ExecuteAction(new MovementThenWait(leader, leader.TilemapPosition, origin) { ManualWalkCommand = true });
        AssertAction(leader.HeroAnimator, AnimatedAction.Idle, "A wait after a move in the same batch ends retained walking.");
        Capture("Dungeon-idle");
    }

    [UnityTest] public IEnumerator OverworldHeldInputContinuesAndScriptedStepsIdle()
    {
        yield return harness.LoadCommon();
        Common.Instance.BeginSandbox(42);
        var context = Common.Instance.CampaignContext;
        var companion = context.Campaign.Companions.First();
        context.Roster.Add(companion.Id);
        Assert.That(context.SetParty(new[] { companion.Id }), Is.True);
        yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
        overworld = SceneManager.GetSceneByName("Overworld");
        var world = Object.FindFirstObjectByType<OverworldScene>();
        yield return harness.WaitUntil(() => world.IsReady, "overworld initialization");
        Assert.That(world.SimulateDungeonVictory(), Is.True);
        var origin = world.Position;
        var next = OverworldMovement.Neighbors(origin).First(p => world.CanStep(origin, p));
        int dx = next.X - origin.X, dy = next.Y - origin.Y;
        keyboard = InputSystem.AddDevice<Keyboard>();
        var keys = new System.Collections.Generic.List<Key>();
        if (dx != 0) keys.Add(dx > 0 ? Key.D : Key.A);
        if (dy != 0) keys.Add(dy > 0 ? Key.W : Key.S);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
        yield return harness.WaitUntil(() => world.IsMoving, "held overworld input");
        // Keep each decision deterministic while sampling input during interpolation.
        Field(world, "nextMove", Time.time + 1000f);
        yield return harness.WaitUntil(() => !world.IsMoving, "overworld arrival");
        AssertAction(world.Player.HeroAnimator, AnimatedAction.MoveFWD);
        var before = State(world.Player.HeroAnimator);
        keys.Clear();
        if (dx != 0) keys.Add(dx > 0 ? Key.A : Key.D);
        if (dy != 0) keys.Add(dy > 0 ? Key.S : Key.W);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
        Field(world, "nextMove", 0f);
        yield return harness.WaitUntil(() => world.IsMoving, "overworld direction change");
        Field(world, "nextMove", Time.time + 1000f);
        yield return harness.WaitUntil(() => !world.IsMoving, "second overworld arrival");
        AssertContinued(world.Player.HeroAnimator, before);
        AssertAction(world.Followers.Single().HeroAnimator, AnimatedAction.MoveFWD);
        Capture("Overworld-held");
        var followerBefore = State(world.Followers.Single().HeroAnimator);
        keys.Clear();
        if (dx != 0) keys.Add(dx > 0 ? Key.D : Key.A);
        if (dy != 0) keys.Add(dy > 0 ? Key.W : Key.S);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
        Field(world, "nextMove", 0f);
        yield return harness.WaitUntil(() => world.IsMoving, "third overworld step");
        AssertContinued(world.Followers.Single().HeroAnimator, followerBefore);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return harness.WaitUntil(() => !world.IsMoving, "mid-step overworld release");
        AssertAction(world.Player.HeroAnimator, AnimatedAction.Idle);
        AssertAction(world.Followers.Single().HeroAnimator, AnimatedAction.Idle);
        yield return new WaitForSeconds(.2f);
        Assert.That(world.Position, Is.EqualTo(next), "Release must not enqueue an extra step.");
        Assert.That(world.TryMove(-dx, -dy), Is.True);
        yield return harness.WaitUntil(() => !world.IsMoving, "scripted overworld step");
        AssertAction(world.Player.HeroAnimator, AnimatedAction.Idle);
        Capture("Overworld-idle");
    }

    private sealed class MovementThenWait : MovementAction
    {
        internal MovementThenWait(Character actor, Vector3Int from, Vector3Int to) : base(actor, from, to) { }
        internal override System.Collections.Generic.List<GameAction> ExecuteImmediate(Character actor)
        {
            var effects = base.ExecuteImmediate(actor);
            effects.Add(new WaitAction());
            return effects;
        }
    }

    private sealed class WalkPauseProbe : GameAction
    {
        private readonly Ally leader;
        internal bool Played;
        internal WalkPauseProbe(Ally leader) => this.leader = leader;
        internal override bool IsValid(Character actor) => true;
        internal override System.Collections.Generic.List<GameAction> ExecuteImmediate(Character actor) => new();
        internal override IEnumerator ExecuteRoutine(Character actor, bool skipAnimation = false)
        {
            AssertAction(leader.HeroAnimator, AnimatedAction.Idle);
            leader.PlayTakeDamageAnimation();
            Game.Instance.PlayerController.StopHeldWalk();
            AssertAction(leader.HeroAnimator, AnimatedAction.GetHit);
            Played = true;
            yield break;
        }
    }
}
#endif
