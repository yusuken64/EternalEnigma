using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Guardian kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Guardian
{
	public const string Id = "guardian";
	public const string Folder = "Guardian";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Guardian Novice Training", 1, MasteryCost(1), "Unlocks tier 1. +1 Defense with a shield equipped.", null, new ConditionalStatPassive { Condition = StatCondition.ShieldEquipped, Bonus = new StatModification { Defense = 1 } });
		k.Shared(1, SkillKind.Gathering, "Mining", 1);
		k.Existing(1, SkillKind.Normal, "Assets/Prefabs/Dungeon/Skills/SkillsData/Passive_HPUp.asset", 5);
		k.Active(1, "Provoke", 5, Cost(1), SpLow, "Taunts enemies within 2 tiles.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 2), Apply("TauntStatus"));
		k.Active(1, "Shield Smite", 5, Cost(1), SpLow, "Damage scaled by your shield. Needs a shield.", SkillTargetSpec.Melee(), Strike(1f, scaling: DamageScaling.ShieldStrength, requires: EquipmentRequirement.Shield)).Weapon();
		k.Passive(1, SkillKind.Normal, "Defense Boost", 5, Cost(1), "+2 Defense.", new StatModification { Defense = 2 });
		k.Active(1, "Rally", 1, Cost(1), SpMed, "Removes ailments and binds from you and adjacent allies.", SkillTargetSpec.AroundSelf(TargetTeam.Allies, 1), new CleanseAction { Ailments = true, Binds = true });
		k.Passive(2, SkillKind.Mastery, "Guardian Adept Training", 1, MasteryCost(2), "Unlocks tier 2. +1 Defense with a shield equipped.", null, new ConditionalStatPassive { Condition = StatCondition.ShieldEquipped, Bonus = new StatModification { Defense = 1 } });
		k.Passive(2, SkillKind.Normal, "HP Regen", 3, Cost(2), "Faster HP regeneration.", new StatModification { HPRegenAcccumlateThreshold = -1 });
		k.Passive(2, SkillKind.Normal, "Last Stand", 5, Cost(2), "+4 Defense while below 25% HP.", null, new ConditionalStatPassive { Condition = StatCondition.HpBelowFraction, HpFraction = 0.25f, Bonus = new StatModification { Defense = 4 } });
		k.Passive(2, SkillKind.Normal, "Cover", 5, Cost(2), "30% chance to take an attack aimed at an adjacent ally.", null, new CoverPassive { Chance = 0.3f, ChancePerRank = 0.05f });
		k.Active(2, "Shield Bash", 5, Cost(2), SpLow, "Damage + Silence.", SkillTargetSpec.Melee(), Strike(1f), Apply("SilenceStatus")).Weapon();
		k.Active(2, "Flame Barrier", 5, Cost(2), SpMed, "Fire damage to the party -75% until your next turn.", SkillTargetSpec.AllVisible(TargetTeam.Allies), Apply("FireBarrierStatus"));
		k.Active(2, "Frost Barrier", 5, Cost(2), SpMed, "Ice damage to the party -75% until your next turn.", SkillTargetSpec.AllVisible(TargetTeam.Allies), Apply("IceBarrierStatus"));
		k.Active(2, "Storm Barrier", 5, Cost(2), SpMed, "Lightning damage to the party -75% until your next turn.", SkillTargetSpec.AllVisible(TargetTeam.Allies), Apply("LightningBarrierStatus"));
		k.Passive(3, SkillKind.Mastery, "Guardian Master Training", 1, MasteryCost(3), "Unlocks tier 3. +1 Defense with a shield equipped.", null, new ConditionalStatPassive { Condition = StatCondition.ShieldEquipped, Bonus = new StatModification { Defense = 1 } });
		k.Active(3, "Parry Stance", 5, Cost(3), SpMed, "Taunts enemies within 2 tiles and blocks 50% of adjacent hits for 2 turns.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 2), Apply("TauntStatus"), Apply("ParryStatus", onCaster: true));
		k.Active(3, "Bulwark", 5, Cost(3), SpHigh, "All damage to the party -50% until your next turn.", SkillTargetSpec.AllVisible(TargetTeam.Allies), Apply("BulwarkStatus"));
		k.Active(3, "Sanctuary Wall", 5, Cost(3), SpHigh, "Each visible ally absorbs the next 10 damage within 3 turns.", SkillTargetSpec.AllVisible(TargetTeam.Allies), Apply("SanctuaryShieldStatus"));
		k.Passive(3, SkillKind.SingleRank, "Aegis", 1, Cost(3), "Barriers last 1 extra turn.", null, new StatusDurationPassive { Family = BuffFamily.Barrier, Turns = 1 });
		k.Save(new StatModification { HPMax = 10, Defense = 2 });
	}
}
