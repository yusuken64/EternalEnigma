// Exploration behaviour is keyed by SkillName; Phase 7 skill assets must use these exact names.
public static class ExplorationSkillNames
{
	public const string Mining = "Mining";
	public const string Harvesting = "Harvesting";
	public const string Foraging = "Foraging";
	public const string Survey = "Survey";
	public const string TrapSense = "Trap Sense";
	public const string Disarm = "Disarm";
	public const string Caltrops = "Caltrops";
	public const string Retreat = "Retreat";
	public const string SoftStep = "Soft Step";
	public const string SafePassage = "Safe Passage";
	public const string Scavenger = "Scavenger";

	public static string ForKind(EternalEnigma.Core.World.GatheringKind kind) => kind switch
	{
		EternalEnigma.Core.World.GatheringKind.Ore => Mining,
		EternalEnigma.Core.World.GatheringKind.Plant => Harvesting,
		EternalEnigma.Core.World.GatheringKind.Forage => Foraging,
		_ => throw new System.ArgumentOutOfRangeException(nameof(kind)),
	};
}
