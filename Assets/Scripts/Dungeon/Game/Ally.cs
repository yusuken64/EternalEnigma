
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Ally : Character
{
	public List<Skill> Skills;

	public Animator HeroAnimator;
	internal Interactable currentInteractable;
	public AllyStrategy AllyStrategy;
	private GameAction _forcedAction;

	private AllyAttackPolicy AllyAttackPolicy;
	private AllyPursuitPolicy PursuitPolicy;
	private WanderPolicy WanderPolicy;
	public override bool IsBusy => false;
	private List<GameAction> determinedActions;
	private void Start()
	{
		AllyAttackPolicy = new AllyAttackPolicy(Game.Instance, this, 1);
		PursuitPolicy = new AllyPursuitPolicy(Game.Instance, this, 2);
		WanderPolicy = new WanderPolicy(Game.Instance, this, 3);
	}

	public void SetAction(GameAction forcedAction)
	{
		_forcedAction = forcedAction;
	}

	public override void DetermineAction()
	{
		if (Vitals.HP <= 0)
		{
			determinedActions = new();
			return;
		}
		
		//this should affect player the same way, to do in characerbase class?
		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
			.Where(x => x != null);
		if (actionOverrides.Any())
		{
			determinedActions = actionOverrides.ToList();
			return;
		}

		if (_forcedAction != null)
		{
			//do action
			determinedActions = new List<GameAction>()
			{
				_forcedAction
			};
			return;
		}

		if (AllyAttackPolicy.ShouldRun())
		{
			determinedActions =  AllyAttackPolicy.GetActions();
			return;
		}

		PursuitTarget = GetTarget();
		if (PursuitTarget != null)
		{
			PursuitPosition = PursuitTarget.TilemapPosition;
		}
		if (PursuitPolicy.ShouldRun())
		{
			determinedActions = PursuitPolicy.GetActions();
			return;
		}
		if (WanderPolicy.ShouldRun())
		{
			determinedActions = WanderPolicy.GetActions();
			return;
		}

		determinedActions = new List<GameAction>()
		{
			new WaitAction()
		};
	}

	private Character GetTarget()
	{
		var game = Game.Instance;

		BoundsInt visionBounds = game.CurrentDungeon.GetVisionBounds(TilemapPosition);

		List<Character> pursuitTargets = new List<Character>();
		
		if (AllyStrategy == AllyStrategy.Follow)
		{
			pursuitTargets.Add(game.PlayerCharacter);
		}
		else if (AllyStrategy == AllyStrategy.Aggresive)
		{
			pursuitTargets.Add(game.PlayerCharacter);
			pursuitTargets.AddRange(game.Enemies);
		}

		return pursuitTargets
			.OrderBy(x => x == game.PlayerCharacter)
			.ThenBy(x => TileWorldDungeon.ChevDistance(x.TilemapPosition, TilemapPosition))
			.ThenBy(x => x.TilemapPosition == PursuitPosition)
			.FirstOrDefault(x => Enemy.Contains2D(visionBounds, x.TilemapPosition));
	}

	public override List<GameAction> GetDeterminedAction()
	{
		this.Vitals.ActionsPerTurnLeft--;
		this.DisplayedVitals.ActionsPerTurnLeft--;
		return determinedActions;
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
		if (action is MovementAction movementAction)
		{
			var target = GetTarget();
			if (target != null)
			{
				PursuitPosition = target.TilemapPosition;
			}
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
		determinedActions = null;
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

public class AllyPursuitPolicy : PolicyBase
{
	private readonly Ally ally;
	private List<AStar.Node> path;

	public AllyPursuitPolicy(Game game, Ally ally, int priority) : base(game, ally as Character, priority)
	{
		this.ally = ally;
	}

	public override List<GameAction> GetActions()
	{
		var newMapPosition = new Vector3Int(path[0].X, path[0].Y);
		character.SetFacingByTargetPosition(newMapPosition);
		return new List<GameAction>() { new MovementAction(character, character.TilemapPosition, newMapPosition) };
	}

	public override bool ShouldRun()
	{
		if (ally.AllyStrategy == AllyStrategy.HoldPosition)
		{
			return false;
		}

		if (character.PursuitPosition == null) { return false; }

		path = character.CalculatePursuitPath();

		if (path != null &&
			path.Count > 0)
		{
			return true;
		}

		return false;
	}
}

public enum AllyStrategy
{
	Follow,
	Aggresive,
	HoldPosition
}