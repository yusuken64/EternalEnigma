using EternalEnigma.Core.Classes;
using UnityEngine;
using static ClassContentBuilder;

// Commander kit (Docs/Classes.md). Re-running overwrites these skill assets; shared skills come from ClassContentShared.
public static class ClassContent_Commander
{
	public const string Id = "commander";
	public const string Folder = "Commander";

	public static void Build()
	{
		var k = new ClassKitBuilder(Id, Folder);
		k.Passive(1, SkillKind.Mastery, "Commander Novice Training", 1, MasteryCost(1), "Unlocks tier 1. Commands +1 turn.", null, new CommandDurationBonus { Turns = 1 });
		k.Shared(1, SkillKind.Gathering, "Mining", 1);
		k.Active(1, "Command: Attack", 3, Cost(1), SpMed, "+3 Strength to every visible ally for 5 turns.", SkillTargetSpec.OnSelf(), new ApplyCommandAction { CommandId = "attack", CommandName = "Command: Attack", Modification = new StatModification { Strength = 3 }, Turns = 5 });
		k.Active(1, "Command: Guard", 3, Cost(1), SpMed, "+3 Defense to every visible ally for 5 turns.", SkillTargetSpec.OnSelf(), new ApplyCommandAction { CommandId = "guard", CommandName = "Command: Guard", Modification = new StatModification { Defense = 3 }, Turns = 5 });
		k.Active(1, "Blazing Arms", 3, Cost(1), SpMed, "Allies' attacks deal bonus fire damage for 5 turns.", SkillTargetSpec.OnSelf(), new ApplyCommandAction { CommandId = "blazing-arms", CommandName = "Blazing Arms", Modification = new StatModification(), BonusElement = DamageElement.Fire, BonusElementPercent = 30, Turns = 5 });
		k.Active(1, "Frost Arms", 3, Cost(1), SpMed, "Allies' attacks deal bonus ice damage for 5 turns.", SkillTargetSpec.OnSelf(), new ApplyCommandAction { CommandId = "frost-arms", CommandName = "Frost Arms", Modification = new StatModification(), BonusElement = DamageElement.Ice, BonusElementPercent = 30, Turns = 5 });
		k.Active(1, "Storm Arms", 3, Cost(1), SpMed, "Allies' attacks deal bonus lightning damage for 5 turns.", SkillTargetSpec.OnSelf(), new ApplyCommandAction { CommandId = "storm-arms", CommandName = "Storm Arms", Modification = new StatModification(), BonusElement = DamageElement.Lightning, BonusElementPercent = 30, Turns = 5 });
		k.Passive(2, SkillKind.Mastery, "Commander Adept Training", 1, MasteryCost(2), "Unlocks tier 2. Commands +1 turn.", null, new CommandDurationBonus { Turns = 1 });
		k.Passive(2, SkillKind.SingleRank, "Reinforce", 1, Cost(2), "When a command ends, affected allies recover 10% HP.", null, new CommandExpiryHeal { HealPercent = 0.1f });
		k.Active(2, "Rally Cry", 1, Cost(2), SpMed, "Removes ailments from every visible ally.", SkillTargetSpec.AllVisible(TargetTeam.Allies), new CleanseAction { Ailments = true });
		k.Active(2, "Inspire", 5, Cost(2), SpLow, "Restores 2 SP to one ally.", SkillTargetSpec.Selected(TargetTeam.Allies), new RestoreSPAction { Amount = 2 });
		k.Active(2, "Command: Endure", 3, Cost(2), SpHigh, "For 3 turns, the first lethal hit on each visible ally leaves them at 1 HP.", SkillTargetSpec.AllVisible(TargetTeam.Allies), Apply("EndureStatus"));
		k.Passive(2, SkillKind.Normal, "Aura of Command", 5, Cost(2), "Allies within 2 tiles regain 1 SP every 3 turns.", null, new AuraOfCommandPassive { Radius = 2, Interval = 3 });
		k.Active(2, "Command: Advance", 1, Cost(2), SpHigh, "Visible allies get +1 action next turn.", SkillTargetSpec.OnSelf(), new ApplyCommandAction { CommandId = "advance", CommandName = "Command: Advance", Modification = new StatModification { ActionsPerTurnMax = 1 }, Turns = 1 });
		k.Passive(3, SkillKind.Mastery, "Commander Master Training", 1, MasteryCost(3), "Unlocks tier 3. Commands +1 turn.", null, new CommandDurationBonus { Turns = 1 });
		k.Active(3, "Decisive Order", 1, Cost(3), SpHigh, "Doubles active command effects for 2 turns, then ends them.", SkillTargetSpec.OnSelf(), new AmplifyCommandsAction { Multiplier = 2f, Turns = 2 });
		k.Passive(3, SkillKind.SingleRank, "Leadership", 1, Cost(3), "Commands last +3 turns.", null, new CommandDurationBonus { Turns = 3 });
		k.Active(3, "Command: Hold", 3, Cost(3), SpMed, "Visible allies regenerate 3 HP each turn for 5 turns.", SkillTargetSpec.OnSelf(), new ApplyCommandAction { CommandId = "hold", CommandName = "Command: Hold", Modification = new StatModification(), HealPerTurn = 3, Turns = 5 });
		k.Passive(3, SkillKind.Normal, "Royal Bearing", 5, Cost(3), "+2 Strength, +2 Defense.", new StatModification { Strength = 2, Defense = 2 });
		k.Shared(3, SkillKind.Normal, "Vigor", 5);
		k.Save(new StatModification { HPMax = 3, SPMax = 3 });
	}
}
