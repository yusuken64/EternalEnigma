using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class MovementAction : GameAction
{
	private Vector3Int originalPosition;
	internal Vector3Int newMapPosition;

	public Character Character { get; }

	public MovementAction(Character character, Vector3Int originalPosition, Vector3Int newMapPosition)
	{
		Character = character;
		this.originalPosition = originalPosition;
		this.newMapPosition = newMapPosition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		bool excludeAllies = character is Player;

		var overlapTarget = Game.Instance.CurrentDungeon
			.OverlapsAnyOtherCharacter(character, Character.ToBounds(character.FootPrint, newMapPosition), excludeAllies) != null;

		if (overlapTarget)
		{
			newMapPosition = originalPosition;
		}
		else
		{
			if (character is Player player)
			{
				player.currentInteractable = Game.Instance.CurrentDungeon?.GetInteractable(newMapPosition);
			}
		}

		character.TilemapPosition = newMapPosition;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		var worldPosition = Game.Instance.CurrentDungeon.CellToWorld(newMapPosition);

		character.PlayWalkAnimation();
		yield return character.transform.DOMove(worldPosition, 0.1f / character.FinalStats.ActionsPerTurnMax)
			.WaitForCompletion();

		character.PlayIdleAnimation();
	}

	internal override bool IsValid(Character character)
	{
		var canWalk = Game.Instance.CurrentDungeon.CanWalkTo(originalPosition, newMapPosition);

		bool excludeAllies = character is Player;
		var overlapTarget = Game.Instance.CurrentDungeon
			.OverlapsAnyOtherCharacter(character, Character.ToBounds(character.FootPrint, newMapPosition), excludeAllies) != null;

		var canMove = canWalk &&
			!overlapTarget;

		return canMove;
	}

	internal override bool CanBeCombined(GameAction action)
	{
		return action is MovementAction ||
			action is SwapAllyPositionAction ||
			action is WaitAction;
	}
}

internal class AttackAction : GameAction
{
	private readonly Character attacker;
	private Vector3Int originalPosition;
	internal Vector3Int attackPosition;

	public AttackAction(Character attacker,
		Vector3Int originalPosition,
		Vector3Int attackPosition)
	{
		this.attacker = attacker;
		this.originalPosition = originalPosition;
		this.attackPosition = attackPosition;
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
		var target = Game.Instance.CurrentDungeon.OverlapsAnyOtherCharacter(attacker, Character.ToBounds(attackPosition));

		if (target == null)
		{
			return new();
		}

		List<GameAction> ret = new();

		AddMetricsModification(attacker, (stats, vitals) =>
		{
			attacker.Vitals.AttacksPerTurnLeft -= 1;
		});

		bool hit;
		int damage;
		GetAttackDamage(attacker, target, out hit, out damage);

		ret.Add(new TakeDamageAction(attacker, target, damage, true, !hit));
		return ret;
	}

	public static void GetAttackDamage(Character attacker, Character target, out bool hit, out int damage)
	{
		hit = UnityEngine.Random.value > 0.2f;
		var baseDamage = attacker.FinalStats.Strength * MathF.Pow((15f / 16f), target.FinalStats.Defense);
		float n = (float)UnityEngine.Random.Range(112, 143);
		damage = (int)MathF.Floor(baseDamage * (n / 128f));
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		character.PlayAttackAnimation();

		yield return new WaitForSecondsRealtime(0.5f);
		character.PlayIdleAnimation();
	}

	internal override bool IsValid(Character character)
	{
		var canMove = Game.Instance.CurrentDungeon.IsWalkable(attackPosition);

		return canMove;
	}
}

public class TakeDamageAction : GameAction
{
	private readonly Character attacker;
	internal readonly Character target;
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
			game.DoFloatingText(damage.ToString(), Color.red, target.VisualParent.gameObject.transform.position);
		}
		else
		{
			game.DoFloatingText("miss", Color.white, target.VisualParent.gameObject.transform.position);
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
	internal Character target;
	private Vector3Int dropPosition;
	private bool droppedItem;
	private readonly Character attacker;

	public DeathAction(Character target, Character attacker)
	{
		this.target = target;
		this.attacker = attacker;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game.Instance.Allies.Remove(target as Ally);
		Game.Instance.Enemies.Remove(target as Enemy);
		Game.Instance.DeadUnits.Add(target);

		var gainXP = new AddXPAction(attacker, target.FinalStats.EXPOnKill);

		float value = UnityEngine.Random.value;
		droppedItem = target.FinalStats.DropRate > 0 &&
			value < character.FinalStats.DropRate;

		dropPosition = Game.Instance.CurrentDungeon.GetDropPosition(target.TilemapPosition);

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
		if (droppedItem)
		{
			var item = game.ItemManager.GetRandomDrop(target as Enemy);
			game.CurrentDungeon.SetDroppedItem(dropPosition, item);
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

			IEnumerable<LevelInfo> levelUps = levelSystem.LevelData.Where(x =>
				x.Level > player.Vitals.Level &&
				x.Experience <= player.Vitals.Exp);


			foreach (var levelUp in levelUps)
			{
				this.AddMetricsModification(this.character, ((stats, vitals) =>
				{
					vitals.Level ++;
				}));

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
	private Interactable currentInteractable;

	public InteractAction(Interactable currentInteractable)
	{
		this.currentInteractable = currentInteractable;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return currentInteractable.GetInteractionSideEffects(character);
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
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.1f)
			.WaitForCompletion();
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}

	internal override bool CanBeCombined(GameAction action)
	{
		return action is MovementAction ||
			action is SwapAllyPositionAction ||
			action is WaitAction;
	}
}