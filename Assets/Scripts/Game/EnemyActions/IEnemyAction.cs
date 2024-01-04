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
	internal readonly Enemy enemy;
	public readonly int priority;

	public PolicyBase(Game game, Enemy enemy, int priority)
	{
		this.game = game;
		this.enemy = enemy;
		this.priority = priority;
	}

	public abstract List<GameAction> GetActions();
	public abstract bool ShouldRun();
}

public class AttackPolicy : PolicyBase
{
	public AttackPolicy(Game game, Enemy enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
{
		enemy.SetFacingByTargetPosition(game.PlayerCharacter.TilemapPosition); //TODO add this to action?
		return new List<GameAction>() { new AttackAction(enemy, enemy.TilemapPosition, game.PlayerCharacter.TilemapPosition) };
	}

	public override bool ShouldRun()
	{
		//if will attack attack
		if (WillAttack(enemy) &&
			CanAttack(game, game.PlayerCharacter, enemy))
		{
			return true;
		}

		return false;
	}
	private bool WillAttack(Enemy enemy)
	{
		return
			enemy.attacksPerTurnLeft > 0
			&&	(enemy.CurrentEnemyState == EnemyState.Idle ||
				enemy.CurrentEnemyState == EnemyState.Wander ||
				enemy.CurrentEnemyState == EnemyState.Pursuit);
	}

	public static bool CanAttack(Game game, Player playerCharacter, Enemy enemy)
	{
		if (enemy.attacksPerTurnLeft <= 0)
		{
			return false;
		}

		var direction = playerCharacter.TilemapPosition - enemy.TilemapPosition;
		var attackFacing = enemy.GetFacing(direction);
		var validAttackDirections = game.CurrentDungeon.GetValidAttackDirections(enemy.TilemapPosition);

		if (!validAttackDirections.Contains(attackFacing))
		{
			return false;
		}

		var x1 = playerCharacter.TilemapPosition.x;
		var y1 = playerCharacter.TilemapPosition.y;
		var x2 = enemy.TilemapPosition.x;
		var y2 = enemy.TilemapPosition.y;
		//target is within attack distance
		var chebyshevDistance = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));

		return chebyshevDistance <= 1;
	}
}

public class PursuitPolicy : PolicyBase
{
	private List<AStar.Node> path;

	public PursuitPolicy(Game game, Enemy enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
	{
		var newMapPosition = new Vector3Int(path[0].X, path[0].Y);
		enemy.SetFacingByTargetPosition(newMapPosition);
		return new List<GameAction>() { new MovementAction(enemy, enemy.TilemapPosition, newMapPosition) };
	}

	public override bool ShouldRun()
	{
		if(enemy.PursuitPosition == null) { return false; }

		path = enemy.CalculatePursuitPath();

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
	public WanderPolicy(Game game, Enemy enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
	{
		var offset = Dungeon.GetFacingOffset(enemy.CurrentFacing);
		Vector3Int newMapPosition = enemy.TilemapPosition + offset;
		var canMoveForward = game.CurrentDungeon.TileMap_Floor.GetTile(newMapPosition) != null;

		if (!canMoveForward)
		{
			var validWalkDirections = game.CurrentDungeon.GetValidWalkDirections(enemy.TilemapPosition);

			if (validWalkDirections.Any())
			{
				enemy.CurrentFacing = validWalkDirections[UnityEngine.Random.Range(0, validWalkDirections.Count())];
				offset = Dungeon.GetFacingOffset(enemy.CurrentFacing);
				newMapPosition = enemy.TilemapPosition + offset;
			}
		}
		enemy.SetFacingByTargetPosition(newMapPosition);
		return new List<GameAction>() { new MovementAction(enemy, enemy.TilemapPosition, newMapPosition) };
	}

	public override bool ShouldRun()
	{
		return enemy.CurrentEnemyState == EnemyState.Wander;
	}
}