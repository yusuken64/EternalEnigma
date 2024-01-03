using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
	public SimultaneousCoroutines SimultaneousCoroutines;
	public SequentialCoroutines SequentialCoroutines;
	public TurnPhase CurrentTurnPhase { get; internal set; }

	private void Start()
	{
		SimultaneousCoroutines = new SimultaneousCoroutines(this);
		SequentialCoroutines = new SequentialCoroutines(this);
	}

	//player is assumed to be the first actor
	public void ProcessTurn(List<Actor> actors)
	{
		StartCoroutine(ProcessTurnRoutine(actors));
	}

	private IEnumerator ProcessTurnRoutine(List<Actor> actors)
	{
		CurrentTurnPhase = TurnPhase.Enemy;
		List<ActorAction> actionReplays = new ();

		foreach (var actor in actors)
		{
			int actions = actor.ActionsPerTurn;
			for (int i = 0; i < actions; i++)
			{
				var primaryAction = actor?.DetermineActions();
				if (primaryAction == null) { continue; }
				List<GameAction> sideEffectActions = primaryAction;

				while (sideEffectActions.Any())
				{
					var sideEffectAction = sideEffectActions.First();
					sideEffectActions.Remove(sideEffectAction);
					actionReplays.Add(new ActorAction(actor, sideEffectAction));

					sideEffectActions.AddRange(actor.ExecuteActionImmediate(sideEffectAction));
				}
			}
		}

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
		}

		if (Game.Instance.DeadUnits.Contains(Game.Instance.PlayerCharacter))
		{
			Game.Instance.ShowGameOver();
		}

		foreach(var deadUnit in Game.Instance.DeadUnits)
		{
			Destroy(deadUnit.gameObject);
		}
		Game.Instance.DeadUnits.Clear();

		foreach (var actor in actors)
		{
			actor.StartTurn();
		}
		CurrentTurnPhase = TurnPhase.Player;

		CheckStats();
	}

	private void CheckStats()
	{
		var player = Game.Instance.PlayerCharacter;
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
	List<GameAction> DetermineActions();
	List<GameAction> ExecuteActionImmediate(GameAction action);
	IEnumerator ExecuteActionRoutine(GameAction action);
	void StartTurn();
	int ActionsPerTurn { get; }
	int AttacksPerTurn { get; }
}

public abstract class GameAction
{
	abstract internal bool IsValid(Character character);
	abstract internal IEnumerator ExecuteRoutine(Character character);
	abstract internal List<GameAction> ExecuteImmediate(Character character);

	virtual internal bool CanBeCombined(GameAction action)
	{
		return action == this;
	}

	//immediately applied to realstats, enques change to displayed stats
	public void AddMetricsModification(Character target, Action<Stats, Vitals> metricModification)
	{
		metricModification?.Invoke(target.BaseStats, target.Vitals);
		Action applyToDisplayedStats = () => metricModification?.Invoke(target.DisplayedStats, target.DisplayedVitals);
		MetricsModifications.Add(applyToDisplayedStats);
	}

	private List<Action> MetricsModifications = new();

	internal void UpdateDisplayedStats()
	{
		MetricsModifications.ForEach(x => x.Invoke());
	}
}