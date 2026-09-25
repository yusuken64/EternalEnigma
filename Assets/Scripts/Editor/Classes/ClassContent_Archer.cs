using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Archer kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Archer
{
	public const string Id = "archer";
	public const string Folder = "Archer";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Archer Novice Training", 1, MasteryCost(1), "Unlocks tier 1. +5% bow damage.", null, new DamageBonus { Category = DamageCategory.Bow, Percent = 5f });
		k.Shared(1, SkillKind.Gathering, "Foraging", 1);
		k.Active(1, "Aimed Shot", 5, Cost(1), SpLow, "150% damage that always hits. Uses 1 arrow.", SkillTargetSpec.Missile(8, arrow: true), Strike(1.5f, category: DamageCategory.Bow, rollToHit: false, requires: EquipmentRequirement.Bow)).Arrows(1).Weapon();
		k.Active(1, "Pinning Shot", 5, Cost(1), SpLow, "Damage + Leg bind. Uses 1 arrow.", SkillTargetSpec.Missile(8, arrow: true), Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow), Apply("StuckStatus", 1f, ailment: true)).Arrows(1).Weapon();
		k.Active(1, "Disarming Shot", 5, Cost(1), SpLow, "Damage + Arm bind. Uses 1 arrow.", SkillTargetSpec.Missile(8, arrow: true), Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow), Apply("ArmBindStatus", 1f, ailment: true)).Arrows(1).Weapon();
		k.Active(1, "Stunning Shot", 5, Cost(1), SpLow, "Damage + Head bind (Silence). Uses 1 arrow.", SkillTargetSpec.Missile(8, arrow: true), Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow), Apply("SilenceStatus", 1f, ailment: true)).Arrows(1).Weapon();
		k.Passive(1, SkillKind.Normal, "Keen Eye", 5, Cost(1), "+10% hit chance.", new StatModification { HitBonus = 0.1f });
		k.Passive(2, SkillKind.Mastery, "Archer Adept Training", 1, MasteryCost(2), "Unlocks tier 2. +5% bow damage.", null, new DamageBonus { Category = DamageCategory.Bow, Percent = 5f });
		k.Passive(2, SkillKind.Normal, "Longshot", 5, Cost(2), "+3 range for arrow skills.", null, new MissileRangePassive { Tiles = 3 });
		k.Active(2, "Volley", 5, Cost(2), SpMed, "Hits the target and every enemy adjacent to it. Uses 1 arrow.", SkillTargetSpec.Missile(8, radius: 1, arrow: true), Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow)).Arrows(1).Weapon();
		k.Active(2, "Multishot", 5, Cost(2), SpMed, "3 arrows at random visible enemies. Uses 1 arrow per shot.", SkillTargetSpec.OnSelf(), new RandomHitsAction { Visible = true, MinHits = 3, MaxHits = 3, ArrowPerHit = true, Damage = Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow) }).Weapon();
		k.Active(2, "Venom Arrow", 5, Cost(2), SpLow, "Damage + Poison. Uses 1 arrow.", SkillTargetSpec.Missile(8, arrow: true), Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow), Apply("DotStatus", 1f, ailment: true)).Arrows(1).Weapon();
		k.Active(2, "Sleep Arrow", 5, Cost(2), SpMed, "Damage + Sleep. Uses 1 arrow.", SkillTargetSpec.Missile(8, arrow: true), Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow), Apply("SleepStatus", 1f, ailment: true)).Arrows(1).Weapon();
		k.Active(2, "Retreat Shot", 5, Cost(2), SpLow, "Shoot, then step one tile back. Uses 1 arrow.", SkillTargetSpec.Missile(8, arrow: true), Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow), new StepBackAction { Tiles = 1 }).Arrows(1).Weapon();
		k.Passive(2, SkillKind.Normal, "Deadeye", 5, Cost(2), "+15% critical chance with a bow.", null, new ConditionalStatPassive { Condition = StatCondition.BowEquipped, Bonus = new StatModification { CritChance = 0.15f } });
		k.Passive(3, SkillKind.Mastery, "Archer Master Training", 1, MasteryCost(3), "Unlocks tier 3. +5% bow damage.", null, new DamageBonus { Category = DamageCategory.Bow, Percent = 5f });
		k.Active(3, "Rain of Arrows", 5, Cost(3), SpHigh, "Hits every visible enemy at 70%. Uses 1 arrow per enemy.", SkillTargetSpec.AllVisible(TargetTeam.Enemies), Strike(0.7f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow)).Arrows(1, ArrowCostMode.PerTarget).Weapon();
		k.Active(3, "Piercing Arrow", 5, Cost(3), SpMed, "Passes through every enemy in a line. Uses 1 arrow.", SkillTargetSpec.OnSelf(), new PierceLineAction { Range = 8, MaxTargets = 0, Damage = Strike(1f, category: DamageCategory.Bow, requires: EquipmentRequirement.Bow) }).Arrows(1).Weapon();
		k.Passive(3, SkillKind.Normal, "Double Shot", 5, Cost(3), "25% chance for a normal bow attack to fire twice (uses a second arrow).", null, new ExtraShotPassive { Chance = 0.25f, ChancePerRank = 0.05f });
		k.Passive(3, SkillKind.Normal, "Arrow Recovery", 5, Cost(3), "30% chance a skill arrow is not used up.", null, new ArrowRecoveryPassive { Chance = 0.3f, ChancePerRank = 0.05f });
		k.Save(new StatModification { Strength = 1, HitBonus = 0.05f });
	}
}
