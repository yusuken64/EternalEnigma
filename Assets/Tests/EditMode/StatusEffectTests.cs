using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class StatusEffectTests
{
	private List<GameObject> gameObjectsToCleanup = new();

	[SetUp]
	public void SetUp()
	{
		gameObjectsToCleanup.Clear();
	}

	[TearDown]
	public void TearDown()
	{
		foreach (var obj in gameObjectsToCleanup)
		{
			if (obj != null)
				Object.DestroyImmediate(obj);
		}
		gameObjectsToCleanup.Clear();
	}

	// Helper: Creates a status effect component on a new GameObject, tracked for cleanup
	private T Status<T>() where T : StatusEffect
	{
		var go = new GameObject();
		gameObjectsToCleanup.Add(go);
		return go.AddComponent<T>();
	}

	// Helper: Creates an Ally (or Enemy if Ally has Awake dependencies)
	private Ally Actor()
	{
		var go = new GameObject();
		gameObjectsToCleanup.Add(go);
		return go.AddComponent<Ally>();
	}

	[Test]
	public void ArmBindBlocksAttacksAndWeaponSkills()
	{
		var armBind = Status<ArmBindStatusEffect>();

		// AttackAction should be interrupted
		Assert.That(armBind.Interupts(new AttackAction()), Is.True);

		// WaitAction should not be interrupted
		Assert.That(armBind.Interupts(new WaitAction()), Is.False);

		// Test SkillAction with IsWeaponSkill = true
		var weaponSkill = ScriptableObject.CreateInstance<Skill>();
		weaponSkill.IsWeaponSkill = true;

		var actor = Actor();
		var skillAction = new SkillAction(actor, weaponSkill, actor);
		Assert.That(armBind.Interupts(skillAction), Is.True);

		// Test SkillAction with non-weapon skill and ArrowCost 0
		var nonWeaponSkill = ScriptableObject.CreateInstance<Skill>();
		nonWeaponSkill.IsWeaponSkill = false;
		nonWeaponSkill.ArrowCost = 0;

		var nonWeaponAction = new SkillAction(actor, nonWeaponSkill, actor);
		Assert.That(armBind.Interupts(nonWeaponAction), Is.False);
	}

	[Test]
	public void StunSkipsActionAndMenu()
	{
		var stun = Status<StunStatusEffect>();
		var actor = Actor();

		var actionOverride = stun.GetActionOverride(actor);
		Assert.That(actionOverride, Is.TypeOf<SleepTurnAction>());

		Assert.That(stun.PreventsMenu(), Is.True);
	}

	[Test]
	public void ParalysisRespectsChance()
	{
		// Test with SkipChance = 1.0 (should always skip)
		var paralysis1 = Status<ParalysisStatusEffect>();
		paralysis1.SkipChance = 1.0f;
		paralysis1.Apply();
		Assert.That(paralysis1.GetActionOverride(Actor()), Is.Not.Null);

		// Test with SkipChance = 0.0 (should never skip)
		var paralysis0 = Status<ParalysisStatusEffect>();
		paralysis0.SkipChance = 0.0f;
		paralysis0.Apply();
		Assert.That(paralysis0.GetActionOverride(Actor()), Is.Null);
	}

	[Test]
	public void BurnTicksFireDamage()
	{
		var burn = Status<BurnStatusEffect>();
		burn.TickDamage = 5;

		var actor = Actor();
		var tickEffects = burn.GetTickEffects(actor);

		Assert.That(tickEffects, Is.Not.Null);
		Assert.That(tickEffects.Count, Is.EqualTo(1));
		Assert.That(tickEffects[0], Is.TypeOf<TakeDamageAction>());

		var damageAction = tickEffects[0] as TakeDamageAction;
		Assert.That(damageAction.Element, Is.EqualTo(DamageElement.Fire));
		Assert.That(damageAction.Damage, Is.EqualTo(5));
	}

	[Test]
	public void CurseReflectsOnlyPrimaryHitsByOwner()
	{
		var curse = Status<CurseStatusEffect>();
		curse.ReflectFraction = 0.5f;

		var owner = Actor();
		var other = Actor();

		// Test: Owner attacks other with 10 damage -> should reflect 5
		var damageAction = new TakeDamageAction(owner, other, 10, false, false, DamageElement.Physical);
		damageAction.ResponseDepth = 0; // Primary hit
		var reflectedActions = curse.GetResponseTo(owner, damageAction);
		var reflectedList = new List<GameAction>(reflectedActions);

		Assert.That(reflectedList.Count, Is.EqualTo(1));
		var reflected = reflectedList[0] as TakeDamageAction;
		Assert.That(reflected.Damage, Is.EqualTo(5));
		Assert.That(reflected.ResponseDepth, Is.EqualTo(1));

		// Test: ResponseDepth 1 on the trigger gives none
		var responseAction = new TakeDamageAction(owner, other, 10, false, false, DamageElement.Physical);
		responseAction.ResponseDepth = 1;
		var responseReflect = curse.GetResponseTo(owner, responseAction);
		Assert.That(new List<GameAction>(responseReflect).Count, Is.EqualTo(0));

		// Test: Missed hit gives none
		var missedAction = new TakeDamageAction(owner, other, 10, false, true, DamageElement.Physical);
		missedAction.ResponseDepth = 0;
		var missedReflect = curse.GetResponseTo(owner, missedAction);
		Assert.That(new List<GameAction>(missedReflect).Count, Is.EqualTo(0));

		// Test: Hit by someone else gives none
		var otherAttacker = Actor();
		var otherAttack = new TakeDamageAction(otherAttacker, owner, 10, false, false, DamageElement.Physical);
		otherAttack.ResponseDepth = 0;
		var otherReflect = curse.GetResponseTo(owner, otherAttack);
		Assert.That(new List<GameAction>(otherReflect).Count, Is.EqualTo(0));
	}

	[Test]
	public void DebuffStatMods()
	{
		// Weaken -> Strength -3
		var weaken = Status<WeakenStatusEffect>();
		weaken.Amount = 3;
		var weakenMod = weaken.GetStatModification();
		Assert.That(weakenMod.Strength, Is.EqualTo(-3));

		// Blind -> HitBonus -0.3
		var blind = Status<BlindStatusEffect>();
		blind.Amount = 0.3f;
		var blindMod = blind.GetStatModification();
		Assert.That(blindMod.HitBonus, Is.EqualTo(-0.3f));

		// Exposed (ResistanceShift) -> all three resistances -1
		var exposed = Status<ResistanceShiftStatusEffect>();
		exposed.FireShift = -1;
		exposed.IceShift = -1;
		exposed.LightningShift = -1;
		var exposedMod = exposed.GetStatModification();
		Assert.That(exposedMod.FireResistance, Is.EqualTo(-1));
		Assert.That(exposedMod.IceResistance, Is.EqualTo(-1));
		Assert.That(exposedMod.LightningResistance, Is.EqualTo(-1));

		// Re-applying with TurnsLeft 9 sets TurnsLeft to 9 without changing magnitude
		var weaken2 = Status<WeakenStatusEffect>();
		weaken2.Amount = 3;
		weaken2.TurnsLeft = 5;
		var weakenNew = Status<WeakenStatusEffect>();
		weakenNew.Amount = 3;
		weakenNew.TurnsLeft = 9;
		weaken2.ReApply(weakenNew);
		Assert.That(weaken2.TurnsLeft, Is.EqualTo(9));
		Assert.That(weaken2.GetStatModification().Strength, Is.EqualTo(-3));
	}

	[Test]
	public void TauntRecordsSource()
	{
		var taunt = Status<TauntStatusEffect>();
		var owner = Actor();
		var source = Actor();

		taunt.OnApplied(owner, source);
		Assert.That(taunt.Taunter, Is.SameAs(source));
	}

	[Test]
	public void StealthEndsWhenOwnerAttacks()
	{
		var stealth = Status<StealthStatusEffect>();
		var owner = Actor();
		var other = Actor();

		// When owner attacks other, Stealth should return RemoveStatusEffectAction
		var attackAction = new TakeDamageAction(owner, other, 1, false, false, DamageElement.Physical);
		var response = stealth.GetResponseTo(owner, attackAction);
		var responseList = new List<GameAction>(response);

		Assert.That(responseList.Count, Is.EqualTo(1));
		Assert.That(responseList[0], Is.TypeOf<RemoveStatusEffectAction>());

		// When owner is the target, return none
		var selfAttackAction = new TakeDamageAction(owner, owner, 1, false, false, DamageElement.Physical);
		var selfResponse = stealth.GetResponseTo(owner, selfAttackAction);
		var selfResponseList = new List<GameAction>(selfResponse);

		Assert.That(selfResponseList.Count, Is.EqualTo(0));
	}

	[Test]
	public void FearBlocksAttacksForAllies()
	{
		var fear = Status<FearStatusEffect>();
		var actor = Actor();

		// Player team short-circuits GetActionOverride before it touches Game.Instance.
		actor.Team = Team.Player;

		// GetActionOverride should be null
		Assert.That(fear.GetActionOverride(actor), Is.Null);

		// Interupts(new AttackAction()) should be true
		Assert.That(fear.Interupts(new AttackAction()), Is.True);
	}

	[Test]
	public void TimedBuffStackKeysAndStrongerWins()
	{
		// Create Song Strength+2
		var songStrength2 = Status<TimedBuffStatusEffect>();
		songStrength2.BuffName = "Strength";
		songStrength2.Family = BuffFamily.Song;
		songStrength2.Modification = new StatModification { Strength = 2 };

		// Create Song Strength+5
		var songStrength5 = Status<TimedBuffStatusEffect>();
		songStrength5.BuffName = "Strength";
		songStrength5.Family = BuffFamily.Song;
		songStrength5.Modification = new StatModification { Strength = 5 };

		// They should have equal StackKeys
		Assert.That(songStrength2.StackKey, Is.EqualTo(songStrength5.StackKey));

		// Create Song Defense
		var songDefense = Status<TimedBuffStatusEffect>();
		songDefense.BuffName = "Defense";
		songDefense.Family = BuffFamily.Song;
		songDefense.Modification = new StatModification { Defense = 3 };

		// Song Strength vs Song Defense should differ
		Assert.That(songStrength2.StackKey, Is.Not.EqualTo(songDefense.StackKey));

		// Create Command Strength
		var commandStrength = Status<TimedBuffStatusEffect>();
		commandStrength.BuffName = "Strength";
		commandStrength.Family = BuffFamily.Command;
		commandStrength.Modification = new StatModification { Strength = 3 };

		// Song vs Command on Strength should differ
		Assert.That(songStrength2.StackKey, Is.Not.EqualTo(commandStrength.StackKey));

		// None-family uses BuffName
		var noneBuff = Status<TimedBuffStatusEffect>();
		noneBuff.BuffName = "CustomBuff";
		noneBuff.Family = BuffFamily.None;
		noneBuff.Modification = new StatModification();

		var noneBuff2 = Status<TimedBuffStatusEffect>();
		noneBuff2.BuffName = "CustomBuff";
		noneBuff2.Family = BuffFamily.None;
		noneBuff2.Modification = new StatModification();

		Assert.That(noneBuff.StackKey, Is.EqualTo("TimedBuff:CustomBuff"));
		Assert.That(noneBuff2.StackKey, Is.EqualTo("TimedBuff:CustomBuff"));

		// ReApply with weaker buff keeps +5
		songStrength5.TurnsLeft = 10;
		songStrength2.TurnsLeft = 5;
		songStrength5.ReApply(songStrength2);
		Assert.That(songStrength5.Magnitude(), Is.GreaterThanOrEqualTo(songStrength2.Magnitude()));

		// ReApply with stronger buff takes it
		var stronger = Status<TimedBuffStatusEffect>();
		stronger.BuffName = "Strength";
		stronger.Family = BuffFamily.Song;
		stronger.Modification = new StatModification { Strength = 10 };
		stronger.TurnsLeft = 7;

		songStrength5.TurnsLeft = 10;
		songStrength5.ReApply(stronger);
		Assert.That(songStrength5.Modification.Strength, Is.EqualTo(10));
		Assert.That(songStrength5.TurnsLeft, Is.EqualTo(7));
	}

	[Test]
	public void BarrierReducesMatchingElementForOwnerOnly()
	{
		var barrier = Status<BarrierStatusEffect>();
		barrier.Element = DamageElement.Fire;
		barrier.Reduction = 0.75f;

		var attacker = Actor();
		var owner = Actor();

		// Test: Fire damage to owner -> 20 * (1 - 0.75) = 5
		var fireContext = new DamageContext(attacker, owner, 20, DamageElement.Fire, false, 0);
		barrier.ModifyIncomingDamage(owner, fireContext);
		Assert.That(fireContext.Damage, Is.EqualTo(5));

		// Test: Ice damage -> 20 (no change)
		var iceContext = new DamageContext(attacker, owner, 20, DamageElement.Ice, false, 0);
		barrier.ModifyIncomingDamage(owner, iceContext);
		Assert.That(iceContext.Damage, Is.EqualTo(20));

		// Test: Target that isn't the owner -> 20 (no change)
		var other = Actor();
		var wrongTargetContext = new DamageContext(attacker, other, 20, DamageElement.Fire, false, 0);
		barrier.ModifyIncomingDamage(owner, wrongTargetContext);
		Assert.That(wrongTargetContext.Damage, Is.EqualTo(20));

		// Test: Missed -> unchanged
		var missedContext = new DamageContext(attacker, owner, 20, DamageElement.Fire, true, 0);
		barrier.ModifyIncomingDamage(owner, missedContext);
		Assert.That(missedContext.Damage, Is.EqualTo(20));
	}

	[Test]
	public void SilenceStatusEffectDefaultStackKey()
	{
		var silence = Status<SilenceStatusEffect>();
		var expectedKey = typeof(SilenceStatusEffect).FullName;
		Assert.That(silence.StackKey, Is.EqualTo(expectedKey));
	}
}
