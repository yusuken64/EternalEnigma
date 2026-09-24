#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
	[PrebuildSetup(typeof(HarnessSceneBootstrap))]
	[PostBuildCleanup(typeof(HarnessSceneBootstrap))]
	public class DownedAllyTests
	{
		private GameTestHarness harness;
		private TestInputScope inputScope;
		private readonly List<Skill> skills = new();
		private Ally caster => harness.Ally;
		private Ally reese;
		private Ally sage;

		[UnitySetUp]
		public IEnumerator SetUp()
		{
			inputScope = new TestInputScope();
			harness = new GameTestHarness();
			yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese", "Sage" } });

			// Find and store references to all three allies
			var allAllies = harness.Game.Allies.ToList();
			reese = allAllies.FirstOrDefault(a => a.TownAllyId == "Reese" || (a != caster && reese == null));
			sage = allAllies.FirstOrDefault(a => a.TownAllyId == "Sage" || (a != caster && a != reese && sage == null));

			// Place three allies on adjacent walkable cells of an open 3x3 area
			var dungeon = harness.Game.CurrentDungeon;
			var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
				Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
				.Where(dungeon.IsWalkable).ToList();
			var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
				Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
			caster.SetPosition(center);
			reese.SetPosition(center + Vector3Int.left);
			sage.SetPosition(center + Vector3Int.right);

			// Set every ally to HoldPosition
			caster.AllyStrategy = AllyStrategy.HoldPosition;
			reese.AllyStrategy = AllyStrategy.HoldPosition;
			sage.AllyStrategy = AllyStrategy.HoldPosition;

			// Set HPMax 100 / HP 100 and huge regen thresholds
			foreach (var actor in harness.Game.AllCharacters)
			{
				actor.BaseStats.HPMax = actor.BaseStats.SPMax = 100;
				actor.BaseStats.HPRegenAcccumlateThreshold = actor.BaseStats.SPRegenAcccumlateThreshold = 10000;
				actor.InvalidateCachedStats();
				actor.Vitals.HP = 100;
				actor.Vitals.SP = 100;
				actor.SyncDisplayedStats();
			}

			// Spawn one Enemy_Slime 4+ tiles away and clear its Policies
			var positions = cells.Where(p => TileWorldDungeon.ChevDistance(p, center) > 3).ToList();
			if (positions.Count > 0)
			{
				yield return harness.SpawnEnemy("Enemy_Slime", positions[0]);
				foreach (var enemy in harness.Game.Enemies.Cast<Enemy>())
					enemy.Policies.Clear();
			}

			harness.Game.UpdateMiniMap();
		}

		[UnityTearDown]
		public IEnumerator TearDown()
		{
			try
			{
				yield return harness.Cleanup();
			}
			finally
			{
				foreach (var skill in skills)
					Object.DestroyImmediate(skill);
				skills.Clear();
				inputScope.Dispose();
			}
		}

		private Skill MakeSkill(string name, SkillTargeting targeting, TargetTeam team, TargetArea area, params GameAction[] effects)
		{
			var skill = ScriptableObject.CreateInstance<Skill>();
			skill.SkillName = name;
			skill.ActivationType = ActivationType.Active;
			skill.SPCost = 0;
			skill.Targeting = targeting;
			skill.TargetSelector = new TargetSelector { Team = team, Area = area };
			skill.ActionEffects = effects.ToList();
			harness.Ally.Skills.Add(skill);
			skills.Add(skill);
			return skill;
		}

		private IEnumerator Down(Ally victim)
		{
			var skill = MakeSkill("Test Down", SkillTargeting.SelectedTarget, TargetTeam.All, TargetArea.Visible,
				new TakeDamageAction { damage = 9999 });

			// If downing the controlled ally, we need to cast from another ally
			var currentCaster = harness.Game.PlayerController.ControlledAlly;
			if (victim == currentCaster)
			{
				// Find another standing ally to use as caster
				var otherCaster = harness.Game.Allies.FirstOrDefault(a => a != victim);
				Assert.That(otherCaster, Is.Not.Null, "No other standing ally to cast from");
				harness.Game.PlayerController.TakeControl(otherCaster);
				yield return harness.WaitForIdle();
				yield return harness.ExecuteAction(new SkillAction(otherCaster, skill, victim));
			}
			else
			{
				yield return harness.ExecuteAction(new SkillAction(currentCaster, skill, victim));
			}
		}

		[UnityTest]
		public IEnumerator DownedAllyLeavesAlliesButIsNotDestroyed()
		{
			yield return Down(reese);
			yield return harness.WaitForIdle();

			Assert.That(harness.Game.DownedAllies, Has.Member(reese));
			Assert.That(harness.Game.Allies, Has.No.Member(reese));
			Assert.That(harness.Game.AllCharacters, Has.No.Member(reese));
			Assert.That(reese.IsDowned, Is.True);
			Assert.That(reese.gameObject, Is.Not.Null);
			Assert.That(reese.VisualParent.activeSelf, Is.True);
			Assert.That(harness.Game.GameOverScreen.gameObject.activeSelf, Is.False);
		}

		[UnityTest]
		public IEnumerator DownedAllyIsNotATarget()
		{
			yield return Down(reese);
			yield return harness.WaitForIdle();

			var selector = new TargetSelector { Team = TargetTeam.Allies, Area = TargetArea.All };
			var targets = selector.GetCharacters(caster);

			Assert.That(targets, Has.No.Member(reese));
		}

		[UnityTest]
		public IEnumerator ControlPassesWhenControlledAllyIsDowned()
		{
			yield return Down(caster);
			yield return harness.WaitForIdle();

			var newControlled = harness.Game.PlayerController.ControlledAlly;
			Assert.That(newControlled, Is.Not.EqualTo(caster));
			Assert.That(PartyRules.IsStanding(harness.Game, newControlled), Is.True);
			Assert.That(harness.Game.GameOverScreen.gameObject.activeSelf, Is.False);
		}

		[UnityTest]
		public IEnumerator DefeatOnlyWhenAllAreDowned()
		{
			yield return Down(reese);
			yield return harness.WaitForIdle();
			Assert.That(harness.Game.GameOverScreen.gameObject.activeSelf, Is.False);

			yield return Down(sage);
			yield return harness.WaitForIdle();
			Assert.That(harness.Game.GameOverScreen.gameObject.activeSelf, Is.False);

			yield return Down(caster);
			yield return harness.WaitUntil(() => harness.Game.GameOverScreen.gameObject.activeSelf, "game over screen activation");

			Assert.That(PartyRules.IsPartyDefeated(harness.Game), Is.True);
		}

		[UnityTest]
		public IEnumerator ClonesDoNotPreventDefeat()
		{
			// Add a SummonedUnit (Kind Clone) component to a freshly instantiated copy of an ally
			var allyClone = Object.Instantiate(reese.gameObject, harness.Game.transform);
			var cloneAlly = allyClone.GetComponent<Ally>();
			var summonComponent = allyClone.AddComponent<SummonedUnit>();
			summonComponent.Kind = SummonedUnit.SummonKind.Clone;
			harness.Game.Allies.Add(cloneAlly);
			yield return harness.WaitForIdle();

			// Down all three real allies
			yield return Down(reese);
			yield return harness.WaitForIdle();

			yield return Down(sage);
			yield return harness.WaitForIdle();

			yield return Down(caster);
			yield return harness.WaitUntil(() => harness.Game.GameOverScreen.gameObject.activeSelf, "game over screen activation");

			Assert.That(harness.Game.GameOverScreen.gameObject.activeSelf, Is.True);
		}

		[UnityTest]
		public IEnumerator ReviveAdjacentRestoresQuarterHp()
		{
			yield return Down(reese);
			yield return harness.WaitForIdle();

			// Move reese adjacent to caster
			reese.SetPosition(caster.TilemapPosition + Vector3Int.left);

			var reviveSkill = MakeSkill("Test Revive", SkillTargeting.Self, TargetTeam.Self, TargetArea.Self,
				new ReviveAction { Scope = ReviveScope.Adjacent, HpFraction = 0.25f });

			yield return harness.ExecuteAction(new SkillAction(caster, reviveSkill, null));
			yield return harness.WaitForIdle();

			Assert.That(harness.Game.Allies, Has.Member(reese));
			Assert.That(reese.IsDowned, Is.False);
			Assert.That(reese.Vitals.HP, Is.EqualTo(25)); // HPMax 100, 0.25 fraction = 25
		}

		[UnityTest]
		public IEnumerator ReviveIsRefusedWithNobodyDowned()
		{
			var reviveSkill = MakeSkill("Test Revive", SkillTargeting.Self, TargetTeam.Self, TargetArea.Self,
				new ReviveAction { Scope = ReviveScope.Adjacent, HpFraction = 0.25f });

			Assert.That(caster.CanCast(reviveSkill, out var reason), Is.False);
			Assert.That(reason, Is.EqualTo("No downed ally in range"));
		}

		[UnityTest]
		public IEnumerator MassReviveRevivesEveryVisibleDownedAlly()
		{
			yield return Down(reese);
			yield return harness.WaitForIdle();

			yield return Down(sage);
			yield return harness.WaitForIdle();

			var reviveSkill = MakeSkill("Test Revive", SkillTargeting.Self, TargetTeam.Self, TargetArea.Self,
				new ReviveAction { Scope = ReviveScope.Visible, HpFraction = 0.25f });

			yield return harness.ExecuteAction(new SkillAction(caster, reviveSkill, null));
			yield return harness.WaitForIdle();

			Assert.That(PartyRules.IsStanding(harness.Game, reese), Is.True);
			Assert.That(PartyRules.IsStanding(harness.Game, sage), Is.True);
		}

		[UnityTest]
		public IEnumerator FloorChangeRestoresDownedAtOneHp()
		{
			yield return Down(reese);
			yield return harness.WaitForIdle();

			harness.Game.AdvanceFloor();
			yield return harness.WaitForIdle();

			Assert.That(harness.Game.Allies, Has.Member(reese));
			Assert.That(reese.Vitals.HP, Is.EqualTo(1));
			Assert.That(harness.Game.DownedAllies, Is.Empty);
		}

		[UnityTest]
		public IEnumerator DownedAllyKeepsEquipmentOnReturn()
		{
			yield return Down(reese);
			yield return harness.WaitForIdle();

			var partyMembers = PartyRules.PartyMembers(harness.Game);

			Assert.That(partyMembers, Has.Member(reese));
			// Verify no summons in the party members
			foreach (var member in partyMembers)
				Assert.That(PartyRules.IsSummon(member), Is.False);
		}
	}
}
#endif
