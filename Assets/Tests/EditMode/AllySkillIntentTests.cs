using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests
{
	public class AllySkillIntentTests
	{
		private readonly List<Object> created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
			created.Clear();
		}

		private Skill MakeSkill(TargetTeam team, SkillTargeting targeting, params GameAction[] effects)
		{
			var s = ScriptableObject.CreateInstance<Skill>();
			created.Add(s);
			s.ActivationType = ActivationType.Active;
			s.SPCost = 2;
			s.Targeting = targeting;
			s.TargetSelector = new TargetSelector { Team = team, Area = TargetArea.Visible };
			s.ActionEffects = new List<GameAction>(effects);
			return s;
		}

		private T MakeStatus<T>() where T : StatusEffect
		{
			var go = new GameObject(typeof(T).Name);
			created.Add(go);
			var s = go.AddComponent<T>();
			s.TurnsLeft = 3;
			return s;
		}

		[Test]
		public void DamageOnEnemiesIsDamage()
		{
			// Damage on enemies should classify as Damage
			var skill = MakeSkill(TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 5 });
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Damage), Is.True);

			// Damage on allies should NOT classify as Damage
			var allySkill = MakeSkill(TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 5 });
			var allyIntent = SkillIntents.Classify(allySkill);
			Assert.That(allyIntent.Has(SkillIntent.Damage), Is.False);
		}

		[Test]
		public void HealIsHealAndReserveSkill()
		{
			// Heal should classify as Heal
			var skill = MakeSkill(TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeHealAction { healing = 10 });
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Heal), Is.True);

			// Heal should be a reserve skill
			Assert.That(AllySkillBudget.IsReserveSkill(skill), Is.True);
		}

		[Test]
		public void SleepOnEnemiesIsCrowdControl()
		{
			// Sleep on enemies should classify as CrowdControl
			var skill = MakeSkill(TargetTeam.Enemies, SkillTargeting.SelectedTarget,
				new ApplyStatusEffectAction { StatusEffect = MakeStatus<SleepStatusEffect>() });
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.CrowdControl), Is.True);
		}

		[Test]
		public void DotOnEnemiesIsDebuff()
		{
			// Dot on enemies should classify as Debuff
			var skill = MakeSkill(TargetTeam.Enemies, SkillTargeting.SelectedTarget,
				new ApplyStatusEffectAction { StatusEffect = MakeStatus<DotStatusEffect>() });
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Debuff), Is.True);

			// Should NOT classify as CrowdControl
			Assert.That(intent.Has(SkillIntent.CrowdControl), Is.False);
		}

		[Test]
		public void StrengthOnSelfIsBuff()
		{
			// Strength on self should classify as Buff
			var skill = MakeSkill(TargetTeam.Self, SkillTargeting.Self,
				new ApplyStatusEffectAction { StatusEffect = MakeStatus<StrengthStatusEffect>() });
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Buff), Is.True);
		}

		[Test]
		public void RetreatIsNever()
		{
			// Retreat should classify as Never
			var skill = MakeSkill(TargetTeam.Self, SkillTargeting.Self, new RetreatAction());
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Never), Is.True);
		}

		[Test]
		public void ReviveIsRevive()
		{
			// Revive should classify as Revive
			var skill = MakeSkill(TargetTeam.Allies, SkillTargeting.SelectedTarget, new ReviveAction());
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Revive), Is.True);
		}

		[Test]
		public void DashStrikeIsDamageAndMovement()
		{
			// DashStrike on enemies should classify as Damage and Movement
			var skill = MakeSkill(TargetTeam.Enemies, SkillTargeting.SelectedTarget, new DashStrikeAction());
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Damage), Is.True);
			Assert.That(intent.Has(SkillIntent.Movement), Is.True);
		}

		[Test]
		public void CureEffectIsCure()
		{
			// CureStatusEffectsAction should classify as Cure
			var skill = MakeSkill(TargetTeam.Allies, SkillTargeting.SelectedTarget, new CureStatusEffectsAction());
			var intent = SkillIntents.Classify(skill);
			Assert.That(intent.Has(SkillIntent.Cure), Is.True);
		}

		[Test]
		public void PassiveOrInventoryOrNullIsNone()
		{
			// Passive skill should classify as None
			var passiveSkill = MakeSkill(TargetTeam.Self, SkillTargeting.Self, new RetreatAction());
			passiveSkill.ActivationType = ActivationType.Passive;
			var passiveIntent = SkillIntents.Classify(passiveSkill);
			Assert.That(passiveIntent, Is.EqualTo(SkillIntent.None));

			// InventoryItem skill should classify as None
			var inventorySkill = MakeSkill(TargetTeam.Self, SkillTargeting.InventoryItem, new TakeDamageAction { damage = 5 });
			var inventoryIntent = SkillIntents.Classify(inventorySkill);
			Assert.That(inventoryIntent, Is.EqualTo(SkillIntent.None));

			// Null skill should classify as None
			var nullIntent = SkillIntents.Classify(null);
			Assert.That(nullIntent, Is.EqualTo(SkillIntent.None));
		}

		[Test]
		public void SpReserveIsHighestHealCostAndHalvedWhenAggressive()
		{
			// Create heal skill with SPCost 4
			var healSkill = MakeSkill(TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeHealAction { healing = 10 });
			healSkill.SPCost = 4;

			// Create damage skill with SPCost 9
			var damageSkill = MakeSkill(TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 5 });
			damageSkill.SPCost = 9;

			var skills = new List<Skill> { healSkill, damageSkill };

			// Follow strategy: reserve should be 4 (highest heal/revive cost)
			Assert.That(AllySkillBudget.SpReserve(skills, AllyStrategy.Follow), Is.EqualTo(4));

			// Aggressive strategy: reserve should be 2 (4 / 2)
			Assert.That(AllySkillBudget.SpReserve(skills, AllyStrategy.Aggresive), Is.EqualTo(2));

			// No heal skills: reserve should be 0
			var damageOnlySkills = new List<Skill> { damageSkill };
			Assert.That(AllySkillBudget.SpReserve(damageOnlySkills, AllyStrategy.Follow), Is.EqualTo(0));
		}

		[Test]
		public void CanAffordRespectsReserve()
		{
			// Create a damage skill with SPCost 2
			var damageSkill = MakeSkill(TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 5 });
			damageSkill.SPCost = 2;

			// Create a heal skill with SPCost 4
			var healSkill = MakeSkill(TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeHealAction { healing = 10 });
			healSkill.SPCost = 4;

			int reserve = 4;

			// Damage skill with SP 5 and reserve 4: 5 - 2 = 3, which is < 4, so false
			Assert.That(AllySkillBudget.CanAfford(5, damageSkill, reserve), Is.False);

			// Damage skill with SP 6 and reserve 4: 6 - 2 = 4, which is >= 4, so true
			Assert.That(AllySkillBudget.CanAfford(6, damageSkill, reserve), Is.True);

			// Heal skill with SP 4 and reserve 4: heal is reserve skill, so true
			Assert.That(AllySkillBudget.CanAfford(4, healSkill, reserve), Is.True);

			// Damage skill with SP below cost: false
			Assert.That(AllySkillBudget.CanAfford(1, damageSkill, reserve), Is.False);
		}

		[Test]
		public void ArrowsAllowKeepsThree()
		{
			// (10, 3): 10 - 3 = 7 >= 3 → true
			Assert.That(AllySkillBudget.ArrowsAllow(10, 3), Is.True);

			// (5, 3): 5 - 3 = 2 < 3 → false
			Assert.That(AllySkillBudget.ArrowsAllow(5, 3), Is.False);

			// (0, 0): required <= 0 → true
			Assert.That(AllySkillBudget.ArrowsAllow(0, 0), Is.True);

			// (3, 1): 3 - 1 = 2 < 3 → false
			Assert.That(AllySkillBudget.ArrowsAllow(3, 1), Is.False);

			// (4, 1): 4 - 1 = 3 >= 3 → true
			Assert.That(AllySkillBudget.ArrowsAllow(4, 1), Is.True);
		}

		[Test]
		public void EmergencyBelowThirtyPercent()
		{
			// (29, 100): 29 < 30 → true
			Assert.That(AllySkillBudget.IsEmergency(29, 100), Is.True);

			// (30, 100): 30 is not < 30 → false
			Assert.That(AllySkillBudget.IsEmergency(30, 100), Is.False);

			// (0, 100): 0 is not > 0 → false
			Assert.That(AllySkillBudget.IsEmergency(0, 100), Is.False);

			// (5, 0): hpMax is not > 0 → false
			Assert.That(AllySkillBudget.IsEmergency(5, 0), Is.False);
		}

		[Test]
		public void DangerScoreDoublesForBosses()
		{
			// DangerScore(10, 100, 100, false): health = 100/100 = 1, score = 10 * (0.5 + 1) * 1 = 15
			Assert.That(AllySkillBudget.DangerScore(10, 100, 100, false), Is.EqualTo(15));

			// Boss: score should be doubled → 30
			Assert.That(AllySkillBudget.DangerScore(10, 100, 100, true), Is.EqualTo(30));

			// hpMax 0: health = 0, score = 10 * (0.5 + 0) * 1 = 5
			Assert.That(AllySkillBudget.DangerScore(10, 100, 0, false), Is.EqualTo(5));
		}

		[Test]
		public void CureMatchesConfiguredGroups()
		{
			// Default (CureAilments = true, CureBinds = false)
			var defaultCure = new CureStatusEffectsAction();

			// Should cure Sleep (ailment)
			Assert.That(defaultCure.Cures(MakeStatus<SleepStatusEffect>()), Is.True);

			// Should cure Dot (ailment)
			Assert.That(defaultCure.Cures(MakeStatus<DotStatusEffect>()), Is.True);

			// Should NOT cure Stuck (bind)
			Assert.That(defaultCure.Cures(MakeStatus<StuckStatusEffect>()), Is.False);

			// Custom: CureAilments = false, CureBinds = true
			var customCure = new CureStatusEffectsAction { CureAilments = false, CureBinds = true };

			// Should NOT cure Sleep
			Assert.That(customCure.Cures(MakeStatus<SleepStatusEffect>()), Is.False);

			// Should cure Stuck
			Assert.That(customCure.Cures(MakeStatus<StuckStatusEffect>()), Is.True);

			// ExtraEffectNames with Strength
			var extraCure = new CureStatusEffectsAction { ExtraEffectNames = new List<string> { "Strength" } };

			// Should cure Strength (extra effect)
			Assert.That(extraCure.Cures(MakeStatus<StrengthStatusEffect>()), Is.True);

			// Cures(null) should return false
			Assert.That(defaultCure.Cures(null), Is.False);
		}
	}
}
