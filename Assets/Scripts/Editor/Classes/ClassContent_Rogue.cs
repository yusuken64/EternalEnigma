using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Rogue kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Rogue
{
	public const string Id = "rogue";
	public const string Folder = "Rogue";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Rogue Novice Training", 1, MasteryCost(1), "Unlocks tier 1. +5% evasion.", new StatModification { Evasion = 0.05f });
		k.Shared(1, SkillKind.Gathering, "Foraging", 1);
		k.Active(1, "Shadow Step", 5, Cost(1), SpLow, "Teleports behind an enemy within 4 tiles and strikes.", SkillTargetSpec.Selected(TargetTeam.Enemies), new TeleportBehindAction { MaxRange = 4, DamagePercent = 1f }).Weapon();
		k.Active(1, "Smoke Bomb", 5, Cost(1), SpLow, "Blinds adjacent enemies.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 1), Apply("BlindStatus", 1f, ailment: true));
		k.Passive(1, SkillKind.Normal, "Evasion", 5, Cost(1), "+10% evasion.", new StatModification { Evasion = 0.1f });
		k.Active(1, "Hamstring", 5, Cost(1), SpLow, "Damage + Leg bind.", SkillTargetSpec.Melee(), Strike(1f), Apply("StuckStatus", 1f, ailment: true)).Weapon();
		k.Passive(2, SkillKind.Mastery, "Rogue Adept Training", 1, MasteryCost(2), "Unlocks tier 2. +5% evasion.", new StatModification { Evasion = 0.05f });
		k.Passive(2, SkillKind.Normal, "Swiftness", 5, Cost(2), "15% chance of an extra action each turn.", null, new SwiftnessPassive { Chance = 0.15f, ChancePerRank = 0.03f });
		k.Active(2, "Clone", 3, Cost(2), SpHigh, "Summons a clone with 50% stats for 10 turns.", SkillTargetSpec.OnSelf(), new SummonCloneAction { StatPercent = 0.5f, Turns = 10 });
		k.Active(2, "Vanish", 5, Cost(2), SpMed, "Stealth until you attack. Enemies ignore you.", SkillTargetSpec.OnSelf(), Apply("StealthStatus"));
		k.Passive(2, SkillKind.Normal, "Ambush", 5, Cost(2), "Attacks from stealth deal double damage.", null, new AmbushBonus { Multiplier = 2f, MultiplierPerRank = 0.1f });
		k.Active(2, "Caltrops", 5, Cost(2), SpLow, "Places a visible trap that Sticks the enemy that steps on it.", SkillTargetSpec.OnSelf(), new PlaceTrapAction());
		k.Active(2, "Ember Scroll", 5, Cost(2), SpMed, "Fire damage to adjacent enemies.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 1), Spell(DamageElement.Fire, 6, 1.5f));
		k.Active(2, "Throat Strike", 5, Cost(2), SpLow, "Damage + Head bind (Silence).", SkillTargetSpec.Melee(), Strike(1f), Apply("SilenceStatus", 1f, ailment: true)).Weapon();
		k.Passive(3, SkillKind.Mastery, "Rogue Master Training", 1, MasteryCost(3), "Unlocks tier 3. +5% evasion.", new StatModification { Evasion = 0.05f });
		k.Active(3, "Shadow Dance", 5, Cost(3), SpHigh, "Teleports to and strikes each enemy within 2 tiles once.", SkillTargetSpec.OnSelf(), new ShadowDanceAction { Radius = 2, DamagePercent = 1f }).Weapon();
		k.Passive(3, SkillKind.SingleRank, "Twin Shadows", 1, Cost(3), "Up to 2 clones at once.", null, new SummonLimitBonus { ExtraClones = 1 });
		k.Active(3, "Assassinate", 5, Cost(3), SpMed, "Kills a non-boss enemy below 20% HP. Otherwise a normal hit.", SkillTargetSpec.Melee(), Strike(1f, executeBelow: 0.2f)).Weapon();
		k.Passive(3, SkillKind.SingleRank, "Escape Artist", 1, Cost(3), "Immune to Leg bind. Kills from stealth keep you hidden.", null, new StatusImmunityPassive { EffectName = "Stuck" }, new StealthOnKillPassive());
		k.Active(3, "Blinding Flash", 5, Cost(3), SpMed, "Blinds enemies within 2 tiles.", SkillTargetSpec.AroundSelf(TargetTeam.Enemies, 2), Apply("BlindStatus", 1f, ailment: true));
		k.Save(new StatModification { Strength = 1, Evasion = 0.05f });
	}
}
