using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Occultist kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Occultist
{
	public const string Id = "occultist";
	public const string Folder = "Occultist";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Occultist Novice Training", 1, MasteryCost(1), "Unlocks tier 1. +10% ailment chance.", null, new AilmentChancePassive { Bonus = 0.1f });
		k.Shared(1, SkillKind.Gathering, "Harvesting", 1);
		k.Active(1, "Venom Hex", 5, Cost(1), SpLow, "Poison (80%).", SkillTargetSpec.Missile(8), Apply("DotStatus", 0.8f, ailment: true));
		k.Active(1, "Paralysis Hex", 5, Cost(1), SpLow, "Paralysis (70%).", SkillTargetSpec.Missile(8), Apply("ParalysisStatus", 0.7f, ailment: true));
		k.Active(1, "Slumber Hex", 5, Cost(1), SpLow, "Sleep (70%).", SkillTargetSpec.Missile(8), Apply("SleepStatus", 0.7f, ailment: true));
		k.Active(1, "Enfeeble", 5, Cost(1), SpLow, "Weaken (-Strength) (90%).", SkillTargetSpec.Missile(8), Apply("WeakenStatus", 0.9f, ailment: true));
		k.Passive(2, SkillKind.Mastery, "Occultist Adept Training", 1, MasteryCost(2), "Unlocks tier 2. +10% ailment chance.", null, new AilmentChancePassive { Bonus = 0.1f });
		k.Active(2, "Curse", 5, Cost(2), SpMed, "The target takes back 50% of the damage it deals (70%).", SkillTargetSpec.Missile(8), Apply("CurseStatus", 0.7f, ailment: true));
		k.Active(2, "Terror", 5, Cost(2), SpMed, "Fear: the target flees and cannot attack (60%).", SkillTargetSpec.Missile(8), Apply("FearStatus", 0.6f, ailment: true));
		k.Active(2, "Silence Hex", 5, Cost(2), SpLow, "Silence (80%).", SkillTargetSpec.Missile(8), Apply("SilenceStatus", 0.8f, ailment: true));
		k.Passive(2, SkillKind.Normal, "Malice", 5, Cost(2), "+15% ailment chance.", null, new AilmentChancePassive { Bonus = 0.15f, BonusPerRank = 0.03f });
		k.Passive(2, SkillKind.SingleRank, "Lingering Hex", 1, Cost(2), "Ailments you inflict last +2 turns.", null, new StatusDurationPassive { AilmentsOnly = true, Turns = 2 });
		k.Active(2, "Blinding Mist", 5, Cost(2), SpMed, "Blind around the impact point (80%).", SkillTargetSpec.Missile(8, radius: 1), Apply("BlindStatus", 0.8f, ailment: true));
		k.Active(2, "Exploit", 5, Cost(2), SpMed, "Damage +50% for each ailment on the target.", SkillTargetSpec.Missile(8), Spell(DamageElement.Physical, 8, 2f, perAilment: 0.5f));
		k.Passive(3, SkillKind.Mastery, "Occultist Master Training", 1, MasteryCost(3), "Unlocks tier 3. +10% ailment chance.", null, new AilmentChancePassive { Bonus = 0.1f });
		k.Active(3, "Plague", 1, Cost(3), SpHigh, "Copies the target's ailments to enemies within 2 tiles of it.", SkillTargetSpec.Missile(8), new SpreadAilmentsAction { Radius = 2 });
		k.Active(3, "Dominate", 1, Cost(3), SpHigh, "A Feared non-boss target fights for the party for 3 turns.", SkillTargetSpec.Missile(8), new DominateAction { Turns = 3 });
		k.Active(3, "Siphon", 5, Cost(3), SpMed, "Damage. Heals you 3 HP per ailment on the target.", SkillTargetSpec.Missile(8), Spell(DamageElement.Physical, 6, 1.5f, healPerAilment: 3));
		k.Shared(3, SkillKind.Normal, "SP Up", 5);
		k.Passive(3, SkillKind.Normal, "Grim Harvest", 5, Cost(3), "Restores SP when an enemy with an ailment dies.", null, new GrimHarvestPassive { SP = 1 });
		k.Save(new StatModification { HPMax = -4, SPMax = 4 });
	}
}
