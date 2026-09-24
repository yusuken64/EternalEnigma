using NUnit.Framework;

public class CombatMathTests
{
	[TestCase(-3, 1.5f)]
	[TestCase(-1, 1.5f)]
	[TestCase(0, 1f)]
	[TestCase(1, 0.5f)]
	[TestCase(2, 0f)]
	[TestCase(4, 0f)]
	public void ElementMultiplierSteps(int steps, float expectedMultiplier)
	{
		float result = ElementMath.Multiplier(steps);
		Assert.That(result, Is.EqualTo(expectedMultiplier));
	}

	[Test]
	public void EveryElementReadsItsOwnResistance()
	{
		var s = new Stats { FireResistance = 1, IceResistance = -1, LightningResistance = 2 };

		Assert.That(ElementMath.Apply(10, s, DamageElement.Fire), Is.EqualTo(5));
		Assert.That(ElementMath.Apply(10, s, DamageElement.Ice), Is.EqualTo(15));
		Assert.That(ElementMath.Apply(10, s, DamageElement.Lightning), Is.EqualTo(0));
		Assert.That(ElementMath.Apply(10, s, DamageElement.Physical), Is.EqualTo(10));
		Assert.That(ElementMath.Apply(10, null, DamageElement.Fire), Is.EqualTo(10));
	}

	[Test]
	public void StatsPlusModificationAddsEveryField()
	{
		var stats = new Stats
		{
			HPMax = 1,
			SPMax = 2,
			HungerMax = 3,
			Strength = 4,
			Defense = 5,
			EXPOnKill = 6,
			HungerAccumulateThreshold = 7,
			HPRegenAcccumlateThreshold = 8,
			SPRegenAcccumlateThreshold = 9,
			DropRate = 10f,
			ActionsPerTurnMax = 11,
			AttacksPerTurnMax = 12,
			FireResistance = 13,
			IceResistance = 14,
			LightningResistance = 15,
			CritChance = 0.16f,
			Evasion = 0.17f,
			HitBonus = 0.18f
		};

		var modification = new StatModification
		{
			HPMax = 101,
			SPMax = 102,
			HungerMax = 103,
			Strength = 104,
			Defense = 105,
			EXPOnKill = 106,
			HungerAccumulateThreshold = 107,
			HPRegenAcccumlateThreshold = 108,
			SPRegenAcccumlateThreshold = 109,
			DropRate = 110f,
			ActionsPerTurnMax = 111,
			AttacksPerTurnMax = 112,
			FireResistance = 113,
			IceResistance = 114,
			LightningResistance = 115,
			CritChance = 0.116f,
			Evasion = 0.117f,
			HitBonus = 0.118f
		};

		var result = stats + modification;

		Assert.That(result.HPMax, Is.EqualTo(102));
		Assert.That(result.SPMax, Is.EqualTo(104));
		Assert.That(result.HungerMax, Is.EqualTo(106));
		Assert.That(result.Strength, Is.EqualTo(108));
		Assert.That(result.Defense, Is.EqualTo(110));
		Assert.That(result.EXPOnKill, Is.EqualTo(112));
		Assert.That(result.HungerAccumulateThreshold, Is.EqualTo(114));
		Assert.That(result.HPRegenAcccumlateThreshold, Is.EqualTo(116));
		Assert.That(result.SPRegenAcccumlateThreshold, Is.EqualTo(118));
		Assert.That(result.DropRate, Is.EqualTo(120f));
		Assert.That(result.ActionsPerTurnMax, Is.EqualTo(122));
		Assert.That(result.AttacksPerTurnMax, Is.EqualTo(124));
		Assert.That(result.FireResistance, Is.EqualTo(126));
		Assert.That(result.IceResistance, Is.EqualTo(128));
		Assert.That(result.LightningResistance, Is.EqualTo(130));
		Assert.That(result.CritChance, Is.EqualTo(0.276f).Within(0.001f));
		Assert.That(result.Evasion, Is.EqualTo(0.287f).Within(0.001f));
		Assert.That(result.HitBonus, Is.EqualTo(0.298f).Within(0.001f));
	}

	[Test]
	public void StatModificationCopiesSPMaxAndNewFields()
	{
		var modification = new StatModification { SPMax = 3, CritChance = 0.2f, FireResistance = 1 };
		var copy = new StatModification(modification);

		Assert.That(copy.SPMax, Is.EqualTo(3));
		Assert.That(copy.CritChance, Is.EqualTo(0.2f));
		Assert.That(copy.FireResistance, Is.EqualTo(1));

		var stats = new Stats { SPMax = 5 };
		var statsCopy = new StatModification(stats);
		Assert.That(statsCopy.SPMax, Is.EqualTo(5));
	}

	[Test]
	public void SyncAndHashIncludeNewFields()
	{
		var s1 = new Stats();
		var s2 = new Stats { Evasion = 0.1f };

		int hash1 = s1.GetHashCode();
		int hash2 = s2.GetHashCode();
		Assert.That(hash1, Is.Not.EqualTo(hash2));

		s1.Sync(s2);
		Assert.That(s1.Evasion, Is.EqualTo(0.1f));
		Assert.That(s1.GetHashCode(), Is.EqualTo(hash2));
	}

	[Test]
	public void HitChanceIsClamped()
	{
		Assert.That(CombatMath.HitChance(null, null), Is.EqualTo(0.8f).Within(1e-5f));

		var attacker1 = new Stats { HitBonus = 1 };
		Assert.That(CombatMath.HitChance(attacker1, null), Is.EqualTo(1f));

		var target1 = new Stats { Evasion = 5 };
		Assert.That(CombatMath.HitChance(null, target1), Is.EqualTo(0.05f));

		var attacker2 = new Stats { HitBonus = 0.1f };
		var target2 = new Stats { Evasion = 0.3f };
		Assert.That(CombatMath.HitChance(attacker2, target2), Is.EqualTo(0.6f).Within(1e-5f));
	}

	[Test]
	public void CritMultipliesByOneAndAHalf()
	{
		Assert.That(CombatMath.ApplyCrit(10), Is.EqualTo(15));
		Assert.That(CombatMath.ApplyCrit(1), Is.EqualTo(1));
	}
}
