using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class RangedAttackAction : GameAction
{
	private Character attacker;
	private Character target;
	private int damage;
	private GameObject projectilePrefab;
	private Vector3Int rangedAttackTargetPosition;

	public RangedAttackAction()
	{

	}
	public RangedAttackAction(Character attacker, Character target, int damage, GameObject projectilePrefab)
	{
		this.attacker = attacker;
		this.target = target;
		this.damage = damage;
		this.projectilePrefab = projectilePrefab;
	}

    internal override bool ValidateCommand(Character character, out string reason)
    {
        reason = ArrowSupply.HasBow(attacker) && ArrowSupply.Count(attacker) < 1 ? "no arrows" : null;
        return reason == null;
    }
    internal override List<GameAction> ExecuteImmediate(Character character)
    {
        var ret = new List<GameAction>();
        if (!ValidateCommand(character, out _)) return ret;
        bool bow = ArrowSupply.HasBow(attacker);
        var direction = target != null ? target.TilemapPosition - attacker.TilemapPosition : Dungeon.GetFacingOffset(attacker.CurrentFacing);
        direction = new Vector3Int(Math.Sign(direction.x), Math.Sign(direction.y));
        var line = MissileTargeting.TraceLine(attacker, direction, 10 + ClassPassives.MissileRangeBonus(attacker), bow ? ArrowSupply.Penetration(attacker) : 1);
        float ammo = bow ? ArrowSupply.DamageMultiplier(attacker) : 1;
        if (bow) ArrowSupply.Consume(attacker, 1, ClassPassives.ArrowRecoveryChance(attacker));
        rangedAttackTargetPosition = line.Endpoint;
        Visuals.Configure(CharacterCombatEffects.Attack(attacker, true), attacker, line.Endpoint);
        if (Visuals.Sequence != null) { Visuals.Sequence.SingleFlight = true; Visuals.Sequence.ContinuousFlight = true; }
        foreach (var hit in line.Encounters)
        {
            var recipient = hit.Character;
            GameMessages.ForCharacter(attacker, $"{GameMessages.Name(attacker)} attacked {GameMessages.VisibleName(recipient)}!");
            bool godmode = AutoplayRunner.GodmodeFor(attacker);
            bool landed = godmode || CombatMath.RollHit(attacker, recipient);
            bool critical = !godmode && landed && CombatMath.RollCrit(attacker);
            int power = godmode ? recipient.Vitals.HP : Mathf.RoundToInt(damage * ammo * (bow ? ClassPassives.DamageMultiplier(
                new OutgoingDamage(attacker, recipient, DamageCategory.Bow, DamageElement.Physical, false)) : 1));
            if (critical) power = CombatMath.ApplyCrit(power);
            ret.Add(new TakeDamageAction(attacker, recipient, power, true, !landed) { Critical = critical });
        }
        if (Game.Instance.CurrentDungeon.PropAt(line.Endpoint) is DungeonProp prop && prop.Alive)
            ret.Add(prop.Damage(attacker, Mathf.RoundToInt(damage * ammo)));
        if (bow && allowExtra && ArrowSupply.Count(attacker) > 0 && UnityEngine.Random.value < ClassPassives.ExtraShotChance(attacker))
            ret.Add(new RangedAttackAction(attacker, target, damage, projectilePrefab) { allowExtra = false });
        return ret;
    }
    private bool allowExtra = true;

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || Visuals.Sequence != null || projectilePrefab == null) yield break;
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();

		var game = Game.Instance;
		var projectile = UnityEngine.Object.Instantiate(projectilePrefab, null);
        Vector3 startPosition = character.VisualParent.transform.position;
        projectile.transform.position = startPosition;
		var attackerWorldPosition = game.CurrentDungeon.CellToWorld(character.TilemapPosition);
		var offset = new Vector3(1.25f, 1.25f, 0);
		var targetWorldPosition = game.CurrentDungeon.CellToWorld(rangedAttackTargetPosition) + offset;
		projectile.transform.LookAt(targetWorldPosition);

		float distance = Vector3.Distance(startPosition, targetWorldPosition);
        float projectileSpeed = 20f;
        float duration = distance / projectileSpeed;
		yield return projectile.transform.DOMove(targetWorldPosition, duration)
			.SetEase(Ease.Linear)
			.WaitForCompletion();

		UnityEngine.Object.Destroy(projectile.gameObject);
	}

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        foreach (var cell in AnimationPath(actor, rangedAttackTargetPosition)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		return ValidateCommand(character, out _);
	}
}
