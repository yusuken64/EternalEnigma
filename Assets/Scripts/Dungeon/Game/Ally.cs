using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Ally : Character
{
	public Animator HeroAnimator;
	internal Interactable currentInteractable;
	public AllyStrategy AllyStrategy;
	private GameAction _forcedAction;

	private AllyAttackPolicy AllyAttackPolicy;
	private PursuitPolicy PursuitPolicy;

	private void Start()
	{
		AllyAttackPolicy = new AllyAttackPolicy(Game.Instance, this, 1);
		PursuitPolicy = new PursuitPolicy(Game.Instance, this, 2);
	}

	public void SetAction(GameAction forcedAction)
	{
		_forcedAction = forcedAction;
	}

	public override List<GameAction> DetermineActions()
	{
		if (Vitals.HP <= 0)
		{
			return null;
		}
		
		//this should affect player the same way, to do in characerbase class?
		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
			.Where(x => x != null);
		if (actionOverrides.Any())
		{
			return actionOverrides.ToList();
		}

		if (_forcedAction != null)
		{
			//do action
			return new List<GameAction>()
			{
				_forcedAction
			};
		}

		if (AllyAttackPolicy.ShouldRun())
		{
			return AllyAttackPolicy.GetActions();
		}

		PursuitPosition = Game.Instance.PlayerCharacter.TilemapPosition;
		if (PursuitPolicy.ShouldRun())
		{
			return PursuitPolicy.GetActions();
		}

		switch (AllyStrategy)
		{
			case AllyStrategy.Passive:
				//follow player
				PursuitPosition = Game.Instance.PlayerCharacter.TilemapPosition;
				break;
			case AllyStrategy.Aggresive:
				//if can see enemy, walk toward them
				//else follow player
				break;
		}

		return new List<GameAction>()
		{
			new WaitAction()
		};
	}

	public override List<GameAction> ExecuteActionImmediate(GameAction action)
	{
		if (GetActionInterupt(action))
		{
			return new();
		}

		var sideEffects = action.ExecuteImmediate(this);
		var actionResponses = GetActionResponses(action);
		var actionResponseEffects = actionResponses.SelectMany(x => x.ExecuteImmediate(this));
		sideEffects.AddRange(actionResponseEffects);

		return sideEffects;
	}

	public override IEnumerator ExecuteActionRoutine(GameAction action)
	{
		if (this == null) { yield break; }

		yield return StartCoroutine(action.ExecuteRoutine(this));
		action.UpdateDisplayedStats();
	}

	public override IEnumerable<GameAction> GetResponseTo(GameAction action)
	{
		if (this == null ||
			this.Vitals.HP <= 0)
		{
			return new List<GameAction>();
		}
		return GetActionResponses(action);
	}

	public override List<GameAction> GetTrapSideEffects()
	{
		if (currentInteractable is Trap trap)
		{
			currentInteractable = null;
			Game.Instance.DoFloatingText(trap.GetInteractionText(), Color.yellow, this.VisualParent.transform.position);
			return trap.GetTrapSideEffects(this);
		}

		return new();
	}

	public override void StartTurn()
	{
		Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
		Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

		SyncDisplayedStats();
		_forcedAction = null;
	}

	internal override void PlayWalkAnimation()
	{
		HeroAnimator.Play("Walk_SwordShield", 0);
		HeroAnimator.speed = 5f;
	}

	internal override void PlayIdleAnimation()
	{
		HeroAnimator.Play("Idle_SwordShield", 0);
	}

	internal override void PlayAttackAnimation()
	{
		var attack1 = "NormalAttack01_SwordShield";
		var attack2 = "NormalAttack02_SwordShield";

		var attack = UnityEngine.Random.value > 0.5f ? attack1 : attack2;
		HeroAnimator.Play(attack, 0, 0f);
	}

	internal override void PlayTakeDamageAnimation()
	{
		HeroAnimator.Play("GetHit_SwordShield", 0);
	}

	internal override void PlayDeathAnimation()
	{
		HeroAnimator.Play("Die_SwordShield", 0);
	}
}

internal class AllyAttackPolicy : PolicyBase
{
	private readonly Ally _ally;
	private Character target;

	public AllyAttackPolicy(Game game, Character character, int priority) : base(game, character, priority)
	{
		_ally = character as Ally;
	}

	public override List<GameAction> GetActions()
	{
		character.SetFacingByTargetPosition(target.TilemapPosition);
		return new List<GameAction>() { new AttackAction(_ally, _ally.TilemapPosition, target.TilemapPosition) };
	}

	public override bool ShouldRun()
	{
		var neighborhoodTiles = Game.Instance.CurrentDungeon.GetWalkableNeighborhoodTiles(_ally.TilemapPosition);
		target = neighborhoodTiles.Select(x => Game.Instance.AllCharacters.FirstOrDefault(y => y.TilemapPosition == x))
			.Where(x => x != null)
			.Where(x => x.Team != _ally.Team)
			.FirstOrDefault();

		return target != null;
	}
}

public enum AllyStrategy
{
	Passive,
	Aggresive
}