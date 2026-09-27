using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
	public bool IsProcessingTurn { get; private set; }
    public bool AwaitingCommand { get; private set; }
    public bool FullControlThisRound { get; private set; }
    public Character ActiveActor { get; private set; }
    public bool UsesFullControl => IsProcessingTurn ? FullControlThisRound : DungeonPreferences.FullControl;
    public string PhaseLabel => !IsProcessingTurn ? "Your turn" : AwaitingCommand ? "Awaiting order" : ActiveActor is Enemy ? "Enemy turn" : "Resolving action";

    internal bool SubmitCommand(Character actor, GameAction action)
    {
        if (actor == null || action == null || actor.Vitals.HP <= 0) return false;
        var player = Game.Instance.PlayerController;
        if (IsProcessingTurn && (!AwaitingCommand || actor != ActiveActor)) return false;
        if (UsesFullControl && actor != player.ControlledAlly) return false;
        if (!action.IsValid(actor)) return false;
        actor._forcedAction = action;
        if (actor != player.ControlledAlly) return true;
        actor.IsWaitingForPlayerInput = false;
        if (IsProcessingTurn) AwaitingCommand = false;
        else ProcessTurn();
        return true;
    }
	public SimultaneousCoroutines SimultaneousCoroutines;
	public SequentialCoroutines SequentialCoroutines;
	private bool roundHasAction;
	private bool interuptTurn = false; //this happens when the stairs are taken

    private void Update()
    {
        var game = Game.Instance;
        if (!IsProcessingTurn && DungeonPreferences.FullControl && game != null && game.IsReady &&
            !AutoplayRunner.BlocksPlayerInput && !MenuManager.Instance.Opened && !Common.Instance.GlobalSettings.IsOpen &&
            game.PlayerController.ControlledAlly != null && game.PlayerController.ControlledAlly.IsWaitingForPlayerInput)
            ProcessTurn();
    }

	private void Start()
	{
		SimultaneousCoroutines = new SimultaneousCoroutines(this);
		SequentialCoroutines = new SequentialCoroutines(this);
	}

	public void ProcessTurn()
	{
		if (IsProcessingTurn) return;
		interuptTurn = false;
		StartCoroutine(TrackTurnRoutine());
	}

	private IEnumerator TrackTurnRoutine()
	{
		IsProcessingTurn = true;
		try { yield return ProcessTurnRoutine(); }
		finally { IsProcessingTurn = false; AwaitingCommand = false; ActiveActor = null; }
	}

	private IEnumerator ProcessTurnRoutine()
	{
		FullControlThisRound = DungeonPreferences.FullControl;
        roundHasAction = false;
        var controlledAlly = Game.Instance.PlayerController.PartyLeader ?? Game.Instance.PlayerController.ControlledAlly;
		var actors = new List<Actor> { controlledAlly };
		actors.AddRange(Game.Instance.Allies.Where(a => a != controlledAlly && !PartyRules.IsSummon(a)));
        actors.AddRange(Game.Instance.Allies.Where(PartyRules.IsSummon));
		actors.AddRange(Game.Instance.Enemies);

		interuptTurn = false;
		List<ActorAction> actionReplays = new();

		foreach (var actor in actors)
		{
            if (actor is not Character living || living == null || living.Vitals.HP <= 0) continue;
            ActiveActor = living;
            bool manual = FullControlThisRound && living is Ally && !PartyRules.IsSummon(living);
            if (FullControlThisRound && !manual) Game.Instance.PlayerController.RestoreLeader();
			do
			{
                var requested = living._forcedAction;
                bool commanded = requested != null && living == Game.Instance.PlayerController.ControlledAlly;
                if (manual)
                {
                    var hero = (Ally)living;
                    Game.Instance.PlayerController.FocusCommand(hero);
                    if (!hero.PrepareManualAction())
                    {
                        AwaitingCommand = true; hero.IsWaitingForPlayerInput = true;
                        while (AwaitingCommand && !interuptTurn && hero != null && hero.Vitals.HP > 0)
                        {
                            if (!roundHasAction && !DungeonPreferences.FullControl)
                            { Game.Instance.PlayerController.RestoreLeader(); hero.IsWaitingForPlayerInput=true; yield break; }
                            yield return null;
                        }
                        hero.IsWaitingForPlayerInput = false;
                        if (interuptTurn) yield break;
                        if (hero.Vitals.HP <= 0) break;
                        hero.PrepareManualAction(false);
                        commanded = true;
                    }
                    else commanded = commanded && hero.determinedActions.Contains(requested);
                }
                else actor.DetermineAction();
                var focus = Game.Instance.PlayerController.ControlledAlly;
				if (!roundHasAction) GameMessages.BeginTurn();
                var primaryAction = actor.GetDeterminedAction();
                roundHasAction = true;
				if (primaryAction == null) { continue; }
				List<GameAction> gameActions = primaryAction;
                var playback = new DungeonActionPlayback(commanded, focus, living);

				if (actor is Ally ally)
				{
					gameActions.AddRange(Game.Instance.PlayerController.MoveSideEffects(ally));
				}

				while (gameActions.Any())
				{
					var sideEffectAction = gameActions.First();
					gameActions.Remove(sideEffectAction);
					if (sideEffectAction == null) { continue; }

					if (actor == null) { continue; }
                    sideEffectAction.SetPlaybackContext(playback);
					gameActions.AddRange(actor.ExecuteActionImmediate(sideEffectAction));
					actionReplays.Add(new ActorAction(actor, sideEffectAction));

					if (interuptTurn)
					{
						yield break;
					}

					foreach (var actor2 in actors)
					{
                        if (actor2 is not Character other || other == null) continue;
						gameActions.AddRange(actor2.GetResponseTo(sideEffectAction));
						if (actor2 is Character responder && responder != null)
							gameActions.AddRange(responder.GetClassResponses(sideEffectAction));
					}
				}

				if (interuptTurn)
				{
					yield break;
				}
                if (FullControlThisRound)
                {
                    yield return Replay(actionReplays);
                    Game.Instance.RefreshSight(); Game.Instance.UpdateMiniMap();
                    GameMessages.FinishAction();
                    if (Game.Instance.PlayerController.PendingRetreat)
                    { Game.Instance.PlayerController.RestoreLeader(); Game.Instance.PlayerController.StartTurn(); yield break; }
                }
			} while (living != null && living.Vitals.HP > 0 && actor.ActionsLeft > 0);

			if (interuptTurn)
			{
				yield break;
			}
		}

		if (interuptTurn) { yield break; }

		foreach (var actor in actors)
		{
            if (actor is not Character alive || alive == null || alive.Vitals.HP <= 0) continue;
			if (actor is Ally ally &&
				ally.MovedThisTurn)
			{
				ally.currentInteractable = Game.Instance.CurrentDungeon?.GetInteractable(ally.TilemapPosition);

				var interactableSideEffects = actor.GetInteractableSideEffects();
				while (interactableSideEffects.Any())
				{
					var trapSideEffect = interactableSideEffects.First();
					interactableSideEffects.Remove(trapSideEffect);
					actionReplays.Add(new ActorAction(actor, trapSideEffect));
					actor.ExecuteActionImmediate(trapSideEffect);
				}
			}

			var statusSideEffectActions = actor.GetStatusEffectSideEffects();
			var trapSideEffects = actor.GetTrapSideEffects();
			while (trapSideEffects.Any())
			{
				var trapSideEffect = trapSideEffects.First();
				trapSideEffects.Remove(trapSideEffect);
				actionReplays.Add(new ActorAction(actor, trapSideEffect));

				statusSideEffectActions.AddRange(actor.ExecuteActionImmediate(trapSideEffect));
			}

			while (statusSideEffectActions.Any())
			{
				var sideEffectAction = statusSideEffectActions.First();
				statusSideEffectActions.Remove(sideEffectAction);
				actionReplays.Add(new ActorAction(actor, sideEffectAction));

				statusSideEffectActions.AddRange(actor.ExecuteActionImmediate(sideEffectAction));
			}
		}

		if (interuptTurn) { yield break; }

		foreach (var actor in actors)
		{
            if (actor is Character survivor && survivor != null && survivor.Vitals.HP > 0) actor.TickStatusEffects();
		}

		if (interuptTurn) { yield break; }

        yield return Replay(actionReplays);
        GameMessages.FinishAction();

		var game = Game.Instance;
		SummonRules.TickSummons(game);
		if (game.FloorReveal != null && game.FloorReveal.EnemiesRevealedTurns > 0)
			game.FloorReveal.EnemiesRevealedTurns--;

		// Defeat only when no non-summon party member is standing; a downed protagonist does not end the run.
		if (PartyRules.IsPartyDefeated(game))
		{
			game.ShowGameOver();
		}
		else if (!PartyRules.IsStanding(game, game.PlayerController.ControlledAlly))
		{
			game.PlayerController.RestoreLeader();
		}

		foreach (var deadUnit in Game.Instance.DeadUnits)
		{
			Destroy(deadUnit.gameObject);
		}
		Game.Instance.DeadUnits.Clear();

        if (FullControlThisRound) game.PlayerController.RestoreLeader();
		Game.Instance.PlayerController.StartTurn();
		foreach (var survivor in Game.Instance.AllCharacters.ToArray())
		{
            if (survivor != null && survivor.Vitals.HP > 0) survivor.StartTurn();
		}

		CheckStats();
	}

    private IEnumerator Replay(List<ActorAction> actionReplays)
    {
		while (actionReplays.Any())
		{
			var action = actionReplays[0];
            ActiveActor = action.Actor as Character;
			if (action.Actor == null)
			{
				actionReplays.Remove(action);
				continue;
			}
			var simultaneousEffects = GetSimultaneousActions(action, actionReplays);
			actionReplays.RemoveAll(simultaneousEffects.Contains);

            Game.Instance.RefreshSight();
            Game.Instance.PlaybackVisibleTiles.Clear();
            Game.Instance.PlaybackVisibleTiles.UnionWith(Game.Instance.PartyVisibleTiles);
            // Preserve both sides of a movement batch so entering/leaving sight animates.
            foreach (var replay in simultaneousEffects)
                replay.Action.AddDestinationSight(Game.Instance.PlaybackVisibleTiles);

			var effectsGroupedByActors = simultaneousEffects.GroupBy(x => x.Actor);

			List<IEnumerator> simulaneousEffects = effectsGroupedByActors.Select(x =>
			{
				if (x.Count() > 1)
				{
					return SequentialCoroutines.RunCoroutines(x.Select(y => y.Actor.ExecuteActionRoutine(y.Action)).ToList());
				}
				else
				{
					return x.Key.ExecuteActionRoutine(x.First().Action);
				}

			}).ToList();

			yield return SimultaneousCoroutines.RunCoroutines(simulaneousEffects);
            Game.Instance.PlaybackVisibleTiles.Clear();
		}

    }

	internal void InteruptTurn()
	{
		interuptTurn = true;
        AwaitingCommand = false;
        foreach (var hero in Game.Instance.Allies) { hero._forcedAction = null; hero.IsWaitingForPlayerInput = false; }
        Game.Instance.PlayerController.RestoreLeader();
	}

	private void CheckStats()
	{
		var player = Game.Instance.PlayerController;
		if (player.FinalStats.GetHashCode() != player.DisplayedStats.GetHashCode())
		{
			var realStats = player.FinalStats.ToDebugString();
			var displayedStats = player.DisplayedStats.ToDebugString();
			Debug.LogError($@"Stats Hash Error
real: {realStats}
disp: {displayedStats}");
		}

		if (player.Vitals.GetHashCode() != player.DisplayedVitals.GetHashCode())
		{
			var realVitals = player.Vitals.ToDebugString();
			var displayedVitals = player.DisplayedVitals.ToDebugString();
			Debug.LogError($@"Vitals Hash Error
real: {realVitals}
disp: {displayedVitals}");
		}
	}

	private class ActorAction
	{
		public ActorAction(Actor actor, GameAction action)
		{
			Actor = actor;
			Action = action;
		}

		public Actor Actor { get; }
		public GameAction Action { get; }
	}

	private List<ActorAction> GetSimultaneousActions(ActorAction action, List<ActorAction> actions)
	{
		return actions.TakeWhile(x => x.Action.CanBeCombined(action.Action)).ToList();
	}
}

public enum TurnPhase
{
	Player,
	Enemy
}

public interface Actor
{
	bool IsWaitingForPlayerInput { get; }
	List<GameAction> GetDeterminedAction();
	void DetermineAction();
	List<GameAction> ExecuteActionImmediate(GameAction action);
	IEnumerator ExecuteActionRoutine(GameAction action);
	IEnumerable<GameAction> GetResponseTo(GameAction sideEffectAction);
	void StartTurn();
	void TickStatusEffects();
	List<GameAction> GetStatusEffectSideEffects();
	List<GameAction> GetTrapSideEffects();
	List<GameAction> GetInteractableSideEffects();

    int ActionsLeft { get; }
	string CharacterName { get; }
}

[System.Serializable]
public abstract class GameAction
{
	protected GameAction() { }
	abstract internal bool IsValid(Character character);
	abstract internal IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false);
	abstract internal List<GameAction> ExecuteImmediate(Character character);
    internal virtual void RecordOutcome(Character character) { }

	virtual internal bool CanBeCombined(GameAction action)
	{
		return action == this;
	}

    internal virtual void AddDestinationSight(HashSet<Vector3Int> tiles) { }

    protected void AddAllySight(HashSet<Vector3Int> tiles, Character character, Vector3Int destination)
    {
        if (character is Ally && character.DisplayedVitals.HP > 0)
            tiles.UnionWith(Game.Instance.CurrentDungeon.GetVisibleTiles(character, destination));
    }

    private readonly HashSet<Character> animationTargets = new();

    protected void TrackAnimationTarget(Character target)
    { animationTargets.Add(target); playback?.Targets.Add(target); }

    internal virtual IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        var dungeon = Game.Instance.CurrentDungeon;
        foreach (var cell in Character.ToBounds(actor.FootPrint, dungeon.WorldToCell(actor.transform.position)).allPositionsWithin)
            yield return cell;
        foreach (var target in animationTargets)
        {
            if (target == null) continue;
            foreach (var cell in Character.ToBounds(target.FootPrint, dungeon.WorldToCell(target.transform.position)).allPositionsWithin)
                yield return cell;
        }
    }

    private DungeonActionPlayback playback;
    internal void SetPlaybackContext(DungeonActionPlayback context)
    {
        playback = context;
        playback.Targets.UnionWith(animationTargets);
    }
    internal void SetPlaybackContext(DungeonAnimationMode mode, bool commanded, Character focus, Character origin)
        => SetPlaybackContext(new DungeonActionPlayback(commanded, focus, origin, mode));

    internal bool ShouldAnimate(Character actor)
    {
        var game = Game.Instance;
        var mode = playback?.Mode ?? DungeonPreferences.AnimationMode;
        var focus = playback != null ? playback.Focus : game.PlayerController.ControlledAlly;
        if (mode == DungeonAnimationMode.YourActionOnly && playback?.Commanded != true) return false;
        if (mode == DungeonAnimationMode.ControllingHero && actor != focus && playback?.Origin != focus &&
            !animationTargets.Contains(focus) && playback?.Targets.Contains(focus) != true) return false;
        if (actor == game.PlayerController.ControlledAlly ||
            animationTargets.Contains(game.PlayerController.ControlledAlly)) return true;
        game.RefreshSight();
        var camera = game.PlayerController.CameraController?.Camera;
        if (camera == null) return true;
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        float size = game.CurrentDungeon.CellToWorld(Vector3Int.right).x;
        foreach (var cell in AnimationCells(actor))
        {
            if (!game.PartyVisibleTiles.Contains(cell) && !game.PlaybackVisibleTiles.Contains(cell)) continue;
            // Include the tile's model volume, not just its ground anchor.
            var bounds = new Bounds(game.CurrentDungeon.CellToWorld(cell) + new Vector3(size / 2, size / 2, 0),
                new Vector3(size * 2, size * 2, size * 3));
            if (GeometryUtility.TestPlanesAABB(planes, bounds)) return true;
        }
        return false;
    }

    protected IEnumerable<Vector3Int> AnimationPath(Character actor, Vector3Int destination)
    {
        var origin = Game.Instance.CurrentDungeon.WorldToCell(actor.transform.position);
        int steps = Mathf.Max(Mathf.Abs(destination.x - origin.x), Mathf.Abs(destination.y - origin.y));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            yield return new Vector3Int(Mathf.RoundToInt(Mathf.Lerp(origin.x, destination.x, t)),
                Mathf.RoundToInt(Mathf.Lerp(origin.y, destination.y, t)), 0);
        }
    }

	//immediately applied to realstats, enques change to displayed stats
	public void AddMetricsModification(Character target, Action<Stats, Vitals> metricModification)
	{
		TrackAnimationTarget(target);
		metricModification?.Invoke(target.BaseStats, target.Vitals);
		Action applyToDisplayedStats = () => metricModification?.Invoke(target.DisplayedStats, target.DisplayedVitals);
		MetricsModifications.Add(applyToDisplayedStats);
	}

	private List<Action> MetricsModifications = new();

	internal void UpdateDisplayedStats()
	{
		MetricsModifications.ForEach(x => x.Invoke());
	}

	//this is a temp fix
	internal virtual GameAction AsTargetedSkill(Character caster, Character target) { return this; }

	// Rank-aware variant used by Skill.GetEffects. Effects that scale with rank override this one.
	internal virtual GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) => AsTargetedSkill(caster, target);
}

// One policy snapshot per root action, shared by its complete consequence chain.
// Capture at playback so changing speed applies at the next action boundary.
internal sealed class DungeonActionPlayback
{
    private DungeonAnimationMode? mode;
    internal DungeonAnimationMode Mode => mode ??= DungeonPreferences.AnimationMode;
    internal readonly bool Commanded;
    internal readonly Character Focus, Origin;
    internal readonly HashSet<Character> Targets = new();
    internal DungeonActionPlayback(bool commanded, Character focus, Character origin, DungeonAnimationMode? mode = null)
    { Commanded = commanded; Focus = focus; Origin = origin; this.mode = mode; }
}
