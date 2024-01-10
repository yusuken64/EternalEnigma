using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IPolicy
{
	public bool ShouldRun();
	public List<GameAction> GetActions();
}

public abstract class PolicyBase : IPolicy
{
	internal readonly Game game;
	internal readonly Character character;
	public readonly int priority;

	public PolicyBase(Game game, Character character, int priority)
	{
		this.game = game;
		this.character = character;
		this.priority = priority;
	}

	public abstract List<GameAction> GetActions();
	public abstract bool ShouldRun();
}

public class AttackPolicy : PolicyBase
{
	public AttackPolicy(Game game, Character enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
{
		character.SetFacingByTargetPosition(character.PursuitTarget.TilemapPosition); //TODO add this to action?
		return new List<GameAction>() { new AttackAction(character, character.TilemapPosition, character.PursuitTarget.TilemapPosition) };
	}

	public override bool ShouldRun()
	{
		if (character.PursuitTarget == null)
		{
			return false;
		}

		//if will attack attack
		if (WillAttack(character) &&
			CanAttack(game, character.PursuitTarget, character))
		{
			return true;
		}

		return false;
	}
	private bool WillAttack(Character character)
	{
		var attacksLeft = character.Vitals.AttacksPerTurnLeft > 0;
		bool attackStrategy = false;

		if (character is Enemy enemy)
		{
			attackStrategy =
				enemy.CurrentEnemyState == EnemyState.Idle ||
				enemy.CurrentEnemyState == EnemyState.Wander ||
				enemy.CurrentEnemyState == EnemyState.Pursuit;
		}
		else if (character is Ally ally)
		{
			attackStrategy = true;
		}

		return attacksLeft && attackStrategy;	
	}

	public static bool CanAttack(Game game, Character targetCharacter, Character character)
	{
		if (character.Vitals.AttacksPerTurnLeft <= 0)
		{
			return false;
		}

		var direction = targetCharacter.TilemapPosition - character.TilemapPosition;
		var attackFacing = character.GetFacing(direction);
		var validAttackDirections = game.CurrentDungeon.GetValidAttackDirections(character.TilemapPosition);

		if (!validAttackDirections.Contains(attackFacing))
		{
			return false;
		}

		var attackBounds = character.GetAttackBounds();
		var targetBounds = targetCharacter.ToBounds();

		return attackBounds.Overlaps2D(targetBounds);

		//var x1 = targetCharacter.TilemapPosition.x;
		//var y1 = targetCharacter.TilemapPosition.y;
		//var x2 = character.TilemapPosition.x;
		//var y2 = character.TilemapPosition.y;
		////target is within attack distance
		//var chebyshevDistance = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));

		//return chebyshevDistance <= 1;
	}
}

public class PursuitPolicy : PolicyBase
{
	private List<AStar.Node> path;

	public PursuitPolicy(Game game, Character enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
	{
		var newMapPosition = new Vector3Int(path[0].X, path[0].Y);
		character.SetFacingByTargetPosition(newMapPosition);
		return new List<GameAction>() { new MovementAction(character, character.TilemapPosition, newMapPosition) };
	}

	public override bool ShouldRun()
	{
		if(character.PursuitPosition == null) { return false; }

		path = character.CalculatePursuitPath();

		if (path != null &&
			path.Count > 0)
		{
			return true;
		}

		return false;
	}
}
public class WanderPolicy : PolicyBase
{
	public WanderPolicy(Game game, Character enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
	{
		var offset = Dungeon.GetFacingOffset(character.CurrentFacing);
		Vector3Int newMapPosition = character.TilemapPosition + offset;
		var canMoveForward = game.CurrentDungeon.CanWalkTo(character.TilemapPosition, newMapPosition);

		if (!canMoveForward)
		{
			var validWalkDirections = game.CurrentDungeon.GetValidWalkDirections(character.TilemapPosition);

			if (validWalkDirections.Any())
			{
				character.CurrentFacing = validWalkDirections[UnityEngine.Random.Range(0, validWalkDirections.Count())];
				offset = Dungeon.GetFacingOffset(character.CurrentFacing);
				newMapPosition = character.TilemapPosition + offset;
			}
		}
		character.SetFacingByTargetPosition(newMapPosition);
		return new List<GameAction>() { new MovementAction(character, character.TilemapPosition, newMapPosition) };
	}

	public override bool ShouldRun()
	{
		if (character is Enemy enemy)
		{
			return enemy.CurrentEnemyState == EnemyState.Wander;
		}
		if (character is Ally ally)
		{
			return ally.AllyStrategy != AllyStrategy.HoldPosition;
		}

		return true;
	}
}