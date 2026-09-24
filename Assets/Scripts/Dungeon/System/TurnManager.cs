using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
	public bool IsProcessingTurn { get; private set; }
	public SimultaneousCoroutines SimultaneousCoroutines;
	public SequentialCoroutines SequentialCoroutines;
	private bool interuptTurn = false; //this happens when the stairs are taken

	private void Start()
	{
		SimultaneousCoroutines = new SimultaneousCoroutines(this);
		SequentialCoroutines = new SequentialCoroutines(this);
	}

	public void ProcessTurn()
	{
		interuptTurn = false;
		StartCoroutine(TrackTurnRoutine());
	}

	private IEnumerator TrackTurnRoutine()
	{
		IsProcessingTurn = true;
		try { yield return ProcessTurnRoutine(); }
		finally { IsProcessingTurn = false; }
	}

	private IEnumerator ProcessTurnRoutine()
	{
		var controlledAlly = Game.Instance.PlayerController.ControlledAlly;
		var actors = new List<Actor> { controlledAlly };
		actors.AddRange(Game.Instance.Allies.Where(a => a != controlledAlly));
		actors.AddRange(Game.Instance.Enemies);

		interuptTurn = false;
		List<ActorAction> actionReplays = new();

		foreach (var actor in actors)
		{
			do
			{
				actor?.DetermineAction();
				var primaryAction = actor?.GetDeterminedAction();
				if (primaryAction == null) { continue; }
				List<GameAction> gameActions = primaryAction;

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
					gameActions.AddRange(actor.ExecuteActionImmediate(sideEffectAction));
					actionReplays.Add(new ActorAction(actor, sideEffectAction));

					if (interuptTurn)
					{
						yield break;
					}

					foreach (var actor2 in actors)
					{
						gameActions.AddRange(actor2.GetResponseTo(sideEffectAction));
						if (actor2 is Character responder && responder != null)
							gameActions.AddRange(responder.GetClassResponses(sideEffectAction));
					}
				}

				if (interuptTurn)
				{
					yield break;
				}
			} while (actor.ActionsLeft > 0);

			if (interuptTurn)
			{
				yield break;
			}
		}

		if (interuptTurn) { yield break; }

		foreach (var actor in actors)
		{
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
			actor.TickStatusEffects();
		}

		if (interuptTurn) { yield break; }

		while (actionReplays.Any())
		{
			var action = actionReplays[0];
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

		if (Game.Instance.DeadUnits.Contains(Game.Instance.PlayerController.ControlledAlly))
		{
			var allies = Game.Instance.Allies
				.Where(x => x.Vitals.HP > 0)
				.ToList();

			if (allies.Count == 0)
			{
				Game.Instance.ShowGameOver();
			}
			else
			{
				FindFirstObjectByType<PlayerController>().TakeControlNextAlly();
			}
		}

		foreach (var deadUnit in Game.Instance.DeadUnits)
		{
			Destroy(deadUnit.gameObject);
		}
		Game.Instance.DeadUnits.Clear();

		Game.Instance.PlayerController.StartTurn();
		foreach (var actor in actors)
		{
			actor.StartTurn();
		}

		CheckStats();
	}

	internal void InteruptTurn()
	{
		interuptTurn = true;
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

    protected void TrackAnimationTarget(Character target) => animationTargets.Add(target);

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

    internal bool ShouldAnimate(Character actor)
    {
        var game = Game.Instance;
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
		animationTargets.Add(target);
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
