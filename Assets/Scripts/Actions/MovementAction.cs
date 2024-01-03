using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class MovementAction : GameAction
{
	private Vector3Int originalPosition;
	private Vector3Int newMapPosition;

	public MovementAction(Character character, Vector3Int originalPosition, Vector3Int newMapPosition)
	{
		this.originalPosition = originalPosition;
		this.newMapPosition = newMapPosition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var overlapTarget = Game.Instance
			.AllCharacters.Any(x => x.TilemapPosition == newMapPosition);

		if (overlapTarget)
		{
			newMapPosition = originalPosition;
		}
		character.TilemapPosition = newMapPosition;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		var worldPosition = Game.Instance.CurrentDungeon.TileMap_Floor.CellToWorld(newMapPosition);

		character.PlayWalkAnimation();
		yield return character.transform.DOMove(worldPosition, 0.1f / character.ActionsPerTurnMax)
			.WaitForCompletion();

		character.PlayIdleAnimation();
	}

	internal override bool IsValid(Character character)
	{
		var tile = Game.Instance.CurrentDungeon.TileMap_Floor.GetTile(newMapPosition);

		Game game = Game.Instance;

		var overlapping = game.AllCharacters
			.Select(x => x.TilemapPosition)
			.Any(x => x == newMapPosition);

		var canMove = tile != null &&
			!overlapping;

		return canMove;
	}

	internal override bool CanBeCombined(GameAction action)
	{
		return action is MovementAction;
	}
}

internal class AttackAction : GameAction
{
	private readonly Character attacker;
	private Vector3Int originalPosition;
	private Vector3Int newMapPosition;

	public AttackAction(Character attacker,
		Vector3Int originalPosition,
		Vector3Int newMapPosition)
	{
		this.attacker = attacker;
		this.originalPosition = originalPosition;
		this.newMapPosition = newMapPosition;
	}

	//http://000.la.coocan.jp/torneco/damage.html#attack
	/// <summary>
	/// 基本ダメージ＝攻撃力×(15/16)^防御力
	/// ダメージ＝基本ダメージ×n/128 (n=112～143の整数値乱数)の端数を切り捨てた値
	/// </summary>
	/// <param name="character"></param>
	/// <returns></returns>
	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var target = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == newMapPosition);

		if (target == null)
		{
			return new();
		}

		attacker.attacksPerTurnLeft -= 1;
		bool hit = UnityEngine.Random.value > 0.2f;
		var baseDamage = attacker.FinalStats.Strength * MathF.Pow((15f/16f), target.FinalStats.Defense);
		float n = (float)UnityEngine.Random.Range(112, 143);
		int damage = (int)MathF.Floor(baseDamage * (n / 128f));
		return new List<GameAction>()
		{
			new TakeDamageAction(attacker, target, damage, true, !hit)
		};
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		character.PlayAttackAnimation();

		yield return new WaitForSecondsRealtime(0.5f);
		character.PlayIdleAnimation();
	}

	internal override bool IsValid(Character character)
	{
		var tile = Game.Instance.CurrentDungeon.TileMap_Floor.GetTile(newMapPosition);
		var canMove = tile != null;

		return canMove;
	}
}

public class TakeDamageAction : GameAction
{
	private readonly Character attacker;
	private readonly Character target;
	private readonly int damage;
	private readonly bool doDamageAnimation;
	private readonly bool miss;

	public TakeDamageAction(Character attacker, Character target, int damage, bool doDamageAnimation = true, bool miss = false)
	{
		this.attacker = attacker;
		this.target = target;
		this.damage = damage;
		this.doDamageAnimation = doDamageAnimation;
		this.miss = miss;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!miss)
		{
			AddMetricsModification(target, (metrics, vitals) =>
			{
				vitals.HP -= damage;
			});
		}

		if (target.Vitals.HP <= 0)
		{
			return new List<GameAction>()
			{
				new DeathAction(target, attacker)
			};
		}

		return new();
	}


	internal override IEnumerator ExecuteRoutine(Character character)
	{
		Game game = Game.Instance;
		if (!miss)
		{
			game.DoFloatingText(damage.ToString(), Color.red, target.gameObject.transform.position);
		}
		else
		{
			game.DoFloatingText("miss", Color.white, target.gameObject.transform.position);
		}

		if (doDamageAnimation && !miss)
		{
			target.PlayTakeDamageAnimation();
			yield return new WaitForSecondsRealtime(0.3f);
		}
	}

	internal override bool IsValid(Character character)
	{
		return target.Vitals.HP > 0;
	}
}

public class ModifyStatAction : GameAction
{
	private readonly Character attacker;
	private readonly Character target;
	private readonly Action<Stats, Vitals> modifyAction;
	private readonly bool doDamageAnimation;

	public ModifyStatAction(Character attacker, Character target, Action<Stats, Vitals> modifyAction, bool doDamageAnimation = true)
	{
		this.attacker = attacker;
		this.target = target;
		this.modifyAction = modifyAction;
		this.doDamageAnimation = doDamageAnimation;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		AddMetricsModification(target, modifyAction);

		if (target.Vitals.HP <= 0)
		{
			return new List<GameAction>()
			{
				new DeathAction(target, attacker)
			};
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		if (doDamageAnimation)
		{
			target.PlayTakeDamageAnimation();
			yield return new WaitForSecondsRealtime(0.3f);
		}
	}

	internal override bool IsValid(Character character)
	{
		return target.Vitals.HP > 0;
	}

	internal override bool CanBeCombined(GameAction action)
	{
		return true;
	}
}

public class DeathAction : GameAction
{
	private Character target;
	private List<BFS.Node> path;
	private bool droppedItem;
	private readonly Character attacker;

	public DeathAction(Character target, Character attacker)
	{
		this.target = target;
		this.attacker = attacker;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		//TODO keep a list of all characters
		Game.Instance.Enemies.Remove(target as Enemy);
		Game.Instance.DeadUnits.Add(target);

		//Also remove all queued actions?
		var gainXP = new AddXPAction(attacker, target.FinalStats.EXPOnKill);

		float value = UnityEngine.Random.value;
		droppedItem = target.FinalStats.DropRate > 0 &&
			value < character.FinalStats.DropRate;

		//Debug.Log($"{target.RealStats.DropRate} ? {value}");

		//BFS.FindPath
		Game game = Game.Instance;
		BFS.Node[,] grid = new BFS.Node[game.DungeonGenerator.dungeonWidth, game.DungeonGenerator.dungeonHeight];

		for (int i = 0; i < game.DungeonGenerator.dungeonWidth; i++)
		{
			for (int j = 0; j < game.DungeonGenerator.dungeonHeight; j++)
			{
				var tile = game.CurrentDungeon.TileMap_Floor.GetTile(new Vector3Int(i, j));
				var isWalkable = tile != null;

				if (isWalkable)
				{
					grid[i, j] = new BFS.Node(i, j);
				}
			}
		}

		BFS.Node startNode = grid[target.TilemapPosition.x, target.TilemapPosition.y];

		path = BFS.FindPath(grid,
			startNode,
			(node) =>
			{
				Vector3Int position = new Vector3Int(node.X, node.Y);
				var tile = game.CurrentDungeon.ObjectTileMap.GetTile(position);
				return tile == null;
			});

		return new()
		{
			gainXP
		};
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		target.PlayDeathAnimation();
		yield return new WaitForSecondsRealtime(0.4f);
		target.VisualParent.gameObject.SetActive(false);

		Game game = Game.Instance;
		if (droppedItem && path != null)
		{
			var item = game.ItemManager.GetRandomDrop(target as Enemy);
			BFS.Node node = path.Last();
			Vector3Int dropPosition = new Vector3Int(node.X, node.Y);
			game.CurrentDungeon.SetDroppedItem(dropPosition, item, game.DungeonGenerator.DroppedItemTile);
		}
	}

	internal override bool IsValid(Character character)
	{
		return Game.Instance.AllCharacters.Contains(character);
	}
}

internal class AddXPAction : GameAction
{
	private Character character;
	private int eXP;

	public AddXPAction(Character character, int eXP)
	{
		this.character = character;
		this.eXP = eXP;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		this.AddMetricsModification(this.character, ((stats, vitals) =>
		{
			vitals.Exp += eXP;
		}));
		List<GameAction> ret = new();
		if (character is Player player)
		{
			var game = Game.Instance;
			var levelSystem = game.LevelSystem;

			var levelUps = levelSystem.LevelData.Where(x =>
				x.Level > player.Vitals.Level &&
				x.Experience <= player.Vitals.Exp);

			foreach(var levelUp in levelUps)
			{
				ret.Add(new LevelUpAction(character, levelUp));
			}
		}

		return ret;
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}

public class InteractAction : GameAction
{
	private InteractableTile currentInteractable;

	public InteractAction(InteractableTile currentInteractable)
	{
		this.currentInteractable = currentInteractable;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		currentInteractable.DoInteraction();
		currentInteractable = null;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();
	}

	internal override bool IsValid(Character character)
	{
		return currentInteractable != null;
	}
}

public class WaitAction : GameAction
{
	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}