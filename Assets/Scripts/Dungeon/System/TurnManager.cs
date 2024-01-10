using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
	public SimultaneousCoroutines SimultaneousCoroutines;
	public SequentialCoroutines SequentialCoroutines;
	private bool interuptTurn = false; //this happens when the stairs are taken

	private void Start()
	{
		SimultaneousCoroutines = new SimultaneousCoroutines(this);
		SequentialCoroutines = new SequentialCoroutines(this);
	}

	//player is assumed to be the first actor
	public void ProcessTurn()
	{
		interuptTurn = false;
		StartCoroutine(ProcessTurnRoutine());
	}

	private IEnumerator ProcessTurnRoutine()
	{
		while (!interuptTurn)
		{
			List<Actor> actors = new();
			actors.Add(Game.Instance.PlayerCharacter);
			actors.AddRange(Game.Instance.Allies);
			actors.AddRange(Game.Instance.Enemies);

			interuptTurn = false;
			List<ActorAction> actionReplays = new();

			foreach (var actor in actors)
			{
				actor.TickStatusEffects();
			}

			foreach (var actor in actors)
			{
				while (actor.ActionsLeft > 0)
				{
					while (actor.IsBusy)
					{
						yield return null;
					}
					actor?.DetermineAction();
					var primaryAction = actor?.GetDeterminedAction();
					if (primaryAction == null) { continue; }
					List<GameAction> gameActions = primaryAction;

					while (gameActions.Any())
					{
						var sideEffectAction = gameActions.First();
						gameActions.Remove(sideEffectAction);
						if (sideEffectAction == null) { continue; }

						gameActions.AddRange(actor.ExecuteActionImmediate(sideEffectAction));
						actionReplays.Add(new ActorAction(actor, sideEffectAction));

						if (interuptTurn)
						{
							break;
						}

						foreach (var actor2 in actors)
						{
							gameActions.AddRange(actor2.GetResponseTo(sideEffectAction));
						}
					}

					if (interuptTurn)
					{
						break;
					}
				}

				if (interuptTurn)
				{
					break;
				}

				var statusSideEffectActions = actor.GetStatusEffectSideEffects();

				while (statusSideEffectActions.Any())
				{
					var sideEffectAction = statusSideEffectActions.First();
					statusSideEffectActions.Remove(sideEffectAction);
					actionReplays.Add(new ActorAction(actor, sideEffectAction));

					statusSideEffectActions.AddRange(actor.ExecuteActionImmediate(sideEffectAction));
				}

				var trapSideEffects = actor.GetTrapSideEffects();
				while (trapSideEffects.Any())
				{
					var trapSideEffect = trapSideEffects.First();
					trapSideEffects.Remove(trapSideEffect);
					actionReplays.Add(new ActorAction(actor, trapSideEffect));

					statusSideEffectActions.AddRange(actor.ExecuteActionImmediate(trapSideEffect));
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

			foreach (var deadUnit in Game.Instance.DeadUnits)
			{
				Destroy(deadUnit.gameObject);
			}
			Game.Instance.DeadUnits.Clear();

			foreach (var actor in actors)
			{
				actor.StartTurn();
			}

			CheckStats();
		}
	}

	internal void InteruptTurn()
	{
		interuptTurn = true;
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
	bool IsBusy { get; }
	List<GameAction> GetDeterminedAction();
	void DetermineAction();
	List<GameAction> ExecuteActionImmediate(GameAction action);
	IEnumerator ExecuteActionRoutine(GameAction action);
	IEnumerable<GameAction> GetResponseTo(GameAction sideEffectAction);
	void StartTurn();
	void TickStatusEffects();
	List<GameAction> GetStatusEffectSideEffects();
	List<GameAction> GetTrapSideEffects();

	int ActionsLeft { get; }
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