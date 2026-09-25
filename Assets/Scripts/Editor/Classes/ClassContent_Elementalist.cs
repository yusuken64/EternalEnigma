using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Elementalist kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Elementalist
{
	public const string Id = "elementalist";
	public const string Folder = "Elementalist";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Elementalist Novice Training", 1, MasteryCost(1), "Unlocks tier 1. +5% elemental damage.", null, new DamageBonus { Category = DamageCategory.Magic, Percent = 5f });
		k.Shared(1, SkillKind.Gathering, "Harvesting", 1);
		k.Active(1, "Fire Bolt", 5, Cost(1), SpLow, "Fire damage.", SkillTargetSpec.Missile(8), Spell(DamageElement.Fire, 8, 2f, canEcho: true));
		k.Active(1, "Ice Bolt", 5, Cost(1), SpLow, "Ice damage.", SkillTargetSpec.Missile(8), Spell(DamageElement.Ice, 8, 2f, canEcho: true));
		k.Active(1, "Lightning Bolt", 5, Cost(1), SpLow, "Lightning damage.", SkillTargetSpec.Missile(8), Spell(DamageElement.Lightning, 8, 2f, canEcho: true));
		k.Shared(1, SkillKind.Normal, "SP Up", 5);
		k.Passive(1, SkillKind.Normal, "Focus", 5, Cost(1), "+10% elemental damage.", null, new DamageBonus { Category = DamageCategory.Magic, Percent = 10f, PercentPerRank = 2f });
		k.Passive(2, SkillKind.Mastery, "Elementalist Adept Training", 1, MasteryCost(2), "Unlocks tier 2. +5% elemental damage.", null, new DamageBonus { Category = DamageCategory.Magic, Percent = 5f });
		k.Active(2, "Fireball", 5, Cost(2), SpMed, "Fire damage around the impact point.", SkillTargetSpec.Missile(8, radius: 1), Spell(DamageElement.Fire, 6, 1.5f));
		k.Active(2, "Blizzard", 5, Cost(2), SpMed, "Ice damage around the impact point.", SkillTargetSpec.Missile(8, radius: 1), Spell(DamageElement.Ice, 6, 1.5f));
		k.Active(2, "Thunderclap", 5, Cost(2), SpMed, "Lightning damage around the impact point.", SkillTargetSpec.Missile(8, radius: 1), Spell(DamageElement.Lightning, 6, 1.5f));
		k.Active(2, "Expose", 5, Cost(2), SpLow, "Target elemental resistances -1 step.", SkillTargetSpec.Missile(8), Apply("ExposedStatus", 1f, ailment: true));
		k.Active(2, "Amplify", 1, Cost(2), SpLow, "Your next elemental spell deals +50%.", SkillTargetSpec.OnSelf(), Apply("AmplifyStatus"));
		k.Passive(2, SkillKind.Normal, "Mana Flow", 5, Cost(2), "Faster SP regeneration.", new StatModification { SPRegenAcccumlateThreshold = -100 }).RankStep(50);
		k.Passive(3, SkillKind.Mastery, "Elementalist Master Training", 1, MasteryCost(3), "Unlocks tier 3. +5% elemental damage.", null, new DamageBonus { Category = DamageCategory.Magic, Percent = 5f });
		k.Active(3, "Inferno", 5, Cost(3), SpHigh, "Fire damage to every visible enemy.", SkillTargetSpec.AllVisible(TargetTeam.Enemies), Spell(DamageElement.Fire, 5, 1.5f));
		k.Active(3, "Glacier", 5, Cost(3), SpHigh, "Ice damage to every visible enemy.", SkillTargetSpec.AllVisible(TargetTeam.Enemies), Spell(DamageElement.Ice, 5, 1.5f));
		k.Active(3, "Tempest", 5, Cost(3), SpHigh, "Lightning damage to every visible enemy.", SkillTargetSpec.AllVisible(TargetTeam.Enemies), Spell(DamageElement.Lightning, 5, 1.5f));
		k.Passive(3, SkillKind.Normal, "Elemental Mastery", 5, Cost(3), "Elemental hits can inflict Burn (fire), Stuck (ice) or Paralysis (lightning).", null, new ElementalProcPassive { Chance = 0.15f, ChancePerRank = 0.05f });
		k.Passive(3, SkillKind.Normal, "Spell Echo", 5, Cost(3), "20% chance for a bolt to strike twice.", null, new SpellEchoPassive { Chance = 0.2f, ChancePerRank = 0.05f });
		k.Save(new StatModification { HPMax = -4, SPMax = 5 });
	}
}
