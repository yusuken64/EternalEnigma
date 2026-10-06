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
	private bool blocked;

	public Character Character { get; }
	public MovementAction() { }
	public MovementAction(Character character, Vector3Int originalPosition, Vector3Int newMapPosition)
	{
		Character = character;
		this.originalPosition = originalPosition;
		this.newMapPosition = newMapPosition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if ((character != null && character.IsMovementBlocked)) { blocked = true; newMapPosition=originalPosition; return new(); }
		bool excludeAllies = false;

		var overlapTarget = Game.Instance.CurrentDungeon
			.OverlapsAnyOtherCharacter(character, Character.ToBounds(character.FootPrint, newMapPosition), excludeAllies) != null;

		if (overlapTarget)
		{
			newMapPosition = originalPosition;
		}

		character.TilemapPosition = newMapPosition;
		return new();
	}

	internal override void RecordOutcome(Character character)
	{
		if (blocked) GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} can't move!");
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		var worldPosition = Game.Instance.CurrentDungeon.CellToWorld(newMapPosition);
		if (blocked || originalPosition == newMapPosition)
		{
			(character as Ally)?.HeroAnimator?.StopWalkContinuation();
			yield break;
		}
		if (skipAnimation)
		{
			character.PlayIdleAnimation();
			character.transform.position = worldPosition;
			yield break;
		}

		bool continuous = character is Ally && Game.Instance.PlayerController?.CanContinueHeldWalk == true;
		BeginWalk(character, continuous);
		var tween = character.transform.DOMove(worldPosition, 0.1f / character.FinalStats.ActionsPerTurnMax);
		if (continuous) tween.SetEase(Ease.Linear);
		yield return tween.WaitForCompletion();
		CompleteWalk(character, continuous && Game.Instance.PlayerController?.CanContinueHeldWalk == true);
	}

	internal static void BeginWalk(Character character, bool continuous)
	{
		if (character is Ally ally) ally.HeroAnimator?.BeginWalk(continuous);
		else character.PlayWalkAnimation();
	}

	internal static void CompleteWalk(Character character, bool retain)
	{
		if (character is Ally ally) ally.HeroAnimator?.CompleteWalk(retain);
		else character.PlayIdleAnimation();
	}

    internal override void AddDestinationSight(HashSet<Vector3Int> tiles)
    {
        AddAllySight(tiles, Character, newMapPosition);
    }

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        foreach (var cell in AnimationPath(actor, newMapPosition)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		var canWalk = Game.Instance.CurrentDungeon.CanWalkTo(originalPosition, newMapPosition);
		// A blocked mover (rooted/stuck) is still a valid command: ExecuteImmediate cancels the step and the turn is spent.

		bool excludeAllies = true;
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

	public AttackAction() {}

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
        if (Visuals.Sequence == null) Visuals.Configure(CharacterCombatEffects.Attack(attacker, false), attacker, attackPosition);

		if (target == null)
        {
            var prop = Game.Instance.CurrentDungeon.PropAt(attackPosition);
            if (prop == null || !prop.Alive) return new();
            AddMetricsModification(attacker, (stats, vitals) => vitals.AttacksPerTurnLeft -= 1);
            return new() { prop.Damage(attacker, Mathf.Max(1, Mathf.FloorToInt(attacker.FinalStats.Strength * UnityEngine.Random.Range(112,143) / 128f))) };
        }

		List<GameAction> ret = new();

        GameMessages.ForCharacter(attacker, $"{GameMessages.Name(attacker)} attacked {GameMessages.VisibleName(target)}!");
        TrackAnimationTarget(target);

		AddMetricsModification(attacker, (stats, vitals) =>
		{
			attacker.Vitals.AttacksPerTurnLeft -= 1;
		});

		bool hit;
		int damage;
		bool critical;
		GetAttackDamage(attacker, target, out hit, out damage, out critical);

		if (hit && damage > 0 && !AutoplayRunner.GodmodeFor(attacker))
			damage = Mathf.Max(1, Mathf.RoundToInt(damage * ClassPassives.DamageMultiplier(
				new OutgoingDamage(attacker, target, DamageCategory.Weapon, DamageElement.Physical, false))));

		ret.Add(new TakeDamageAction(attacker, target, damage, true, !hit) { Critical = critical });
		return ret;
	}

	public static void GetAttackDamage(Character attacker, Character target, out bool hit, out int damage) =>
		GetAttackDamage(attacker, target, out hit, out damage, out _);

	public static void GetAttackDamage(Character attacker, Character target, out bool hit, out int damage, out bool critical) =>
		GetAttackDamage(attacker, target, attacker.FinalStats.Strength, out hit, out damage, out critical);

	/// <summary>Same swing formula, with the attacker's strength supplied (e.g. swapping in a different weapon's bonus).</summary>
	public static void GetAttackDamage(Character attacker, Character target, int strength, out bool hit, out int damage, out bool critical)
	{
		// Resolve infinite strength as lethal damage, avoiding overflowing integer stats or saved equipment.
		if (AutoplayRunner.GodmodeFor(attacker)) { hit = true; damage = Math.Max(0, target.Vitals.HP); critical = false; return; }
		hit = CombatMath.RollHit(attacker, target);
		var baseDamage = strength * MathF.Pow((15f / 16f), target.FinalStats.Defense);
		float n = (float)UnityEngine.Random.Range(112, 143);
		damage = (int)MathF.Floor(baseDamage * (n / 128f));
		critical = hit && CombatMath.RollCrit(attacker);
		if (critical) damage = CombatMath.ApplyCrit(damage);
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation)
		{
			character.PlayIdleAnimation();
			yield break;
		}
		//TODO play sound based on implementation
		AudioManager.Instance.SoundEffects.Slash.PlayAsSound();
		if (Visuals.Sequence != null) yield break;
		character.PlayAttackAnimation();

		yield return new WaitForSecondsRealtime(0.5f);
		character.PlayIdleAnimation();
	}

	internal override bool IsValid(Character character)
	{
		var canMove = Game.Instance.CurrentDungeon.IsFloorCell(attackPosition);

		return canMove;
	}
}

public class TakeDamageAction : GameAction
{
	private readonly Character attacker;
	internal Character target;
	public int damage;
	[SerializeField]
	public bool doDamageAnimation;
	private bool miss;
	public DamageElement Element;
	public bool RollToHit;
    public bool Environmental;
    internal bool AwardExperience = true;
	public int ResponseDepth;
	internal bool Critical;
	private bool resolved;

	public TakeDamageAction() {}

	public TakeDamageAction(Character attacker, Character target, int damage, bool doDamageAnimation = true, bool miss = false, DamageElement element = DamageElement.Physical)
	{
		this.attacker = attacker;
		this.target = target;
		this.damage = damage;
		this.doDamageAnimation = doDamageAnimation;
		this.miss = miss;
		Element = element;
	}

	public Character Attacker => attacker;
	public Character Target => target;
	public int Damage => damage;
	public bool Missed => miss;

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return new TakeDamageAction(caster, target, damage, doDamageAnimation, miss, Element) { RollToHit = RollToHit };
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var scaling = rank.Scaling ?? new SkillRankScaling();
		return new TakeDamageAction(caster, target, scaling.ScalePower(damage, rank.Rank), doDamageAnimation, miss, Element) { RollToHit = RollToHit };
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
        if (target is Enemy provoked && !Environmental) provoked.Provoke();
		if (!resolved)
		{
			resolved = true;
			if (RollToHit && !miss) miss = !CombatMath.RollHit(attacker, target);
			if (!miss && !Environmental) damage = ElementMath.Apply(damage, target.FinalStats, Element);
			var context = new DamageContext(attacker, target, damage, Element, miss, ResponseDepth);
			var game = Game.Instance;
			if (game != null && !Environmental)
				foreach (var character_char in game.AllCharacters.ToList())
					if (character_char != null && character_char.Vitals != null && character_char.Vitals.HP > 0)
						character_char.InterceptDamage(context);
			if (context.Target != null) target = context.Target;
			damage = System.Math.Max(0, context.Damage);
			miss = context.Missed;
		}

        TrackAnimationTarget(target);
		if (!miss)
		{
			AddMetricsModification(target, (metrics, vitals) =>
			{
				vitals.HP -= damage;
			});
		}

        GameMessages.ForCharacter(target, miss ? $"{GameMessages.Name(target)} evaded the attack." : $"{GameMessages.Name(target)} took {damage} damage{(Critical ? " (critical)!" : ".")}");
		if (target.Vitals.HP <= 0)
		{
			return new List<GameAction>()
			{
				new DeathAction(target, attacker) { AwardExperience = !Environmental && AwardExperience }
			};
		}

        if (!miss && damage > 0 && !Environmental && target is Enemy survivor &&
            survivor.GetComponent<EnemyBehavior>() is { } behavior && behavior.WarpWhenHitChance > 0 &&
            !survivor.IsMovementBlocked && UnityEngine.Random.value < behavior.WarpWhenHitChance)
            return new() { new WarpAction(survivor) };

		return new();
	}


	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) { yield break; }
		Game game = Game.Instance;
        DungeonFloatingText.Show(game, miss ? "Miss" : damage.ToString(), miss ? Color.white : new Color(1f,.38f,.35f), target);
		if (!miss)
		{
			AudioManager.Instance.SoundEffects.Impact_flesh.PlayAsSound();

		}
		else
		{
			AudioManager.Instance.SoundEffects.Miss_Evade.PlayAsSound();

		}

		if (doDamageAnimation && !miss)
		{
			target.PlayTakeDamageAnimation();
			yield return new WaitForSecondsRealtime(0.3f);
			if (target.Vitals.HP > 0) target.PlayIdleAnimation();
		}
	}

	internal override bool IsValid(Character character)
	{
		return target.Vitals.HP > 0;
	}
}

public class TakeHealAction : GameAction
{
	private readonly Character attacker;
	internal readonly Character target;
	public int healing;
	public bool doHealAnimation;
	private readonly bool miss;

	public TakeHealAction() {}
	public TakeHealAction(Character attacker, Character target, int healing, bool doHealAnimation = true, bool miss = false)
	{
		this.attacker = attacker;
		this.target = target;
		this.healing = healing;
		this.doHealAnimation = doHealAnimation;
		this.miss = miss;
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return new TakeHealAction(caster, target, healing, doHealAnimation, miss);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var scaling = rank.Scaling ?? new SkillRankScaling();
		return new TakeHealAction(caster, target, scaling.ScalePower(healing, rank.Rank), doHealAnimation, miss);
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
        TrackAnimationTarget(target);
		if (!miss)
		{
			AddMetricsModification(target, (metrics, vitals) =>
			{
				vitals.HP += healing;
			});
		}

        GameMessages.ForCharacter(target, miss ? $"{GameMessages.Name(target)}: healing missed." : $"{GameMessages.Name(target)} recovered {healing} HP.");
		if (target.Vitals.HP <= 0)
		{
			return new List<GameAction>()
			{
				new DeathAction(target, attacker)
			};
		}

		return new();
	}


	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) { yield break; }
		Game game = Game.Instance;
        DungeonFloatingText.Show(game, miss ? "Miss" : "+" + healing, new Color(.55f,1f,.5f), target);
		if (!miss)
		{
			AudioManager.Instance.SoundEffects.Impact_heal.PlayAsSound();
		}
		else
		{
			AudioManager.Instance.SoundEffects.Miss_Evade.PlayAsSound();
		}

		if (doHealAnimation && !miss)
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
	internal override bool InterruptsWalking => doDamageAnimation;
	private readonly Character attacker;
	private readonly Character target;
	private readonly Action<Stats, Vitals> modifyAction;
	private readonly bool doDamageAnimation;

	public ModifyStatAction() {}

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

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (doDamageAnimation && !skipAnimation)
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
	private bool resolved;
	internal bool AwardExperience = true;
	internal Character target;
	private Vector3Int dropPosition;
	private bool droppedItem;
	private readonly Character attacker;
	private bool downed;

	public DeathAction() {}
	public DeathAction(Character target, Character attacker)
	{
		this.target = target;
		this.attacker = attacker;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
        if (resolved || target == null || Game.Instance.DeadUnits.Contains(target)) return new();
        resolved = true;
        TrackAnimationTarget(target);
        global::PendingCast.Cancel(target, "dead or downed");
		if (target is Ally ally && !PartyRules.IsSummon(ally))
		{
			// Downed, not dead: stays in the scene, leaves the Allies list, restored by Revive or the next floor.
			downed = true;
			PartyRules.MarkDowned(Game.Instance, ally);
		}
		else
		{
			Game.Instance.Allies.Remove(target as Ally);
			Game.Instance.Enemies.Remove(target as Enemy);
			Game.Instance.DeadUnits.Add(target);
		}

        GameMessages.ForCharacter(target, $"{GameMessages.Name(target)} {(downed ? "was downed" : "died")}.");
        target.GetComponent<EnemyBehavior>()?.DropStolenLoot();
        target.GetComponent<TrapCarriedItem>()?.TryDrop(true);
        foreach (var owner in Game.Instance.AllCharacters.ToList())
            foreach (var status in owner.StatusEffects.ToList())
                if (status != null) status.OnCharacterDied(owner, target);
		var gainXP = new AddXPAction(attacker, target.FinalStats.EXPOnKill);

		float value = UnityEngine.Random.value;
		droppedItem = target.FinalStats.DropRate > 0 &&
			value < character.FinalStats.DropRate;

		dropPosition = Game.Instance.CurrentDungeon.GetDropPosition(target.TilemapPosition);

		var effects = new List<GameAction>();
        if (target is Enemy enemy && enemy.GetComponent<EnemyBehavior>() is { } behavior)
        {
            if (behavior.CanExplode(enemy))
            {
                Visuals.Configure(CombatVisualCatalog.Instance?.Fire, enemy, enemy.TilemapPosition, 1);
                Visuals.Cast = false;
                Visuals.AddImpact(enemy, enemy.TilemapPosition, true);
            }
            effects.AddRange(behavior.DeathEffects(enemy));
        }
		if (AwardExperience) effects.Add(gainXP);
		return effects;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
        if (!skipAnimation)
        {
            AudioManager.Instance.SoundEffects.Enemy_death.PlayAsSound();
            target.PlayDeathAnimation();
            yield return new WaitForSecondsRealtime(0.4f);
        }
		if (!downed) target.VisualParent.gameObject.SetActive(false);

		Game game = Game.Instance;
		if (droppedItem)
		{
			var item = Common.Instance.ItemManager.GetRandomDrop(target as Enemy, Game.Instance.PlayerController.Floor);
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

	public AddXPAction() {}
	public AddXPAction(Character character, int eXP)
	{
		this.character = character;
		this.eXP = eXP;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (this.character == null || PartyRules.IsSummon(this.character)) return new();
		this.AddMetricsModification(this.character, ((stats, vitals) =>
		{
			vitals.Exp += eXP;
		}));
		List<GameAction> ret = new();

		var game = Game.Instance;
		var levelSystem = game.LevelSystem;

		int currentLevel = this.character.Vitals.Level;
		int currentExp = this.character.Vitals.Exp;

		var levelUps = levelSystem.GetLevelUps(currentLevel, currentExp);

		foreach (var levelUp in levelUps)
		{
			this.AddMetricsModification(this.character, ((stats, vitals) =>
			{
				vitals.Level++;
			}));

			ret.Add(new LevelUpAction(this.character));
		}

		return ret;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation) yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}

internal sealed class RevealMimicAction : GameAction
{
    private readonly Enemy mimic;

    internal RevealMimicAction(Enemy mimic) { this.mimic = mimic; }

    internal override bool IsValid(Character character) => character is Ally && mimic != null &&
        EnemyBehavior.IsDisguised(mimic) &&
        TileWorldDungeon.ChevDistance(character.TilemapPosition, mimic.TilemapPosition) == 1;

    internal override List<GameAction> ExecuteImmediate(Character character)
    {
        if (!IsValid(character)) return new();
        TrackAnimationTarget(mimic);
        mimic.Provoke();
        mimic.PursuitTarget = character;
        mimic.PursuitPosition = character.TilemapPosition;
        mimic.CurrentEnemyState = EnemyState.Pursuit;
        return new();
    }

    internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
        yield break;
    }
}

public class InteractAction : GameAction
{
	private Interactable currentInteractable;

	public InteractAction()	{}

	public InteractAction(Interactable currentInteractable)
	{
		this.currentInteractable = currentInteractable;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return currentInteractable.GetInteractionSideEffects(character);
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
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
	public WaitAction() {}
    internal override void RecordOutcome(Character character) { if (character is Ally) GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} waited."); }
	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation) yield return null;
		//yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.1f)
		//	.WaitForCompletion();
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
