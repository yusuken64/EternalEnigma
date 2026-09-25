using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Healer kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Healer
{
	public const string Id = "healer";
	public const string Folder = "Healer";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Healer Novice Training", 1, MasteryCost(1), "Unlocks tier 1. +10% healing.", null, new HealingBonus { Percent = 10f });
		k.Shared(1, SkillKind.Gathering, "Harvesting", 1);
		k.Active(1, "Heal", 5, Cost(1), SpLow, "Restores HP to one ally.", SkillTargetSpec.Selected(TargetTeam.Allies), Heal(10, 2f));
		k.Passive(1, SkillKind.Normal, "Healing Touch", 5, Cost(1), "+15% healing.", null, new HealingBonus { Percent = 15f, PercentPerRank = 3f });
		k.Active(1, "Cure", 1, Cost(1), SpLow, "Removes ailments from one ally.", SkillTargetSpec.Selected(TargetTeam.Allies), new CleanseAction { Ailments = true });
		k.Active(1, "Unbind", 1, Cost(1), SpLow, "Removes binds from one ally.", SkillTargetSpec.Selected(TargetTeam.Allies), new CleanseAction { Binds = true });
		k.Passive(2, SkillKind.Mastery, "Healer Adept Training", 1, MasteryCost(2), "Unlocks tier 2. +10% healing.", null, new HealingBonus { Percent = 10f });
		k.Active(2, "Group Heal", 5, Cost(2), SpMed, "Restores HP to allies within 2 tiles.", SkillTargetSpec.AroundSelf(TargetTeam.Allies, 2), Heal(8, 1.5f));
		k.Active(2, "Revive", 1, Cost(2), SpHigh, "Revives an adjacent downed ally at 25% HP.", SkillTargetSpec.OnSelf(), new ReviveAction { Scope = ReviveScope.Adjacent, HpFraction = 0.25f });
		k.Active(2, "Renew", 5, Cost(2), SpLow, "Regeneration over time on one ally.", SkillTargetSpec.Selected(TargetTeam.Allies), Apply("HotStatus"));
		k.Passive(2, SkillKind.Normal, "Triage", 5, Cost(2), "+50% healing on allies below 30% HP.", null, new TriagePassive { Threshold = 0.3f, Multiplier = 1.5f, MultiplierPerRank = 0.1f });
		k.Passive(2, SkillKind.Normal, "Field Medicine", 5, Cost(2), "The party recovers 10% HP on each new floor.", null, new FieldMedicinePassive { HealFraction = 0.1f, HealFractionPerRank = 0.02f });
		k.Active(2, "Heavy Strike", 5, Cost(2), SpLow, "Weapon hit with a 30% chance to Stun.", SkillTargetSpec.Melee(), Strike(1f), Apply("StunStatus", 0.3f, ailment: true)).Weapon();
		k.Passive(3, SkillKind.Mastery, "Healer Master Training", 1, MasteryCost(3), "Unlocks tier 3. +10% healing.", null, new HealingBonus { Percent = 10f });
		k.Active(3, "Full Heal", 5, Cost(3), SpHigh, "Large heal to every visible ally.", SkillTargetSpec.AllVisible(TargetTeam.Allies), Heal(20, 3f));
		k.Active(3, "Mass Cure", 1, Cost(3), SpMed, "Removes ailments and binds from every visible ally.", SkillTargetSpec.AllVisible(TargetTeam.Allies), new CleanseAction { Ailments = true, Binds = true });
		k.Passive(3, SkillKind.SingleRank, "Second Wind", 1, Cost(3), "Once per floor, heals an ally who drops below 10% HP.", null, new SecondWindPassive { Threshold = 0.1f, HealFraction = 0.3f });
		k.Active(3, "Mass Revive", 1, Cost(3), SpHigh, "Revives every visible downed ally at 25% HP.", SkillTargetSpec.OnSelf(), new ReviveAction { Scope = ReviveScope.Visible, HpFraction = 0.25f });
		k.Passive(3, SkillKind.Normal, "Meditation", 5, Cost(3), "Faster SP regeneration.", new StatModification { SPRegenAcccumlateThreshold = -100 }).RankStep(50);
		k.Passive(3, SkillKind.Normal, "Vitality", 5, Cost(3), "+10 HPMax.", new StatModification { HPMax = 10 }).RankStep(5);
		k.Save(new StatModification { SPMax = 4 });
	}
}
