using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Warrior kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Warrior
{
	public const string Id = "warrior";
	public const string Folder = "Warrior";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Warrior Novice Training", 1, MasteryCost(1), "Unlocks tier 1. +5% damage with swords, axes and hammers.", null, new DamageBonus { Category = DamageCategory.Weapon, Percent = 5f });
		k.Shared(1, SkillKind.Gathering, "Mining", 1);
		k.Active(1, "Double Strike", 5, Cost(1), SpLow, "Two hits at 60% each.", SkillTargetSpec.Melee(), Strike(0.6f, hits: 2)).Weapon();
		k.Passive(1, SkillKind.Normal, "Vanguard", 5, Cost(1), "+3 Strength, -1 Defense.", new StatModification { Strength = 3, Defense = -1 });
		k.Passive(1, SkillKind.Normal, "Power Boost", 5, Cost(1), "+2 Strength.", new StatModification { Strength = 2 });
		k.Passive(1, SkillKind.Normal, "Iron Skin", 5, Cost(1), "+2 Defense.", new StatModification { Defense = 2 });
		k.Passive(2, SkillKind.Mastery, "Warrior Adept Training", 1, MasteryCost(2), "Unlocks tier 2. +5% weapon damage.", null, new DamageBonus { Category = DamageCategory.Weapon, Percent = 5f });
		k.Active(2, "Cleave", 5, Cost(2), SpMed, "Strikes every adjacent enemy at 80%.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 1), Strike(0.8f)).Weapon();
		k.Active(2, "Piercing Thrust", 5, Cost(2), SpMed, "Hits the first two enemies in a line in front of you.", SkillTargetSpec.OnSelf(), new PierceLineAction { Range = 2, MaxTargets = 2, Damage = Strike(1f) }).Weapon();
		k.Passive(2, SkillKind.SingleRank, "Initiative", 1, Cost(2), "+1 action on the first turn of each floor.", null, new OpeningActionPassive { WholeParty = false });
		k.Active(2, "Sunder", 5, Cost(2), SpLow, "Damage + Weaken (-Strength).", SkillTargetSpec.Melee(), Strike(1f), Apply("WeakenStatus")).Weapon();
		k.Active(2, "Rattle", 5, Cost(2), SpLow, "Damage + Silence.", SkillTargetSpec.Melee(), Strike(1f), Apply("SilenceStatus")).Weapon();
		k.Active(2, "Flame Follow-up", 5, Cost(2), SpMed, "Hit + Fire mark: party hits on the target add a fire strike.", SkillTargetSpec.Melee(), Strike(1f), Apply("FireMarkStatus")).Weapon();
		k.Active(2, "Frost Follow-up", 5, Cost(2), SpMed, "Hit + Ice mark: party hits on the target add an ice strike.", SkillTargetSpec.Melee(), Strike(1f), Apply("IceMarkStatus")).Weapon();
		k.Active(2, "Shock Follow-up", 5, Cost(2), SpMed, "Hit + Lightning mark: party hits on the target add a lightning strike.", SkillTargetSpec.Melee(), Strike(1f), Apply("LightningMarkStatus")).Weapon();
		k.Passive(3, SkillKind.Mastery, "Warrior Master Training", 1, MasteryCost(3), "Unlocks tier 3. +5% weapon damage.", null, new DamageBonus { Category = DamageCategory.Weapon, Percent = 5f });
		k.Active(3, "Whirlwind", 5, Cost(3), SpHigh, "4-6 hits at 70% on random enemies within 2 tiles.", SkillTargetSpec.OnSelf(), new RandomHitsAction { Radius = 2, MinHits = 4, MaxHits = 6, Damage = Strike(0.7f) }).Weapon();
		k.Active(3, "Lunge", 5, Cost(3), SpMed, "Dash to the first enemy in a line (3 tiles) and strike at 150%.", SkillTargetSpec.Missile(3), new DashStrikeAction { DamagePercent = 1.5f }).Weapon();
		k.Passive(3, SkillKind.SingleRank, "Improved Follow-ups", 1, Cost(3), "Marks last 2 more turns. Follow-up hits deal +25%.", null, new FollowUpBonusPassive { Turns = 2, DamageBonus = 0.25f });
		k.Passive(3, SkillKind.SingleRank, "Follow-up Mastery", 1, Cost(3), "Follow-up hits have a 30% chance to strike twice.", null, new FollowUpChainPassive { Chance = 0.3f });
		k.Save(new StatModification { HPMax = 5, Strength = 1 });
	}
}
