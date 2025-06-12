
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Ally : Character
{
	public HeroAnimator HeroAnimator;
	internal Interactable currentInteractable;
	public AllyStrategy AllyStrategy;
    private AllyAttackPolicy AllyAttackPolicy;
	private AllyPursuitPolicy PursuitPolicy;
	private WanderPolicy WanderPolicy;
	public override bool IsWaitingForPlayerInput { get; set; }
	private void Start()
	{
		AllyAttackPolicy = new AllyAttackPolicy(Game.Instance, this, 1);
		PursuitPolicy = new AllyPursuitPolicy(Game.Instance, this, 2);
		WanderPolicy = new WanderPolicy(Game.Instance, this, 3);
	}

    public override void DetermineAction()
	{
		if (Vitals.HP <= 0)
		{
			determinedActions = new();
			return;
		}
		
		var actionOverrides = StatusEffects.Select(x => x.GetActionOverride(this))
			.Where(x => x != null);
		if (actionOverrides.Any())
		{
			determinedActions = actionOverrides.ToList();
			return;
		}

		//set by player input
		if (_forcedAction != null)
		{
			//do action
			determinedActions = new List<GameAction>()
			{
				_forcedAction
			};
			_forcedAction = null;
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

		BoundsInt visionBounds = game.CurrentDungeon.GetVisionBounds(this, TilemapPosition);

		List<Character> pursuitTargets = new List<Character>();
		
		if (AllyStrategy == AllyStrategy.Follow)
		{
			pursuitTargets.Add(game.PlayerController.ControlledAlly);
		}
		else if (AllyStrategy == AllyStrategy.Aggresive)
		{
			pursuitTargets.Add(game.PlayerController.ControlledAlly);
			pursuitTargets.AddRange(game.Enemies);
		}

		return pursuitTargets
			.OrderBy(x => x == game.PlayerController)
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

	public override List<GameAction> GetInteractableSideEffects()
	{
        if (currentInteractable is not Trap and not null)
        {
			if (currentInteractable is not Stairs)
			{
				//Game.Instance.DoFloatingText(currentInteractable.GetInteractionText(), Color.yellow, this.VisualParent.transform.position);
				var effects = currentInteractable.GetInteractionSideEffects(this);
				currentInteractable = null;
				return effects;
			}
        }

        return new();
    }

	public override void StartTurn()
	{
		determinedActions.Clear();
		Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
		Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;

		SyncDisplayedStats();
		_forcedAction = null;
	}

	internal override void PlayWalkAnimation()
	{
		HeroAnimator?.PlayWalkAnimation();
	}

	internal override void PlayIdleAnimation()
	{
		HeroAnimator?.PlayIdleAnimation();
	}

	internal override void PlayAttackAnimation()
	{
		HeroAnimator?.PlayAttackAnimation();
	}

	internal override void PlayTakeDamageAnimation()
	{
		HeroAnimator?.PlayTakeDamageAnimation();
	}

	internal override void PlayDeathAnimation()
	{
		HeroAnimator?.PlayDeathAnimation();
	}

	internal void InitialzeModel(OverworldAlly overworldAlly)
	{
		var heroAnimator = overworldAlly.GetComponent<HeroAnimator>();
		var newHeroAnimator = this.gameObject.AddComponent<HeroAnimator>();
		heroAnimator.CopyFieldsTo(newHeroAnimator);
		this.HeroAnimator = newHeroAnimator;
		this.HeroAnimator.Animator.applyRootMotion = false;

		ReplaceChildGameObject(this.gameObject, "GameObject/RPGHeroHP", overworldAlly.AnimatedModel);
	}

	private void ReplaceChildGameObject(GameObject gameObject, string childPath, GameObject animatedModel)
	{
		Transform targetTransform = gameObject.transform.Find(childPath);
		ReplaceModel(targetTransform.gameObject, animatedModel);

		Destroy(targetTransform.gameObject);
	}

	public void ReplaceModel(GameObject oldChild, GameObject newChild)
	{
		// Replace the old child with the new GameObject
		newChild.transform.parent = oldChild.transform.parent;
		newChild.transform.localPosition = oldChild.transform.localPosition;
		newChild.transform.localRotation = oldChild.transform.localRotation;
		newChild.transform.localScale = oldChild.transform.localScale;
		newChild.transform.SetAsFirstSibling();
	}

	public override void Inventory_HandleInventoryChanged()
	{
		base.Inventory_HandleInventoryChanged();

		HeroAnimator.SetWeapon(
			Equipment.EquipedWeapon?.ItemDefinition as EquipmentItemDefinition,
			Equipment.EquipedShield?.ItemDefinition as EquipmentItemDefinition);
	}

	public override void Equipment_HandleEquipmentChanged(EquipChangeType equipChangeType, EquipableInventoryItem item)
	{
		base.Equipment_HandleEquipmentChanged(equipChangeType, item);

		switch (equipChangeType)
		{
			case EquipChangeType.Equip:
				Game.Instance.PlayerController.Inventory.InventoryItems.Remove(item);
				break;
			case EquipChangeType.UnEquip:
				Game.Instance.PlayerController.Inventory.InventoryItems.Add(item);
				break;
		}

		HeroAnimator?.SetWeapon(
			Equipment.EquipedWeapon?.ItemDefinition as EquipmentItemDefinition,
			Equipment.EquipedShield?.ItemDefinition as EquipmentItemDefinition);
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
		var attackBounds =_ally.GetAttackBounds();
		target = Game.Instance.AllCharacters
			.Where(x => x != null)
			.Where(x => x.Team != _ally.Team)
			.Where(x => attackBounds.Overlaps2D(x.ToBounds()))
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