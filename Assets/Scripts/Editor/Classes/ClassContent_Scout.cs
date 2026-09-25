using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Scout kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Scout
{
	public const string Id = "scout";
	public const string Folder = "Scout";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Scout Novice Training", 1, MasteryCost(1), "Unlocks tier 1. Hunger drains slower.", new StatModification { HungerAccumulateThreshold = 2 });
		k.Passive(1, SkillKind.Gathering, "Survey", 1, Cost(1), "Can harvest any gathering point type.", null);
		k.Passive(1, SkillKind.Normal, "Trap Sense", 5, Cost(1), "Reveals traps within 2 tiles.", null);
		k.Active(1, "Disarm", 5, Cost(1), SpLow, "Removes an adjacent trap; 50% chance to recover Trap Parts.", SkillTargetSpec.OnSelf(), new DisarmTrapAction());
		k.Passive(1, SkillKind.Normal, "Pathfinder", 5, Cost(1), "+10 HungerMax.", new StatModification { HungerMax = 10 }).RankStep(5);
		k.Active(1, "Floor Sense", 1, Cost(1), SpMed, "Reveals the floor layout and stairs.", SkillTargetSpec.OnSelf(), new RevealFloorLayoutAction());
		k.Passive(2, SkillKind.Mastery, "Scout Adept Training", 1, MasteryCost(2), "Unlocks tier 2. Hunger drains slower.", new StatModification { HungerAccumulateThreshold = 2 });
		k.Passive(2, SkillKind.SingleRank, "Soft Step", 1, Cost(2), "Sleeping enemies do not wake when the party passes.", null);
		k.Passive(2, SkillKind.Normal, "Resourceful", 5, Cost(2), "25% chance a consumable you use is not used up.", null, new ResourcefulPassive { Chance = 0.25f, ChancePerRank = 0.05f });
		k.Passive(2, SkillKind.Normal, "Throwing Arm", 5, Cost(2), "Thrown items deal +50% damage.", null, new ThrowingArmPassive { Multiplier = 1.5f, MultiplierPerRank = 0.1f });
		k.Active(2, "Retreat", 1, Cost(2), SpHigh, "Returns the party to town under victory rules. Not usable on boss floors.", SkillTargetSpec.OnSelf(), new RetreatAction());
		k.Passive(2, SkillKind.SingleRank, "Take Point", 1, Cost(2), "The whole party gets +1 action on the first turn of each floor.", null, new OpeningActionPassive { WholeParty = true });
		k.Active(2, "Safe Passage", 5, Cost(2), SpMed, "Party stealth for 5 turns or until anyone attacks.", SkillTargetSpec.OnSelf(), new PartyStealthAction { BaseTurns = 5 });
		k.Passive(3, SkillKind.Mastery, "Scout Master Training", 1, MasteryCost(3), "Unlocks tier 3. Hunger drains slower.", new StatModification { HungerAccumulateThreshold = 2 });
		k.Active(3, "Grapple Line", 1, Cost(3), SpMed, "Pulls you up to 5 tiles in the direction you face.", SkillTargetSpec.OnSelf(), new GrappleLineAction { MaxRange = 5 });
		k.Passive(3, SkillKind.SingleRank, "Treasure Hunter", 1, Cost(3), "Better drops. Reveals treasure on each floor.", new StatModification { DropRate = 0.1f }, new RevealTreasurePassive());
		k.Passive(3, SkillKind.Normal, "Scavenger", 5, Cost(3), "20% chance for gathering to yield double.", null);
		k.Active(3, "Field Kitchen", 3, Cost(3), SpMed, "Uses one food item to restore 30 hunger to the whole party.", SkillTargetSpec.OnSelf(), new FieldKitchenAction { HungerRestore = 30 });
		k.Active(3, "Farsight", 3, Cost(3), SpMed, "Reveals every enemy on the floor for 10 turns.", SkillTargetSpec.OnSelf(), new RevealEnemiesAction { Turns = 10 });
		k.Shared(3, SkillKind.Normal, "Vigor", 5);
		k.Save(new StatModification { HPMax = 2, HungerMax = 20 });
	}
}
