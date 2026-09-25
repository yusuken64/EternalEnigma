using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class AllySkillPolicy : PolicyBase
{
	public static IReadOnlyList<IAllySkillEvaluator> CreateDefaultEvaluators() => new IAllySkillEvaluator[]
	{
		new ReviveEvaluator(), new EmergencyHealEvaluator(), new CureEvaluator(), new BuffUpkeepEvaluator(),
		new CrowdControlEvaluator(), new DamageEvaluator(), new UtilityEvaluator()
	};

	// Raised once per AI cast (autoplay reports and tests subscribe).
	public static event Action<Ally, AllySkillChoice> Cast;

	public bool Enabled = true;
	public bool IncludeControlledAlly;
	public AllySkillChoice LastChoice { get; private set; }

	private readonly Ally ally;
	private readonly IReadOnlyList<IAllySkillEvaluator> evaluators;
	private AllySkillChoice pending;
	private GameAction pendingAction;

	public AllySkillPolicy(Game game, Ally ally, int priority, IReadOnlyList<IAllySkillEvaluator> evaluators = null)
		: base(game, ally, priority)
	{
		this.ally = ally;
		this.evaluators = evaluators ?? CreateDefaultEvaluators();
	}

	public AllySkillChoice Decide()
	{
		if (game == null || ally == null || ally.Skills == null || ally.Skills.Count == 0)
		{
			return null;
		}

		var context = AllySkillContext.Build(ally, game);
		if (context.Castable.Count == 0)
		{
			return null;
		}

		foreach (var evaluator in evaluators)
		{
			var choice = evaluator?.Evaluate(context);
			if (choice != null)
			{
				return choice;
			}
		}

		return null;
	}

	public override bool ShouldRun()
	{
		pending = null;
		pendingAction = null;

		if (!Enabled || ally == null || ally.Vitals.HP <= 0 || ally.IsDowned)
		{
			return false;
		}

		if (!IncludeControlledAlly && game?.PlayerController != null && game.PlayerController.ControlledAlly == ally)
		{
			return false;
		}

		if (ally.StatusEffects.Any(s => s != null && !s.IsExpired() && s.PreventsMenu()))
		{
			return false;
		}

		try
		{
			pending = Decide();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			pending = null;
		}

		if (pending == null)
		{
			return false;
		}

		pendingAction = pending.ToAction(ally);
		if (pendingAction == null || !pendingAction.IsValid(ally))
		{
			pending = null;
			pendingAction = null;
			return false;
		}

		LastChoice = pending;
		return true;
	}

	public override List<GameAction> GetActions()
	{
		if (pendingAction == null)
		{
			return new List<GameAction>();
		}

		// Face the target
		var option = pending.Option;
		if (option.Direction.HasValue)
		{
			ally.SetFacing(Character.GetFacing(option.Direction.Value));
		}
		else if (option.Target != null && option.Target != ally)
		{
			ally.SetFacingByTargetPosition(option.Target.TilemapPosition);
		}

		Cast?.Invoke(ally, pending);

		var result = new List<GameAction> { pendingAction };
		pending = null;
		pendingAction = null;
		return result;
	}
}
